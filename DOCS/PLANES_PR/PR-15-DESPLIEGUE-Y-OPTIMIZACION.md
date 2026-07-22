# PR-15 — Despliegue industrializado y optimización medida

Estado: propuesto. Dependencia principal: PR-14.

## Propósito

Cerrar el programa con una entrega repetible, segura y operable, y optimizar sólo cuellos demostrados.

## Alcance

- Empaquetado/instalación corporativa.
- Firma, checksum, SBOM y procedencia.
- Promoción por ambientes.
- Migraciones expand/contract automatizadas.
- Canary y rollback.
- Baseline/carga/tuning.
- Retención y housekeeping.
- Retiro final de código/paquetes/formularios huérfanos verificados.

## Implementación

1. Elegir formato de paquete y estrategia framework-dependent/self-contained.
2. Automatizar build, firma, publicación y promoción inmutable.
3. separar configuración por entorno y referencias de secretos;
4. automatizar prerequisitos/servicio/ACL y verificaciones;
5. ensayar instalación limpia, upgrade y rollback;
6. medir carga real anonimizada: throughput, p95/p99, memoria, handles, SQL y disco;
7. perfilar y cambiar un cuello por vez;
8. aplicar retención/reconciliación;
9. retirar dead code/dependencias sólo con pruebas de reachability y aprobación.

## Fuera de alcance

- Optimización sin métrica.
- Limpieza estética masiva.
- Borrado inmediato de compatibilidad/esquema anterior.
- Acceso del pipeline a secretos productivos fuera del despliegue autorizado.

## Escenarios de rendimiento

- Carga normal/pico de schedules.
- Backlog después de caída.
- Reporte más pesado repetido.
- Notificaciones concurrentes dentro del límite.
- Ejecución prolongada para fugas.
- SQL/SMTP lentos.
- Disco cercano a umbral.

## Criterios de aceptación

- Artefacto firmado, versionado, escaneado y reproducible.
- Instalación/upgrade/rollback desde automatización autorizada.
- Sin pasos manuales ocultos.
- Canary y alertas validados.
- SLO acordados cumplidos bajo carga.
- Ninguna “optimización” cambia salida.
- Retención impide crecimiento indefinido.
- Runbook y responsables actualizados.

## Rollback

Artefacto anterior inmutable disponible, esquema compatible y script probado. Cambios de tuning usan configuración/flags cuando sea razonable. Una limpieza de código sólo se revierte por Git; nunca se recuperan artefactos locales ignorados como fuente.

