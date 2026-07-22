# Rollback de PR-10

Este cambio usa **expand/contract**. El rollback inmediato no elimina columnas,
índices, auditoría ni procedimientos porque hacerlo podría destruir estado de
entrega y bloquear a la versión anterior.

1. Detener todos los consumidores; una versión antigua ignora leases y no debe
   coexistir con estado durable activo.
2. Esperar al menos el valor configurado en `P360_NOTIFICATION_LEASE_SECONDS`
   para que no quede un envío durable en curso.
3. Verificar que no existan filas `processing` y reconciliar `retry`,
   `dead_letter` y entregas inciertas antes de reanudar el lector heredado.
4. Fijar `P360_NOTIFICATION_QUEUE_MODE=legacy` y reiniciar una sola instancia
   únicamente cuando los estados no terminales estén resueltos.
5. Conservar la expansión. Una contracción física necesita respaldo, ventana
   DBA y un PR independiente después de estabilización.

Consulta de verificación, sin mutaciones:

```sql
SELECT
    p360_delivery_status,
    COUNT_BIG(*) AS filas,
    MIN(p360_lease_until_utc) AS lease_min_utc,
    MAX(p360_lease_until_utc) AS lease_max_utc
FROM P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES
GROUP BY p360_delivery_status;
```
