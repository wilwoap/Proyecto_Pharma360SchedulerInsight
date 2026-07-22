# PR-04 — Higiene y actualización de dependencias

Estado: propuesto. Dependencia: PR-03.

## Propósito

Obtener un grafo de dependencias soportado, trazable y sin conflictos conocidos, mediante lotes pequeños.

## Alcance

- Inventario directo/transitivo/licencias.
- Retiro confirmado de paquetes sin uso.
- Resolución de Microsoft.Extensions y System.Memory.
- Actualización compatible de Quartz 3.x y Dapper 2.1.x.
- Revisión de Newtonsoft.Json.
- Registro de excepción temporal para Crystal/log4net hasta su aislamiento controlado en PR-13.
- SBOM y vulnerability scan.

## Orden recomendado

1. Retirar referencias realmente no usadas, una familia por vez.
2. Alinear System.* y Microsoft.Extensions requeridos por DevExpress.
3. Actualizar Dapper dentro de su rama compatible.
4. Actualizar Quartz a una versión 3.x estable soportada, revisando notas.
5. resolver Newtonsoft como retiro o actualización;
6. congelar la familia Crystal y documentar procedencia, hashes, licencia, exposición y controles compensatorios hasta PR-13;
7. no modificar runtime, paquetes Crystal ni log4net en este PR.

No adoptar Quartz 4 hasta que sea una versión soportada y su cambio mayor haya sido evaluado. Las versiones actuales se vuelven a consultar al abrir el PR.

## Pruebas

- Carga de proceso y assemblies.
- Registro/ejecución de jobs.
- Acceso Dapper/ADO.NET de contrato.
- Renderizado de cada tecnología.
- Validación licenses.licx.
- Análisis de vulnerabilidad/licencia y SBOM diff.

## Criterios de aceptación

- Sin conflictos MSBuild de versiones.
- Cero vulnerabilidades críticas/altas sin excepción.
- log4net 2.0.12 no está en el host principal o dispone de una excepción temporal aprobada y limitada al componente Crystal hasta PR-13.
- Cada paquete tiene fuente/propietario/licencia.
- Reportes y jobs pasan caracterización.
- Rollback por lote demostrado.

## Rollback

Revertir sólo la familia que falla, restaurar lock/grafo anterior y mantener el resto si sus pruebas son independientes. No obtener paquetes Crystal de fuentes no autorizadas para “hacer compilar”.
