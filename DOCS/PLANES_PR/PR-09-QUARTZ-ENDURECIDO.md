# PR-09 — Scheduler Quartz endurecido

Estado: propuesto. Dependencias: PR-04, PR-07 y PR-08; decisiones D-007 y D-009.

## Propósito

Hacer determinista y observable la interpretación de programaciones, concurrencia, misfires y cambios.

## Alcance

- Modelo tipado de definición.
- Validación aislada de cada fila.
- Identidades estables basadas en IDs.
- Cron y zona horaria explícitos.
- Política de misfire explícita.
- No solapamiento cuando corresponda.
- Límite global de concurrencia.
- Reconciliación de altas/cambios/bajas.
- Manejo seguro de next fire time opcional.

## Implementación

1. Inventariar schedules reales anonimizados.
2. Aprobar zona/misfire/solapamiento por tipo.
3. Parsear y validar todas las filas antes de aplicar.
4. rechazar una inválida sin abortar válidas y alertar;
5. usar claves que no dependan sólo del nombre visible;
6. aplicar DisallowConcurrentExecution o coordinación equivalente;
7. configurar thread pool y límites con medición;
8. reconciliar cambios de SQL de forma idempotente;
9. definir JobExecutionException/refire según clasificación.

## Fuera de alcance

- Idempotencia de la cola, cubierta por PR-10.
- Cambiar reglas Cron sin aprobación.
- Adoptar Quartz 4.

## Pruebas

- Cron inválido, identidad repetida y tipo desconocido.
- Reinicio con misfires.
- Dos triggers simultáneos.
- Cambio/eliminación de definición.
- NextFireTime ausente.
- Zona horaria/DST aunque el entorno local no cambie reloj.
- Límite de concurrencia y backlog.

## Criterios de aceptación

- Cien por ciento de definiciones tienen política registrada.
- Una fila mala no derriba el servicio.
- No hay solapamiento no aprobado.
- Reinicio produce exactamente el comportamiento D-007.
- Métricas muestran rechazados, misfires, activos y duración.
- Upgrade Quartz validado con las mismas pruebas.

## Rollback

Bandera para volver al cargador anterior sólo durante canary, sin ejecutar ambos a la vez. Preservar definiciones SQL. Pausar schedules si la semántica observada difiere, en vez de generar catch-up masivo.

