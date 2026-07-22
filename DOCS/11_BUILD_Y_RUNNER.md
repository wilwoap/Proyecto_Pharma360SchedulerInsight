# Build reproducible y runner Windows x64

Estado: implementado localmente en PR-01; pendiente validar el primer job en el runner autorizado.

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

Para repetir un build con paquetes ya restaurados:

    .\build.ps1 -Configuration Release -Target Rebuild -SkipRestore

El script siempre fija Platform=x64, valida que no aumenten las advertencias heredadas y muestra el SHA-256 del ejecutable producido.

## Baseline de advertencias

| Código | Máximo observado | Tratamiento |
|---|---:|---|
| CS0162 | 2 | Deuda congelada |
| CS0168 | 11 | Deuda congelada |
| CS0169 | 2 | Deuda congelada |
| CS0219 | 1 | Deuda congelada |
| CS0414 | 1 | Deuda congelada |
| MSB3277 | 2 familias | Resolver en PR-04 |
| NU1902 | 1 | log4net 2.0.12; resolver en PR-04 |

Las familias MSB3277 conocidas son Microsoft.Extensions.Configuration.Abstractions y System.Memory. La restauración también informa la vulnerabilidad moderada GHSA-4f7c-pmjv-c25w en log4net 2.0.12. PR-01 congela ambos hallazgos; PR-04 debe resolverlos después de contar con el arnés de caracterización de PR-02.

## GitHub Actions

El workflow requiere un runner propio con las etiquetas self-hosted, Windows, X64 y p360-build. Ese runner debe tener las dependencias propietarias anteriores y una versión compatible con actions/checkout v6. Un runner genérico de GitHub no contiene SAP Crystal ni la licencia de DevExpress.

El job usa permisos de solo lectura, verifica higiene del repositorio y ejecuta exactamente el mismo build local. No recibe credenciales SQL, SMTP ni claves de API.

## Artefacto y reproducibilidad

El archivo esperado es SchedulerP360Insight/bin/x64/Release/SchedulerP360Insight.exe. Su hash se registra como evidencia de cada ejecución, pero no se exige igualdad binaria entre máquinas hasta confirmar que todas las herramientas propietarias emiten metadatos deterministas.

Las carpetas bin, obj, packages y publish permanecen fuera de Git.
