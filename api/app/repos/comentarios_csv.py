"""Fuente de redes sociales: lee social_comments.csv (simula una API externa) y lo sirve paginado."""
import csv
import logging
import threading
from datetime import datetime

from ..errores import FuenteNoDisponible

log = logging.getLogger("api.fuente_csv")


class RepositorioComentariosCsv:
    def __init__(self, ruta):
        self._ruta = ruta
        self._datos = None
        self._lock = threading.Lock()

    def existe(self):
        return self._ruta.exists()

    def _cargar(self):
        with self._lock:
            if self._datos is not None:
                return self._datos
            if not self._ruta.exists():
                log.error("No existe el archivo de la fuente: %s", self._ruta)
                raise FuenteNoDisponible(str(self._ruta))
            filas = []
            with open(self._ruta, newline="", encoding="utf-8-sig") as f:
                for n, r in enumerate(csv.DictReader(f), start=2):
                    try:
                        fecha = datetime.strptime((r.get("Fecha") or "").strip(), "%Y-%m-%d").date()
                        filas.append({
                            "id_comentario": r["IdComment"].strip(),
                            "id_cliente": (r.get("IdCliente") or "").strip() or None,
                            "id_producto": r["IdProducto"].strip(),
                            "canal": r["Fuente"].strip(),
                            "fecha": fecha,
                            "comentario": r["Comentario"].strip(),
                        })
                    except (KeyError, ValueError, AttributeError):
                        log.warning("Fila %s omitida por datos inválidos", n)
            filas.sort(key=lambda x: (x["fecha"], x["id_comentario"]))
            self._datos = filas
            log.info("Fuente cargada en memoria: %s comentarios", len(filas))
            return filas

    def buscar(self, desde, hasta, canal, pagina, tamano):
        datos = self._cargar()
        if desde:
            datos = [d for d in datos if d["fecha"] >= desde]
        if hasta:
            datos = [d for d in datos if d["fecha"] <= hasta]
        if canal:
            datos = [d for d in datos if d["canal"] == canal]
        total = len(datos)
        inicio = (pagina - 1) * tamano
        return total, datos[inicio:inicio + tamano]
