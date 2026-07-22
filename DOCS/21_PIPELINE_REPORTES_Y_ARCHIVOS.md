# Pipeline de reportes y archivos

Estado: implementado y validado localmente en PR-11 el 2026-07-22. La comparación visual con datos P360 anonimizados y la política de retención de PDF finales son gates de despliegue, no supuestos resueltos por el código.

## Resultado

Los tres caminos de reporte usan un contrato neutral `IReportRenderer`. DevExpress y Crystal producen un `ReportRenderResult` tipado con un PDF promovido de forma atómica; HTML devuelve explícitamente un resultado sin artefacto y conserva la composición del cuerpo para PR-12.

Crystal continúa siendo una caja negra. PR-11 no modifica, convierte ni interpreta archivos `.rpt`, fórmulas, consultas o diseño. La fachada sólo controla carga, parámetros heredados, exportación y liberación del `ReportDocument`.

## Contratos

`ReportRenderRequest` transporta únicamente:

- UID y nombre del reporte;
- código de colaborador y referencia del evento;
- instante estable del lote;
- raíces de origen/salida y nombre del activo, cuando corresponden.

`ReportRenderResult` distingue `pdf` de `none`, conserva el tipo de renderer y, para PDF, entrega ruta final y longitud. `ReportRenderException` limita el código de fallo a 64 caracteres seguros y declara si el fallo es permanente; la cola durable conserva esa clasificación.

El contrato no expone `ReportDocument`, `XtraReport`, streams ni tipos SMTP/SQL.

## Flujo de un PDF

    solicitud validada
      -> nombre PDF saneado y acotado
      -> temporal aleatorio dentro de la raíz autorizada
      -> exportación del proveedor
      -> comprobación de firma PDF y tamaño
      -> promoción atómica sin sobrescritura
      -> resultado tipado
      -> entrega existente

Ante fallo o cancelación, el temporal de esa operación se elimina en `finally`. Si ya existe el nombre solicitado, se añade `-1` hasta `-999`; el nombre final continúa limitado a 180 caracteres y nunca se reemplaza un PDF anterior.

## Matriz de renderers y UID

| Renderer | UID admitidos | Artefacto |
|---|---|---|
| Crystal | `PVM`, `PVMM`, `PVG`, `PVGM` | PDF |
| DevExpress | `AURX`, `AUMD`, `RPED`, `DPED`, `XPED`, `VPED` | PDF |
| HTML | los 14 UID heredados conocidos | ninguno en PR-11 |

Una combinación tipo/UID incompatible se rechaza antes de crear el job Quartz. Si un adaptador recibe un UID desconocido, falla permanentemente con `renderer.unknown_uid`; no genera un archivo vacío.

## Adaptador DevExpress

- Usa `XtraReport` fuertemente tipado; se retiró `dynamic` del job.
- Conserva los parámetros y clases de reporte heredados.
- `XtraReport`, `MemoryStream` y `FileStream` se liberan determinísticamente por notificación.
- La copia al archivo temporal admite cancelación.
- La exportación sincrónica propia de DevExpress sólo puede comprobar cancelación antes y después de la llamada; no se abandona un `Task` que siga reteniendo recursos.

## Fachada Crystal

El orden observable heredado se conserva:

1. cargar el `.rpt`;
2. registrar la cola asíncrona existente;
3. aplicar información de conexión;
4. leer las notificaciones;
5. aplicar `p_codFichero`, `p_codColaborador` y `p_urlLogoEmpresa` por notificación;
6. exportar con la utilidad existente;
7. cerrar y disponer el documento al terminar el lote.

La fachada carga un único `ReportDocument` por ejecución del job, igual que el camino anterior, y no altera el contenido del reporte. Un fallo al cerrar se registra como `report.renderer.dispose_failed` sin convertir una entrega ya realizada en fallo ni reintentar el correo.

La exportación Crystal es sincrónica y no ofrece preempción segura dentro del proceso. PR-11 comprueba cancelación en los límites de la etapa. El timeout duro, límite de memoria y reciclado pertenecen al worker aislado de PR-13; ejecutar la llamada en un `Task` abandonado produciría exactamente la fuga que se quiere evitar.

## Política de rutas y archivos

La raíz de salida debe ser absoluta, existir y ser escribible por la identidad del servicio. El pipeline no crea directorios por sorpresa. El nombre solicitado:

- no puede ser una ruta absoluta ni incluir directorios;
- reemplaza caracteres inválidos/de control;
- protege nombres de dispositivo Windows, incluso con extensiones adicionales;
- termina en `.pdf` y se limita a 180 caracteres.

El origen Crystal debe estar bajo una raíz absoluta existente, tener extensión `.rpt`, existir y no atravesar puntos de reparación debajo de esa raíz. La comprobación lexical impide `..` y rutas absolutas externas.

Los temporales propios usan `.p360-render-<guid>.tmp`. Una instancia reconcilia cada raíz una vez por proceso y elimina sólo sus temporales con más de 24 horas, sólo en el nivel superior y nunca mediante un punto de reparación. No elimina PDF finales ni archivos ajenos.

## Retención y capacidad

PR-11 preserva la conducta heredada respecto de los PDF finales: permanecen después del envío. No se ha inventado un plazo de borrado porque D-011 requiere clasificación de datos, SLO, propietario operativo y requisitos de auditoría.

Antes de activar este release en producción se debe:

1. confirmar espacio y alerta de disco para la raíz de salida;
2. cerrar D-011 o documentar formalmente la retención heredada aceptada;
3. probar permisos de lectura/escritura/borrado con la identidad real;
4. prohibir una tarea genérica que elimine fuera de la raíz aprobada.

## Cancelación y fallos

- La cancelación se propaga fuera del job y no se registra como fallo de notificación/reintento.
- Un temporal faltante, vacío o sin firma PDF es un fallo permanente clasificado.
- Un UID o raíz configurada inválidamente es permanente.
- Los fallos del proveedor conservan su tipo y pasan por la clasificación durable existente.
- No se borra un PDF promovido si falla una etapa posterior; así se conserva evidencia y se evita confundir limpieza temporal con retención.

## Observabilidad

Se miden `render.crystal`, `render.devexpress` y `render.html`. Al completar o fallar se publican sólo campos en lista permitida:

- `renderer_kind`;
- `artifact_kind` y `artifact_bytes`;
- `working_set_delta_bytes`;
- `handle_delta`.

La ruta, nombre del reporte, destinatario, código de colaborador y contenido del documento no se publican. La reconciliación usa `report.temp.reconciled`, `report.temp.reconciliation_failed` y `report.temp.delete_failed` con conteos acotados, nunca nombres de archivo.

## Evidencia local

- Build canónico Release/x64 aprobado con el baseline de advertencias sin aumento.
- 120/120 pruebas MSTest net48/x64, sin SQL, SMTP ni Internet.
- Fixture PDF golden promovido byte a byte sin cambios.
- Cobertura de nombre heredado, traversal, nombres reservados, límite, colisión y concurrencia.
- Cobertura de fallo, cancelación, PDF inválido y limpieza selectiva de temporales.
- Validación de catálogo UID, clasificación durable y campos observables sin rutas.
- Los hashes protegidos de los cinco `.rpt` continúan bajo el arnés de caracterización.

La evidencia local no afirma paridad visual de un PDF real generado por SAP/DevExpress con datos P360. Ese canary necesita el dataset anonimizado y los servicios licenciados/autorizados del entorno; debe ejecutarse antes de promover PR-13.

## Despliegue y rollback

Canary recomendado:

1. usar una raíz de salida aislada y restringida;
2. ejecutar un caso representativo por UID con datos anonimizados;
3. comparar páginas/contenido y parámetros con el release anterior;
4. comprobar que el PDF se puede abrir, adjuntar, eliminar y que no quedan temporales;
5. observar duración, memoria, handles y crecimiento de disco;
6. habilitar sólo después los schedules restantes.

Rollback: detener la candidata, restaurar el binario/configuración anterior y ejecutar una única instancia. PR-11 no requiere migración SQL ni modifica activos de reporte. Los PDF ya promovidos se conservan; cualquier limpieza posterior se hace sólo con la política de D-011.

## Pendientes deliberados

- PR-12: composición HTML y frontera completa de correo/plantillas.
- PR-13: aislamiento de Crystal, timeout duro, límites de proceso y reciclado; sin editar `.rpt`.
- D-011: retención de PDF finales y dead-letter.
- Gate de despliegue: golden visual por UID con dataset anonimizado y runtime/licencias reales.
