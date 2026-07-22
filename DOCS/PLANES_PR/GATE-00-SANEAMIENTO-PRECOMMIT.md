# Gate 0 — Saneamiento previo al primer commit

Estado: saneamiento local validado; publicación bloqueada hasta confirmar rotación externa.

## Propósito

Crear una línea base Git que nunca haya contenido credenciales activas ni artefactos privados.

## Alcance

- Clave de mapas presente en App.config.
- Credencial SQL presente en Settings.settings y Settings.Designer.cs.
- Artefactos ignorados que pueden contener valores embebidos.
- Índice Git preparado para la importación inicial.
- Política mínima de secretos para ambientes.

## Implementación

1. Congelar la publicación del repositorio.
2. Inventariar credenciales sin copiar sus valores.
3. Coordinar rotación/revocación con propietarios.
4. Reemplazar fuentes por configuración no sensible.
5. Regenerar Settings.Designer.cs desde una fuente limpia.
6. Limpiar/reconstruir artefactos locales.
7. Escanear árbol, índice y artefacto candidato.
8. comprobar con una build que la configuración se obtiene externamente;
9. revisar manualmente App.config, Settings, recursos y archivos de proyecto;
10. sólo entonces crear commit/remoto.

## Fuera de alcance

- Refactor de configuración.
- Migración de runtime.
- Cambio de proveedor de mapas.
- Publicación o despliegue.

## Evidencia

- Identificadores de rotación, sin valores.
- Informe de escaneo con rutas y reglas, redactado.
- Diff que elimina valores y no afecta reglas de negocio.
- Build Release x64.
- Confirmación de que binario/config resultante no contiene valores anteriores.

## Criterios de aceptación

- Propietarios confirman que valores anteriores no sirven.
- Cero secretos verificables en archivos rastreables.
- Ningún PFX, publish, bak, bin, obj, packages o archivo de usuario en el índice.
- Arranque falla de forma segura si falta configuración; no inventa ni imprime credenciales.
- Primer commit aprobado para publicación.

## Rollback

No restaurar credenciales antiguas. Si la nueva fuente falla, detener publicación/ejecución y corregir permisos o referencia. Mantener copia recuperable del código sólo en almacenamiento seguro autorizado.

## Riesgo especial

Si aparece un commit remoto antes de completar el gate, activar respuesta a incidente: rotación inmediata, identificación de clones/cachés, limpieza coordinada de historia y notificación según política.

## Evidencia local 2026-07-21

- App.config, Settings.settings y Settings.Designer.cs no contienen valores de conexión.
- Cinco copias de la clave de mapas fueron retiradas de comentarios en ResourcePlantillasEmail.resx.
- Conexión y mapas se obtienen de P360_CONNECTION_PRINCIPAL y P360_GOOGLE_MAPS_API_KEY.
- Sin clave de mapas, se conserva un enlace de ubicación sin imagen estática.
- El índice Git no contiene patrones de clave Google, credenciales de conexión ni claves privadas.
- Rebuild Release x64 finaliza con cero errores; se conservan las advertencias heredadas inventariadas.
- El ejecutable y .config recién generados no contienen los patrones sensibles evaluados.
- bin y obj antiguos fueron eliminados y regenerados; eran artefactos recuperables.
- publish legado y ConsoleApp5_TemporaryKey.pfx permanecen ignorados y no se distribuirán.

Pendiente externo:

- rotar/revocar la credencial SQL;
- rotar/revocar la clave de mapas;
- confirmar si publish legado y el PFX siguen teniendo uso operativo.

Hasta cerrar esos puntos no se permite push, publicación ni reutilización del paquete ClickOnce legado.
