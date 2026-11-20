"""Autenticación por clave de API en el encabezado X-API-Key."""
import hmac
import logging
from typing import Optional

from fastapi import HTTPException, Security
from fastapi.security import APIKeyHeader

from .config import config

log = logging.getLogger("api.seguridad")
_esquema = APIKeyHeader(name="X-API-Key", auto_error=False, description="Clave de acceso a la API")


def exigir_api_key(clave: Optional[str] = Security(_esquema)):
    if not config.API_KEY:
        raise HTTPException(status_code=503, detail="La API no tiene una clave configurada")
    # compare_digest evita ataques de temporización al comparar la clave
    if not clave or not hmac.compare_digest(clave.encode("utf-8"), config.API_KEY.encode("utf-8")):
        log.warning("Acceso rechazado: clave de API ausente o inválida")
        raise HTTPException(status_code=401, detail="Clave de API inválida o ausente",
                            headers={"WWW-Authenticate": "ApiKey"})
