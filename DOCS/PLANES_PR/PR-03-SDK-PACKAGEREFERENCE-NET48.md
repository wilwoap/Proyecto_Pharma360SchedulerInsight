# PR-03 — SDK-style y PackageReference sobre net48

Estado: propuesto. Dependencia: PR-02.

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

Puede dividirse en dos PRs si PackageReference y SDK-style no son revisables juntos.

## Fuera de alcance

- Actualizar versiones.
- Migrar a .NET moderno.
- Regenerar reportes/datasets.
- Reestructurar namespaces.

## Criterios de aceptación

- Release x64 y pruebas en verde.
- Mismos recursos/reportes incluidos.
- Aplicación carga assemblies y licencia en entorno controlado.
- Sin packages.config.
- Sin rutas de usuario/máquina en csproj.
- Restore desde fuentes autorizadas.
- Conflictos de binding no empeoran.

## Rollback

Revertir formato/gestión de paquetes como unidad. Conservar evidencias para separar PackageReference y SDK-style en intentos posteriores.

## Referencia

El orden sigue la guía oficial de migración de WinForms: primero preparar dependencias/PackageReference/SDK-style sobre .NET Framework, después portar el runtime.

