"""Configuración centralizada. Los secretos se leen de variables de entorno o del archivo .env (fuera de Git)."""
import os
from pathlib import Path

RAIZ_API = Path(__file__).resolve().parent.parent


def _cargar_env():
    ruta = RAIZ_API / ".env"
    if not ruta.exists():
        return
    for linea in ruta.read_text(encoding="utf-8").splitlines():
        linea = linea.strip()
        if not linea or linea.startswith("#") or "=" not in linea:
            continue
        clave, valor = linea.split("=", 1)
        os.environ.setdefault(clave.strip(), valor.strip().strip('"').strip("'"))


_cargar_env()


class Config:
    API_KEY = os.getenv("API_KEY", "")
    DB_HOST = os.getenv("DB_HOST", "127.0.0.1")
    DB_PORT = int(os.getenv("DB_PORT", "3306"))
    DB_USER = os.getenv("DB_USER", "root")
    DB_PASSWORD = os.getenv("DB_PASSWORD", "")
    DB_DW = os.getenv("DB_DW_NAME", "opiniones_dw")
    DB_OLTP = os.getenv("DB_OLTP_NAME", "opiniones_clientes")
    RUTA_CSV_SOCIAL = Path(os.getenv("RUTA_CSV_SOCIAL", str(RAIZ_API.parent / "data" / "social_comments.csv")))


config = Config()
