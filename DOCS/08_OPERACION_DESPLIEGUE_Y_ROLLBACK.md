# Operación, despliegue y rollback

## Estado actual y objetivo

Desde PR-06, el núcleo ejecuta un ciclo no interactivo con señales, `Standby`, apagado acotado y códigos de salida. Se mantiene un adaptador de consola y la compatibilidad temporal sin argumentos; falta confirmar cómo se inicia realmente en cada ambiente. El objetivo recomendado sigue siendo un Windows Service x64 con identidad dedicada y ciclo de vida administrado.

ClickOnce no es el mecanismo recomendado para un proceso desatendido. Mientras Crystal siga presente, el runtime oficial debe instalarse mediante un medio soportado por SAP; para el servicio/aplicación se elegirá MSI, paquete corporativo o automatización equivalente después de D-006.

## Inventario previo

Recopilar sin secretos:

- ambiente, host, edición/build de Windows y soporte vigente;
- CPU/memoria/disco y zona horaria;
- método de inicio y usuario;
- versión de SAP Crystal runtime y arquitectura;
- versión/licencia de DevExpress;
- .NET Framework/.NET instalados;
- acceso y latencia a SQL/SMTP;
- rutas de reportes/salida y ACL;
- antivirus/EDR y exclusiones aprobadas;
- programación, volumen, tamaño de adjuntos y duración;
- procedimiento actual de despliegue/rollback;
- ventanas de mantenimiento y contactos.

La estación de evaluación reporta Windows 10 build 19045. Windows 10 22H2 terminó soporte general el 2025-10-14 salvo ediciones/programas específicos. No se asume que sea producción; se debe comprobar edición y cobertura ESU/LTSC.

## Ciclo de vida objetivo

Arranque:

1. cargar configuración y resolver referencias;
2. validar secretos por presencia, nunca imprimirlos;
3. probar permisos/rutas y conectividad con límites;
4. cargar/validar todas las definiciones;
5. publicar readiness;
6. iniciar scheduler.

Parada:

1. retirar readiness;
2. dejar de aceptar nuevos trabajos;
3. solicitar cancelación;
4. esperar el límite configurado;
5. conservar estados/leases recuperables;
6. liberar renderers, SMTP, archivos y scheduler;
7. terminar con código significativo.

No existe ReadKey, MessageBox, prompt ni modificación automática de variables de máquina. El flujo implementado y sus límites se documentan en `16_CICLO_DE_VIDA_NO_INTERACTIVO.md`.

Mientras D-006 permanezca pendiente, el inicio explícito es:

    .\SchedulerP360Insight.exe --console

La parada se solicita con `Ctrl+C` o mediante la señal del host. El proceso espera 30 segundos por defecto, configurable con `P360_SHUTDOWN_TIMEOUT_SECONDS`, y devuelve 4 si necesita forzar el apagado. El adaptador `ServiceBase`, la identidad y el instalador todavía no están implementados.

## Telemetría mínima

PR-07 implementa eventos JSON permitidos, correlación, métricas en memoria y health opcional en `P360_HEALTH_FILE_PATH`. El contrato y el dashboard neutral están en `17_OBSERVABILIDAD_Y_SALUD.md`; D-010 todavía debe seleccionar y operar el colector corporativo.

Logs:

- arranque/parada/configuración válida;
- definición aceptada/rechazada;
- job iniciado/finalizado;
- claim, render, envío y confirmación;
- reintento/dead-letter;
- fallo con categoría y correlación.

Métricas:

- scheduler_jobs_registered;
- scheduler_definitions_invalid;
- job_duration_seconds;
- notifications_claimed_total;
- notifications_sent_total;
- notifications_failed_total;
- notification_retry_count;
- notification_queue_age_seconds;
- render_duration_seconds;
- render_failures_total;
- smtp_duration_seconds;
- process_working_set_bytes;
- process_handle_count;
- temporary_files_count/bytes.

Salud:

- liveness: proceso y event loop responden;
- readiness: configuración válida, scheduler iniciado y dependencias esenciales disponibles según política;
- detalle diagnóstico protegido: SQL, SMTP, espacio, licencia/renderer.

## SLO candidatos

Estos valores no se fijan sin datos:

- porcentaje de notificaciones confirmadas;
- retraso p95 desde programación hasta envío;
- antigüedad máxima de cola;
- tasa de duplicados;
- tiempo de recuperación tras caída;
- tasa de renderizado fallido;
- tiempo de arranque y apagado.

PR-07 recoge baseline; negocio/operación aprueba objetivos y alertas.

## Procedimiento de despliegue

Precondiciones:

- release firmado y hash verificado;
- SBOM y escaneo aprobados;
- backup/rollback de configuración y esquema;
- compatibilidad hacia atrás validada;
- destinatarios interceptados en preproducción;
- ventana y responsable confirmados.

Pasos:

1. retirar instancia del servicio;
2. detener de forma ordenada y verificar que no quedan jobs activos;
3. aplicar expansión SQL compatible si corresponde;
4. instalar versión junto a la anterior, no sobrescribir la única copia;
5. aplicar configuración por referencia a secretos;
6. iniciar en canary;
7. verificar salud, carga de jobs y smoke test sin correo real;
8. observar métricas durante la ventana acordada;
9. ampliar despliegue;
10. conservar versión anterior hasta cerrar observación.

## Rollback

Disparadores sugeridos:

- no inicia/readiness;
- jobs ausentes o duplicados;
- aumento de fallos o latencia sobre umbral;
- reporte inválido;
- duplicación/pérdida de notificaciones;
- crecimiento anormal de memoria/handles;
- incidente de seguridad.

Procedimiento:

1. pausar nuevos triggers si continuar aumenta el daño;
2. detener la versión nueva;
3. conservar evidencia y estado de cola;
4. reactivar binario/config anterior;
5. no revertir una expansión SQL compatible salvo necesidad;
6. reconciliar leases y notificaciones antes de reanudar;
7. verificar salud y una ejecución controlada;
8. abrir análisis causal.

Nunca se restaura una credencial revocada como rollback.

## Recuperación de incidentes

- Correo duplicado: pausar el schedule afectado, preservar notificationId/correlationId y reconciliar.
- Backlog: limitar concurrencia, priorizar antigüedad/criticidad y evitar disparar todos los misfires sin decisión.
- SQL caído: no esperar indefinidamente; registrar localmente, reintentar con backoff y proteger la base de una estampida.
- SMTP caído: mantener estado durable y reintento limitado.
- Renderer con fuga: circuit breaker/aislamiento y reciclado controlado como mitigación temporal.
- Disco lleno: parar nuevos renderizados, alertar y limpiar sólo bajo la raíz/retención aprobada.

## Retención

Definir por ambiente:

- PDFs temporales;
- logs locales/centrales;
- eventos de auditoría;
- estados de cola/dead-letter;
- artefactos de release;
- backups de configuración/esquema.

Toda eliminación debe ser trazable, limitada a rutas validadas y coherente con regulación/negocio.
