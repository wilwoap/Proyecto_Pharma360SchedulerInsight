# Dependencias, excepción de seguridad y SBOM

Estado: validado localmente en PR-04 el 2026-07-22.

## Resultado

PR-04 redujo el grafo directo de la aplicación de 19 a 8 paquetes NuGet, eliminó los conflictos `MSB3277`, actualizó Dapper y Quartz dentro de sus líneas compatibles y añadió controles reproducibles de vulnerabilidades, obsolescencia y composición del artefacto. El build canónico conserva Windows x64 y .NET Framework 4.8.

No se actualizó el runtime de SAP Crystal Reports, no se modificó ninguna referencia Crystal y no cambió ningún archivo `.rpt`.

## Paquetes directos de la aplicación

| Paquete | Antes | PR-04 | Licencia | Decisión |
|---|---:|---:|---|---|
| Dapper | 2.1.35 | 2.1.79 | Apache-2.0 | Actualizado y cubierto con contrato ADO.NET sin red |
| log4net | 2.0.12 | 2.0.12 | Apache-2.0 | Congelado temporalmente por la ABI de Crystal 13.0.4000 |
| Microsoft.Extensions.Configuration.Abstractions | 2.1.1 | 8.0.0 | MIT | Alineado con DevExpress 25.2.8 |
| Microsoft.Extensions.Configuration.Json | transitivo/ausente | 8.0.1 | MIT | Fija el ensamblado 8.0.0.1 requerido por DevExpress |
| Microsoft.Extensions.Logging.Abstractions | 2.1.1 | 8.0.0 | MIT | Alineado con DevExpress 25.2.8 |
| Quartz | 3.6.2 | 3.18.2 | Apache-2.0 | Actualizado dentro de Quartz 3.x y probado con RAMJobStore |
| System.Memory | 4.5.4 | 4.5.5 | MIT | Fija el ensamblado 4.0.1.2 requerido por DevExpress |
| System.Text.Json | transitivo/ausente | 8.0.6 | MIT | Fija el ensamblado 8.0.0.6 requerido por DevExpress |

Las versiones transitivas exactas están fijadas en `packages.lock.json`. SAP Crystal Reports 13.0.4000 y DevExpress 25.2.8 son componentes propietarios instalados fuera de NuGet; su uso y redistribución dependen de las licencias del propietario del proyecto.

## Referencias directas retiradas

Se retiraron las referencias que no tenían uso directo en código o eran transitivas de los paquetes conservados:

- `Microsoft.Extensions.DependencyInjection.Abstractions`, `FileProviders.Abstractions`, `Hosting.Abstractions`, `Options` y `Primitives` 2.1.1;
- `Quartz.Extensions.DependencyInjection`, `Quartz.Extensions.Hosting` y `Quartz.Serialization.Json` 3.6.2;
- `Newtonsoft.Json` 13.0.1;
- `System.Buffers`, `System.Diagnostics.DiagnosticSource`, `System.Numerics.Vectors` y `System.Runtime.CompilerServices.Unsafe`.

El retiro de Newtonsoft no cambia serialización de negocio: no existían usos en el código. Las extensiones de Quartz tampoco estaban conectadas; el host crea el scheduler mediante la API base.

## Alineación binaria con DevExpress

El análisis de `DevExpress.DataAccess.v25.2.dll` determinó las ABI que deben llegar al output:

| Ensamblado | Versión requerida |
|---|---:|
| Microsoft.Extensions.Configuration | 8.0.0.0 |
| Microsoft.Extensions.Configuration.Abstractions | 8.0.0.0 |
| Microsoft.Extensions.Configuration.Json | 8.0.0.1 |
| System.Memory | 4.0.1.2 |
| System.Text.Json | 8.0.0.6 |

El grafo seleccionado satisface esas versiones sin redirecciones manuales y el build ya no emite conflictos `MSB3277`.

## Excepción temporal de log4net

El escaneo detecta una vulnerabilidad moderada en log4net 2.0.12: [GHSA-4f7c-pmjv-c25w / CVE-2026-40021](https://github.com/advisories/GHSA-4f7c-pmjv-c25w). El problema puede provocar pérdida silenciosa de eventos cuando se usan `XmlLayout` o `XmlLayoutSchemaLog4J` con caracteres XML no permitidos.

No es seguro sustituir el ensamblado de forma aislada: `CrystalDecisions.Shared.dll` y `CrystalDecisions.Web.dll` 13.0.4000 declaran la ABI exacta `log4net, Version=2.0.12.0`. La aplicación no referencia log4net directamente.

Controles compensatorios:

- se prohíbe una sección `<log4net>` y el uso de los layouts XML afectados dentro del host;
- una prueba comprueba que el host no referencia log4net y que Crystal conserva la ABI exacta;
- `eng/dependency-exceptions.json` limita la excepción al paquete, versión, severidad y advisory exactos;
- la excepción vence el 2026-10-31 o al aislar Crystal en PR-13, lo que ocurra primero;
- el escaneo falla si la excepción vence, deja de corresponder al hallazgo o aparece cualquier vulnerabilidad no exceptuada;
- la auditoría funcional existente se almacena en SQL y no depende de los layouts afectados.

El resultado local de PR-04 es: cero vulnerabilidades altas o críticas, una vulnerabilidad moderada exceptuada y cero paquetes marcados como obsoletos. log4net 2.x está fuera de soporte activo; por eso esta excepción no es una solución permanente. Apache mantiene la rama 3.x como línea activa según su [política de versiones](https://logging.apache.org/log4net/versioning.html).

## SBOM reproducible

El comando siguiente genera un manifiesto SPDX 2.2 del output Release y lo valida contra los archivos producidos:

    .\eng\generate-sbom.ps1 -Configuration Release -PackageVersion 1.0.0

Se usa [Microsoft SBOM Tool 4.1.5](https://github.com/microsoft/sbom-tool). La evidencia local del PR contiene 77 paquetes y 111 archivos; los 111 archivos pasaron validación de existencia y SHA-256. El manifiesto se crea bajo `artifacts/sbom`, no se versiona y el workflow lo publica durante 30 días como artefacto de GitHub Actions.

El control de política se ejecuta de forma independiente:

    .\eng\verify-dependencies.ps1

Este control bloquea vulnerabilidades altas o críticas, vulnerabilidades sin excepción exacta, excepciones vencidas o sobrantes y paquetes NuGet obsoletos.

## Evidencia funcional

- build Release x64 completado sin `MSB3277`;
- 25 de 25 pruebas de caracterización superadas;
- Dapper materializa correctamente un registro mediante un proveedor ADO.NET simulado;
- Quartz 3.18.2 inicia, agenda, ejecuta y detiene un job con RAMJobStore;
- los hashes de los binarios Crystal y de log4net permanecen congelados por prueba;
- el diff de archivos `.rpt` está vacío.

## Fuentes de actualización

- [Dapper 2.1.79](https://www.nuget.org/packages/Dapper)
- [Quartz 3.18.2](https://www.nuget.org/packages/Quartz/3.18.2)
- [Guía de migración Quartz 3.x](https://www.quartz-scheduler.net/documentation/quartz-3.x/migration-guide.html)
- [Microsoft.Extensions.Configuration.Abstractions 8.0.0](https://www.nuget.org/packages/Microsoft.Extensions.Configuration.Abstractions/8.0.0)
- [Microsoft.Extensions.Logging.Abstractions 8.0.0](https://www.nuget.org/packages/Microsoft.Extensions.Logging.Abstractions/8.0.0)
- [System.Memory](https://www.nuget.org/packages/System.Memory)

## Rollback

Los cambios se dividieron en tres commits funcionales —alineación del grafo, Dapper y Quartz— más el commit de controles y documentación. Ante una regresión se revierte primero la familia afectada y sus lock files. Si falla la política o la generación del SBOM se revierte el commit de controles sin tocar los binarios Crystal ni los `.rpt`.
