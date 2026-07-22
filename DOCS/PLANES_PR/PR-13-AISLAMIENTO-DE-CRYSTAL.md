# PR-13 — Aislamiento de Crystal sin modificar reportes

Estado: propuesto. Decisión D-005 aceptada. Dependencias: PR-02, PR-04 y PR-11.

## Propósito

Separar Crystal Reports del host principal para permitir su evolución, conservando intactos los cinco archivos .rpt y el comportamiento que hoy funciona.

## Regla de protección

Este PR no modifica:

- archivos .rpt;
- fórmulas, parámetros ni consultas internas;
- datasets o procedimientos de los reportes;
- diseño, fuentes, paginación o gráficos;
- lógica funcional que selecciona datos.

Crystal se trata como una caja negra. Sólo se encapsulan entrada, salida, ciclo de proceso y operación.

## Inventario protegido

- DashboardPerformanceVisita.rpt;
- DashboardPerformanceVisitaResumenGerencial.rpt;
- DashboardPerformanceVisitaSupervisor.rpt;
- rpt_IncentivoPreviewResumenModeloACX_RXxColaborador.rpt;
- rpt_ResumenGeneralVisita.rpt.

## Alcance

- Worker separado .NET Framework 4.8 x64.
- Runtime oficial SAP y procedencia trazable.
- Contrato local versionado para solicitar un render.
- Autenticación/ACL, allowlist de reportId y validación de parámetros.
- Timeout, concurrencia limitada y cancelación del cliente.
- Directorio temporal controlado.
- Correlación, métricas, límites de memoria y reciclado.
- Eliminación de assemblies Crystal del host principal.

## Implementación

1. Congelar hashes de los .rpt y crear prueba que falle si cambian.
2. Capturar salida de referencia con fixtures aprobados.
3. Encapsular el camino de ejecución actual con el mínimo movimiento de código.
4. crear worker net48 x64 y contrato de solicitud/resultado;
5. restringir el transporte a la identidad/host autorizados;
6. usar runtime SAP soportado y verificar licenciamiento;
7. ejecutar en shadow: mismo input, salida del worker no enviada;
8. comparar texto, valores, páginas, tamaño y visuales críticos;
9. activar por reportId con rollback inmediato;
10. retirar Crystal del proceso principal después del canary.

El cambio de procedencia/runtime se separará en un PR hijo si no puede demostrarse junto con el aislamiento.

## Fuera de alcance

- Conversión a DevExpress.
- Edición o regeneración de .rpt.
- Corrección estética o funcional de reportes.
- Sustitución de consultas/datasets.
- Retirada obligatoria del worker durante este programa.

## Pruebas

- Hash de cada .rpt sin cambios.
- Comparación externa de cada reporte antes/después.
- Dataset vacío, normal y máximo aprobado.
- Worker ausente, lento, caído y reiniciado.
- Dos solicitudes concurrentes dentro/fuera del límite.
- Timeout y limpieza de temporales.
- Memoria/handles durante ejecución prolongada.
- Contrato rechazando reportId/ruta/parámetros no permitidos.
- Instalación y licencia en host limpio.

## Criterios de aceptación

- Los cinco .rpt conservan exactamente sus hashes.
- Salidas de referencia aprobadas sin diferencias no explicadas.
- Host principal no carga ni distribuye assemblies Crystal.
- Sólo el worker net48 x64 tiene acceso a runtime/reportes.
- Runtime/paquetes tienen procedencia y soporte documentados.
- Fallo del worker no bloquea indefinidamente el host ni produce PDF vacío.
- Canary y rollback se ejecutaron sin duplicar correo.

## Rollback

Detener el cliente del worker y volver al camino Crystal legado completo mientras el host principal siga en net48. No cambiar archivos .rpt ni esquema. Antes de reanudar, reconciliar jobs/notificaciones para impedir doble procesamiento.

## Riesgo residual aceptado

Crystal y su runtime permanecen como tecnología legada. El aislamiento reduce su impacto y permite modernizar el resto, pero requiere mantener un worker Windows net48 x64 mientras exista al menos un reporte Crystal.
