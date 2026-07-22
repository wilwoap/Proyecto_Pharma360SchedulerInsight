# SDK-style y PackageReference sobre .NET Framework 4.8

Estado: implementado y validado localmente en PR-03 el 2026-07-22.

## Resultado

El proyecto WinForms usa ahora `Microsoft.NET.Sdk.WindowsDesktop`, `PackageReference` y restauración con lock files, sin cambiar el runtime, la arquitectura ni la lógica funcional. El destino continúa siendo Windows x64 con .NET Framework 4.8.

La migración conserva explícitamente:

- todos los archivos fuente y sus relaciones de diseñador;
- recursos `.resx`, datasets `.xsd/.xsc/.xss` y `licenses.licx`;
- los tres reportes Crystal `.rpt` sin regenerarlos;
- referencias SAP Crystal Reports 13.0.4000 x64 y DevExpress 25.2.8;
- propiedades ClickOnce y generación de binding redirects;
- la ruta histórica del output `bin\x64\<Configuration>`.

## Restauración determinista

Cada proyecto declara `RestorePackagesWithLockFile=true` y versiona su `packages.lock.json`. El build normal falla si la resolución ya no coincide con el lock:

    .\build.ps1 -Configuration Release -Target Rebuild

Sólo una actualización de dependencias autorizada debe ejecutar:

    .\build.ps1 -Configuration Release -Target Rebuild -UpdateLockFile

Después de actualizar, se revisan los cambios de ambos lock files junto con el proyecto. `NuGet.Config` elimina fuentes implícitas de la estación y autoriza únicamente `https://api.nuget.org/v3/index.json`.

## Dependencias propietarias

Crystal Reports y DevExpress siguen siendo prerrequisitos licenciados de la estación Windows. No se copian instaladores, claves ni paquetes no oficiales al repositorio.

El antiguo `packages.config` enumeraba paquetes `CrystalReports.*` 13.0.4003, pero el build clásico resolvía y distribuía los DLL oficiales SAP 13.0.4000 instalados en `Program Files (x86)`. Se preservó ese comportamiento real. Cambiar la versión o el mecanismo de carga de Crystal queda fuera de PR-03 y ningún `.rpt` debe abrirse para esta tarea.

## Equivalencia demostrada

Se construyó el commit anterior en un worktree aislado y se comparó contra el proyecto SDK con:

    .\eng\compare-build-outputs.ps1 -BaselineDirectory <salida-clasica> -CandidateDirectory <salida-sdk>

Resultado observado:

- 104 archivos en ambas salidas, sin añadidos ni faltantes;
- 56 DLL idénticos por SHA-256;
- `SchedulerP360Insight.exe.config` equivalente como XML;
- mismo conjunto de identidades de dependencias en el manifiesto;
- 22/22 pruebas de caracterización correctas;
- cero diferencias en archivos `.rpt`.

No se exige igualdad binaria del EXE ni de los artefactos ClickOnce porque incluyen metadatos variables de build y ruta. La identidad del ensamblado se mantuvo en `SchedulerP360Insight, Version=1.0.0.0`.

## Rollback

Revertir PR-03 restaura conjuntamente el csproj clásico y `packages.config`. No se requiere conversión de datos ni rollback de reportes porque el cambio sólo afecta build y restauración.
