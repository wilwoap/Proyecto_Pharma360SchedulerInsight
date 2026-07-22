# PR-04 — Higiene y actualización de dependencias

Estado: validado localmente el 2026-07-22. Dependencia: PR-03 completada.

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

## Implementación realizada

1. Se retiraron referencias directas no usadas y paquetes que ya llegan de forma transitiva.
2. Se alinearon `System.*` y `Microsoft.Extensions.*` con las ABI requeridas por DevExpress 25.2.8.
3. Dapper se actualizó de 2.1.35 a 2.1.79 y se añadió una prueba de materialización ADO.NET.
4. Quartz se actualizó de 3.6.2 a 3.18.2 y se probó la ejecución real de un job sobre RAMJobStore.
5. Newtonsoft.Json se retiró al confirmar que no tenía usos en código.
6. Crystal Reports 13.0.4000 y log4net 2.0.12 se congelaron sin modificar sus binarios; la excepción moderada quedó limitada, ejecutable y con vencimiento.
7. Se añadió generación y validación de SBOM SPDX 2.2 con Microsoft SBOM Tool 4.1.5.

Quartz 4 queda fuera del alcance hasta evaluar de forma independiente su cambio mayor. No se modificó el runtime Crystal ni ningún archivo `.rpt`.

## Pruebas

- Carga de proceso y assemblies.
- Registro/ejecución de jobs.
- Acceso Dapper/ADO.NET de contrato.
- Renderizado de cada tecnología.
- Validación licenses.licx.
- Análisis de vulnerabilidad/licencia y SBOM diff.

## Criterios de aceptación

- [x] Sin conflictos MSBuild de versiones.
- [x] Cero vulnerabilidades críticas/altas.
- [x] log4net 2.0.12 no es referenciado por el host y dispone de una excepción temporal exacta, limitada a Crystal y con vencimiento el 2026-10-31 o PR-13.
- [x] Cada dependencia directa tiene fuente, propietario y licencia documentados.
- [x] Reportes, Dapper y ejecución de jobs pasan 25 pruebas de caracterización.
- [x] SBOM generado y validado contra 111 archivos del output.
- [x] Rollback independiente por familia de cambios.

## Evidencia de cierre

- `build.ps1 -Configuration Release -Target Rebuild`: correcto, 25/25 pruebas.
- `eng/verify-dependencies.ps1`: 0 altas/críticas, 1 moderada exceptuada, 0 paquetes obsoletos.
- `eng/generate-sbom.ps1`: SPDX 2.2 válido, 77 paquetes y 111/111 archivos válidos.
- advertencias permitidas: `CS0162`, `CS0168`, `CS0169`, `CS0219`, `CS0414` y `NU1902`; `MSB3277` eliminado.
- diff de `.rpt`: vacío.

El inventario, las decisiones de ABI, la excepción y los comandos están en `DOCS/14_DEPENDENCIAS_Y_SBOM.md`.

## Rollback

Revertir sólo la familia que falla, restaurar lock/grafo anterior y mantener el resto si sus pruebas son independientes. No obtener paquetes Crystal de fuentes no autorizadas para “hacer compilar”.
