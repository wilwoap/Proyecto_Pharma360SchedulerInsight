# Configuración, composición y snapshot de parámetros

Estado: validado localmente en PR-05 el 2026-07-22.

## Resultado

PR-05 crea un único punto de composición antes de iniciar Quartz. La configuración del proceso y los parámetros funcionales se validan una vez, se convierten en objetos inmutables y se inyectan en cada job mediante una fábrica de Quartz. Ningún constructor nuevo abre SQL, lee archivos ni consulta variables de entorno.

El flujo implementado es:

```text
entorno + appSettings -> SchedulerOptions -----------+
                                                      |
T_PARAMETROS -> fuente batch -> snapshot inmutable --+-> composition root
                                                          |-> acceso SQL
                                                          |-> fábrica Quartz
                                                          `-> jobs/reportes
```

`SchedulerP360Insight.Composition.SchedulerComposition` es el único lugar que realiza la carga inicial. Si falta configuración o el snapshot es inválido, el proceso falla antes de crear o iniciar el scheduler y el error identifica únicamente el nombre de la configuración, nunca su valor.

## Opciones del proceso

`SchedulerOptions` concentra:

- cadena de conexión obtenida de `P360_CONNECTION_PRINCIPAL`;
- clave opcional obtenida de `P360_GOOGLE_MAPS_API_KEY`;
- consultas `P360.Reports.Query` y `P360.InfoColaNotificaciones.Query`;
- modo del proveedor de parámetros.

El objeto es inmutable y su `ToString()` redacta la conexión y la clave de mapas. Los jobs reciben las opciones por constructor; ningún secreto o connection string se copia a `JobDataMap`.

## Snapshot de laboratorio

`LaboratoryConstants` dejó de consultar SQL en su constructor y ya no expone setters. El snapshot contiene los mismos parámetros funcionales del legado:

- `MAIL_SSL`, `MAIL_SMTP`, `MAIL_USER`, `MAIL_PASS` y `MAIL_PORT`;
- `LABORATORIO_URL_LOGO`, `LABORATORIO_IMPLEMENTACION` y `MAIL_ADMINISTRADOR_LABORATORIO`;
- `EMPRESA_PAIS`, `EMPRESA_CIUDAD`, `EMPRESA_DIRECCION`, `EMPRESA_SITIO_WEB`, `EMPRESA_EMAIL_CONTACTO` y `EMPRESA_TELEFONO_CONTACTO`.

La carga valida la presencia de los 14 nombres, exige `MAIL_SSL` igual a `0` o `1` y limita `MAIL_PORT` al rango 1–65535. Los campos empresariales que históricamente podían estar vacíos conservan esa compatibilidad. El usuario SMTP continúa siendo el remitente, como en el comportamiento previo.

La contraseña SMTP y la clave de mapas permanecen accesibles sólo para sus adaptadores; nunca aparecen en `ToString()`, errores, documentación o pruebas.

## Reducción de consultas y caché

Antes, cada `new LaboratoryConstants()` ejecutaba 14 consultas independientes y el constructor se repetía en el arranque, los jobs y dos reportes DevExpress. El modo predeterminado `batch` ejecuta un único `SELECT` parametrizado para los 14 nombres y publica un solo snapshot por proceso.

`StartupParameterSnapshotProvider` usa exclusión mutua y publicación segura:

- llamadas concurrentes reciben exactamente la misma instancia;
- una carga correcta se ejecuta una sola vez;
- una carga fallida no se cachea como snapshot válido;
- no existe refresh en caliente: la política explícita es refrescar mediante reinicio controlado.

Esta política evita que un job observe una mezcla de valores anteriores y nuevos. Una futura recarga por TTL o señal requerirá semántica operacional propia y queda fuera de PR-05.

## Modo de compatibilidad y rollback

El modo normal no requiere configuración adicional:

    P360_PARAMETER_PROVIDER_MODE=batch

`batch` también es el valor cuando la variable no existe. Para una reversión operacional temporal:

    P360_PARAMETER_PROVIDER_MODE=legacy

`legacy` obtiene los mismos 14 nombres mediante el método histórico, pero sólo durante la carga inicial; el snapshot evita repetir esas consultas por job. El cambio de modo requiere reiniciar el proceso y no contiene información sensible.

La ruta de rollback es:

1. establecer `P360_PARAMETER_PROVIDER_MODE=legacy` en el entorno del servicio;
2. reiniciar y comprobar la carga del scheduler;
3. investigar la consulta batch sin cambiar parámetros ni reportes;
4. volver a `batch` y reiniciar cuando la causa esté resuelta.

Si el problema no está limitado al proveedor, se revierte el PR completo. Ninguna opción exige modificar base de datos, Crystal o archivos `.rpt`.

## Composición de jobs y compatibilidad heredada

`ComposedJobFactory` crea los tres tipos históricos de job con las mismas instancias inmutables de opciones y laboratorio, además de un acceso a datos explícitamente configurado. Cada job crea sus utilitarios sin I/O y Quartz conserva el mismo `ReportJobFactory`, claves, cron y `JobDataMap` funcional.

`AppConfig` se mantiene como puente inicializado una sola vez para:

- TableAdapters generados que todavía requieren una conexión estática;
- métodos estáticos heredados de acceso a datos;
- constructores predeterminados usados por diseñadores de DevExpress.

La ruta compuesta de Quartz no usa esos constructores predeterminados. No se añadieron nuevos consumidores de estado global. PR-08 encapsuló el acceso de datos y PR-11 movió renderizado/artefactos detrás de contratos; HTML y DevExpress admiten renderer inyectado en pruebas. Crystal permanece como fachada opaca concreta hasta aislarla en PR-13.

## Excepciones sin efectos secundarios

`BusinessP360Exception` conserva el mapeo histórico de códigos y ahora inicializa correctamente `Exception.Message`. Su constructor ya no escribe en SQL ni oculta un fallo de logging. El punto que captura la excepción continúa siendo responsable de registrarla; la separación evita I/O sorpresivo durante la creación del error.

## Evidencia

- build Release Windows x64 correcto;
- 37/37 pruebas de caracterización;
- ocho llamadas concurrentes produjeron un único acceso a la fuente y la misma instancia de snapshot;
- una carga fallida pudo reintentarse sin quedar cacheada;
- configuración ausente, modo inválido, puerto inválido y SSL inválido fallan sin exponer valores;
- el modo heredado ejecuta exactamente una lectura por cada uno de los 14 nombres;
- los tres tipos de job se construyen sin consultar la fuente de parámetros;
- `JobDataMap` no contiene conexión, clave de mapas ni contraseña SMTP;
- el golden hash de `XtraReportPedidosP360.cs` se actualizó sólo por la inyección del snapshot; su Designer, `.resx` y lógica visual permanecen intactos;
- el diff de los cinco archivos `.rpt` está vacío.

## Limitaciones conscientes

La prueba automatizada usa una fuente en memoria y no abre SQL ni SMTP. La estructura del proveedor batch emite un único comando SQL, pero su conectividad y permisos deben validarse en el ambiente no productivo autorizado antes del despliegue. No se cambió el proveedor definitivo de secretos: D-008 continúa pendiente y las variables de entorno siguen siendo el puente aprobado.
