# Cola durable e idempotencia de notificaciones

Estado: implementación técnica validada localmente en PR-10 el 2026-07-22. La
expansión y activación sobre P360 requieren revisión DBA, respaldo, ambiente no
productivo y canary. No se usó una conexión P360 real ni un servidor SMTP real.

## Decisión de entrega

D-003 queda aceptada con la dirección aprobada para el roadmap:

- entrega **al menos una vez**;
- se prefiere un duplicado detectable antes que una pérdida silenciosa;
- una clave estable acompaña todos los intentos de la misma fila;
- SQL conserva estado durable, claim/lease, intentos y auditoría;
- SMTP y SQL no forman una transacción distribuida, por lo que no se promete
  `exactly once`;
- el caso “SMTP aceptó y SQL no confirmó” queda como
  `notification.delivery.uncertain` para reconciliación.

La cabecera SMTP personalizada `X-P360-Notification-Key` hace observable la
clave, pero no obliga al servidor a deduplicar. El residuo de duplicación sólo
puede reducirse con reconciliación o capacidad idempotente del proveedor SMTP.
Después de la aceptación SMTP, la transición durable a `sent` se intenta antes
de escribir el log SQL heredado. Ese log es auditoría secundaria: si falla, se
emite `audit.write.failed` y no se deshace una confirmación ya completada.

## Compatibilidad y activación

`P360_NOTIFICATION_QUEUE_MODE=legacy` es el valor predeterminado. En ese modo,
el binario conserva la consulta y confirmación históricas. La migración SQL es
expand-only: no elimina ni renombra columnas, mantiene `enviado`,
`intentos_envio` y `fecha_envio`, y permite que la versión anterior siga
leyendo la vista existente.

`durable` no arranca si la expansión está incompleta. Antes de crear Quartz, el
proceso verifica columnas, tabla de auditoría y los cuatro procedimientos
críticos; una ausencia termina como fallo de dependencia, no como ejecución
parcial.

Para canary puede configurarse una lista de `report_id`:

```text
P360_NOTIFICATION_QUEUE_MODE=durable
P360_NOTIFICATION_DURABLE_REPORT_IDS=42,57
```

Con lista vacía, `durable` aplica a todos los reportes. Los IDs no incluidos
continúan por el lector heredado. Un reporte con envío desactivado no reclama
filas durable ni consume intentos.

La compatibilidad de expansión no permite mezclar consumidores sobre el mismo
reporte después de activar durable: una versión antigua ignora leases y podría
reenviar una fila `processing` o `dead_letter`. Antes del canary deben haberse
detenido todas las instancias anteriores; todas las instancias activas deben
usar el binario PR-10 y la misma lista. Los reportes no incluidos pueden seguir
en legacy dentro de ese binario porque cada `report_id` pertenece a una sola
ruta.

## Modelo de estado

```text
pending/retry --claim--> processing --SMTP+confirmación--> sent
     ^                         |
     |                         +--fallo transitorio--> retry
     |                         +--permanente/máximo--> dead_letter
     |                         +--lease expira--------> reclaim
     +--------requeue manual auditado----------------- dead_letter
```

Cada fila recibe:

- `p360_notification_key`: UUID estable y único;
- `p360_delivery_status`: `pending`, `processing`, `retry`, `sent` o
  `dead_letter`;
- propietario, token y vencimiento UTC del lease;
- contador de intentos y próxima elegibilidad UTC;
- código de error acotado, sin mensaje, destinatario ni stack trace;
- fechas UTC de envío/dead-letter/modificación;
- versionado de fila y auditoría append-only de transiciones.

## Claim y concurrencia

`SP_ClaimScheduledReportNotifications` selecciona y actualiza en una misma
transacción. Usa `UPDLOCK`, `READPAST`, `ROWLOCK` y `READCOMMITTEDLOCK`, devuelve
las filas mediante `OUTPUT` y crea un token distinto por claim. Las
confirmaciones, renovaciones y fallos incluyen ID, propietario y token en el
`WHERE`; un worker anterior no puede completar después de un reclaim.

La renovación ocurre inmediatamente antes de SMTP, después del render. Si el
render tardó más que el lease y otro worker ya reclamó la fila, el primero no
envía. Los fallos de render también liberan/programan la fila; no quedan
silenciosamente fuera de la política.

El lote predeterminado es 25 y el lease 600 segundos. Ambos son configurables y
validados. `READPAST` reduce contención de una cola SQL, pero no garantiza que
nunca exista un lock de página; el índice de elegibilidad y las pruebas de
volumen deben revisarse con DBA.

## Reintentos y dead-letter

Valores predeterminados:

| Parámetro | Valor |
|---|---:|
| Lote de claim | 25 |
| Lease | 600 s |
| Máximo de intentos | 8 |
| Backoff inicial | 60 s |
| Backoff máximo | 3600 s |

El backoff es exponencial, acotado y con jitter determinista de 80–120 %. Un
fallo de destinatario/formato/configuración se clasifica permanente; timeout,
SMTP general y fallos no concluyentes se reintentan. Al alcanzar el máximo, la
fila pasa a `dead_letter`.

La elegibilidad se evalúa cuando vuelve a ejecutarse el job Quartz del reporte.
`next_attempt_utc` impide un intento demasiado temprano, pero PR-10 no agrega un
poller ni mantiene un job bloqueado hasta esa hora. Por ello el reintento puede
ocurrir después del backoff si el Cron del reporte es menos frecuente. PR-11
podrá separar el dispatcher si el SLO aprobado exige una cadencia independiente.

Operación inspecciona sin PII:

```sql
EXEC P360Insight.SP_GetDeadScheduledReportNotifications
    @report_id = NULL,
    @limit = 200;
```

El reproceso exige clave, operador y motivo; no requiere editar la tabla:

```sql
EXEC P360Insight.SP_RequeueDeadScheduledReportNotification
    @notification_key = '00000000-0000-0000-0000-000000000000',
    @operator = N'operador-autorizado',
    @reason = N'incidente aprobado';
```

El UUID del ejemplo es sintético. La retención y el equipo que atiende
dead-letter deben cerrarse en D-011 antes de producción.

## Migración DBA

Artefactos:

- `database/migrations/20260722_PR10_DurableNotificationQueue.sql`;
- `database/migrations/20260722_PR10_DurableNotificationQueue_ROLLBACK.md`.

Secuencia:

1. Inventariar versión/compatibilidad de SQL Server, RCSI, tamaño, índices,
   triggers, permisos y columnas reales.
2. Respaldar y ensayar tiempo/bloqueos en copia equivalente.
3. Mantener todas las instancias en `legacy` y aplicar la expansión.
4. Ejecutar dos veces el script en no productivo para demostrar reejecución.
5. Desplegar el binario todavía en `legacy`, retirar todas las versiones
   anteriores y validar el preflight.
6. Activar `durable` sólo para un `report_id` con SMTP interceptado, usando la
   misma configuración en todas las instancias activas.
7. Inyectar caída antes de SMTP, después de SMTP y antes de confirmación.
8. Revisar auditoría, cabecera, duplicados, latencia, locks y dead-letter.
9. Ampliar la lista gradualmente. Lista vacía sólo después del canary.

No se debe aplicar el script directamente en producción desde este repositorio.
La revisión y ejecución pertenecen al flujo DBA autorizado.

## Rollback

1. Detener consumidores; no iniciar todavía una versión que ignore leases.
2. Esperar al menos un lease completo.
3. Confirmar que no queden filas `processing` y reconciliar `retry`,
   `dead_letter` y eventos `uncertain`; el lector heredado no debe ver esos
   estados sin una decisión operativa explícita.
4. Volver a `P360_NOTIFICATION_QUEUE_MODE=legacy` y reiniciar una sola instancia
   únicamente cuando las filas no terminales estén resueltas.
5. Conservar columnas/procedimientos/auditoría; no hacer down migration durante
   el incidente.

Una contracción física sería destructiva y sólo puede ocurrir en otro PR, con
backup y evidencia posterior a estabilización.

## Evidencia reproducible

```powershell
.\build.ps1 -Configuration Release -Target Rebuild
.\eng\Test-NotificationQueueContracts.ps1 -Configuration Release
```

El segundo comando crea una instancia LocalDB efímera `P360PR10_*`, habilita
`READ_COMMITTED_SNAPSHOT` en una base sintética, construye su tabla/vista,
aplica dos veces la migración y
demuestra:

- claim concurrente sin doble propietario;
- token obsoleto incapaz de confirmar;
- renovación, expiración y reclaim;
- backoff y exclusión antes de `next_attempt_utc`;
- máximo de intentos, dead-letter y reproceso manual;
- clave estable entre intentos;
- auditoría de transiciones y cleanup de LocalDB.

No lee `P360_CONNECTION_PRINCIPAL`, no usa datos P360 y no abre SMTP.

## Fuentes de diseño

- Microsoft documenta `READPAST` como un mecanismo orientado a reducir
  contención en colas de trabajo SQL y sus restricciones con RCSI:
  https://learn.microsoft.com/sql/t-sql/queries/hints-transact-sql-table
- La cláusula `OUTPUT` devuelve las filas afectadas por un `UPDATE`:
  https://learn.microsoft.com/sql/t-sql/queries/update-transact-sql
- `rowversion` es una versión binaria monotónica, no una fecha:
  https://learn.microsoft.com/sql/t-sql/data-types/rowversion-transact-sql
- `MailMessage.Headers` admite cabeceras personalizadas transmitidas con el
  mensaje:
  https://learn.microsoft.com/dotnet/api/system.net.mail.mailmessage
