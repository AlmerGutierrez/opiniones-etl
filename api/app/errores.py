class ErrorBaseDatos(Exception):
    """La base de datos no respondió. El detalle se registra en el log, nunca se envía al cliente."""


class FuenteNoDisponible(Exception):
    """No se encontró o no se pudo leer la fuente de datos (por ejemplo, el CSV)."""
