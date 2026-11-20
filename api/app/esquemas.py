"""Modelos de entrada y salida (también generan la documentación OpenAPI en /docs)."""
from datetime import date
from enum import Enum
from typing import Dict, List, Optional

from pydantic import BaseModel


class Canal(str, Enum):
    facebook = "Facebook"
    instagram = "Instagram"
    twitter = "Twitter"


class CanalAnalitico(str, Enum):
    encuesta = "EncuestaInterna"
    sitio_web = "SitioWeb"
    facebook = "Facebook"
    instagram = "Instagram"
    twitter = "Twitter"


class Sentimiento(str, Enum):
    positiva = "Positiva"
    neutra = "Neutra"
    negativa = "Negativa"
    sin_clasificar = "Sin clasificar"


class OrdenProducto(str, Enum):
    comentarios = "comentarios"
    promedio = "promedio"
    negativas = "negativas"
    satisfaccion = "satisfaccion"


# ---------- Fuente: comentarios de redes sociales ----------
class ComentarioSocial(BaseModel):
    id_comentario: str
    id_cliente: Optional[str] = None      # None = comentario anónimo
    id_producto: str
    canal: str
    fecha: date
    comentario: str


class PaginaComentarios(BaseModel):
    total: int
    pagina: int
    tamano: int
    hay_mas: bool
    datos: List[ComentarioSocial]


# ---------- Analítica (estrella) ----------
class ResumenAnalitico(BaseModel):
    total_comentarios: int
    promedio_puntaje: Optional[float]
    opiniones_con_puntaje: int
    por_sentimiento: Dict[str, int]
    pct_sobre_total: Dict[str, float]
    pct_sobre_clasificadas: Dict[str, Optional[float]]


class FilaCanal(BaseModel):
    canal: str
    tipo_fuente: str
    ambito: str
    comentarios: int
    clasificadas: int
    pct_negativas_clasificadas: Optional[float]


class FilaProducto(BaseModel):
    id_producto: int
    producto: str
    categoria: str
    comentarios: int
    opiniones_con_puntaje: int
    promedio_puntaje: Optional[float]
    negativas: int
    clasificadas: int
    pct_satisfaccion: Optional[float]


class FilaTendencia(BaseModel):
    anio: int
    mes: int
    nombre_mes: str
    comentarios: int
    promedio_puntaje: Optional[float]


class FilaOpinion(BaseModel):
    id_opinion: int
    id_origen: str
    fecha: date
    producto: str
    cliente: str
    tipo_fuente: str
    canal: str
    sentimiento: str
    puntaje: Optional[int]


class PaginaOpiniones(BaseModel):
    total: int
    pagina: int
    tamano: int
    hay_mas: bool
    datos: List[FilaOpinion]


# ---------- ETL ----------
class ConteoTabla(BaseModel):
    base: str
    tabla: str
    registros: int


class FilaRechazo(BaseModel):
    archivo: str
    tipo_error: str
    campo: str
    registros: int


class Salud(BaseModel):
    estado: str
    base_analitica: str
    base_transaccional: str
    fuente_csv_social: str
