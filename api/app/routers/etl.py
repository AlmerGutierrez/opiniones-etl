"""Estado del proceso ETL: conteos de las bases y registros rechazados (bitácora etl_rechazo)."""
from typing import List, Optional

from fastapi import APIRouter, Depends, Query

from ..config import config
from ..db import consultar, numero
from ..esquemas import ConteoTabla, FilaRechazo
from ..seguridad import exigir_api_key

router = APIRouter(prefix="/api/v1/etl", tags=["Estado del ETL"], dependencies=[Depends(exigir_api_key)])

# Lista fija de tablas: los nombres nunca vienen del usuario.
_TABLAS = [
    (config.DB_OLTP, ["cliente", "producto", "fuente_datos", "opinion", "opinion_encuesta",
                      "opinion_social", "opinion_web", "etl_rechazo"]),
    (config.DB_DW, ["dim_tiempo", "dim_producto", "dim_cliente", "dim_fuente", "dim_sentimiento", "fact_opinion"]),
]


@router.get("/resumen", response_model=List[ConteoTabla], summary="Registros por tabla (transaccional y analítica)")
def resumen():
    salida = []
    for base, tablas in _TABLAS:
        for tabla in tablas:
            n = numero(consultar(base, f"SELECT COUNT(*) AS n FROM {tabla}")[0]["n"]) or 0
            salida.append(ConteoTabla(base=base, tabla=tabla, registros=n))
    return salida


@router.get("/rechazos", response_model=List[FilaRechazo], summary="Registros rechazados por el ETL, por motivo")
def rechazos(ejecucion: Optional[str] = Query(None, pattern=r"^(\d{14}|todas)$",
                                              description="Id de ejecución (14 dígitos), 'todas', o vacío para la última")):
    if ejecucion == "todas":
        donde, params = "", []
    elif ejecucion:
        donde, params = "WHERE IdEjecucion = %s", [ejecucion]
    else:
        donde, params = "WHERE IdEjecucion = (SELECT MAX(IdEjecucion) FROM etl_rechazo)", []
    filas = consultar(config.DB_OLTP, f"""
        SELECT Archivo, TipoError, Campo, COUNT(*) AS registros FROM etl_rechazo {donde}
        GROUP BY Archivo, TipoError, Campo ORDER BY Archivo, registros DESC""", params)
    return [FilaRechazo(archivo=r["Archivo"], tipo_error=r["TipoError"], campo=r["Campo"],
                        registros=numero(r["registros"]) or 0) for r in filas]
