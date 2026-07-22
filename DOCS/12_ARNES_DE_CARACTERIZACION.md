# Arnés de caracterización

Estado: implementado en PR-02 y ampliado/validado localmente hasta PR-06 el 2026-07-22.

## Decisión de framework

El arnés usa MSTest.Sdk 4.3.2, fijado en el proyecto de pruebas. Esa versión soporta .NET Framework 4.6.2 o superior y, por tanto, net48. El runner integrado de Microsoft Testing Platform produce un ejecutable x64 que puede ejecutarse sin depender de descubrimiento externo de Visual Studio.

Referencias oficiales:

- https://www.nuget.org/packages/MSTest.Sdk/4.3.2
- https://learn.microsoft.com/dotnet/core/testing/unit-testing-mstest-running-tests
- https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-cli-options

## Ejecución

El comando canónico restaura, recompila y ejecuta el arnés:

    .\build.ps1 -Configuration Release -Target Rebuild

Para desarrollo con paquetes ya restaurados:

    .\build.ps1 -Configuration Release -Target Build -SkipRestore

SkipTests existe sólo para diagnóstico del build. No debe usarse como evidencia de un PR.

El gate exige al menos 45 pruebas, un timeout global de 60 segundos y genera TRX bajo artifacts/test-results. Esa carpeta no se versiona.

## Fronteras introducidas

- ReportJobFactory concentra el mapeo exacto de los tres valores heredados de report_type y la construcción del trigger Cron.
- AppConfig permite sustituir únicamente el lector de variables de entorno.
- IEmailTransport separa el envío SMTP.
- INotificationDeliveryStore separa contactos, auditoría y confirmación de cola.
- ReportPathPolicy define una ruta hija y rechaza traversal o nombres absolutos.
- SchedulerApplication separa registro, inicio, espera y apagado mediante fronteras simulables.
- ConsoleApplicationLifetime convierte señales en cancelación idempotente sin leer input.
- Los constructores predeterminados conservan los adaptadores SQL y SMTP actuales.

La aplicación usa la fábrica de jobs y las fronteras de correo. La política de rutas se conectará al pipeline completo en PR-11, donde podrá aplicarse a todos los renderizadores con rollback conjunto.

## Cobertura inicial

| Riesgo | Evidencia local |
|---|---|
| Tres tipos de reporte | Crystal, DevExpress y HTML se mapean al tipo de job heredado |
| Cron | Trigger válido construido; expresión inválida detectada |
| Configuración ausente | Falla determinista, sin prompt ni valor sensible |
| UID | Catálogo conocido y compatibilidad observada para UID desconocido |
| PDF | Fixture sintético no vacío con firma PDF |
| HTML especial | Se captura que el legado inserta caracteres sin codificar |
| SMTP | Éxito y excepción simulados sin red |
| SQL | Timeout simulado antes del envío, sin conexión real |
| Rutas | Ruta hija aceptada; traversal y ruta absoluta rechazados |
| Reportes | SHA-256 de cinco .rpt y una definición DevExpress representativa |
| Dapper | Materialización de un registro con un proveedor ADO.NET simulado y cierre de conexión |
| Quartz | Inicio, agenda, ejecución y parada de un job sobre RAMJobStore |
| Crystal/log4net | El host no referencia log4net y Crystal declara exactamente la ABI 2.0.12.0 |
| Configuración | Opciones completas/incompletas, modo batch/legacy y redacción de secretos |
| Snapshot | Inmutabilidad, validación, concurrencia, fallo transitorio y reducción de lecturas |
| Composición | Los tres jobs se crean sin I/O ni secretos en JobDataMap |
| Excepciones | BusinessP360Exception mapea mensajes sin escribir en SQL desde el constructor |
| Ciclo de vida | Orden de inicio/parada, cancelación, timeout, fallback y códigos de salida sin dependencias externas |
| Host no interactivo | Escaneo de producción sin ReadKey, ReadLine, MessageBox ni Environment.Exit |

## Deudas observadas, no corregidas en este PR

- Una clave de plantilla inexistente retorna null aunque el comentario heredado indica cadena vacía.
- Los valores dinámicos de HTML se insertan sin codificación.
- Un UID desconocido se conserva y llega al job por compatibilidad histórica.
- La prueba de PDF es sintética; todavía no existe un fixture SQL anonimizado autorizado para generar golden masters visuales reales.
- No se prueba una conexión SQL real ni un servidor SMTP real en CI.

Estas observaciones son intencionales: PR-02 impide que cambien accidentalmente. HTML se endurece en PR-12; archivos y PDFs en PR-11; contratos SQL en PR-08. La aprobación visual de PDFs se requiere antes de PR-13 y nunca implica modificar archivos Crystal.

## Datos y aislamiento

Todas las identidades, correos, teléfonos, rutas y contenido de prueba son sintéticos. Los dominios usan example.test. El arnés no lee P360_CONNECTION_PRINCIPAL, no abre SQL y no envía correo. La prueba de Quartz usa exclusivamente RAMJobStore en proceso y no abre servicios externos.

Los cinco .rpt sólo se leen como bytes para calcular SHA-256. No se cargan con Crystal, no se convierten y no se reescriben.

## Actualización del golden master

Un cambio de hash falla. Para actualizarlo se requiere:

1. explicar por qué cambia el activo;
2. demostrar que no contiene datos nuevos;
3. obtener aprobación del propietario funcional del reporte;
4. actualizar el hash en el mismo PR;
5. adjuntar comparación semántica o visual cuando exista un fixture aprobado.

Nunca se actualiza el manifiesto sólo para hacer verde una prueba.
