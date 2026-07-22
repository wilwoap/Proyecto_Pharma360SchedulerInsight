# Configuración de entorno

## Regla

El repositorio, el binario y su archivo .config no contienen credenciales. Los valores reales se suministran al proceso mediante el mecanismo de secretos del ambiente.

## Variables

| Variable | Requerida | Uso |
|---|---|---|
| P360_CONNECTION_PRINCIPAL | Sí | Conexión principal de SQL Server |
| P360_GOOGLE_MAPS_API_KEY | No | Imagen estática del mapa en correos |
| P360_PARAMETER_PROVIDER_MODE | No | `batch` por defecto; `legacy` sólo para rollback temporal |
| P360_SHUTDOWN_TIMEOUT_SECONDS | No | Presupuesto de apagado de 1–900 segundos; 30 por defecto |
| P360_HEALTH_FILE_PATH | No | Ruta absoluta `.json` para liveness/readiness; deshabilitado por defecto |
| P360_SQL_CONNECTION_TIMEOUT_SECONDS | No | Apertura SQL de 1–120 segundos; 15 por defecto |
| P360_SQL_COMMAND_TIMEOUT_SECONDS | No | Comandos SQL de 1–300 segundos; 30 por defecto |
| P360_QUARTZ_TIME_ZONE | No | ID `TimeZoneInfo` del host; zona local por defecto |
| P360_QUARTZ_MISFIRE_POLICY | No | `fire_once_now` por defecto o `do_nothing` |
| P360_QUARTZ_DISALLOW_CONCURRENT_EXECUTION | No | Impide solapar el mismo `report_id`; `true` por defecto |
| P360_QUARTZ_MAX_CONCURRENCY | No | Límite global de 1–64 jobs; 10 por defecto |
| P360_NOTIFICATION_QUEUE_MODE | No | `legacy` por defecto; `durable` sólo tras expansión SQL |
| P360_NOTIFICATION_DURABLE_REPORT_IDS | No | IDs positivos separados por coma para canary; vacío aplica durable a todos |
| P360_NOTIFICATION_CLAIM_BATCH_SIZE | No | Filas por claim, 1–500; 25 por defecto |
| P360_NOTIFICATION_LEASE_SECONDS | No | Lease de 30–3600 s; 600 por defecto |
| P360_NOTIFICATION_MAX_ATTEMPTS | No | Intentos de 1–100; 8 por defecto |
| P360_NOTIFICATION_RETRY_BASE_SECONDS | No | Backoff inicial de 1–3600 s; 60 por defecto |
| P360_NOTIFICATION_RETRY_MAX_SECONDS | No | Tope de 1–86400 s; 3600 por defecto y no menor al inicial |

Si no existe P360_GOOGLE_MAPS_API_KEY, el correo incluye únicamente un enlace a la ubicación. La aplicación nunca crea variables, imprime sus valores ni incorpora una conexión predeterminada.

`P360_PARAMETER_PROVIDER_MODE` no es sensible. El valor normal es `batch`, incluso cuando la variable no existe. `legacy` conserva temporalmente la lectura histórica de parámetros, cacheada al arrancar; cualquier otro valor detiene el proceso antes de iniciar Quartz.

`P360_SHUTDOWN_TIMEOUT_SECONDS` tampoco es sensible. Debe ser un entero decimal entre 1 y 900. El presupuesto incluye `Standby` y `Shutdown(true)`; al agotarse se solicita un apagado sin espera y el proceso devuelve código 4.

`P360_HEALTH_FILE_PATH` no es sensible, pero su valor no se imprime. Si se configura, el directorio debe existir y la identidad del proceso debe poder crear/reemplazar el archivo. El snapshot se actualiza atómicamente cada 15 segundos y en cambios de estado. Si no existe la variable, sólo se desactiva el archivo; los eventos estructurados permanecen en stdout.

Los dos timeouts SQL son enteros en segundos y nunca admiten cero: en `System.Data.SqlClient`, cero significaría una espera indefinida. El valor de conexión reemplaza cualquier `Connect Timeout` recibido dentro de la cadena, sin imprimirla. El comando particular que conserva un baseline histórico de 300 segundos es la búsqueda de pedido por CUD; las demás rutas usan el presupuesto configurado. Cambiar estos valores exige reiniciar el proceso.

Las cuatro variables Quartz tampoco son sensibles y todo cambio exige reinicio. Si no se fija zona, el proceso resuelve `TimeZoneInfo.Local` una vez al arrancar. Un ID desconocido, una política distinta de `fire_once_now`/`do_nothing`, un booleano inválido o una concurrencia fuera de 1–64 impiden iniciar; el valor recibido no se imprime. `fire_once_now` conserva la interpretación Cron histórica. `false` en la política de solapamiento existe sólo como rollback temporal.

Las variables de cola tampoco contienen secretos y se validan sin imprimir el
valor recibido. El valor normal sigue siendo `legacy`. Activar `durable` exige
haber aplicado la expansión de PR-10: el arranque hace un preflight y termina
como fallo de dependencia si faltan columnas, auditoría o procedimientos. La
lista de reportes permite canary; omitirla en modo durable habilita todos. El
backoff marca la primera elegibilidad, pero la fila se vuelve a reclamar cuando
se ejecuta otra vez el Cron de ese reporte. Consulte
`20_COLA_DURABLE_E_IDEMPOTENTE.md` antes de cambiar estos valores.

## Desarrollo local

Obtener los valores desde el almacén autorizado y configurarlos sólo para la sesión:

    $env:P360_CONNECTION_PRINCIPAL = "<valor obtenido del almacén autorizado>"
    $env:P360_GOOGLE_MAPS_API_KEY = "<valor obtenido del almacén autorizado>"
    $env:P360_PARAMETER_PROVIDER_MODE = "batch"
    $env:P360_SHUTDOWN_TIMEOUT_SECONDS = "30"
    $env:P360_HEALTH_FILE_PATH = "D:\Pharma360\Scheduler\state\health.json"
    $env:P360_SQL_CONNECTION_TIMEOUT_SECONDS = "15"
    $env:P360_SQL_COMMAND_TIMEOUT_SECONDS = "30"
    $env:P360_QUARTZ_TIME_ZONE = "SA Pacific Standard Time"
    $env:P360_QUARTZ_MISFIRE_POLICY = "fire_once_now"
    $env:P360_QUARTZ_DISALLOW_CONCURRENT_EXECUTION = "true"
    $env:P360_QUARTZ_MAX_CONCURRENCY = "10"
    $env:P360_NOTIFICATION_QUEUE_MODE = "legacy"

Ejemplo de canary sólo después de la expansión DBA:

    $env:P360_NOTIFICATION_QUEUE_MODE = "durable"
    $env:P360_NOTIFICATION_DURABLE_REPORT_IDS = "42"
    $env:P360_NOTIFICATION_CLAIM_BATCH_SIZE = "25"
    $env:P360_NOTIFICATION_LEASE_SECONDS = "600"
    $env:P360_NOTIFICATION_MAX_ATTEMPTS = "8"
    $env:P360_NOTIFICATION_RETRY_BASE_SECONDS = "60"
    $env:P360_NOTIFICATION_RETRY_MAX_SECONDS = "3600"

No guardar esos comandos con valores reales en scripts, historial de terminal, perfiles, capturas o documentación.

## Servicio

- Configurar las variables para la identidad que ejecuta el servicio mediante la plataforma de despliegue.
- Reiniciar el proceso después de una rotación.
- Reiniciar también después de cambiar parámetros en `T_PARAMETROS`: PR-05 usa un snapshot por vida del proceso.
- Reiniciar después de cambiar definiciones SQL: PR-09 reconcilia una vez antes de iniciar Quartz y no hace polling.
- Mantener cola `legacy` durante la expansión; habilitar `durable` por lista de reportes y reiniciar después de cada cambio.
- Validar el ID de zona horaria en la imagen Windows exacta antes de promoverlo.
- Verificar sólo presencia y conectividad; nunca mostrar el valor.
- Restringir lectura/configuración a operación y a la identidad de servicio.
- Mantener valores distintos por ambiente.

La variable de entorno es el puente inicial. D-008 decidirá si el estado final usa un almacén administrado o DPAPI/configuración protegida.

## Rotación pendiente de Gate 0

- Revocar o rotar la credencial SQL que estuvo embebida.
- Revocar o rotar la clave de mapas que estuvo embebida.
- Revisar consumo y accesos históricos.
- Limitar la nueva clave de mapas a APIs/cuotas necesarias.
- Confirmar que ninguna otra aplicación depende de esos valores antes del corte.

No se publicará el repositorio mientras esta lista no tenga propietario y evidencia.

## Diagnóstico seguro

Ante configuración ausente, comprobar:

1. identidad efectiva del proceso;
2. presencia de la variable sin imprimir su contenido;
3. reinicio posterior a la configuración;
4. permisos/red/TLS;
5. conectividad desde un cliente autorizado.

Nunca resolver el incidente agregando el secreto a App.config o Settings.settings.
