# PR-05 — Configuración, composición y caché

Estado: validado localmente el 2026-07-22. Dependencias: PR-02 a PR-04 completadas.

## Propósito

Eliminar I/O en constructores y estado global, validando una configuración inmutable y reduciendo lecturas repetidas.

## Alcance

- Composition root en el punto de entrada.
- Contratos para conexión, parámetros y opciones.
- Sustitución gradual de AppConfig.ConnectionString estático.
- LaboratoryConstants convertido en snapshot validado.
- Carga de parámetros en bloque y caché con política explícita.
- BusinessP360Exception sin efectos secundarios.

## Implementación realizada

1. Se caracterizaron los 14 nombres, conversiones, defaults y errores del constructor heredado.
2. `SchedulerOptions` concentra entorno y appSettings y redacta todos los secretos al imprimirse.
3. `IParameterSnapshotProvider` publica un `LaboratoryConstants` inmutable y thread-safe.
4. El proveedor `batch` obtiene los 14 parámetros con un único comando SQL parametrizado.
5. La composición valida presencia, `MAIL_SSL` y el rango de `MAIL_PORT` antes de crear Quartz.
6. `ComposedJobFactory` inyecta opciones, snapshot y acceso de datos en los tres jobs.
7. Se eliminaron todas las construcciones de `new LaboratoryConstants()` en producción.
8. `BusinessP360Exception` quedó libre de I/O y conserva el mapeo de mensajes.
9. La política de refresh es reinicio controlado; no se mezclan snapshots durante la ejecución.
10. `P360_PARAMETER_PROVIDER_MODE=legacy` permite rollback temporal sin repetir consultas por job.

## Fuera de alcance

- Cambiar valores/reglas de negocio.
- Nuevo proveedor de secretos definitivo si D-008 sigue abierto.
- Refactor completo de acceso a datos.

## Pruebas y métricas

- Configuración completa/incompleta/mal tipada.
- Refresh concurrente y fallo durante refresh.
- Sin valor sensible en ToString/log.
- Conteo de consultas: objetivo máximo una carga por snapshot, frente a múltiples consultas por instancia.
- Compatibilidad de valores resultantes.

## Criterios de aceptación

- [x] Ningún constructor nuevo realiza I/O.
- [x] Jobs no reciben connection strings ni secretos en `JobDataMap`.
- [x] La ruta nueva recibe dependencias explícitas; `AppConfig` queda sólo como adaptador heredado.
- [x] Snapshot inmutable y validado antes del scheduler.
- [x] Reducción demostrada de 14 lecturas por instancia a una carga batch por proceso.
- [x] Existe modo de compatibilidad/reversión durante despliegue.
- [x] 37/37 pruebas pasan y ningún `.rpt` cambia.

## Evidencia de cierre

- `build.ps1 -Configuration Release -Target Rebuild`: correcto, 37/37 pruebas.
- concurrencia: ocho consumidores, una carga y una instancia compartida.
- fallo de carga: no se publica ni cachea un snapshot parcial.
- modo heredado: 14 nombres/14 lecturas sólo al arrancar.
- creación de los tres jobs: cero llamadas adicionales al proveedor.
- golden master DevExpress actualizado únicamente por inyección de configuración; Designer y `.resx` intactos.
- inventario y operación: `DOCS/15_CONFIGURACION_Y_COMPOSICION.md`.

## Rollback

Establecer `P360_PARAMETER_PROVIDER_MODE=legacy` y reiniciar para volver temporalmente a la lectura histórica. Si la incidencia no está aislada al batch, revertir el PR completo. Retirar el adaptador tras una ventana operativa estable; nunca persistir su selección en código ni incluir secretos en la variable.
