# PR-01 — Build reproducible y plataforma x64

Estado: propuesto. Dependencia: Gate 0.

## Propósito

Poder restaurar y compilar el mismo ejecutable Release x64 desde una estación/runner documentado, sin cambiar comportamiento.

## Alcance

- Declarar x64 como plataforma soportada.
- Retirar AnyCPU de la ruta Release o marcarla explícitamente no soportada.
- Script único de restore/build/test.
- Inventario de prerequisitos SAP/DevExpress/licencias.
- Baseline de advertencias.
- CI inicial en runner Windows autorizado.
- Versionado/hash del artefacto.

## Implementación

1. Capturar build limpio actual y lista de advertencias.
2. Documentar versión de MSBuild, .NET Framework targeting pack, SAP runtime y DevExpress.
3. Eliminar rutas absolutas de publicación y referencias de desarrollo cuando sea posible.
4. Resolver las referencias desde ubicaciones/feeds autorizados y reproducibles.
5. Crear script PowerShell con restore y Rebuild Release x64.
6. Fallar si aparecen advertencias nuevas respecto del baseline.
7. Configurar CI para ejecutar el mismo script.
8. Publicar sólo artefacto y evidencia no sensible.

## Fuera de alcance

- Actualizar paquetes.
- Convertir csproj.
- Corregir toda advertencia heredada.
- Ejecutar SQL/SMTP.

## Pruebas y evidencia

- Build en estación actual y runner limpio.
- Comparación de hashes cuando el build sea determinista, o explicación de metadatos variables.
- Inspección de arquitectura del ejecutable/assemblies.
- Registro de licencias/prerequisitos sin claves.

## Criterios de aceptación

- Rebuild Release x64 con cero errores.
- CI reproduce el build sin rutas personales.
- AnyCPU no puede confundirse con un artefacto publicable.
- Ninguna advertencia nueva.
- Artefactos ignorados no entran a Git.
- Manual de preparación del runner actualizado.

## Rollback

Mantener el csproj/solución anterior en Git. Revertir scripts/config de build sin modificar binario desplegado. No reactivar AnyCPU como release hasta resolver dependencias AMD64.

