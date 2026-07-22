# PR-12 — Correo y plantillas seguras

Estado: propuesto. Dependencias: PR-05, PR-07 y PR-10.

## Propósito

Componer y entregar mensajes con validación, codificación y resultado observable, sin estado global ni exposición de claves.

## Alcance

- IEmailComposer e IEmailSender.
- Modelo de destinatarios/adjuntos validado.
- Codificación HTML por contexto.
- Sustitución de la clave de mapas en URL enviada.
- SMTP con timeout/cancelación y resultado tipado.
- Eliminación de NotificaAdministrador estático.
- Límites de tamaño y redacción.

## Implementación

1. Caracterizar plantillas y placeholders.
2. Separar datos, composición y transporte.
3. definir allowlist de placeholders y codificación;
4. normalizar/validar To, CC, subject y headers;
5. obtener mapa del lado servidor o mediante URL temporal controlada;
6. adjuntar streams/archivos con vida acotada;
7. no absorber errores SMTP;
8. integrar resultado con estados de PR-10;
9. añadir rate limit/backoff según proveedor.

## Fuera de alcance

- Cambiar contenido/branding sin aprobación.
- Prometer entrega exactamente una vez.
- Cambiar proveedor SMTP sin decisión.

## Pruebas

- Caracteres HTML, script, URL y header injection.
- Lista To/CC válida/inválida.
- Clave de mapa ausente en cuerpo/URL/log.
- SMTP accept/reject/timeout.
- Adjuntos grandes/bloqueados.
- Ejecuciones concurrentes sin fuga de estado.
- Sink confirma cuerpo/encoding sin enrutar.

## Criterios de aceptación

- Ningún dato dinámico entra al HTML sin codificación contextual.
- Ninguna clave aparece en el mensaje.
- No existe estado global por notificación.
- Fallo SMTP llega a retry/dead-letter correcto.
- Límites y TLS están configurados.
- Logs/telemetría no exponen cuerpo ni destinatarios completos.
- Plantillas críticas mantienen aprobación visual.

## Rollback

Conservar adaptador SMTP anterior sólo detrás de contrato, seleccionable para canary, pero nunca restaurar exposición de clave ni HTML inseguro. Pausar envío si el proveedor nuevo falla persistentemente.

