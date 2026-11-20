"""Fuente de datos que consume el ApiExtractor del Worker Service."""
from datetime import date
from typing import Optional

from fastapi import APIRouter, Depends, HTTPException, Query

from ..config import config
from ..esquemas import Canal, PaginaComentarios
from ..repos.comentarios_csv import RepositorioComentariosCsv
from ..seguridad import exigir_api_key

router = APIRouter(prefix="/api/v1", tags=["Fuente: redes sociales"], dependencies=[Depends(exigir_api_key)])
repo = RepositorioComentariosCsv(config.RUTA_CSV_SOCIAL)


@router.get("/comentarios-sociales", response_model=PaginaComentarios,
            summary="Comentarios de redes sociales (paginado, con filtro incremental por fecha)")
def comentarios_sociales(
    desde: Optional[date] = Query(None, description="Fecha mínima (AAAA-MM-DD). Permite extracciones incrementales."),
    hasta: Optional[date] = Query(None, description="Fecha máxima (AAAA-MM-DD)."),
    canal: Optional[Canal] = Query(None),
    pagina: int = Query(1, ge=1),
    tamano: int = Query(100, ge=1, le=500, description="Registros por página (máximo 500)"),
):
    if desde and hasta and desde > hasta:
        raise HTTPException(status_code=422, detail="'desde' no puede ser posterior a 'hasta'")
    total, datos = repo.buscar(desde, hasta, canal.value if canal else None, pagina, tamano)
    return PaginaComentarios(total=total, pagina=pagina, tamano=tamano,
                             hay_mas=pagina * tamano < total, datos=datos)
