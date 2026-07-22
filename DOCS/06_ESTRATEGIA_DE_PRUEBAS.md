# Estrategia de pruebas

## Propósito

Crear una red de seguridad antes de refactorizar. Las primeras pruebas capturan el comportamiento actual; no intentan demostrar que toda regla heredada sea correcta. Cualquier discrepancia observada se registra como decisión de negocio antes de corregirla.

## Pirámide propuesta

| Nivel | Alcance | Frecuencia |
|---|---|---|
| Unitarias | Parseo, validación, plantillas, nombres, políticas de retry/misfire | Cada commit |
| Caracterización | Comportamiento actual de jobs y utilitarios detrás de seams | Cada PR |
| Contrato SQL | Parámetros, resultados y estados de procedimientos/vistas | Cada PR que toque datos |
| Integración | Quartz, renderer, almacenamiento temporal y SMTP sink | Cada PR relevante |
| Golden master | Salida de reportes con datos anonimizados | Cambios de renderer/dependencia |
| End-to-end | Programar, reclamar, renderizar, enviar a sink y confirmar | Antes de promover |
| Resiliencia/carga | caída, timeout, concurrencia, volumen y recuperación | Antes de release/cambios operativos |

## Infraestructura de prueba

- Base SQL dedicada y desechable, nunca producción.
- Esquema versionado o restauración de una copia anonimizada aprobada.
- Cuenta sin acceso a otras bases.
- SMTP sink local/interno que no enrute a Internet.
- Directorio temporal aislado por ejecución.
- Reloj y generador de identificadores sustituibles.
- Fixtures sin PII, secretos ni propiedad intelectual innecesaria.
- Runtime/assemblies SAP y DevExpress instalados en runner Windows autorizado.

## Catálogo mínimo de caracterización

### Arranque y programación

- Configuración válida crea el número esperado de jobs/triggers.
- Fila inválida se rechaza sin impedir las válidas.
- Tipo de reporte desconocido produce diagnóstico accionable.
- Expresión Cron inválida no inicia un job.
- Identidades repetidas se detectan.
- Zona horaria y siguiente ejecución coinciden con casos aprobados.
- Configuración ausente falla sin interacción ni exposición de valores.

### Cola

- Una notificación sólo tiene un propietario activo.
- Dos workers concurrentes no envían la misma fila.
- Lease vencido puede recuperarse.
- Fallo transitorio incrementa intento y programa reintento.
- Fallo permanente llega a cola muerta.
- SMTP exitoso seguido de fallo SQL se reconcilia según D-003.
- Repetir el mismo identificador no produce un segundo efecto no deseado.

### Correo y HTML

- To/CC válidos se normalizan.
- Direcciones/cabeceras inválidas se rechazan.
- Caracteres HTML y URL maliciosos se codifican.
- La clave de mapas no aparece en cuerpo ni URL.
- Límite de adjunto/cuerpo se aplica.
- El resultado de SMTP se propaga; no se registra éxito falso.

### Archivos

- traversal y rutas absolutas externas se rechazan;
- caracteres inválidos y nombres repetidos son seguros;
- un error limpia temporales;
- reconciliación elimina sólo temporales propios bajo la raíz permitida;
- la retención de PDF finales se prueba cuando D-011 defina plazo y propietario.

### Renderizado

Para cada reporte:

- dataset esperado y parámetros;
- número de páginas;
- texto/valores críticos;
- orientación, tamaño y encabezados;
- ausencia/presencia de gráficos;
- PDF no vacío y legible;
- tiempo y memoria dentro de umbral;
- liberación de archivo/handle después de renderizar.

## Golden masters de PDF

La comparación byte a byte suele fallar por metadatos, fechas o identificadores. Usar:

1. fixture SQL anonimizado y versionado;
2. reloj fijo;
3. extracción normalizada de texto y metadatos relevantes;
4. rasterizado de páginas críticas con tolerancia documentada;
5. comprobaciones semánticas explícitas;
6. aprobación manual para cambios visuales intencionales.

Los PDF golden sólo se almacenan si su licencia y clasificación de datos lo permiten. Si no, guardar hashes/resultados derivados y generar el PDF durante la prueba.

## Contratos SQL

Cada operación debe documentar:

- objeto y versión;
- parámetros, tipo, tamaño y nulabilidad;
- conjunto de resultados y significado;
- timeout esperado;
- permisos mínimos;
- idempotencia;
- comportamiento ante reejecución y concurrencia.

Las pruebas de contrato deben ejecutarse contra SQL Server real compatible, no sólo mocks. Los mocks se reservan para lógica de aplicación.

## Pruebas de resiliencia

Inyectar de forma controlada:

- timeout/conexión SQL;
- fallo de logging SQL;
- SMTP no disponible, lento o respuesta permanente;
- archivo bloqueado/disco lleno;
- renderer que falla o consume demasiado;
- parada del servicio durante cada etapa;
- dos instancias concurrentes;
- reloj adelantado y transición de zona horaria;
- backlog grande y misfire.

Verificar que no hay espera infinita, bloqueo interactivo, pérdida silenciosa ni duplicación fuera de la semántica aprobada.

## Datos y ambientes

- Dev: fixtures sintéticos.
- CI: esquema efímero y SMTP sink.
- QA: copia anonimizada autorizada.
- Preproducción: topología y políticas equivalentes, destinatarios interceptados.
- Producción: smoke tests no destructivos y canary.

Nunca se permite ejecutar pruebas automatizadas con destinatarios o credenciales de producción.

## Cobertura y calidad

La cobertura numérica no es el objetivo inicial. Gates propuestos:

- todo código nuevo de dominio/aplicación con pruebas;
- toda corrección incluye una prueba que falla antes;
- rutas críticas del catálogo cubiertas antes de PR-10;
- cobertura de cambios, no global heredada, como control progresivo;
- ninguna exclusión sin explicación.

## Evidencia por PR

- comando y resultado;
- configuración/fixture utilizado, sin secreto;
- pruebas nuevas y riesgo cubierto;
- capturas o PDFs sólo si están anonimizados;
- métricas antes/después para optimización;
- resultado de rollback cuando aplique.

## Cobertura inicial ejecutada en PR-02

El arnés net48 x64 ejecuta 22 casos aislados. Cubre configuración, mapeo de jobs, Cron, UID, transporte SMTP simulado, timeout de acceso simulado, HTML heredado, rutas y activos de reporte. Los cinco .rpt están protegidos por hash y no se cargan.

PR-11 eleva el gate a 120 casos e incorpora contrato neutral de renderer, compatibilidad UID/proveedor, promoción PDF byte a byte, colisiones concurrentes, límite de nombre, cancelación/fallo y reconciliación selectiva. Los `.rpt` siguen sin cargarse en el arnés.

Pendientes deliberados: canary contra SQL Server desechable, SMTP sink de integración, generación de PDFs reales y comparación visual. Requieren infraestructura/fixtures autorizados y se completan en PR-12 y antes de aislar Crystal en PR-13.
