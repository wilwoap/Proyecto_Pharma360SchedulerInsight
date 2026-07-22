# Valoración integral

## Escala

La puntuación representa capacidad de evolucionar y operar con seguridad, no valor funcional.

- 1–2: riesgo crítico o control ausente.
- 3–4: control manual y frágil.
- 5–6: base útil, todavía inconsistente.
- 7–8: controlado y medible.
- 9–10: optimizado y continuamente mejorado.

## Resultado

| Dimensión | Puntuación | Síntesis |
|---|---:|---|
| Funcionalidad y compilación | 8/10 | El sistema es usado como funcional y compila en x64; falta prueba end-to-end controlada |
| Arquitectura | 4/10 | Responsabilidades concentradas y dependencias estáticas |
| Mantenibilidad | 4/10 | Núcleo manuscrito acotado, pero utilitarios y acceso a datos tienen alto acoplamiento |
| Pruebas | 1/10 | No existen proyectos ni automatización |
| Seguridad | 2/10 | Secretos embebidos, paquete vulnerable y superficies de HTML/rutas |
| Fiabilidad | 3/10 | Fallos parciales, concurrencia e idempotencia no controlados |
| Observabilidad | 3/10 | Consola y tabla SQL sin métricas, salud ni correlación consistente |
| Rendimiento | 5/10 | Volumen de código razonable, pero hay consultas repetidas y recursos no liberados |
| Build y reproducibilidad | 4/10 | Compila localmente, depende de instalaciones/rutas y configuraciones ambiguas |
| Operación y despliegue | 3/10 | Ciclo interactivo, ClickOnce heredado y rollback no documentado |

Valoración global orientativa: 3,7/10 en madurez de ingeniería. No implica que el producto “funcione mal”; indica que cambiarlo o recuperarlo tiene más riesgo del necesario.

## Arquitectura y diseño

Hallazgos:

- El punto de entrada consulta configuración, interpreta filas SQL, construye jobs y controla el ciclo de vida.
- Utilitarios.cs mezcla envío SMTP, renderizado HTML, acceso a cola, Crystal, rutas y lógica de presentación.
- ModuleCapaAccesoDatos.cs mezcla consultas, comandos, logging y manejo de errores.
- LaboratoryConstants ejecuta numerosas consultas individuales y se instancia repetidamente.
- AppConfig.ConnectionString y otros estados estáticos dificultan pruebas y concurrencia.
- BusinessP360Exception realiza escritura en SQL desde su constructor.

Impacto:

- una prueba unitaria requiere infraestructura real o sustituciones difíciles;
- una modificación de correo puede afectar reportes y datos;
- el comportamiento depende del orden de inicialización y estado global.

Acción:

- introducir interfaces en los bordes, composición explícita y modelos inmutables;
- separar Scheduler, Rendering, Notifications, Data y Configuration;
- mantener adaptadores legados detrás de contratos durante la transición.

## Seguridad

Hallazgos:

- App.config contiene una clave de API de mapas.
- Properties/Settings.settings y su archivo generado contienen una cadena SQL con usuario y contraseña.
- La clave de mapas se inserta en una URL de imagen dentro del correo, por lo que puede llegar al receptor.
- contenido procedente de SQL se inserta en HTML sin codificación sistemática;
- rutas y nombres de archivo proceden de configuración/datos sin una frontera explícita de canonicalización;
- logs pueden registrar direcciones, rutas y contexto identificable;
- log4net 2.0.12 aparece marcado con vulnerabilidad moderada en NuGet.

Acción:

- ejecutar Gate 0 antes del primer commit;
- rotar, externalizar y limitar secretos por identidad/origen/cuota;
- codificar valores HTML y validar URI, destinatarios y rutas;
- crear política de redacción y retención;
- encapsular y aislar la dependencia Crystal; corregir su procedencia y log4net sólo dentro de una validación específica, sin modificar los .rpt.

## Fiabilidad y consistencia

Hallazgos:

- No se observa reclamación atómica de una notificación antes de procesarla.
- No se usa una política explícita para impedir ejecución concurrente del mismo job.
- El envío SMTP y la marca de “enviado” en SQL no forman una operación atómica.
- Si SMTP tiene éxito y la actualización SQL falla, un reintento puede duplicar el correo.
- Algunos métodos absorben excepciones; Quartz puede interpretar éxito.
- Se accede a NextFireTime.Value aunque el valor puede no existir.
- Una fila de programación inválida puede abortar todo el arranque.
- La política de misfire de Cron no está elegida de forma explícita.

Acción:

- definir semántica de entrega con negocio;
- implementar claim/lease, clave de idempotencia, intentos y cola muerta;
- validar cada definición aisladamente;
- configurar concurrencia, misfire y zona horaria;
- propagar fallos con clasificación transitorio/permanente.

## Rendimiento y recursos

Hallazgos:

- LaboratoryConstants realiza cerca de catorce lecturas separadas de parámetros por instancia.
- Se crean múltiples instancias durante la ejecución de jobs y reportes.
- ReportDocument no siempre se cierra o libera.
- Se envuelven operaciones en Task.Run sin convertir realmente el acceso a datos/SMTP en asíncrono.
- Hay commandTimeout igual a cero en rutas de datos, equivalente a espera indefinida.
- No existe política visible de retención para PDF generados.

Acción:

- cargar parámetros en bloque y cachear una instantánea validada;
- aplicar using/Dispose a reportes, streams, conexiones y comandos;
- retirar Task.Run artificial y añadir asincronía/cancelación sólo donde la API lo soporte;
- establecer presupuestos de tiempo por operación;
- medir antes/después y limpiar archivos por política.

## Dependencias y plataforma

Hallazgos:

- .NET Framework 4.8 continúa ligado al ciclo de vida de Windows, pero no recibe evolución de producto.
- En la fecha de evaluación, .NET 10 es LTS hasta noviembre de 2028; .NET 8 y .NET 9 terminan soporte en noviembre de 2026.
- DevExpress 25.2 declara soporte para .NET 10 y .NET Framework.
- El SDK de Crystal utilizado está orientado a .NET Framework; es el principal bloqueo del salto completo.
- SAP mantiene Crystal Reports developer version for Visual Studio hasta finales de 2029, pero el runtime de 32 bits quedó fuera de mantenimiento después de diciembre de 2025.
- Los paquetes Crystal 13.0.4003 encontrados en NuGet no son publicados por SAP.
- Quartz 3.6.2 es antiguo frente a la rama 3.x estable actual; no se debe saltar a una futura rama mayor sin validación.

Acción:

- estabilizar primero en net48 x64;
- convertir a SDK/PackageReference sin cambiar runtime;
- actualizar dependencias por lotes pequeños;
- preservar los reportes Crystal sin cambios y aislar su ejecución en un worker .NET Framework x64;
- seleccionar .NET 10 para el host moderno, sujeto a matriz de compatibilidad.

## Datos y manejo de errores

Hallazgos:

- La mayoría de consultas observadas están parametrizadas, lo cual es positivo.
- Hay usos de AddWithValue que pueden provocar inferencias de tipo/plan deficientes.
- Existen capturas amplias que devuelven vacío o continúan, perdiendo causa y estado.
- El logging de base de datos está acoplado al flujo de error y también puede fallar.
- StackFrame para inferir al llamador no es estable bajo optimización.

Acción:

- contratos tipados por operación;
- parámetros con tipo, tamaño y nulabilidad explícitos;
- timeouts finitos y cancellation tokens;
- error estructurado con identificador de correlación;
- repositorios/adaptadores probables con pruebas de contrato.

## Experiencia operativa

Hallazgos:

- Console.ReadKey mantiene vivo el proceso.
- Ante configuración faltante se solicita interacción y se intenta modificar una variable de máquina.
- Environment.Exit y MessageBox aparecen en caminos de error.
- Quartz usa almacenamiento en memoria; la programación se reconstruye desde SQL.
- No hay endpoint/sonda de salud, métricas, apagado coordinado ni manual de recuperación.
- Hay propiedades ClickOnce y una ruta de publicación local absoluta.

Acción:

- proceso no interactivo y cuenta de servicio de mínimo privilegio;
- validación fail-fast antes de registrar trabajos;
- arranque/parada controlados y drenaje de jobs;
- salud, métricas y alertas;
- artefacto versionado, instalación repetible y rollback probado.

## Referencias oficiales consultadas

- Microsoft, migración de Windows Forms: https://learn.microsoft.com/dotnet/desktop/winforms/migration/
- Microsoft, ciclo de soporte de .NET: https://learn.microsoft.com/dotnet/core/releases-and-support
- Microsoft, ciclo de vida de .NET Framework: https://learn.microsoft.com/lifecycle/products/microsoft-net-framework
- DevExpress, soporte de .NET para WinForms: https://docs.devexpress.com/WindowsForms/401191/dotnet-core-support
- DevExpress, Reporting 25.2: https://docs.devexpress.com/XtraReports/2162/reporting?v=25.2
- SAP, mantenimiento de Crystal Reports para Visual Studio: https://help.sap.com/docs/SUPPORT_CONTENT/crystalreports/3354088411.html
- Quartz, documentación de CronTrigger y misfires: https://www.quartz-scheduler.net/documentation/quartz-4.x/tutorial/crontriggers.html
- NuGet, Quartz: https://www.nuget.org/packages/Quartz
- NuGet, log4net 2.0.12: https://www.nuget.org/packages/log4net/2.0.12
- NuGet, paquete CrystalReports.Engine observado: https://www.nuget.org/packages/CrystalReports.Engine/

Los datos de versiones y soporte son válidos a la fecha de evaluación y deben revisarse al iniciar cada PR de plataforma.
