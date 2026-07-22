# PR-11 — Pipeline de reportes y archivos

Estado: propuesto. Dependencias: PR-02 y PR-05.

## Propósito

Unificar el renderizado detrás de contratos seguros, liberar recursos y controlar la salida de archivos sin cambiar contenido aprobado.

## Alcance

- IReportRenderer y resultado tipado.
- Adaptadores HTML y DevExpress; fachada opaca para el camino Crystal existente.
- Dispose determinista de ReportDocument, XtraReport, streams y archivos.
- Validación de report UID.
- Raíz de salida, canonicalización y nombres seguros.
- Temporales, retención y limpieza.
- Timeouts/cancelación/aislamiento.

## Implementación

1. Definir solicitud/resultado sin tipos de proveedor.
2. Caracterizar cada reporte y parámetros.
3. Extraer adaptadores uno por uno.
4. tratar Crystal como caja negra: envolver su entrada/salida sin editar .rpt, fórmulas, consultas ni diseño;
5. fallar explícitamente para UID desconocido, nunca generar PDF vacío;
6. envolver recursos no Crystal en using/finally y medir el baseline Crystal sin alterar su comportamiento;
7. renderizar primero a temporal controlado y promover sólo al completar;
8. validar que ruta final permanece bajo la raíz;
9. instrumentar duración, tamaño, memoria y handles;
10. implementar limpieza/reconciliación de temporales.

## Fuera de alcance

- Modificar, convertir o rediseñar cualquier archivo .rpt o lógica interna Crystal.
- Rediseñar reportes.
- Cambiar consultas/datasets sin aprobación.
- Enviar correo; el pipeline devuelve un artefacto.

## Pruebas

- Golden master por reporte.
- Parámetros faltantes/incorrectos.
- UID desconocido.
- Ruta traversal, nombre repetido y archivo bloqueado.
- Disco insuficiente.
- Excepción durante exportación.
- Repetición prolongada y conteo memoria/handles.
- Archivo eliminable inmediatamente después.

## Criterios de aceptación

- Todo renderer implementa el mismo contrato.
- PDF vacío/desconocido es fallo clasificado.
- Cero handle/archivo retenido en HTML/DevExpress; para Crystal, baseline documentado y mitigación mediante PR-13.
- Rutas externas se rechazan.
- Temporales se limpian incluso ante fallo.
- Salida coincide con golden master aprobado.
- Métricas antes/después publicadas.

## Rollback

Selección por reporte permite volver al adaptador legado. No ejecutar ambos salvo modo de comparación que no envía resultado duplicado. Conservar temporales de fallo sólo bajo política diagnóstica y sin datos no autorizados.
