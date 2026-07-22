# PR-07 — Observabilidad y salud

Estado: núcleo neutral validado localmente el 2026-07-22. Dependencia de PR-06 satisfecha; D-010 mantiene pendiente el sink corporativo, retención, dashboard y alertas desplegadas.

## Propósito

Saber si el proceso está sano, qué trabajo realiza y dónde falla, sin depender exclusivamente de SQL ni exponer datos sensibles.

## Alcance

- Logging estructurado.
- correlationId por ejecución/notificación.
- Métricas de scheduler, cola, renderer, SMTP y proceso.
- Liveness/readiness.
- Política de redacción.
- Alertas y dashboard inicial.

## Implementación

1. Se implementó un sink JSON Lines neutral sobre stdout; SQL queda como auditoría secundaria tolerante a fallos.
2. Se definió un catálogo estable de eventos, operaciones y campos permitidos.
3. Cada job genera correlación y cada notificación una correlación hija.
4. Scheduler, jobs, notificaciones, renderers y SMTP emiten inicio/fin/resultado/duración.
5. Se añadieron contadores/duraciones acotados, tamaño de lote y recursos de proceso.
6. Se implementaron estados de salud y un archivo JSON atómico opcional con heartbeat de 15 segundos.
7. Se documentaron paneles y condiciones de alerta neutrales sin umbrales arbitrarios.
8. Se redactaron rutas críticas del logging heredado y se conserva la semántica funcional de fallos SMTP.

## Fuera de alcance

- Reescribir toda consulta.
- Fijar SLO sin observación.
- Guardar contenido de correo para depuración.

## Pruebas

- Redacción de connection strings, API keys, cuerpo y destinatarios.
- Correlación a través de job, notificación, render y envío.
- Sink primario caído.
- SQL caído sin recursión de logging.
- Health cambia con inicio/parada/dependencia.
- Cardinalidad acotada de métricas.

## Criterios de aceptación

- [x] Cada ruta crítica emite inicio, fin, duración y resultado correlacionados.
- [x] Los eventos primarios siguen disponibles si falla el audit SQL.
- [x] Secret/PII scan de eventos y logs críticos de prueba limpio.
- [x] Liveness/readiness consumibles mediante JSON y stdout.
- [x] Dashboard y condiciones de alerta definidos de forma neutral.
- [ ] Dashboard/alertas materializados en la plataforma, pendiente de D-010.
- [ ] Coste y retención aprobados, pendiente de D-010/D-011.

## Evidencia de validación

- build Release x64 y 56/56 pruebas aprobadas;
- fallo SMTP produce métricas de delivery/notificación fallidas sin marcar la cola;
- sink SQL simulado caído sin pérdida del evento principal;
- health `starting/ready/stopping/stopped/faulted`, heartbeat y escritura atómica;
- cardinalidad de métricas fija aunque cambie `report_uid`;
- ningún `.rpt`, diseñador o recurso visual modificado;
- catálogo, runbook y dashboard neutral en `DOCS/17_OBSERVABILIDAD_Y_SALUD.md`.

## Rollback

Quitar `P360_HEALTH_FILE_PATH` y reiniciar para desactivar el exporter de archivo sin afectar controles funcionales. El JSON mínimo de consola se conserva. Para rollback completo, restaurar el release anterior y observar una sola instancia; revertir instrumentación si una medición reproducible demuestra impacto de rendimiento.
