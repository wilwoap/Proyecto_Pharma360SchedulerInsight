# Quartz endurecido

Estado: validado localmente en PR-09 el 2026-07-22. La política se probó con definiciones sintéticas y `RAMJobStore`; el inventario autorizado de schedules P360 no productivos continúa como gate de despliegue.

## Resultado

Cada definición SQL se convierte en un modelo inmutable, se valida de forma independiente y recibe una identidad estable derivada de `report_id`. Quartz registra de forma explícita zona horaria, política de misfire, solapamiento y límite global. Una fila incorrecta queda pausada y genera telemetría, sin impedir que las demás se agenden.

No se cambiaron expresiones Cron, archivos de reporte ni reglas de generación/envío. El ejecutable continúa siendo Windows x64 sobre .NET Framework 4.8 y no se modificó ningún `.rpt`, `.Designer.cs` o `.resx`.

## Política efectiva

| Dimensión | Predeterminado | Motivo |
|---|---|---|
| Fuente de verdad | vista SQL existente, cargada al arrancar | conserva el contrato actual |
| Job store | `RAMJobStore` explícito | no introduce esquema ni segunda fuente de verdad |
| Zona horaria | `TimeZoneInfo.Local`, resuelta una vez | conserva el horario efectivo del host anterior |
| Misfire Cron | `fire_once_now` | hace explícita la interpretación que Quartz aplicaba a `SmartPolicy` para Cron |
| Umbral de misfire | 60 segundos | hace explícito el valor histórico/default de Quartz |
| Solapamiento del mismo reporte | prohibido | evita dos ejecuciones simultáneas con el mismo `report_id` |
| Concurrencia global | 10 | conserva el default de Quartz y queda acotado/configurable |
| Refire inmediato por excepción | nunca | SQL, render y SMTP aún no son idempotentes de extremo a extremo |
| Refresh de SQL | reinicio controlado | no se añade polling ni un segundo scheduler |

`fire_once_now` sólo aplica si el trigger ya existe y supera el umbral por standby o falta de capacidad. Como `RAMJobStore` pierde toda programación cuando termina el proceso, una caída completa no conserva ejecuciones pendientes: al reiniciar se reconstruyen los triggers desde SQL y se continúa con la siguiente ocurrencia futura. No existe catch-up de todas las ocurrencias perdidas.

Quartz documenta que `RAMJobStore` pierde jobs y triggers al terminar el proceso, que el límite predeterminado del thread pool es 10 y que el umbral predeterminado de misfire es 60 segundos. También documenta que `SmartPolicy` de un `CronTrigger` se interpreta como `FireOnceNow` ([referencia de configuración](https://www.quartz-scheduler.net/documentation/quartz-3.x/configuration/reference.html), [CronTrigger y misfires](https://www.quartz-scheduler.net/documentation/quartz-3.x/tutorial/crontriggers.html)).

## Variables

| Variable | Valores | Predeterminado |
|---|---|---|
| `P360_QUARTZ_TIME_ZONE` | identificador reconocido por `TimeZoneInfo` en el host | zona local |
| `P360_QUARTZ_MISFIRE_POLICY` | `fire_once_now`, `do_nothing` | `fire_once_now` |
| `P360_QUARTZ_DISALLOW_CONCURRENT_EXECUTION` | `true`, `false` | `true` |
| `P360_QUARTZ_MAX_CONCURRENCY` | entero 1–64 | 10 |

Para eliminar dependencia de la configuración regional del servidor, despliegue debe fijar una zona horaria validada en el Windows objetivo. Por ejemplo, Ecuador suele representarse como `SA Pacific Standard Time`, pero el valor debe comprobarse con `TimeZoneInfo.FindSystemTimeZoneById` en la imagen real antes de promoverlo. Un identificador desconocido detiene el proceso antes de iniciar Quartz y nunca se refleja en el error.

Cambiar cualquiera de estas variables exige reiniciar. `do_nothing` descarta un disparo retrasado y continúa con la siguiente ocurrencia; sólo debe activarse con aprobación operativa porque difiere del comportamiento Cron histórico.

## Identidad y reconciliación

Las claves ya no dependen del nombre visible:

    job:     P360.Reports/report-{report_id}
    trigger: P360.Reports/report-{report_id}-cron

El nombre sigue disponible en `JobDataMap` para la lógica funcional. Dos reportes con el mismo nombre y distinto ID no colisionan; dos filas con el mismo ID se rechazan ambas para evitar que el orden SQL decida cuál gana.

Un fingerprint SHA-256 cubre campos funcionales, Cron y política. La reconciliación prepara y valida todo el lote antes de tocar Quartz, y luego:

- agrega IDs nuevos;
- no modifica definiciones con el mismo fingerprint;
- reemplaza job y trigger cuando cambia el fingerprint;
- elimina IDs que dejaron de existir o quedaron inválidos;
- limpia triggers huérfanos dentro del grupo administrado.

El host ejecuta esta reconciliación una vez antes de `Start`. No existe polling: una modificación SQL entra en vigor después de un reinicio controlado. Esto cierra D-009 para la etapa net48 sin añadir tablas `QRTZ_*`; un job store persistente sólo se reconsiderará si se aprueba catch-up durable, clustering o refresh en caliente.

## Validación y fallos

Se rechazan de manera aislada:

- definición nula;
- `report_id` no positivo o repetido;
- UID o nombre vacío;
- tipo distinto de `crystal reports`, `devexpress reports` o `html`;
- expresión Cron inválida;
- fallo al construir el job/trigger.

La proyección SQL completa se verifica antes de leer filas. Una columna ausente sigue siendo un fallo del contrato de dependencia; un valor defectuoso en una fila se registra como `mapping_error` y no descarta filas sanas.

Las excepciones de jobs crean `JobExecutionException` con `RefireImmediately=false` y sin desagendar triggers. Reintentar de inmediato antes de PR-10 podría duplicar renderizados, correos o confirmaciones. La próxima ejecución opcional se muestra como `no_disponible` cuando Quartz no tiene otra ocurrencia, en lugar de desreferenciar un nullable.

## Concurrencia

Quartz impide el solapamiento por `JobDetail`, por lo que la clave estable hace que la exclusión corresponda exactamente a un `report_id`; reportes diferentes aún pueden ejecutarse en paralelo. El thread pool impone además el límite global. Quartz describe que la exclusión se aplica por definición de job y no por clase completa ([More About Jobs](https://www.quartz-scheduler.net/documentation/quartz-3.x/tutorial/more-about-jobs.html)).

El valor 10 es un límite de seguridad compatible, no un tuning definitivo. PR-15 deberá medir tiempos, CPU, memoria, handles, SQL y SMTP antes de cambiarlo. Reducirlo puede generar backlog/misfires; aumentarlo puede saturar SQL, renderizadores o SMTP.

## Observabilidad

Eventos nuevos:

| Evento | Significado |
|---|---|
| `scheduler.definition.rejected` | fila pausada con razón acotada |
| `scheduler.definition.unchanged` | fingerprint idéntico |
| `scheduler.definition.updated` | definición reemplazada |
| `scheduler.definition.removed` | ID ausente/invalidado retirado |
| `scheduler.definitions.reconciled` | resumen de alta/cambio/baja |
| `scheduler.trigger.misfired` | trigger retrasado sobre el umbral |

Gauges acotados:

- `scheduler_definition_rows_rejected`;
- `scheduler_definitions_rejected`;
- `scheduler_definitions_active`;
- `scheduler_max_concurrency`;
- `scheduler_misfires_total`.

La operación `scheduler.misfire` registra count y duración con outcome `skipped`. Jobs activos y duración por tipo ya forman parte de health/operaciones. El listener captura sus propios fallos y nunca altera el ciclo de Quartz, conforme a la advertencia oficial para listeners ([Trigger and Job Listeners](https://www.quartz-scheduler.net/documentation/quartz-3.x/tutorial/trigger-and-job-listeners.html)).

## Pruebas y evidencia

El arnés aislado valida:

- 85/85 pruebas net48 x64;
- Cron inválido, tipo desconocido, ID repetido y nombres visibles repetidos;
- alta, ejecución idempotente, cambio y baja sobre `RAMJobStore` real;
- políticas `fire_once_now` y `do_nothing` después de una espera superior al umbral;
- zona sintética con transición DST aunque el host no cambie el reloj;
- dos triggers simultáneos del mismo job sin solapamiento;
- cuatro jobs con límite global 2 y backlog observable;
- próxima ejecución ausente;
- refire inmediato deshabilitado;
- métrica/evento de misfire sin cardinalidad dinámica.
- fallo del exportador de telemetría aislado dentro del listener.

Todas las definiciones, rutas y conexiones usadas son sintéticas. Estas pruebas no leen `P360_CONNECTION_PRINCIPAL`, no consultan P360, no envían SMTP y no cargan Crystal.

## Despliegue y rollback

Antes del canary:

1. inventariar en una base P360 no productiva autorizada los IDs, UIDs, tipos y Cron, sin extraer destinatarios ni credenciales;
2. verificar que la zona configurada corresponde al horario aprobado;
3. ejecutar con una sola instancia y confirmar `definitions_active`, rechazados y próximos disparos;
4. simular standby mayor a 60 segundos en no producción y confirmar la política elegida;
5. observar al menos un ciclo de cada tipo y comparar volumen de notificaciones;
6. promover sólo si no hay filas rechazadas inesperadas ni solapamientos.

Rollback parcial de solapamiento: fijar `P360_QUARTZ_DISALLOW_CONCURRENT_EXECUTION=false` y reiniciar una sola instancia. Rollback de zona: retirar `P360_QUARTZ_TIME_ZONE` para volver a la zona local. La política histórica ya es `fire_once_now`. Si identidad o reconciliación difieren del comportamiento aprobado, detener la candidata, restaurar el binario de PR-08 y reiniciar; nunca ejecutar ambos binarios simultáneamente ni generar catch-up manual masivo.

## Riesgo residual

- La zona local sigue dependiendo de la imagen hasta que despliegue fije un ID.
- `RAMJobStore` no ofrece recuperación durable ni coordinación entre procesos.
- Dos procesos independientes podrían ejecutar el mismo reporte; el canary y la etapa net48 admiten una sola instancia.
- No hay calendarios de feriados porque no existe una regla aprobada.
- Los cambios SQL requieren reinicio.
- La idempotencia de correo/cola se implementa posteriormente en D-003/PR-10; PR-09 no reintenta trabajo por sí solo.
