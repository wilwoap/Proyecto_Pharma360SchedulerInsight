# Observabilidad y salud

Estado: núcleo neutral validado localmente en PR-07 y ampliado hasta PR-10 el 2026-07-22. La conexión a una plataforma corporativa, retención, costes y alertas desplegadas siguen pendientes de D-010/D-011.

## Resultado

El scheduler emite una línea JSON por evento operativo crítico, mantiene métricas de cardinalidad acotada y publica liveness/readiness en un archivo JSON opcional. La tabla SQL heredada queda como auditoría secundaria: su caída no suprime el evento primario ni provoca recursión de logging.

No se añadió un proveedor propietario ni un agente. Esta capa puede conectarse después a OpenTelemetry, Windows Event Log o la plataforma corporativa elegida sin cambiar jobs ni reglas de negocio.

## Contrato de evento

Cada registro JSON contiene:

| Campo | Uso |
|---|---|
| `timestampUtc` | fecha UTC ISO-8601 |
| `level` | `information`, `warning` o `error` |
| `eventName` | nombre estable del catálogo |
| `correlationId` | correlación del proceso, job o notificación |
| `fields` | sólo campos de la lista permitida |
| `exceptionType` | categoría de excepción; nunca mensaje ni stack trace |

Ejemplo sintético:

    {"timestampUtc":"2026-07-22T12:00:00Z","level":"information","eventName":"operation.completed","correlationId":"0123456789abcdef0123456789abcdef","fields":{"duration_ms":"24.8","job_type":"html","operation":"job.html","outcome":"success","report_uid":"RVIS"},"exceptionType":null}

Campos permitidos:

    active_jobs
    active_notifications
    attempt_count
    audit_sink
    definitions_added
    definitions_count
    definitions_rejected
    definitions_removed
    definitions_unchanged
    definitions_updated
    duration_ms
    delivery_disposition
    failure_category
    failure_code
    failure_kind
    health_exporter
    job_type
    metric
    misfire_count
    misfire_policy
    notification_count
    notification_key
    operation
    outcome
    overlap_policy
    parent_correlation_id
    process_id
    queue_action
    report_uid
    state
    sql_code
    time_zone
    value

Cualquier clave no incluida se descarta. Los valores se limitan a 128 caracteres y se eliminan caracteres de control. No se aceptan destinatarios, nombres, cuerpos/asuntos de correo, rutas, cadenas de conexión, claves, tokens, mensajes de excepción ni stack traces.

## Catálogo de eventos

| Evento | Momento |
|---|---|
| `scheduler.definitions.loading` | antes de cargar/agendar definiciones |
| `scheduler.definition.registered` | definición aceptada por Quartz |
| `scheduler.definition.rejected` | fila inválida pausada sin abortar las válidas |
| `scheduler.definition.unchanged` | definición ya equivalente por fingerprint |
| `scheduler.definition.updated` | job/trigger reemplazado por cambio aprobado |
| `scheduler.definition.removed` | ID retirado del conjunto administrado |
| `scheduler.definitions.reconciled` | resumen de altas/cambios/bajas |
| `scheduler.definitions.loaded` | lote completo registrado |
| `scheduler.started` | Quartz listo |
| `scheduler.trigger.misfired` | trigger retrasado por encima del umbral |
| `scheduler.stopping` | se retiró readiness y comienza standby |
| `scheduler.stopped` | apagado ordenado finalizado |
| `scheduler.shutdown.timeout` | se agotó el presupuesto y se fuerza shutdown sin espera |
| `health.changed` | cambio de estado del proceso |
| `health.export.failed` | el archivo de salud no pudo actualizarse |
| `audit.write.failed` | el sink SQL secundario falló |
| `operation.started` | inicio de una operación medida |
| `operation.completed` | fin, resultado y duración de una operación |
| `notification.retry.scheduled` | fallo durable liberado con próxima elegibilidad |
| `notification.dead_lettered` | fallo permanente o máximo de intentos |
| `notification.lease_lost` | el worker dejó de ser propietario y no debe enviar/confirmar |
| `notification.delivery.uncertain` | SMTP aceptó pero SQL no pudo confirmar con certeza |
| `notification.failure.persistence_failed` | no se pudo registrar el fallo; el lease queda para expiración/reclaim |

## Correlación

- El proceso recibe un identificador al componer el runtime.
- Cada ejecución Quartz recibe un `correlationId` nuevo.
- Cada notificación recibe un identificador hijo y conserva `parent_correlation_id` del job.
- Render y SMTP usan la correlación de la notificación.
- Un reintento futuro crea una nueva correlación de ejecución y conserva `notification_key`, la clave UUID durable de PR-10.

No se usa el correo, nombre, ID de colaborador ni ruta de archivo como correlación.

## Métricas

Las métricas se mantienen en memoria y se incluyen en el snapshot de salud. Se reinician con el proceso; la plataforma elegida deberá recolectarlas si se necesita histórico.

Operaciones permitidas:

| Operación | Cobertura |
|---|---|
| `scheduler.registration` | carga y registro de definiciones |
| `scheduler.start` | inicio de Quartz |
| `scheduler.shutdown` | standby y shutdown |
| `scheduler.misfire` | tratamiento explícito de un trigger retrasado |
| `job.crystal` | ejecución completa del job Crystal |
| `job.devexpress` | ejecución completa del job DevExpress |
| `job.html` | ejecución completa del job HTML |
| `job.other` | tipo no conocido creado por la fábrica |
| `notification` | procesamiento de una notificación |
| `render.crystal` | exportación Crystal a PDF |
| `render.devexpress` | renderizado DevExpress y escritura del PDF |
| `delivery.smtp` | llamada real al transporte SMTP |
| `data.report-schedules` | consulta y mapeo de definiciones programadas |
| `data.notification-queue` | lectura/claim, preflight, renew, complete y fail de la cola |

Por operación y resultado (`success`, `failure`, `skipped`, `timeout`, `cancelled`) se exponen `count`, duración acumulada y duración máxima. La métrica gauge `notification_batch_size` representa el último lote observado por un job; no equivale al backlog global y no debe usarse todavía como SLO de cola. `queue_action` distingue lectura, preflight, claim, renew, complete y fail sin introducir el ID de reporte como dimensión de métrica.

PR-09 añade gauges acotados `scheduler_definition_rows_rejected`, `scheduler_definitions_rejected`, `scheduler_definitions_active`, `scheduler_max_concurrency` y `scheduler_misfires_total`. No incluyen ID/nombre dinámico. El backlog se demuestra en pruebas de límite, pero no se publica como gauge durable: con `RAMJobStore` no existe una medida persistente de antigüedad.

El snapshot también incluye `workingSetBytes`, `handleCount`, jobs activos, notificaciones activas y definiciones registradas. Ningún identificador dinámico forma parte de la clave de una métrica.

## Liveness y readiness

Estados:

| Estado | Live | Ready | Significado |
|---|---:|---:|---|
| `starting` | sí | no | configuración/definiciones en preparación |
| `ready` | sí | sí | Quartz iniciado y definiciones registradas |
| `stopping` | sí | no | no deben entrar nuevos triggers |
| `stopped` | no | no | apagado completado |
| `faulted` | sí hasta terminar | no | fallo de configuración, dependencia u operación de host |

La variable opcional `P360_HEALTH_FILE_PATH` habilita el exportador JSON. Debe ser una ruta absoluta con extensión `.json`; el directorio debe existir y ser escribible por la identidad del proceso.

Ejemplo sin valores sensibles:

    $env:P360_HEALTH_FILE_PATH = "D:\Pharma360\Scheduler\state\health.json"

El archivo se reemplaza atómicamente al cambiar de estado y cada 15 segundos. No genera historial ni archivos por heartbeat. Si el exporter falla, se emite `health.export.failed` y la ruta funcional continúa. Al quitar la variable se deshabilita sólo el archivo; los eventos JSON mínimos permanecen.

Un consumidor debe comprobar estado, `ready` y antigüedad de `timestampUtc`. El umbral de stale debe incorporar al menos el intervalo de heartbeat y la demora del colector; operación lo fijará con D-010, no desde este PR.

## Dashboard neutral

La definición inicial, independiente del proveedor, contiene:

1. estado live/ready y antigüedad del heartbeat por instancia;
2. definiciones registradas y duración/fallos de start/shutdown;
3. jobs activos y tasa/duración por tipo;
4. notificaciones success/failure/skipped y tamaño de lote observado;
5. fallos y duración de render Crystal/DevExpress;
6. fallos y duración SMTP;
7. memoria y handles del proceso.

Alertas a materializar después de D-010:

- servicio esperado sin heartbeat vigente;
- proceso que no alcanza `ready` después de un despliegue;
- aumento de `failure / total` respecto del baseline observado por operación;
- crecimiento sostenido de memoria o handles respecto del baseline;
- backlog/edad sobre SLO; PR-10 ya define claim/lease, pero la consulta/exportación agregada y el umbral siguen pendientes de D-010/D-011.

No se fijan umbrales numéricos, retención ni coste sin datos de producción y propietario operativo.

## Runbook

1. preparar un directorio local restringido para salud, si se usará;
2. configurar `P360_HEALTH_FILE_PATH` para la identidad del proceso;
3. iniciar y verificar la transición `starting` -> `ready`;
4. confirmar que el archivo cambia dentro de 15 segundos y que no contiene secretos/PII;
5. ante `faulted`, buscar por `correlationId` y `failure_category`, sin solicitar cuerpos ni credenciales;
6. ante `audit.write.failed`, revisar SQL sin perder los eventos de consola;
7. ante `health.export.failed`, revisar existencia/ACL/disco y mantener vigilancia del proceso por el host;
8. al detener, verificar `stopping` -> `stopped`.

Rollback del exporter: quitar `P360_HEALTH_FILE_PATH` y reiniciar. Rollback completo: detener la candidata, restaurar el release anterior y ejecutar una sola instancia. El archivo de salud puede conservarse como evidencia; no contiene secretos ni datos personales.

## Evidencia

- 102/102 pruebas aisladas aprobadas;
- redacción de secretos, destinatarios, cuerpos y detalles de excepción;
- correlación y métricas de éxito/fallo/duración;
- cardinalidad acotada frente a valores dinámicos;
- fallo SMTP reflejado sin cambiar la semántica de confirmación de cola;
- fallo del sink SQL no suprime el evento estructurado;
- archivo de salud reemplazado atómicamente sin temporales residuales;
- salud integrada con arranque y apagado del scheduler;
- políticas Quartz, reconciliación, misfires y concurrencia observables;
- reintento/dead-letter/lease perdido y entrega incierta observables mediante clave durable sin destinatarios;
- hash de texto de reportes normalizado entre LF/CRLF; `.rpt` continúa validándose byte a byte;
- ningún archivo `.rpt`, `.Designer.cs` o `.resx` fue modificado.
