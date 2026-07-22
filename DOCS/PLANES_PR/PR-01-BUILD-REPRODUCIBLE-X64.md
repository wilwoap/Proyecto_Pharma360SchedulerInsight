# PR-01 — Build reproducible y plataforma x64

Estado: validado localmente el 2026-07-21; workflow preparado y pendiente del primer runner autorizado.

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

## Evidencia de ejecución

- MSBuild 18.8.2.30814 sobre Windows x64.
- Restore y Rebuild Release x64 completados con cero errores.
- Ejecutable verificado como PE32Plus AMD64.
- SHA-256 repetido en dos rebuilds consecutivos: 7D0E4D625B1C316D55C6AF9419A3F9ECE666C6F4D4899ADAB6068A9FC948C0BB.
- Baseline: 17 advertencias C#, dos familias MSB3277 y una NU1902; ningún aumento permitido.
- Verificación de 127 archivos versionados sin artefactos prohibidos ni secretos de alta confianza.
- Los archivos Crystal .rpt no fueron modificados.

La evidencia del runner de GitHub se añadirá cuando exista una máquina con las etiquetas y licencias descritas en 11_BUILD_Y_RUNNER.md.
