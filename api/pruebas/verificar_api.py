"""Verifica la API en ejecución. Uso:  API_KEY=su_clave python pruebas/verificar_api.py
Los valores esperados corresponden a los datos del proyecto (carga limpia con 1700 filas leidas)."""
import json
import os
import sys
import urllib.error
import urllib.parse
import urllib.request

URL = os.getenv("API_URL", "http://127.0.0.1:8000")
CLAVE = os.getenv("API_KEY", "")
resultados = []


def llamar(ruta, clave=CLAVE, **params):
    qs = urllib.parse.urlencode({k: v for k, v in params.items() if v is not None})
    req = urllib.request.Request(URL + ruta + ("?" + qs if qs else ""))
    if clave:
        req.add_header("X-API-Key", clave)
    try:
        with urllib.request.urlopen(req, timeout=15) as r:
            return r.status, json.loads(r.read().decode("utf-8"))
    except urllib.error.HTTPError as e:
        try:
            return e.code, json.loads(e.read().decode("utf-8"))
        except ValueError:
            return e.code, None


def comprobar(nombre, condicion, detalle=""):
    resultados.append(bool(condicion))
    print(f"[{'OK ' if condicion else 'FALLA'}] {nombre}" + (f"  -> {detalle}" if detalle and not condicion else ""))


# ---- estado y seguridad ----
s, j = llamar("/health", clave="")
comprobar("GET /health responde y las bases están conectadas", s == 200 and j["estado"] == "ok", j)
comprobar("Sin clave -> 401", llamar("/api/v1/comentarios-sociales", clave="")[0] == 401)
comprobar("Clave incorrecta -> 401", llamar("/api/v1/comentarios-sociales", clave="incorrecta")[0] == 401)

# ---- fuente de redes sociales ----
s, j = llamar("/api/v1/comentarios-sociales", tamano=100)
comprobar("Comentarios sociales: total 200, página de 100, hay más", s == 200 and j["total"] == 200 and len(j["datos"]) == 100 and j["hay_mas"], j and j.get("total"))
s, j2 = llamar("/api/v1/comentarios-sociales", tamano=100, pagina=2)
comprobar("Página 2 trae los otros 100 y ya no hay más", s == 200 and len(j2["datos"]) == 100 and not j2["hay_mas"])
ids = {d["id_comentario"] for d in j["datos"]} | {d["id_comentario"] for d in j2["datos"]}
comprobar("Las 2 páginas no repiten ni pierden registros (200 IDs distintos)", len(ids) == 200)
anonimos = sum(1 for d in j["datos"] + j2["datos"] if d["id_cliente"] is None)
comprobar("88 comentarios anónimos (id_cliente nulo)", anonimos == 88, anonimos)
for canal, n in (("Instagram", 77), ("Facebook", 68), ("Twitter", 55)):
    s, c = llamar("/api/v1/comentarios-sociales", canal=canal, tamano=500)
    comprobar(f"Filtro canal={canal}: {n}", s == 200 and c["total"] == n, c and c.get("total"))
s, c = llamar("/api/v1/comentarios-sociales", desde="2025-07-01", hasta="2025-07-31", tamano=500)
comprobar("Filtro por fechas (julio 2025): todos dentro del rango", s == 200 and c["total"] > 0 and all("2025-07-01" <= d["fecha"] <= "2025-07-31" for d in c["datos"]))
comprobar("canal inválido -> 422", llamar("/api/v1/comentarios-sociales", canal="TikTok")[0] == 422)
comprobar("tamano=1000 -> 422", llamar("/api/v1/comentarios-sociales", tamano=1000)[0] == 422)
comprobar("desde > hasta -> 422", llamar("/api/v1/comentarios-sociales", desde="2025-08-01", hasta="2025-07-01")[0] == 422)

# ---- analítica (estrella) ----
s, r = llamar("/api/v1/analitica/resumen")
comprobar("Resumen: 406 comentarios, promedio 3.09, 206 con puntaje", s == 200 and r["total_comentarios"] == 406 and r["promedio_puntaje"] == 3.09 and r["opiniones_con_puntaje"] == 206, r)
comprobar("Resumen: 85 positivas, 51 neutras, 70 negativas, 200 sin clasificar",
          r["por_sentimiento"] == {"Positiva": 85, "Neutra": 51, "Negativa": 70, "Sin clasificar": 200}, r["por_sentimiento"])
comprobar("Resumen: % sobre clasificadas 41.3 / 24.8 / 34.0",
          (r["pct_sobre_clasificadas"]["Positiva"], r["pct_sobre_clasificadas"]["Neutra"], r["pct_sobre_clasificadas"]["Negativa"]) == (41.3, 24.8, 34.0), r["pct_sobre_clasificadas"])
s, r = llamar("/api/v1/analitica/por-canal")
canales = {f["canal"]: f for f in r}
comprobar("Por canal: 5 canales, SitioWeb 200", s == 200 and len(r) == 5 and canales["SitioWeb"]["comentarios"] == 200)
comprobar("Por canal: redes sociales sin porcentaje de negativas; SitioWeb 34.0",
          canales["Twitter"]["pct_negativas_clasificadas"] is None and canales["SitioWeb"]["pct_negativas_clasificadas"] == 34.0)
s, r = llamar("/api/v1/analitica/por-producto", orden="comentarios", limite=3)
comprobar("Producto con más comentarios: Producto_5 (33)", s == 200 and r[0]["producto"] == "Producto_5" and r[0]["comentarios"] == 33, r[:1])
s, r = llamar("/api/v1/analitica/por-producto", orden="promedio", limite=5, minimo=5)
comprobar("Mejor promedio (mín. 5 opiniones): Producto_14 con 4.0", s == 200 and r[0]["producto"] == "Producto_14" and r[0]["promedio_puntaje"] == 4.0 and all(f["opiniones_con_puntaje"] >= 5 for f in r), r[:1])
s, r = llamar("/api/v1/analitica/por-producto", orden="negativas", limite=3)
comprobar("Más negativas: Producto_3 (10)", s == 200 and r[0]["producto"] == "Producto_3" and r[0]["negativas"] == 10, r[:1])
s, r = llamar("/api/v1/analitica/por-producto", orden="satisfaccion", limite=3)
comprobar("Mayor satisfacción: Producto_15 con 66.7", s == 200 and r[0]["producto"] == "Producto_15" and r[0]["pct_satisfaccion"] == 66.7, r[:1])
comprobar("orden inválido -> 422", llamar("/api/v1/analitica/por-producto", orden="x; DROP TABLE")[0] == 422)
s, r = llamar("/api/v1/analitica/tendencia-mensual")
comprobar("Tendencia mensual: 13 meses", s == 200 and len(r) == 13, len(r) if r else r)
s, r = llamar("/api/v1/analitica/opiniones", canal="Twitter", tamano=10)
comprobar("Opiniones filtradas por canal Twitter: total 55", s == 200 and r["total"] == 55 and len(r["datos"]) == 10, r and r.get("total"))
s, r = llamar("/api/v1/analitica/opiniones", sentimiento="Sin clasificar", tamano=5)
comprobar("Opiniones 'Sin clasificar': total 200", s == 200 and r["total"] == 200, r and r.get("total"))

# ---- estado del ETL ----
s, r = llamar("/api/v1/etl/resumen")
conteos = {(f["base"], f["tabla"]): f["registros"] for f in r}
comprobar("ETL: opinion (transaccional) = fact_opinion (estrella) = 406",
          conteos[("opiniones_clientes", "opinion")] == 406 and conteos[("opiniones_dw", "fact_opinion")] == 406)
s, r = llamar("/api/v1/etl/rechazos", ejecucion="todas")
comprobar("ETL: la bitácora de rechazos responde", s == 200 and isinstance(r, list), r)
comprobar("ejecucion con formato inválido -> 422", llamar("/api/v1/etl/rechazos", ejecucion="abc")[0] == 422)

ok = sum(resultados)
print(f"\nResultado: {ok} de {len(resultados)} comprobaciones correctas")
sys.exit(0 if ok == len(resultados) else 1)
