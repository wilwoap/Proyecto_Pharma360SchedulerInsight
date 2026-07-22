# PR-08 — Acceso a datos resiliente

Estado: validado localmente el 2026-07-22. Dependencias: PR-05 y PR-07 satisfechas. Contrato P360 no productivo pendiente como gate de despliegue.

## Propósito

Hacer explícitos contratos, límites y fallos de SQL sin cambiar todavía el esquema.

## Resultado implementado

- `SqlExecutionPolicy` fuerza conexión 1-120 s y comando 1-300 s; defaults 15/30.
- Repositorios explícitos para scheduled reports y notification queue.
- Carga async/cancelable desde Quartz hasta `System.Data.SqlClient`.
- `@ReportId int` y parámetros heredados migrados sin `AddWithValue`.
- `DataAccessException` conserva causa y clasifica cancelación, timeout, transitorio, permanente o desconocido.
- Cero reintentos de consultas/escrituras en código: las escrituras siguen teniendo una sola tentativa.
- Métricas `data.report-schedules` y `data.notification-queue`, sin datos sensibles.
- Adaptador síncrono de cola conservado para compatibilidad, delegando al repositorio async.

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

- 68/68 pruebas aisladas net48 x64.
- Contrato ADO real contra una instancia LocalDB efímera y sintética.
- Timeout y cancelación reales mediante `WAITFOR`.
- Objeto ausente 208 clasificado permanente; login/permiso cubiertos por clasificación, sin credenciales reales.
- Resultado vacío válido frente a excepción tipada.
- 25 ciclos con `Max Pool Size=5` para demostrar pooling/liberación.
- Fallo de logging continúa independiente del error principal por el contrato de PR-07.

## Criterios de aceptación

- Cero commandTimeout infinito en rutas migradas.
- Toda operación migrada documenta parámetros/resultados/permisos.
- Excepciones conservan causa y categoría.
- No se reporta éxito ante fallo.
- Reintentos limitados y seguros.
- Métricas muestran duración/fallo sin datos sensibles.

## Rollback

Detener la candidata, restaurar el binario anterior y retirar las dos variables de timeout si fuera necesario. No existe rollback de esquema. El adaptador síncrono público permanece, pero no se mantiene una segunda implementación SQL que pueda divergir.

## Evidencia detallada

Ver [18_ACCESO_A_DATOS_RESILIENTE.md](../18_ACCESO_A_DATOS_RESILIENTE.md). Ningún `.rpt`, `.Designer.cs` o `.resx` fue modificado.
