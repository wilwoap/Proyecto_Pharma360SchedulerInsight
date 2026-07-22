# Build reproducible y runner Windows x64

Estado: build implementado en PR-01, restauración bloqueada validada en PR-03 y política de dependencias/SBOM validada localmente en PR-04; pendiente validar el primer job en el runner autorizado.

## Plataforma soportada

El ejecutable heredado se compila exclusivamente para Windows x64 sobre .NET Framework 4.8. AnyCPU queda fuera de la matriz soportada porque el proceso carga componentes Crystal AMD64. Linux no es un destino de este ejecutable; el aislamiento posterior permitirá que otros componentes modernos sí sean portables.

## Prerrequisitos de la estación o runner

- Windows x64 actualizado.
- Visual Studio 2022 o Build Tools con MSBuild y .NET Framework 4.8 targeting pack.
- SAP Crystal Reports runtime y ensamblados 13.0.4000 para x64.
- DevExpress 25.2.8 instalado y con licencia válida para compilar.
- Git y PowerShell 5.1 o superior.
- Acceso al origen autorizado de paquetes NuGet.

No se documentan ni versionan claves de licencia. La imagen del runner debe prepararse mediante el mecanismo autorizado por los propietarios de SAP y DevExpress.

## Comandos canónicos

Desde la raíz del repositorio:

    .\eng\verify-repository.ps1
    .\build.ps1 -Configuration Release -Target Rebuild
    .\eng\verify-dependencies.ps1
    .\eng\generate-sbom.ps1 -Configuration Release -PackageVersion 1.0.0

Para repetir un build con paquetes ya restaurados:

    .\build.ps1 -Configuration Release -Target Rebuild -SkipRestore

La ejecución normal restaura exclusivamente las versiones fijadas en los `packages.lock.json`. Cuando una actualización de dependencias esté autorizada, se regeneran los locks de forma explícita:

    .\build.ps1 -Configuration Release -Target Rebuild -UpdateLockFile

`NuGet.Config` limpia las fuentes heredadas de la estación y permite sólo nuget.org. Los ensamblados licenciados de SAP Crystal Reports y DevExpress no se restauran mediante NuGet: deben existir en las rutas de instalación del proveedor indicadas en los prerrequisitos.

El script siempre fija Platform=x64, valida que no aumenten las advertencias heredadas, ejecuta al menos 56 pruebas de caracterización y muestra el SHA-256 del ejecutable producido.

## Baseline de advertencias

| Código | Máximo observado | Tratamiento |
|---|---:|---|
| CS0162 | 2 | Deuda congelada |
| CS0168 | 11 | Deuda congelada |
| CS0169 | 2 | Deuda congelada |
| CS0219 | 1 | Deuda congelada |
| CS0414 | 1 | Deuda congelada |
| NU1902 | 1 | Excepción temporal exacta para log4net 2.0.12 hasta 2026-10-31 o PR-13 |

PR-04 eliminó los conflictos `MSB3277` alineando Microsoft.Extensions y System.Memory con DevExpress 25.2.8. La restauración conserva `NU1902` para log4net 2.0.12 porque Crystal 13.0.4000 requiere su ABI exacta. `eng/verify-dependencies.ps1` limita ese único hallazgo moderado y bloquea cualquier hallazgo nuevo, alto/crítico, obsoleto o vencido. La justificación completa está en `14_DEPENDENCIAS_Y_SBOM.md`.

## GitHub Actions

El workflow requiere un runner propio con las etiquetas self-hosted, Windows, X64 y p360-build. Ese runner debe tener las dependencias propietarias anteriores y una versión compatible con actions/checkout v6. Un runner genérico de GitHub no contiene SAP Crystal ni la licencia de DevExpress.

El job usa permisos de solo lectura, verifica higiene del repositorio, ejecuta exactamente el mismo build local, aplica la política de dependencias y genera un SBOM SPDX 2.2. El manifiesto validado se publica durante 30 días. El job no recibe credenciales SQL, SMTP ni claves de API.

## Artefacto y reproducibilidad

El archivo esperado es SchedulerP360Insight/bin/x64/Release/SchedulerP360Insight.exe. Su hash se registra como evidencia de cada ejecución, pero no se exige igualdad binaria entre máquinas hasta confirmar que todas las herramientas propietarias emiten metadatos deterministas.

Las carpetas bin, obj, packages y publish permanecen fuera de Git.

## Comparación durante cambios de build

Para comparar un build de referencia con uno candidato:

    .\eng\compare-build-outputs.ps1 -BaselineDirectory <salida-base> -CandidateDirectory <salida-candidata>

El control exige el mismo inventario, DLL idénticos, configuración XML equivalente y el mismo conjunto de dependencias en el manifiesto. El ejecutable y los artefactos ClickOnce no se comparan por hash porque incorporan metadatos dependientes de la ruta y de la ejecución.
