# Decisiones y preguntas abiertas

## Registro

| ID | Decisión | Estado | Propuesta/evidencia |
|---|---|---|---|
| D-001 | Estrategia de modernización | Aceptada por diseño inicial | Incremental, sin reescritura |
| D-002 | Arquitectura de proceso durante etapa legado | Propuesta firme | Sólo x64 por dependencias AMD64 y soporte Crystal |
| D-003 | Semántica de entrega de notificación | Pendiente | Al menos una vez, con idempotencia/reconciliación |
| D-004 | Runtime objetivo | Propuesta | .NET 10 LTS; revisar soporte al iniciar PR-14 |
| D-005 | Destino de Crystal | Aceptada 2026-07-21 | Preservar los .rpt sin cambios y aislarlos en un worker net48 x64; conversión fuera del programa |
| D-006 | Empaquetado y hosting | Pendiente | Windows Service; mecanismo corporativo/MSI por decidir |
| D-007 | Misfire, zona horaria y solapamiento | Pendiente | Política explícita por schedule |
| D-008 | Proveedor de secretos | Pendiente | Almacén administrado/identidad; DPAPI como puente |
| D-009 | Persistencia de Quartz | Pendiente | SQL actual como fuente más rebuild, o job store persistente |
| D-010 | Plataforma de telemetría | Pendiente | Logs/métricas compatibles con estándar corporativo |
| D-011 | Retención de PDF, logs y cola muerta | Pendiente | Definir por clasificación y SLO |
| D-012 | Runner y licencias propietarias | Pendiente | Windows self-hosted autorizado inicialmente |
| D-013 | Soporte del OS de producción | Pendiente | Inventario de edición/build/ESU o migración |

“Propuesta” no autoriza implementación si cambia comportamiento o infraestructura.

## Preguntas antes de Gate 0

- ¿Las credenciales encontradas son reales y siguen activas?
- ¿En qué otras aplicaciones se usan?
- ¿Quién autoriza y ejecuta la rotación?
- ¿El repositorio será privado o público?
- ¿Existe un estándar corporativo para secretos y escaneo?

## Preguntas antes de PR-01

- ¿Dónde compila hoy y qué licencias tiene ese equipo?
- ¿Puede existir un runner Windows interno?
- ¿Qué artefacto se instala realmente: publish/ClickOnce, copia de carpeta, tarea o servicio?
- ¿Qué versiones exactas de Crystal runtime hay en cada host?
- ¿AnyCPU tiene algún consumidor real?

## Preguntas antes de PR-02

- ¿Hay una base no productiva con esquema equivalente?
- ¿Qué datasets/reportes representan el comportamiento aprobado?
- ¿Quién puede anonimizar y aprobar fixtures?
- ¿Qué direcciones/dominio intercepta el SMTP de prueba?
- ¿Cuáles son las reglas de negocio críticas por reporte?

## Preguntas antes de PR-07/PR-10

- ¿Cuántas notificaciones por hora/día y cuál es el pico?
- ¿Qué retraso es aceptable?
- ¿Un duplicado es preferible a una pérdida, o viceversa?
- ¿El servidor SMTP soporta un identificador idempotente observable?
- ¿Cuánto tiempo se reintenta y quién atiende dead-letter?
- ¿Dos instancias pueden ejecutarse hoy?
- ¿Cómo se corrigen filas atascadas actualmente?

## Preguntas antes de PR-09

- ¿Qué zona horaria gobierna cada Cron?
- ¿Qué debe ocurrir con ejecuciones perdidas durante una caída?
- ¿Se permite solapamiento del mismo reporte?
- ¿Cambiar la programación en SQL debe surtir efecto sin reiniciar?
- ¿Se necesitan calendarios de feriados?

## Preguntas antes de PR-13

- ¿Qué versión oficial de runtime SAP se instalará en cada host?
- ¿Qué identidad, ACL y transporte local usará el worker?
- ¿Qué límites de memoria, concurrencia, timeout y reciclado necesita?
- ¿Quién valida que el aislamiento conserva exactamente la salida actual?
- ¿Qué política de soporte/licenciamiento tendrá el worker durante su vida útil?

## Preguntas antes de PR-14/PR-15

- ¿Qué Windows Server/desktop se soportará?
- ¿La organización aprueba .NET 10 y su ciclo hasta noviembre de 2028?
- ¿Qué mecanismo de firma y distribución se usa?
- ¿Qué ventanas/canary/rollback existen?
- ¿Qué plataforma recibe logs, métricas y alertas?
- ¿Cuáles son SLO, RTO y RPO?

## Cómo cerrar una decisión

Añadir:

- fecha;
- participantes/propietario;
- contexto y opciones;
- decisión;
- consecuencias y compensaciones;
- PR que la implementa;
- fecha de revisión si depende de soporte/licencia.

No borrar decisiones reemplazadas; marcarlas superseded e indicar la nueva.
