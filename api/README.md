# API de Opiniones de Clientes

API REST (Python + FastAPI) conectada al resto del proyecto. Cumple dos papeles:

1. **Fuente de datos de redes sociales** para el Worker Service: simula una API externa y entrega los comentarios paginados.
2. **Servicio de consulta** sobre la base analítica en estrella (`opiniones_dw`) y el estado del ETL, para el dashboard.

## Cómo se conecta con lo que ya existe

```
data/social_comments.csv ──► API /api/v1/comentarios-sociales ──► Worker Service (ApiExtractor)
CSV originales ──► ETL Python ──► opiniones_clientes (transaccional) ──► opiniones_dw (estrella)
opiniones_dw ──────────────────► API /api/v1/analitica/*  ──► Power BI / dashboard
opiniones_clientes.etl_rechazo ► API /api/v1/etl/*        ──► monitoreo de las cargas
```

## Endpoints

Todas las rutas `/api/v1/...` exigen el encabezado `X-API-Key`. Solo `/health` y `/docs` son públicas.

| Ruta | Qué devuelve |
|---|---|
| `GET /health` | Estado del servicio y de las conexiones (sin autenticación) |
| `GET /api/v1/comentarios-sociales` | Comentarios de redes sociales. Filtros: `desde`, `hasta`, `canal`; paginación: `pagina`, `tamano` (máx. 500) |
| `GET /api/v1/analitica/resumen` | Total, promedio de puntaje, conteo y porcentajes por sentimiento |
| `GET /api/v1/analitica/por-canal` | Comentarios y proporción de negativas por canal |
| `GET /api/v1/analitica/por-producto` | Ranking: `orden` = comentarios, promedio, negativas, satisfaccion; `limite`, `minimo` |
| `GET /api/v1/analitica/tendencia-mensual` | Comentarios y puntaje promedio por mes (filtro opcional `canal`) |
| `GET /api/v1/analitica/opiniones` | Opiniones integradas con filtros y paginación |
| `GET /api/v1/etl/resumen` | Registros por tabla en la base transaccional y en la estrella |
| `GET /api/v1/etl/rechazos` | Rechazos del ETL por archivo y motivo (última ejecución, una en concreto o `todas`) |

## Requisitos previos

- La base **`opiniones_clientes`** cargada con el ETL (`python main.py --reiniciar ...`).
- La base **`opiniones_dw`** creada y cargada con `sql/06_modelo_estrella.sql` y `sql/07_carga_estrella.sql`.
- Python 3.9 o superior.

## Instalación y ejecución (macOS)

Desde la carpeta `Proyecto_ETL_Opiniones/api`, con el entorno virtual del proyecto activo (`(.venv)`):

```bash
pip install -r requirements.txt
cp .env.example .env
python3 -c "import secrets; print(secrets.token_urlsafe(32))"     # genera una clave: cópiala
nano .env                                                          # pega la clave en API_KEY y escribe DB_PASSWORD
python -m uvicorn app.main:app --port 8000
```

El archivo `.env` se lee directamente, sin pasar por la shell, así que la contraseña puede llevar cualquier carácter. **`.env` no debe subirse a GitHub**: agrega la línea `.env` al `.gitignore` del proyecto.

Abre http://127.0.0.1:8000/docs, pulsa **Authorize**, pega la clave y prueba los endpoints desde el navegador.

## Verificación

Con la API en marcha, en otra Terminal:

```bash
cd Proyecto_ETL_Opiniones/api
API_KEY="tu_clave" python pruebas/verificar_api.py
```

Ejecuta 30 comprobaciones (autenticación, paginación, filtros, validaciones, indicadores y coherencia entre bases). Los valores esperados corresponden a los datos de este proyecto.

## Seguridad

- **Clave de API** en el encabezado `X-API-Key`, comparada con `hmac.compare_digest`. Si no hay clave configurada, la API se niega a arrancar.
- **Secretos fuera del código**: clave y credenciales de MySQL vienen de variables de entorno o de `.env` (ignorado por Git). Nunca se escriben en los logs.
- **SQL parametrizado**: ningún valor del usuario se concatena en una consulta. Las columnas de orden salen de listas cerradas.
- **Validación de entrada**: tipos, rangos y listas de valores permitidos; los errores devuelven 422.
- **Errores controlados**: si MySQL falla se responde 503 con un mensaje genérico; el detalle solo queda en el log.
- **Pendiente para producción**: HTTPS (se pone delante con un proxy inverso) y límite de peticiones por cliente.

## Rendimiento y escalabilidad

- Paginación obligatoria (máximo 500 registros por página) y filtro `desde` para extracciones **incrementales**.
- La fuente CSV se carga una sola vez en memoria; los indicadores se calculan en MySQL con índices sobre las claves de la estrella.
- Cada petición registra su duración en el log (equivale a medir con `Stopwatch`).
- Para añadir otra fuente: nuevo repositorio en `app/repos/` y nuevo router en `app/routers/`, sin tocar lo existente.

## Cómo la consumiría el Worker Service (.NET) — ejemplo orientativo, no compilado

```csharp
// appsettings.json: "Api": { "BaseUrl": "http://localhost:8000" }  (la clave va en user-secrets o en una variable de entorno)
public async IAsyncEnumerable<ComentarioSocialDto> ExtraerAsync(DateOnly? desde, [EnumeratorCancellation] CancellationToken ct)
{
    var cliente = _fabrica.CreateClient("opiniones-api");   // BaseAddress y encabezado X-API-Key configurados al registrar el cliente
    for (var pagina = 1; ; pagina++)
    {
        var url = $"/api/v1/comentarios-sociales?pagina={pagina}&tamano=200" + (desde is null ? "" : $"&desde={desde:yyyy-MM-dd}");
        var respuesta = await cliente.GetFromJsonAsync<PaginaComentarios>(url, ct);
        foreach (var c in respuesta!.Datos) yield return c;
        if (!respuesta.HayMas) yield break;
    }
}
```
El DTO debe mapear los nombres en minúsculas con guion bajo (`id_comentario`, `id_cliente`, `hay_mas`...) con `[JsonPropertyName]`.

## Estructura

```
api/
├── app/
│   ├── main.py            aplicación, registro de peticiones y manejo de errores
│   ├── config.py          configuración y lectura de .env
│   ├── seguridad.py       validación de la clave de API
│   ├── db.py              acceso a MySQL (consultas parametrizadas)
│   ├── esquemas.py        modelos de entrada y salida
│   ├── errores.py         excepciones propias
│   ├── routers/           fuentes.py, analitica.py, etl.py
│   └── repos/             comentarios_csv.py
├── pruebas/verificar_api.py
├── requirements.txt
└── .env.example
```
