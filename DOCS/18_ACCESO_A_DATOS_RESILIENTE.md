# Acceso a datos resiliente

Estado: validado localmente en PR-08 el 2026-07-22. No cambia esquema, consultas de negocio, contenido de reportes ni semántica de entrega. La validación contra un ambiente P360 no productivo sigue siendo un gate de despliegue, no una autorización para usar producción.

## Resultado

Las cargas de definiciones Quartz, parámetros de arranque y cola de notificaciones dejaron de depender de comandos con espera indefinida. La conexión y cada comando tienen un presupuesto finito, la cola usa `@ReportId` tipado, Quartz propaga cancelación hasta ADO.NET y los fallos SQL conservan causa, categoría y código sin exponer mensajes, consultas o credenciales.

No se agregó un ORM, una política global de retry ni una dependencia externa. PR-08 conserva una sola tentativa para escrituras generales. PR-10 añade recuperación únicamente dentro del protocolo durable de cola, protegido por clave, claim/lease y transición condicionada.

## Base técnica

- [`SqlCommand.CommandTimeout`](https://learn.microsoft.com/en-us/dotnet/api/system.data.sqlclient.sqlcommand.commandtimeout?view=netframework-4.8.1): el valor predeterminado es 30 segundos y cero significa espera sin límite.
- [`SqlConnection.OpenAsync(CancellationToken)`](https://learn.microsoft.com/en-us/dotnet/api/system.data.sqlclient.sqlconnection.openasync?view=netframework-4.8.1): el token puede abandonar la apertura antes del timeout y las excepciones se propagan por la tarea.
- [Errores transitorios de Azure SQL](https://learn.microsoft.com/en-us/azure/azure-sql/database/troubleshoot-common-errors-issues?view=azuresql): documenta 40197, 40501, 40613 y 49918-49920 como condiciones recuperables del servicio.
- [Guía de deadlocks de SQL Server](https://learn.microsoft.com/en-us/sql/relational-databases/sql-server-deadlocks-guide?view=sql-server-ver17): SQL Server devuelve 1205 al elegir una víctima y permite volver a ejecutar sólo cuando la operación sea segura.

Las fuentes se consultaron el 2026-07-22. La clasificación no activa reintentos; sirve para operación y para diseñar una política idempotente posterior.

## Configuración

| Variable | Predeterminado | Rango | Efecto |
|---|---:|---:|---|
| `P360_SQL_CONNECTION_TIMEOUT_SECONDS` | 15 s | 1-120 s | reemplaza cualquier `Connect Timeout`, incluso cero, en la cadena recibida |
| `P360_SQL_COMMAND_TIMEOUT_SECONDS` | 30 s | 1-300 s | límite de consultas, SP y auditoría migrados |

La búsqueda histórica de pedido por CUD conserva su presupuesto explícito de 300 segundos. Los valores son técnicos, no SLO de negocio, y se aplican después de reiniciar. `SchedulerOptions.ToString()` y `SqlExecutionPolicy.ToString()` nunca incluyen la cadena de conexión.

## Contratos migrados

| Operación | Entrada | Resultado | Permiso | Límite/retry |
|---|---|---|---|---|
| parámetros de sistema | colección no vacía; `varchar(128)` por nombre | diccionario inmutable; duplicado es error | `SELECT` en `T_PARAMETROS` | timeout configurado; sin retry |
| definiciones programadas | consulta `P360.Reports.Query` | lista inmutable de `ReportScheduleDefinition`; cero filas es válido | `SELECT` de la vista configurada | async, cancelable, timeout configurado; sin retry |
| cola pendiente | `@ReportId int` positivo | lista inmutable de `InfoColaNotificaciones`; cero filas es válido | `SELECT` de la vista configurada | async, cancelable, timeout configurado; sin retry |
| poblar cola | `@p_reportUID varchar(128)`, `@p_usuario varchar(256)` | termina o falla; SQL 50000 conserva el no-op funcional del SP | `EXECUTE` del SP existente | una tentativa |
| código de fichero vigente | sin entrada | entero; el fallo ya no se convierte en cero silencioso | `SELECT` en tabla existente | timeout configurado; sin retry |
| pedido por CUD | `varchar(512)` | código de pedido o excepción | `SELECT` existente | 300 s; sin retry |
| contactos adicionales | report ID y referencia del evento | lista; cero filas es válido, fallo SQL es excepción | `EXECUTE` del SP existente | timeout configurado; sin retry |
| marcar notificación | `@ColaNotificacionId int` | éxito sólo si se afectó una fila; cero filas/fallo impiden cerrar la notificación como exitosa | `UPDATE` existente | timeout configurado; una tentativa |
| auditoría SQL secundaria | cinco parámetros `nvarchar(max)` explícitos | `true/false`, sin afectar el evento JSON primario | `INSERT` existente | timeout configurado; una tentativa |

Las consultas Dapper heredadas también reciben conexión finita y `commandTimeout` explícito. `StackFrame` dejó de definir la identidad funcional de estas rutas; se usan nombres estables.

## Fallos

`DataAccessException` conserva la excepción original en `InnerException` y publica sólo:

- operación fija;
- `failure_kind`;
- `sql_code`, cuando existe.

| Categoría | Casos iniciales |
|---|---|
| `Cancelled` | token solicitado por Quartz, incluyendo el caso net48 donde el proveedor responde con `SqlException` al cancelar un comando |
| `Timeout` | `TimeoutException` o SQL `-2` |
| `Transient` | 1205, 40197, 40501, 40613, 49918, 49919, 49920 |
| `Permanent` | sintaxis/columna/objeto/permisos/login/constraints conocidos: 102, 207, 208, 229, 547, 2601, 2627, 18456 |
| `Unknown` | cualquier código no autorizado explícitamente |

Un fallo transitorio no significa “reintentar”. PR-08 ejecuta cero reintentos de consultas o escrituras generales en código y conserva la configuración de recuperación de conexión que ya venga en `ConnectRetryCount`; el proveedor no reejecuta la consulta fallida. Antes de agregar retry de otra operación se debe demostrar idempotencia, limitar intentos/demora y comprobar que no se multiplica con la recuperación del proveedor. La excepción explícita desde PR-10 es la máquina de estados de notificación durable aceptada en D-003.

## Observabilidad

Se añadieron dos operaciones de cardinalidad fija:

- `data.report-schedules`;
- `data.notification-queue`.

Exponen duración y resultado `success`, `failure` o `cancelled`. Los campos `failure_kind`, `sql_code`, `definitions_count` y `notification_count` están permitidos; no se emiten parámetros, filas, consultas, nombres, destinatarios, rutas ni mensajes de excepción.

## Pruebas y evidencia

Gate aislado normal:

    .\build.ps1 -Configuration Release -Target Rebuild

Resultado al cerrar la implementación: 68/68 pruebas net48 x64, sin SQL, SMTP ni Internet. Cubren defaults/rangos, redacción, conexión finita, comando y parámetro tipados, mapeo de ambos contratos, lista vacía frente a excepción, clasificación, cancelación previa, confirmación fallida de cola y telemetría acotada.

El gate del PR registra fuera del commit el SHA-256 del ejecutable, porque el SDK incorpora la revisión Git y el hash cambia al crear un commit. El SBOM SPDX 2.2 validó 77 paquetes y 111 archivos. Política de dependencias: cero vulnerabilidades altas/críticas, una moderada de log4net bajo la excepción vigente y cero paquetes obsoletos.

Contrato SQL real y efímero:

    .\eng\Test-SqlContracts.ps1 -Configuration Release

El script crea una instancia LocalDB con nombre `P360PR08_*`, ejecuta sólo proyecciones sintéticas en `master` y la elimina en `finally`. Verifica:

- mapeo real de schedule y cola;
- resultado vacío válido;
- objeto ausente 208 clasificado permanente;
- timeout real de un `WAITFOR`;
- cancelación real de un comando;
- 25 ciclos con `Max Pool Size=5`, demostrando liberación de conexiones.

No usa `P360_CONNECTION_PRINCIPAL`, no crea objetos persistentes y no toca datos P360. Requiere SQL Server LocalDB; si no está instalado, el script falla de forma explícita en vez de simular éxito.

Antes de desplegar, un responsable debe repetir sólo lecturas en una base P360 no productiva compatible y confirmar nombres/tipos/permisos de las vistas y SP. Las pruebas automatizadas nunca apuntan a producción.

## Rollback

1. detener la instancia candidata;
2. restaurar el binario anterior;
3. retirar las dos variables nuevas si el release anterior no las reconoce;
4. iniciar una sola instancia y verificar carga de definiciones/cola;
5. conservar eventos por correlación como evidencia.

No hay rollback de esquema. El método síncrono público de cola se conserva como adaptador de compatibilidad y delega al repositorio nuevo; los jobs normales usan la API async cancelable.

## Límites deliberados

- No se reintenta ninguna operación.
- En PR-08 no se cambió claim/lease, idempotencia, confirmación ni dead-letter; PR-10 los implementa después bajo D-003.
- No se modificó SQL de negocio ni el esquema.
- No se tocó ningún `.rpt`, `.Designer.cs` o `.resx`.
- El ejecutable completo sigue siendo Windows x64/net48 por Crystal/DevExpress; este cambio no convierte el host legado en ejecutable Linux.
