# PR-03 — SDK-style y PackageReference sobre net48

Estado: validado localmente el 2026-07-22. Dependencia: PR-02 satisfecha.

## Propósito

Modernizar el formato de proyecto y restauración sin cambiar .NET Framework 4.8 ni versiones funcionales.

## Alcance

- Conversión de packages.config a PackageReference.
- Conversión del csproj a SDK-style net48.
- Preservación de WinForms, recursos, datasets, .rpt y licenses.licx.
- Restauración determinista.
- Generación de binding redirects controlada.

## Implementación

1. Crear matriz de todos los Compile, EmbeddedResource, Content y referencias.
2. Convertir paquetes manteniendo versiones.
3. Verificar assets transitivos y assemblies copiados.
4. Convertir a Microsoft.NET.Sdk.WindowsDesktop con TargetFramework net48 y UseWindowsForms.
5. Evitar duplicar AssemblyInfo durante la transición.
6. Preservar relación Designer/DependentUpon y generación de Settings/datasets.
7. Validar licenciamiento y carga SAP/DevExpress.
8. Comparar contenido del output con baseline.

La conversión se mantuvo en un solo PR porque el arnés de PR-02 permitió demostrar la equivalencia del resultado.

## Decisiones de implementación

- Se conserva `net48`, WinForms y `x64`; este PR no vuelve portable a Linux el ejecutable heredado.
- `EnableDefaultItems=false` mantiene explícitas las relaciones `Compile`, `EmbeddedResource`, `DependentUpon`, datasets, recursos, `licenses.licx` y reportes.
- Los paquetes administrados conservan exactamente sus versiones históricas y quedan fijados en `packages.lock.json`.
- `NuGet.Config` limpia fuentes heredadas y autoriza únicamente el endpoint v3 de nuget.org.
- La restauración canónica usa `RestoreLockedMode=true`; la actualización de locks requiere `build.ps1 -UpdateLockFile` de forma explícita.
- Los ensamblados propietarios de Crystal Reports y DevExpress continúan resolviéndose desde sus instalaciones autorizadas mediante propiedades estándar de MSBuild, sin rutas literales de usuario.

### Decisión conservadora sobre Crystal Reports

Los 15 paquetes `CrystalReports.*` 13.0.4003 del antiguo `packages.config` no se trasladaron a `PackageReference`. La comparación del proyecto clásico demostró que esos paquetes no suministraban los DLL ejecutados: MSBuild copiaba los ensamblados oficiales SAP 13.0.4000 instalados para x64. Migrar los paquetes habría cambiado silenciosamente el runtime de reportes.

Se preservaron las referencias SAP reales y los tres archivos `.rpt` como caja negra. No se abrió, regeneró ni modificó ningún reporte.

## Fuera de alcance

- Actualizar versiones.
- Migrar a .NET moderno.
- Regenerar reportes/datasets.
- Reestructurar namespaces.

## Evidencia y criterios de aceptación

- [x] `build.ps1 -Configuration Release -Target Rebuild -UpdateLockFile`: 0 errores y 22/22 pruebas correctas.
- [x] Restauración posterior en modo bloqueado con ambos `packages.lock.json`.
- [x] Comparación clásico/SDK: 104 archivos frente a 104, sin faltantes ni añadidos.
- [x] Los 56 DLL del output son idénticos byte a byte; incluye SAP Crystal y DevExpress.
- [x] Configuración XML generada y dependencias del manifiesto equivalentes.
- [x] Mismos recursos, datasets, licencias y reportes incluidos; `git diff -- '*.rpt'` vacío.
- [x] `packages.config` eliminado y ninguna ruta absoluta de usuario o estación incorporada al proyecto.
- [x] Fuente de paquetes limitada a nuget.org y restauración normal bloqueada por lock files.
- [x] Códigos de advertencia sin aumento: `CS0162`, `CS0168`, `CS0169`, `CS0219`, `CS0414`, `MSB3277` y `NU1902`.

La comparación se automatizó en `eng/compare-build-outputs.ps1`. Las deudas `MSB3277` y `NU1902` permanecen deliberadamente congeladas para resolverlas con actualización controlada de dependencias en PR-04.

## Rollback

Revertir formato/gestión de paquetes como unidad. Conservar evidencias para separar PackageReference y SDK-style en intentos posteriores.

## Referencias

- [Migrar de packages.config a PackageReference](https://learn.microsoft.com/nuget/consume-packages/migrate-packages-config-to-package-reference)
- [Introducción a la migración de Windows Forms](https://learn.microsoft.com/dotnet/desktop/winforms/migration/)
- [Marcos de destino de .NET](https://learn.microsoft.com/dotnet/standard/frameworks)
