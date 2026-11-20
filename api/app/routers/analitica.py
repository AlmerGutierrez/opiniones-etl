"""Indicadores calculados sobre la estrella (opiniones_dw). Alimentan el módulo de visualización."""
from datetime import date
from typing import List, Optional

from fastapi import APIRouter, Depends, HTTPException, Query

from ..config import config
from ..db import consultar, flotante, numero
from ..esquemas import (CanalAnalitico, FilaCanal, FilaOpinion, FilaProducto, FilaTendencia, OrdenProducto,
                        PaginaOpiniones, ResumenAnalitico, Sentimiento)
from ..seguridad import exigir_api_key

router = APIRouter(prefix="/api/v1/analitica", tags=["Analítica (estrella)"], dependencies=[Depends(exigir_api_key)])
DW = config.DB_DW


@router.get("/resumen", response_model=ResumenAnalitico, summary="Indicadores generales")
def resumen():
    f = consultar(DW, """
        SELECT SUM(cantidad) AS total, AVG(puntaje) AS promedio, COUNT(puntaje) AS con_puntaje,
               SUM(es_positiva) AS pos, SUM(es_neutra) AS neu, SUM(es_negativa) AS neg,
               SUM(sk_sentimiento = 4) AS sin_clasificar
        FROM fact_opinion""")[0]
    total = numero(f["total"]) or 0
    pos, neu, neg, sin = (numero(f[k]) or 0 for k in ("pos", "neu", "neg", "sin_clasificar"))
    clasificadas = pos + neu + neg

    def pct(n, base):
        return round(100.0 * n / base, 1) if base else None

    return ResumenAnalitico(
        total_comentarios=total, promedio_puntaje=flotante(f["promedio"]),
        opiniones_con_puntaje=numero(f["con_puntaje"]) or 0,
        por_sentimiento={"Positiva": pos, "Neutra": neu, "Negativa": neg, "Sin clasificar": sin},
        pct_sobre_total={"Positiva": pct(pos, total) or 0.0, "Neutra": pct(neu, total) or 0.0,
                         "Negativa": pct(neg, total) or 0.0, "Sin clasificar": pct(sin, total) or 0.0},
        pct_sobre_clasificadas={"Positiva": pct(pos, clasificadas), "Neutra": pct(neu, clasificadas),
                                "Negativa": pct(neg, clasificadas)})


@router.get("/por-canal", response_model=List[FilaCanal], summary="Comentarios y tono por canal")
def por_canal():
    filas = consultar(DW, """
        SELECT d.canal, d.tipo_fuente, d.ambito, SUM(f.cantidad) AS comentarios,
               SUM(f.sk_sentimiento <> 4) AS clasificadas, SUM(f.es_negativa) AS negativas
        FROM fact_opinion f JOIN dim_fuente d ON d.sk_fuente = f.sk_fuente
        GROUP BY d.sk_fuente, d.canal, d.tipo_fuente, d.ambito
        ORDER BY comentarios DESC, d.canal""")
    salida = []
    for r in filas:
        clasif, neg = numero(r["clasificadas"]) or 0, numero(r["negativas"]) or 0
        salida.append(FilaCanal(canal=r["canal"], tipo_fuente=r["tipo_fuente"], ambito=r["ambito"],
                                comentarios=numero(r["comentarios"]) or 0, clasificadas=clasif,
                                pct_negativas_clasificadas=round(100.0 * neg / clasif, 1) if clasif else None))
    return salida


# La columna de orden y la condición salen de este diccionario fijo: nunca del texto del usuario.
_ORDEN = {
    OrdenProducto.comentarios: ("", "comentarios DESC"),
    OrdenProducto.promedio: ("HAVING con_puntaje >= %s", "promedio_puntaje DESC"),
    OrdenProducto.negativas: ("HAVING negativas > 0", "negativas DESC"),
    OrdenProducto.satisfaccion: ("HAVING clasificadas >= %s", "pct_satisfaccion DESC"),
}


@router.get("/por-producto", response_model=List[FilaProducto], summary="Ranking de productos")
def por_producto(orden: OrdenProducto = Query(OrdenProducto.comentarios),
                 limite: int = Query(10, ge=1, le=100),
                 minimo: int = Query(5, ge=1, description="Opiniones mínimas para rankear por promedio o satisfacción")):
    having, order_by = _ORDEN[orden]
    params = [minimo] if "%s" in having else []
    sql = f"""
        SELECT p.sk_producto AS id_producto, p.nombre AS producto, p.categoria,
               SUM(f.cantidad) AS comentarios, COUNT(f.puntaje) AS con_puntaje, AVG(f.puntaje) AS promedio_puntaje,
               SUM(f.es_negativa) AS negativas, SUM(f.sk_sentimiento <> 4) AS clasificadas,
               100 * SUM(f.es_positiva) / NULLIF(SUM(f.sk_sentimiento <> 4), 0) AS pct_satisfaccion
        FROM fact_opinion f JOIN dim_producto p ON p.sk_producto = f.sk_producto
        GROUP BY p.sk_producto, p.nombre, p.categoria
        {having}
        ORDER BY {order_by}, p.nombre
        LIMIT %s"""
    filas = consultar(DW, sql, params + [limite])
    return [FilaProducto(id_producto=r["id_producto"], producto=r["producto"], categoria=r["categoria"],
                         comentarios=numero(r["comentarios"]) or 0, opiniones_con_puntaje=numero(r["con_puntaje"]) or 0,
                         promedio_puntaje=flotante(r["promedio_puntaje"]), negativas=numero(r["negativas"]) or 0,
                         clasificadas=numero(r["clasificadas"]) or 0, pct_satisfaccion=flotante(r["pct_satisfaccion"], 1))
            for r in filas]


@router.get("/tendencia-mensual", response_model=List[FilaTendencia], summary="Comentarios y puntaje por mes")
def tendencia_mensual(canal: Optional[CanalAnalitico] = Query(None, description="Separa la tendencia por canal")):
    join, where, params = "", "", []
    if canal:
        join, where, params = "JOIN dim_fuente d ON d.sk_fuente = f.sk_fuente", "WHERE d.canal = %s", [canal.value]
    filas = consultar(DW, f"""
        SELECT t.anio, t.mes, t.nombre_mes, SUM(f.cantidad) AS comentarios, AVG(f.puntaje) AS promedio_puntaje
        FROM fact_opinion f JOIN dim_tiempo t ON t.sk_tiempo = f.sk_tiempo {join}
        {where}
        GROUP BY t.anio, t.mes, t.nombre_mes ORDER BY t.anio, t.mes""", params)
    return [FilaTendencia(anio=r["anio"], mes=r["mes"], nombre_mes=r["nombre_mes"],
                          comentarios=numero(r["comentarios"]) or 0, promedio_puntaje=flotante(r["promedio_puntaje"]))
            for r in filas]


@router.get("/opiniones", response_model=PaginaOpiniones, summary="Opiniones integradas (todas las fuentes)")
def opiniones(canal: Optional[CanalAnalitico] = None, sentimiento: Optional[Sentimiento] = None,
              id_producto: Optional[int] = Query(None, ge=1), desde: Optional[date] = None, hasta: Optional[date] = None,
              pagina: int = Query(1, ge=1), tamano: int = Query(50, ge=1, le=200)):
    if desde and hasta and desde > hasta:
        raise HTTPException(status_code=422, detail="'desde' no puede ser posterior a 'hasta'")
    condiciones, params = [], []
    for sql, valor in (("d.canal = %s", canal.value if canal else None), ("s.nombre = %s", sentimiento.value if sentimiento else None),
                       ("f.sk_producto = %s", id_producto), ("t.fecha >= %s", desde), ("t.fecha <= %s", hasta)):
        if valor is not None:
            condiciones.append(sql)
            params.append(valor)
    where = ("WHERE " + " AND ".join(condiciones)) if condiciones else ""
    base = """FROM fact_opinion f
              JOIN dim_tiempo t ON t.sk_tiempo = f.sk_tiempo JOIN dim_producto p ON p.sk_producto = f.sk_producto
              JOIN dim_cliente c ON c.sk_cliente = f.sk_cliente JOIN dim_fuente d ON d.sk_fuente = f.sk_fuente
              JOIN dim_sentimiento s ON s.sk_sentimiento = f.sk_sentimiento"""
    total = numero(consultar(DW, f"SELECT COUNT(*) AS n {base} {where}", params)[0]["n"]) or 0
    filas = consultar(DW, f"""
        SELECT f.id_opinion, f.id_origen, t.fecha, p.nombre AS producto, c.nombre AS cliente,
               d.tipo_fuente, d.canal, s.nombre AS sentimiento, f.puntaje
        {base} {where} ORDER BY t.fecha DESC, f.id_opinion DESC LIMIT %s OFFSET %s""",
        params + [tamano, (pagina - 1) * tamano])
    return PaginaOpiniones(total=total, pagina=pagina, tamano=tamano, hay_mas=pagina * tamano < total,
                           datos=[FilaOpinion(**r) for r in filas])
