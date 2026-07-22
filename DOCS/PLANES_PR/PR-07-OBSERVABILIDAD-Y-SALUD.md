# PR-07 — Observabilidad y salud

Estado: propuesto. Dependencia: PR-06 y decisión D-010.

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

1. Acordar plataforma/sink y disponibilidad durante caída SQL.
2. Definir catálogo de eventos y campos.
3. Generar/propagar correlación.
4. Instrumentar etapas sin registrar cuerpos/secretos.
5. añadir métricas de conteo, duración, backlog y recursos;
6. implementar salud con detalle protegido;
7. crear alertas a partir de baseline, no números arbitrarios;
8. mantener tabla SQL como auditoría sólo si tiene propósito definido.

## Fuera de alcance

- Reescribir toda consulta.
- Fijar SLO sin observación.
- Guardar contenido de correo para depuración.

## Pruebas

- Redacción de connection strings, API keys, cuerpo y destinatarios.
- Correlación a través de job, render, envío y confirmación.
- Sink primario caído.
- SQL caído sin recursión de logging.
- Health cambia con inicio/parada/dependencia.
- Cardinalidad acotada de métricas.

## Criterios de aceptación

- Cada ruta crítica emite inicio, fin, duración y resultado correlacionados.
- Logs siguen disponibles si falla el logging SQL.
- Secret/PII scan de logs de prueba limpio.
- Liveness/readiness consumibles por operación.
- Dashboard y al menos alertas de servicio caído, backlog y tasa de fallo.
- Coste/retención definidos.

## Rollback

Permitir desactivar sink/exportador sin desactivar los controles funcionales. Conservar logging mínimo local seguro. Revertir instrumentación si afecta rendimiento sobre umbral demostrado.

