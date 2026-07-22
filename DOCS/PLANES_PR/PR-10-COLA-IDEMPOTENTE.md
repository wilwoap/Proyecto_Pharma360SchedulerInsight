# PR-10 — Cola idempotente y recuperación

Estado: bloqueado por D-003. Dependencias: PR-08, PR-09 y coordinación DBA.

## Propósito

Evitar doble procesamiento concurrente y hacer recuperables los fallos parciales entre SQL, renderizado y SMTP.

## Alcance

- Identificador/idempotency key estable.
- Estados explícitos de cola.
- Claim atómico con lease.
- Intentos, próximo intento y error clasificado.
- Confirmación de envío.
- Cola muerta y reconciliación.
- Compatibilidad expand/contract.

## Modelo propuesto

Campos conceptuales:

- notificationId/idempotencyKey único;
- status;
- leaseOwner y leaseUntilUtc;
- attemptCount y nextAttemptUtc;
- sentUtc;
- lastErrorCode redactado;
- versión de fila.

La forma exacta se define con DBA y esquema existente.

## Implementación

1. Aprobar semántica: preferencia duplicado/pérdida y ventana.
2. Agregar esquema/procedimientos compatibles.
3. Implementar claim atómico, por ejemplo UPDATE con OUTPUT y condiciones.
4. renovar/liberar lease con límites;
5. separar render, entrega, confirmación;
6. aplicar backoff con jitter y máximo;
7. clasificar permanentes a dead-letter;
8. implementar reconciliación del caso SMTP aceptado/confirmación SQL fallida;
9. activar gradualmente por tipo de reporte.

## Realidad de consistencia

SQL y SMTP no comparten transacción. “Exactly once” no puede prometerse sólo desde la aplicación. El diseño debe usar estado durable, clave observable y reconciliación, y declarar el residuo de duplicación aceptable.

## Pruebas

- Dos workers reclaman simultáneamente.
- Caída antes/después de render, SMTP y confirmación.
- Lease expira/renueva.
- Reintento con backoff.
- Permanente a dead-letter.
- Reprocesamiento manual auditado.
- Migración con versión vieja y nueva coexistiendo.
- Volumen pico y limpieza.

## Criterios de aceptación

- Una fila tiene máximo un lease activo.
- Cada transición es auditable y válida.
- Caídas inyectadas se recuperan sin pérdida silenciosa.
- Duplicación residual cumple D-003 y se detecta.
- Operación puede inspeccionar/reintentar dead-letter sin editar tablas a mano.
- Versión anterior sigue funcionando durante expansión.

## Rollback

Desactivar consumo nuevo, esperar/expirar leases y volver al lector anterior compatible. Mantener columnas/procedimientos añadidos; contraer esquema sólo en un PR posterior tras estabilización. Reconciliar antes de reanudar.

