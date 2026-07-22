# PR-11 — Pipeline de reportes y archivos

Estado: validado localmente el 2026-07-22. Dependencias PR-02 y PR-05 satisfechas. Golden visual con datos P360, canary y retención de PDF finales pendientes como gates de despliegue.

## Propósito

Unificar el renderizado detrás de contratos seguros, liberar recursos y controlar la salida de archivos sin cambiar contenido aprobado.

## Alcance

- IReportRenderer y resultado tipado.
- Adaptadores HTML y DevExpress; fachada opaca para el camino Crystal existente.
- Dispose determinista de ReportDocument, XtraReport, streams y archivos.
- Validación de report UID.
- Raíz de salida, canonicalización y nombres seguros.
- Temporales y reconciliación selectiva; retención final pendiente de D-011.
- Cancelación cooperativa; timeout duro/aislamiento pertenecen a PR-13.

## Implementación

1. [x] Definir solicitud/resultado sin tipos de proveedor.
2. [x] Caracterizar catálogo UID, parámetros y activos protegidos.
3. [x] Extraer adaptadores DevExpress/HTML y fachada Crystal.
4. [x] Tratar Crystal como caja negra: envolver entrada/salida sin editar `.rpt`, fórmulas, consultas ni diseño.
5. [x] Fallar explícitamente para UID desconocido y PDF inválido/vacío.
6. [x] Liberar XtraReport, streams y ReportDocument determinísticamente.
7. [x] Renderizar a temporal controlado y promover sólo al completar.
8. [x] Validar raíces/rutas, sanear/acotar nombres y evitar sobrescritura.
9. [x] Instrumentar duración, tamaño, memoria y handles sin rutas/PII.
10. [x] Reconciliar sólo temporales propios obsoletos.

## Fuera de alcance

- Modificar, convertir o rediseñar cualquier archivo .rpt o lógica interna Crystal.
- Rediseñar reportes.
- Cambiar consultas/datasets sin aprobación.
- Enviar correo; el pipeline devuelve un artefacto.

## Pruebas

- Fixture PDF golden exacto y hashes protegidos de activos.
- Parámetros faltantes/incorrectos.
- UID desconocido.
- Ruta traversal, nombre repetido y archivo bloqueado.
- Excepción durante exportación.
- Archivo eliminable inmediatamente después.
- Promoción concurrente, cancelación y reconciliación selectiva.
- Golden visual real por UID y repetición prolongada en canary autorizado.

## Criterios de aceptación

- [x] Todo renderer implementa el mismo contrato.
- [x] PDF vacío/desconocido es fallo clasificado.
- [x] Recursos administrados se liberan; Crystal se cierra al final del job.
- [x] Rutas externas y UID incompatibles se rechazan antes de operar.
- [x] Temporales se limpian ante fallo/cancelación y se promueven sin sobrescribir.
- [x] Fixture golden sintético conserva exactamente sus bytes y los `.rpt` conservan hashes.
- [x] Métricas antes/después se publican con cardinalidad acotada.
- [ ] Salida visual real coincide por UID con el release anterior; requiere dataset/entorno autorizado.
- [ ] Política de retención final aprobada; pendiente D-011.

## Rollback

Restaurar el binario anterior no requiere rollback SQL ni tocar activos de reporte. No ejecutar ambos caminos con correo real; una comparación canary debe escribir en una raíz aislada y no entregar duplicados. Los PDF ya promovidos se conservan hasta aplicar D-011.

## Evidencia

- Build Release/x64 aprobado con baseline de advertencias sin aumento.
- 120/120 pruebas MSTest net48/x64 aprobadas, sin SQL P360, SMTP ni Internet.
- Ruta, colisión, concurrencia, fallo, cancelación, firma PDF y limpieza cubiertos.
- Clasificación durable y telemetría sin rutas/PII cubiertas.
- Cero cambios en `.rpt`, `.Designer.cs`, `.designer.cs` o `.resx`.
- Diseño/runbook completo en `DOCS/21_PIPELINE_REPORTES_Y_ARCHIVOS.md`.

## Límites conscientes

No se simula timeout duro envolviendo exportaciones sincrónicas en tareas abandonables: eso dejaría recursos nativos vivos. PR-13 aportará aislamiento de proceso, límite de memoria y reciclado. PR-12 completará la composición HTML/correo. La ausencia de dataset anonimizado impide afirmar paridad visual real desde CI local.
