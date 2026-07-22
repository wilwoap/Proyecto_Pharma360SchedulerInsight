# PR-10 — Cola idempotente y recuperación

Estado: validado localmente el 2026-07-22. Dependencias técnicas PR-08/PR-09
cumplidas; D-003 aceptada. Activación P360 pendiente de revisión/ejecución DBA,
SMTP interceptado y canary.

## Propósito

Evitar doble procesamiento concurrente y hacer recuperables los fallos
parciales entre SQL, renderizado y SMTP sin romper el lector heredado.

## Decisión D-003

- Entrega al menos una vez.
- Se prefiere duplicado detectable a pérdida silenciosa.
- La clave durable es estable y se transmite como cabecera observable.
- No se promete exactamente una vez entre SQL y SMTP.
- Una confirmación SQL fallida después de SMTP se marca `uncertain` y exige
  reconciliación.

## Alcance implementado

- Modo `legacy` predeterminado y `durable` opt-in.
- Canary por lista de `report_id`.
- Migración SQL expand-only, reejecutable y compatible con columnas heredadas.
- Identificador estable y estados explícitos.
- Claim atómico con lease, token y bloqueo del worker obsoleto.
- Renovación justo antes de SMTP.
- Fallos de render y entrega persistidos.
- Intentos, próxima elegibilidad, backoff exponencial con jitter y máximo.
- Clasificación permanente/transitoria sin guardar mensajes ni PII.
- Confirmación condicionada al claim.
- Confirmación durable antes del log SQL secundario; un fallo de auditoría no
  revierte una entrega ya confirmada.
- Dead-letter, consulta operativa y reproceso manual auditado.
- Auditoría append-only de transiciones.
- Preflight de esquema fail-fast antes de iniciar Quartz.
- Cabecera `X-P360-Notification-Key` y evento de entrega incierta.

## Fuera de alcance

- Down migration destructiva.
- Poller o dispatcher independiente del Cron; se evalúa en PR-11 según SLO.
- Garantía de deduplicación del servidor SMTP.
- Retención/propietario operativo de dead-letter, pendientes en D-011.
- Ejecución del script contra P360 real desde este entorno.
- Cualquier modificación de `.rpt` o renderers Crystal.

## Implementación

1. D-003 registrada como al menos una vez.
2. Opciones acotadas y modo heredado por defecto.
3. Expansión SQL con clave, estado, lease, intentos, fechas y auditoría.
4. Procedimientos de claim, renew, complete, fail, list y manual requeue.
5. Claim conectado al lector por reporte/canary.
6. Lease renovado después del render y antes de SMTP.
7. Confirmación/fallo condicionados a propietario y token.
8. Fallos de render conectados a la misma máquina de estados.
9. Identidad observable aplicada al mensaje y telemetría.
10. Preflight y contrato LocalDB efímero.

## Pruebas y evidencia

- Build Release x64 y 102/102 pruebas aisladas.
- Configuración default/explicita/inválida.
- Mapeo durable y parámetros ADO.NET tipados.
- Fallo SMTP, confirmación incierta, lease perdido y cabecera estable.
- Fallo del log SQL secundario observable sin deshacer la confirmación.
- Clasificador permanente/transitorio.
- Migración aplicada dos veces en LocalDB.
- Dos workers concurrentes reclaman filas distintas.
- Token obsoleto no renueva ni confirma.
- Lease expira y otro worker reclama con nueva identidad de claim.
- Backoff impide claim temprano.
- Segundo fallo con máximo 2 pasa a dead-letter.
- Requeue manual conserva clave, reinicia intentos y queda auditado.
- Cleanup de la instancia `P360PR10_*` en `finally`.

Comandos:

```powershell
.\build.ps1 -Configuration Release -Target Rebuild
.\eng\Test-NotificationQueueContracts.ps1 -Configuration Release
```

## Criterios de aceptación

- [x] Una fila tiene máximo un lease activo.
- [x] El token anterior no puede confirmar después de reclaim.
- [x] Cada transición implementada es auditable.
- [x] Fallos transitorios son recuperables y acotados.
- [x] Fallos permanentes/máximo llegan a dead-letter.
- [x] Reproceso manual no requiere editar tablas.
- [x] Clave estable se conserva entre intentos y aparece en SMTP/telemetría.
- [x] El modo anterior sigue siendo el predeterminado.
- [x] La aplicación falla antes de Quartz si se activa un esquema incompleto.
- [x] Crystal Reports y activos generados permanecen intactos.
- [ ] Inventario/esquema P360 no productivo aprobado por DBA.
- [ ] Canary con SMTP interceptado y reconciliación de `uncertain`.
- [ ] Retención y propietario de dead-letter cerrados en D-011.

Los tres últimos son gates de despliegue, no evidencia simulable localmente.

## Rollback

Detener consumidores, esperar un lease completo y reconciliar `processing`,
`retry`, `dead_letter` y `uncertain` antes de iniciar una sola instancia en
`legacy`; una versión antigua no puede coexistir con leases activos. Se
conservan los objetos añadidos. Una contracción sólo puede ocurrir en un PR
posterior con backup y ventana DBA.

Runbook completo: `DOCS/20_COLA_DURABLE_E_IDEMPOTENTE.md`.
