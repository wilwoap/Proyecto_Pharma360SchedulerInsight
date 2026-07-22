# Cambios de base de datos

Los scripts de `migrations/` son artefactos para revisión y ejecución DBA. No
se aplican automáticamente durante build, pruebas o arranque de la aplicación.

Reglas:

- probar primero en copia/no productivo equivalente;
- mantener compatibilidad expand/contract;
- acompañar cada expansión con preflight, canary y rollback no destructivo;
- no incluir datos, credenciales ni cadenas de conexión;
- no ejecutar una contracción durante un incidente;
- registrar quién, cuándo y sobre qué versión aplicó cada script.

PR-10 añade la primera expansión. Su runbook está en
`DOCS/20_COLA_DURABLE_E_IDEMPOTENTE.md`.
