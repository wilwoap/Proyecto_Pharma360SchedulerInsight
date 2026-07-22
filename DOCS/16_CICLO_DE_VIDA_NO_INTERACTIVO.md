# Ciclo de vida no interactivo

Estado: validado localmente en PR-06 el 2026-07-22.

## Resultado

El ejecutable net48 x64 ya no depende de teclado, cuadros de diálogo ni una sesión de escritorio para permanecer activo o detenerse. `Main` sólo delega en un proceso componible; el núcleo de ejecución puede ser alojado posteriormente por el mecanismo que cierre D-006.

Esta etapa no convierte el ejecutable en servicio de Windows ni cambia su destino de plataforma. El host legado continúa siendo Windows x64 por Crystal Reports y DevExpress. Un adaptador `ServiceBase`, MSI o paquete corporativo se añadirá únicamente cuando operación defina el mecanismo de instalación, identidad y control del servicio.

## Modos de ejecución

Desde la carpeta del artefacto:

    .\SchedulerP360Insight.exe --console

También se admite temporalmente la ejecución sin argumentos para no romper el lanzador existente. Ese modo muestra una advertencia de transición y ejecuta el mismo ciclo no interactivo. Las opciones disponibles pueden consultarse sin configuración ni acceso a SQL:

    .\SchedulerP360Insight.exe --help

Cualquier argumento desconocido se rechaza antes de cargar configuración.

## Secuencia de arranque y parada

Arranque:

1. cargar y validar configuración y el snapshot inmutable;
2. crear Quartz y conectar la fábrica de jobs;
3. cargar todas las definiciones desde SQL con token de cancelación;
4. registrar jobs y triggers con token de cancelación;
5. iniciar Quartz;
6. esperar una señal de parada sin consumir CPU ni solicitar input.

Parada:

1. recibir `Ctrl+C` o una señal del host;
2. ejecutar `Standby` para impedir nuevos disparos;
3. solicitar `Shutdown(waitForJobsToComplete: true)`;
4. esperar como máximo el presupuesto configurado;
5. si se agota, ejecutar `Shutdown(waitForJobsToComplete: false)` y terminar con código 4.

`ProcessExit` se escucha como defensa de mejor esfuerzo. La garantía real de tiempo de parada dependerá del futuro adaptador de servicio y del timeout que el Service Control Manager o la plataforma concedan.

## Presupuesto de apagado

La variable opcional `P360_SHUTDOWN_TIMEOUT_SECONDS` acepta un entero entre 1 y 900. El valor predeterminado es 30 segundos. Un valor ausente usa el predeterminado; un valor inválido impide iniciar Quartz y nunca se refleja en el mensaje de error.

El presupuesto cubre `Standby` y el apagado ordenado de Quartz. PR-08 propaga cancelación a SQL y PR-11 a los límites del pipeline de reportes; una exportación sincrónica Crystal/DevExpress no puede preemptarse con seguridad dentro del proceso. PR-12 completa SMTP y PR-13 aporta el timeout duro/reciclado mediante aislamiento de Crystal.

## Códigos de salida

| Código | Significado |
|---:|---|
| 0 | ejecución o ayuda completada correctamente |
| 1 | fallo no controlado |
| 2 | argumentos o configuración inválidos |
| 3 | dependencia SQL o Quartz no disponible |
| 4 | se agotó el presupuesto de apagado ordenado |

Los mensajes de error muestran categorías y tipos, no cadenas de conexión, claves ni trazas con secretos.

## Operación

Inicio controlado:

1. comprobar que sólo habrá una instancia activa; D-009 aún debe definir la política formal multiinstancia;
2. configurar las variables para la identidad del proceso;
3. ejecutar con `--console` desde el directorio del release;
4. verificar que se informa el número de definiciones registradas y el inicio de Quartz.

Parada controlada:

1. enviar `Ctrl+C` en consola o la señal equivalente del host;
2. verificar los eventos de `Standby` y apagado;
3. esperar el presupuesto configurado;
4. tratar el código 4 como parada forzada y revisar jobs que no terminaron.

No iniciar la versión anterior y la nueva simultáneamente contra la misma cola. Para rollback, detener la candidata, conservar logs/estado de cola, restaurar el directorio y configuración del release anterior e iniciar una sola instancia.

## Compatibilidad de reportes

Se eliminaron todos los `MessageBox` de las clases de reporte. Los errores cancelan el renderizado y se propagan al job en vez de bloquear una cuenta no interactiva; la configuración opcional de colores conserva su fallback y escribe una advertencia. No se modificó ningún `.rpt`, diseñador ni recurso visual.

El golden master sólo se actualizó para el archivo fuente `XtraReportPedidosP360.cs`, cuya gestión de error dejó de mostrar UI. Sus archivos `.Designer.cs` y `.resx`, y los cinco `.rpt` vigilados, conservan exactamente sus hashes.

## Evidencia

- build canónico Release x64 completado;
- 45 de 45 pruebas de caracterización aprobadas;
- adaptador de ciclo de vida validado también contra Quartz con `RAMJobStore`;
- ayuda devuelve 0 y argumentos desconocidos devuelven 2 sin exigir configuración;
- escaneo de producción sin `Console.ReadKey`, `Console.ReadLine`, `MessageBox.Show` ni `Environment.Exit`;
- diff de archivos `.rpt` vacío;
- SHA-256 del ejecutable de la validación: `2A306EC9D7ACD532B6F02D2D01A8FEDA6EEFFAA10D77EA0E0E6D078BF0A54B9C`.
