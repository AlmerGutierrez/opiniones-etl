"""API REST de Opiniones de Clientes.
Fuente de redes sociales (la consume el Worker Service) + indicadores de la estrella + estado del ETL."""
import logging
import time

from fastapi import FastAPI, Request
from fastapi.responses import JSONResponse

from .config import config
from .db import consultar
from .errores import ErrorBaseDatos, FuenteNoDisponible
from .esquemas import Salud
from .routers import analitica, etl, fuentes

logging.basicConfig(level=logging.INFO, format="%(asctime)s | %(levelname)-7s | %(name)s | %(message)s")
log = logging.getLogger("api")

if not config.API_KEY:
    raise RuntimeError("Falta la variable API_KEY. Defínela en el archivo .env o como variable de entorno.")

app = FastAPI(
    title="API de Opiniones de Clientes",
    version="1.0.0",
    description=("Expone los comentarios de redes sociales (fuente del ETL), los indicadores de la base analítica "
                 "en estrella y el estado de las cargas. Todas las rutas /api/v1 requieren el encabezado X-API-Key."))
app.include_router(fuentes.router)
app.include_router(analitica.router)
app.include_router(etl.router)


@app.middleware("http")
async def registrar_peticiones(request: Request, call_next):
    inicio = time.perf_counter()
    respuesta = await call_next(request)
    log.info("%s %s -> %s (%.1f ms)", request.method, request.url.path, respuesta.status_code,
             (time.perf_counter() - inicio) * 1000)
    return respuesta


@app.exception_handler(ErrorBaseDatos)
async def _error_bd(request: Request, exc: ErrorBaseDatos):
    return JSONResponse(status_code=503, content={"detail": "Base de datos no disponible"})


@app.exception_handler(FuenteNoDisponible)
async def _error_fuente(request: Request, exc: FuenteNoDisponible):
    return JSONResponse(status_code=503, content={"detail": "Fuente de datos no disponible"})


@app.get("/health", response_model=Salud, tags=["Estado"], summary="Estado del servicio (sin autenticación)")
def salud():
    def probar(base):
        try:
            consultar(base, "SELECT 1 AS ok")
            return "ok"
        except ErrorBaseDatos:
            return "sin conexion"
    csv_ok = "ok" if fuentes.repo.existe() else "no encontrado"
    bd_dw, bd_oltp = probar(config.DB_DW), probar(config.DB_OLTP)
    estado = "ok" if "ok" == bd_dw == bd_oltp == csv_ok else "degradado"
    return Salud(estado=estado, base_analitica=bd_dw, base_transaccional=bd_oltp, fuente_csv_social=csv_ok)
