# PR-09 — Scheduler Quartz endurecido

Estado: validado localmente el 2026-07-22. Dependencias PR-04, PR-07 y PR-08 completadas; D-007 y D-009 cerradas técnicamente para la etapa net48.

## Propósito

Hacer determinista y observable la interpretación de programaciones, concurrencia, misfires y cambios sin alterar las expresiones Cron ni los reportes.

## Alcance implementado

- Modelo inmutable de definición.
- Validación aislada por fila y proyección SQL obligatoria.
- Identidades estables basadas en `report_id`.
- Cron, zona horaria y misfire explícitos.
- Exclusión de solapamiento por reporte, con rollback configurable.
- Thread pool global explícito, acotado y medido.
- Reconciliación idempotente de altas/cambios/bajas mediante fingerprint.
- Listener de misfire que no puede interrumpir Quartz.
- Manejo seguro de next fire time opcional.
- Política explícita de cero refire inmediato mientras la entrega no sea idempotente.

## Decisiones conservadoras

1. SQL continúa como fuente de verdad y `RAMJobStore` se reconstruye al arrancar.
2. La zona predeterminada es la local, igual que en el trigger heredado, pero ahora queda registrada y puede fijarse por ambiente.
3. `fire_once_now` hace explícita la interpretación histórica de `SmartPolicy` para Cron.
4. Una caída completa no genera catch-up porque el job store no persiste historial.
5. El mismo `report_id` no se solapa; distintos IDs pueden correr hasta el límite global.
6. El límite predeterminado sigue siendo 10.
7. SQL cambia mediante reinicio controlado; no se añadió polling.
8. No se añadieron calendarios de feriados sin definición funcional.

## Implementación realizada

1. Todas las filas se leen y validan antes de aplicar el lote.
2. Una fila inválida emite `scheduler.definition.rejected`; las válidas continúan.
3. Las claves son `P360.Reports/report-{id}` y `P360.Reports/report-{id}-cron`.
4. Un fingerprint SHA-256 cubre datos funcionales, Cron y política.
5. La reconciliación agrega, conserva, actualiza o elimina sin duplicar.
6. `JobBuilder.DisallowConcurrentExecution(true)` evita solapamiento por clave estable.
7. `DefaultThreadPool.maxConcurrency` se configura entre 1 y 64, con 10 por defecto.
8. `JobExecutionException` nunca solicita refire/unschedule automático.
9. La próxima ejecución ausente se representa como `no_disponible`.
10. `build.ps1` selecciona MSBuild amd64 cuando existe, para resolver de forma reproducible los ensamblados propietarios x64.

No se consultó una base P360 real: el inventario anonimizado de schedules debe ejecutarse sólo en no producción autorizada durante despliegue. La política efectiva de cada definición queda observable en runtime.

## Fuera de alcance

- Idempotencia de la cola, cubierta por PR-10.
- Cambiar expresiones Cron.
- Polling o refresh en caliente.
- Job store persistente, clustering o ejecución multi-instancia.
- Calendarios de feriados.
- Adoptar Quartz 4.
- Modificar o cargar Crystal en pruebas.

## Pruebas

- Cron inválido, identidad repetida y tipo desconocido.
- Dos nombres visibles iguales con IDs distintos.
- Espera superior al umbral con `fire_once_now` y `do_nothing`.
- Dos triggers simultáneos del mismo job.
- Alta, repetición idempotente, cambio y eliminación de definición.
- NextFireTime ausente.
- Zona sintética con DST sin cambiar el reloj del host.
- Límite 2 con cuatro jobs y backlog.
- Evento, gauge y métrica de misfire.
- Política de no refire inmediato.

## Criterios de aceptación

- [x] Cada definición aceptada registra zona, misfire y solapamiento.
- [x] Una fila mala no derriba definiciones válidas.
- [x] IDs repetidos no dependen del orden de lectura.
- [x] No hay solapamiento del mismo reporte por defecto.
- [x] El comportamiento ante reinicio/standby está definido en D-007.
- [x] SQL/RAMJobStore y refresh están definidos en D-009.
- [x] Rechazados, misfires, activos, límite y duración son observables.
- [x] Quartz 3.18.2 pasa las pruebas reales sobre RAMJobStore.
- [x] 85/85 pruebas net48 x64 correctas.
- [x] Ningún `.rpt`, `.Designer.cs` o `.resx` modificado.
- [ ] Inventario/canary contra P360 no productivo autorizado antes de despliegue.

## Rollback

Para volver temporalmente al solapamiento heredado, fijar `P360_QUARTZ_DISALLOW_CONCURRENT_EXECUTION=false` y reiniciar una sola instancia. Para volver a la zona del host, retirar `P360_QUARTZ_TIME_ZONE`. Si las identidades o la reconciliación difieren del canary aprobado, detener la candidata y restaurar el binario de PR-08; no ejecutar ambos a la vez ni intentar catch-up masivo.

La guía completa está en `DOCS/19_QUARTZ_ENDURECIDO.md`.
