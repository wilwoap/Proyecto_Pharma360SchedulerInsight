# PR-08 — Acceso a datos resiliente

Estado: propuesto. Dependencias: PR-05 y PR-07.

## Propósito

Hacer explícitos contratos, límites y fallos de SQL sin cambiar todavía el esquema.

## Alcance

- Repositorios/adaptadores por caso de uso.
- Conexiones/comandos con vida acotada.
- Parámetros tipados.
- Timeouts finitos y cancelación.
- Clasificación de errores transitorio/permanente.
- Retiro de capturas que absorben fallos.
- Logging independiente del resultado funcional.

## Implementación

1. Inventariar cada operación y contrato.
2. Extraer primero scheduled reports y notification queue.
3. Definir timeout por operación a partir de baseline.
4. Reemplazar AddWithValue por tipo/tamaño.
5. usar using/Dispose sistemático;
6. propagar cancellation tokens donde APIs lo permitan;
7. retornar resultados tipados, no null/vacío ambiguo;
8. aplicar retry sólo a operaciones idempotentes;
9. retirar StackFrame como identidad funcional.

## Fuera de alcance

- Cambios destructivos de esquema.
- ORM nuevo.
- Retry global.
- Hacer asíncrona una API sin beneficio/soporte.

## Pruebas

- Contrato contra SQL compatible.
- Timeout y cancelación.
- Login/permiso/objeto ausente.
- Error de logging durante error principal.
- Resultados vacíos válidos frente a fallo.
- Pooling y liberación bajo repetición.

## Criterios de aceptación

- Cero commandTimeout infinito en rutas migradas.
- Toda operación migrada documenta parámetros/resultados/permisos.
- Excepciones conservan causa y categoría.
- No se reporta éxito ante fallo.
- Reintentos limitados y seguros.
- Métricas muestran duración/fallo sin datos sensibles.

## Rollback

Adaptadores nuevos detrás de interfaz y selección temporal. Revertir una operación individual al adaptador legado; no revertir correcciones de seguridad ni credenciales.

