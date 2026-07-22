# Arquitectura objetivo

## Objetivo

Convertir el ejecutable acoplado actual en un servicio de planificación y notificación con límites explícitos. SQL, SMTP y DevExpress permanecen detrás de adaptadores; Crystal se conserva como una caja negra en un worker .NET Framework x64 separado.

## Componentes

    Windows Service / modo consola diagnóstico
        |
        +-- Bootstrap y validación de opciones
        |
        +-- Scheduling
        |     +-- carga definiciones desde SQL
        |     +-- validación Cron, zona horaria y política de misfire
        |     +-- coordinación de concurrencia
        |
        +-- Application
        |     +-- ejecutar reporte
        |     +-- reclamar notificación
        |     +-- renderizar
        |     +-- entregar
        |     +-- confirmar o reintentar
        |
        +-- Rendering
        |     +-- DevExpress renderer
        |     +-- HTML renderer
        |     +-- cliente del worker Crystal net48 x64
        |
        +-- Notifications
        |     +-- composición segura de mensaje
        |     +-- SMTP/provider adapter
        |
        +-- Data
        |     +-- scheduled report repository
        |     +-- notification outbox repository
        |     +-- parameter snapshot provider
        |
        +-- Observability
              +-- logs estructurados
              +-- métricas
              +-- health/readiness
              +-- auditoría redactada

## Reglas de dependencia

- Domain/Application no conoce SqlConnection, Quartz, XtraReport, ReportDocument ni SmtpClient.
- Los adaptadores dependen de contratos del núcleo, no al revés.
- Ningún constructor realiza I/O.
- La configuración se carga una vez, se valida y se expone como instantánea inmutable.
- Los jobs reciben identificadores y servicios; no cadenas de conexión ni secretos en JobDataMap.
- Los mensajes de negocio no dependen de plantillas de recursos generadas.
- Los errores tienen categoría, operación, correlación y causa; el mensaje al operador no contiene secretos.

## Flujo de una notificación

    Trigger
      -> validar definición
      -> reclamar fila con lease y clave de idempotencia
      -> renderizar a almacenamiento temporal controlado
      -> enviar con identificador de operación
      -> confirmar estado
      -> limpiar temporal

Ante fallo:

    fallo transitorio
      -> registrar intento y próximo reintento con backoff

    fallo permanente
      -> cola muerta + alerta operativa

    lease expirado
      -> recuperación segura sin dos propietarios activos

El sistema no puede obtener atomicidad distribuida entre SMTP y SQL. La mitigación realista es entrega al menos una vez con idempotencia/identificador, estado duradero y reconciliación. El comportamiento exacto necesita la decisión D-003.

## Estrategia de transición

### Etapa A: encapsular el legado en .NET Framework 4.8 x64

- Línea base reproducible.
- Pruebas de caracterización.
- Contratos para datos, renderizado y correo.
- Eliminación de estado global y recursos sin liberar.
- Sin cambiar la salida visible.

### Etapa B: endurecer operación y datos

- Proceso no interactivo.
- Configuración validada.
- Observabilidad.
- Scheduler explícito.
- Outbox/lease e idempotencia.

### Etapa C: aislar Crystal sin modificar reportes

Decisión:

- los cinco archivos .rpt, sus fórmulas, consultas, parámetros y diseño visual quedan fuera del alcance de modernización;
- encapsular el camino actual como comportamiento opaco y caracterizarlo externamente;
- ejecutar Crystal en un worker x64 net48 separado con runtime oficial SAP;
- exponer un contrato local restringido, versionado, autenticado y con timeout;
- mantener el host principal libre de ensamblados Crystal;
- limitar concurrencia y memoria del worker, y permitir reciclado controlado;
- conservar comparación de salida sólo para demostrar que el aislamiento no cambió resultados.

Una eventual conversión a DevExpress será un programa independiente, voluntario y con aprobación funcional expresa; no es requisito de este roadmap.

### Etapa D: host .NET 10 LTS

- migrar el núcleo y adaptadores compatibles, consumiendo Crystal únicamente mediante el contrato del worker;
- usar Generic Host y soporte de Windows Service;
- actualizar acceso SQL y paquetes;
- habilitar nullable y analizadores progresivamente;
- realizar canary y convivencia temporal con la versión net48.

## Datos y compatibilidad de despliegue

Toda evolución de la cola se aplica en expand/contract:

1. agregar columnas/objetos compatibles y desplegar;
2. hacer que la aplicación escriba ambos formatos si es necesario;
3. migrar/validar datos;
4. activar lectura nueva;
5. retirar el formato antiguo en un PR posterior.

No se combina una migración destructiva de SQL con un binario que depende exclusivamente de ella.

## Fronteras de seguridad

- Secretos: proveedor externo o configuración protegida accesible sólo a la identidad de servicio.
- SQL: cuenta de mínimo privilegio con acceso a objetos requeridos.
- Archivos: raíz permitida fija, nombres saneados y permisos del servicio.
- HTML: codificación por contexto; las plantillas son código confiable y los datos no.
- SMTP: TLS validado, destinatarios normalizados y límites de tamaño.
- Logs: lista permitida de campos; sin credenciales, cuerpos completos ni PII innecesaria.

## Restricciones que se preservan

- Windows y arquitectura x64 mientras existan dependencias actuales.
- Esquemas/reportes y reglas de negocio existentes hasta disponer de pruebas de paridad.
- SQL como fuente de definiciones mientras no se apruebe otro diseño.
- Compatibilidad con DevExpress 25.2 durante la primera fase.
