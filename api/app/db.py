"""Acceso a MySQL. Todas las consultas usan parámetros (%s): nunca se concatenan valores del usuario."""
import logging

import pymysql
import pymysql.cursors

from .config import config
from .errores import ErrorBaseDatos

log = logging.getLogger("api.db")


def consultar(base, sql, params=()):
    try:
        con = pymysql.connect(host=config.DB_HOST, port=config.DB_PORT, user=config.DB_USER,
                              password=config.DB_PASSWORD, database=base, charset="utf8mb4",
                              cursorclass=pymysql.cursors.DictCursor, connect_timeout=5, read_timeout=15)
        try:
            with con.cursor() as cur:
                cur.execute(sql, params)
                return cur.fetchall()
        finally:
            con.close()
    except pymysql.MySQLError as e:
        log.error("Error de base de datos (%s): %s", base, e)
        raise ErrorBaseDatos(str(e)) from e


def numero(valor):
    """Decimal de MySQL -> int o float de Python (None se conserva)."""
    if valor is None:
        return None
    n = float(valor)
    return int(n) if n.is_integer() else n


def flotante(valor, decimales=2):
    return None if valor is None else round(float(valor), decimales)
