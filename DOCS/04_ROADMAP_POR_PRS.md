# Roadmap por PRs

## Regla de secuenciación

Gate 0 no es un PR normal: debe ocurrir antes de publicar el primer commit. A partir de ahí, cada PR parte de main verde, contiene una sola intención y dispone de rollback. No se inicia un PR bloqueado por una decisión pendiente.

## Mapa

| Orden | Cambio | Tamaño | Dependencia | Resultado principal |
|---:|---|:---:|---|---|
| Gate 0 | Saneamiento previo al historial | S | Ninguna | Árbol sin secretos publicables |
| PR-01 | Build reproducible y x64 | M | Gate 0 | Línea base repetible y CI inicial |
| PR-02 | Caracterización y arnés de pruebas | L | PR-01 | Red de seguridad funcional |
| PR-03 | SDK-style y PackageReference sobre net48 | M | PR-02 | Proyecto modernizable sin cambiar runtime |
| PR-04 | Higiene y actualización de dependencias | M | PR-03 | Grafo soportado y sin conflictos conocidos |
| PR-05 | Configuración, composición y caché | M | PR-02 | Dependencias explícitas y parámetros eficientes |
| PR-06 | Ciclo de vida no interactivo | M | PR-05 | Arranque/parada aptos para servicio |
| PR-07 | Observabilidad y salud | M | PR-06 | Operación medible y alertable |
| PR-08 | Capa de datos resiliente | L | PR-05, PR-07 | Timeouts, tipos, cancelación y errores consistentes |
| PR-09 | Endurecimiento de Quartz | M | PR-07, PR-08 | Cron, misfire y concurrencia definidos |
| PR-10 | Cola idempotente y recuperación | L | decisión D-003, PR-08 | Menos duplicados/pérdidas y reintento controlado |
| PR-11 | Pipeline de renderizado y archivos | L | PR-02, PR-05 | Recursos liberados y salida segura |
| PR-12 | Correo y plantillas seguras | M | PR-05, PR-10 | Entrega observable y HTML codificado |
| PR-13 | Aislamiento de Crystal sin modificar .rpt | M/L | PR-02, PR-11 | Separar el runtime legado del host principal |
| PR-14 | Migración del host a .NET 10 | L | PR-04, PR-13 | Plataforma LTS moderna |
| PR-15 | Empaquetado, despliegue y optimización medida | L | PR-14 | Release firmado, canary, rollback y tuning |

Las fichas detalladas están en PLANES_PR.

Estado al 2026-07-22: Gate 0 y PR-01 a PR-11 están integrados o validados localmente en secuencia. PR-12 es el siguiente incremento. Los gates de despliegue que requieren infraestructura/datos autorizados permanecen explícitos en cada ficha.

## Trenes de entrega

### Tren 0: proteger el activo

Gate 0 y PR-01.

Criterio de salida:

- historia inicial sin secretos;
- x64 como plataforma declarada;
- build limpio reproducible en un runner Windows autorizado;
- artefactos locales fuera de Git.

### Tren 1: poder cambiar sin romper

PR-02 a PR-05.

Criterio de salida:

- contratos y pruebas sobre programación, composición y reportes críticos;
- proyecto SDK/PackageReference net48;
- dependencias inventariadas y verificadas;
- configuración inmutable y sin I/O en constructores.

### Tren 2: operar de forma confiable

PR-06 a PR-12.

Criterio de salida:

- proceso no interactivo con apagado ordenado;
- trazas y métricas con correlación;
- timeouts finitos;
- semántica de scheduler y cola explícita;
- renderizado, archivos, SMTP y plantillas endurecidos.

### Tren 3: aislar el bloqueo legado

PR-13.

Criterio de salida:

- cinco reportes Crystal intactos y caracterizados externamente;
- worker net48 x64 aislado con runtime oficial, límites y operación documentada;
- el host principal no carga Crystal;
- ninguna conversión de .rpt forma parte del programa.

### Tren 4: plataforma moderna y entrega

PR-14 y PR-15.

Criterio de salida:

- host .NET 10 x64;
- despliegue automatizado y firmado;
- canary, observación y rollback ensayados;
- objetivos de rendimiento medidos.

## Reglas de paralelización

Se pueden investigar en paralelo, pero no mezclar en un PR:

- inventario de licencias SAP/DevExpress;
- definición de SLO y métricas;
- fixtures anonimizados;
- prototipo del contrato y empaquetado del worker Crystal, sin editar reportes;
- diseño SQL de claim/lease.

No se deben ejecutar en paralelo sobre producción:

- cambio de semántica de cola y cambio de scheduler;
- migración de runtime y sustitución de reportes;
- rotación de secretos y despliegue sin una ventana coordinada.

## Política de alcance

Un PR se divide si:

- cambia más de una frontera principal;
- necesita dos planes de rollback distintos;
- modifica SQL y además altera salida de reporte sin compatibilidad;
- no puede describirse en una frase;
- exige revisar archivos generados masivos sin una razón funcional.

## Definición de programa completado

- Cero secretos y cero vulnerabilidades críticas/altas aceptadas sin excepción formal.
- Cien por ciento de jobs con propietario, timeout, concurrencia, misfire y zona horaria definidos.
- Cien por ciento de rutas críticas con correlación, métrica y alerta.
- Pruebas de caracterización para cada tipo de reporte y flujo de notificación.
- Recuperación demostrada después de caída entre envío y confirmación.
- Build y despliegue reproducibles desde un runner limpio autorizado.
- Rollback probado para binario y esquema.
- Plataforma soportada por proveedores durante el horizonte operativo acordado.
