# Resumen ejecutivo

## Dictamen

Scheduler P360 Insight tiene un núcleo funcional valioso y compila correctamente en Release x64 en la estación evaluada. La modernización debe ser incremental: primero proteger y hacer reproducible el sistema actual sobre .NET Framework 4.8; después desacoplar infraestructura y endurecer la entrega de notificaciones; finalmente aislar Crystal Reports, sin modificar los .rpt, para migrar sólo el proceso principal a .NET 10 LTS.

No se recomienda una reescritura. Tampoco se recomienda saltar directamente de la solución actual a .NET moderno: la falta de pruebas, el acoplamiento con base de datos/correo/reportes y la dependencia de Crystal convertirían ese salto en una regresión difícil de detectar.

## Prioridad inmediata

Antes de crear el primer remoto o compartir un commit:

1. Tratar como comprometidos los valores sensibles encontrados en App.config y Properties/Settings.settings.
2. Rotar o invalidar la clave de mapas y la cuenta SQL correspondiente, según confirme su propietario.
3. retirar los valores del código y regenerar cualquier archivo derivado que los contenga;
4. escanear el árbol completo, binarios y cambios preparados;
5. crear la línea base Git únicamente cuando el resultado sea limpio.

El repositorio no tiene commits ni remoto configurado. Esto es una ventaja: todavía es posible crear una historia inicial saneada sin preservar los valores sensibles en el historial.

## Hallazgos principales

| Prioridad | Hallazgo | Consecuencia |
|---|---|---|
| Crítica | Valores sensibles en archivos de configuración preparados para el primer commit | Exposición de acceso a datos y consumo de API |
| Alta | No existen pruebas automatizadas ni CI | Cualquier refactor puede romper programación, renderizado o envío sin señal temprana |
| Alta | Paquetes de Crystal publicados en NuGet por un tercero y log4net 2.0.12 marcado con vulnerabilidad moderada | Riesgo de cadena de suministro y parcheo |
| Alta | No hay reclamación atómica ni idempotencia visible en la cola de notificaciones | Correos duplicados o perdidos ante solapamientos y fallos parciales |
| Alta | Proceso interactivo, Console.ReadKey, MessageBox y salidas abruptas | Bloqueo o caída de una ejecución desatendida |
| Alta | ReportDocument y otros recursos no se liberan de forma consistente | Fuga de memoria/handles y degradación prolongada |
| Media | Release AnyCPU produce numerosos avisos por ensamblados AMD64 | Despliegue ambiguo y fallos por arquitectura |
| Media | Conflictos de versiones Microsoft.Extensions y System.Memory | Comportamiento de carga dependiente de binding redirects |
| Media | Configuración leída mediante múltiples consultas repetidas | Latencia, carga SQL y mayor superficie de fallo |
| Media | Tiempos de espera infinitos y errores absorbidos | Tareas colgadas y falsa apariencia de éxito |

## Línea base cuantitativa

- Una solución y un ejecutable clásico de .NET Framework 4.8.
- 47 archivos C# y aproximadamente 34.552 líneas.
- Aproximadamente 86,1 % del C# es código generado por diseñadores.
- 29 archivos manuscritos y aproximadamente 4.802 líneas: el volumen susceptible de refactor es moderado.
- Cinco reportes Crystal y seis clases principales de reportes DevExpress.
- Release x64: compilación completada con cero errores; existen advertencias de código y conflictos de ensamblados.
- Release AnyCPU: compila con cero errores, pero presenta 36 advertencias, varias por referencias AMD64.
- Cero proyectos de pruebas y cero flujos de integración continua.

## Resultado esperado del programa

Al finalizar, el sistema deberá:

- ejecutarse como proceso x64 administrado y no interactivo;
- validar toda su configuración antes de iniciar;
- recuperar programación sin duplicar notificaciones;
- separar planificación, acceso a datos, renderizado y entrega;
- disponer de pruebas de caracterización y contratos de base de datos;
- producir logs estructurados, métricas y estado de salud sin exponer PII;
- generar artefactos repetibles, firmados y con inventario de componentes;
- operar el host principal en .NET 10 LTS y conservar Crystal en un worker .NET Framework x64 aislado, sin convertir los reportes.

## Horizonte y criterio de velocidad

La unidad de avance es el PR, no una gran versión. Los tamaños S, M y L del roadmap expresan complejidad relativa, no días calendario. La velocidad se decidirá después de PR-02, cuando existan pruebas y datos reales de duración, volumen y fallos.

## Lo que no se validó

La aplicación no se ejecutó contra servicios reales porque el arranque puede consultar SQL, registrar trabajos y enviar correo. Tampoco se dispuso de esquema completo de base de datos, procedimientos almacenados, infraestructura de producción, licencias ni SLO. La compilación sólo demuestra compatibilidad con la estación evaluada, que ya tiene instalados componentes SAP Crystal y DevExpress.
