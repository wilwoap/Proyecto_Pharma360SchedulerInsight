# PR-14 — Migración del host a .NET 10 LTS

Estado: bloqueado por PR-13 y revisión vigente de soporte. Dependencias: PR-04, PR-06 a PR-13.

## Propósito

Ejecutar el scheduler principal en .NET 10 LTS x64 con ciclo de vida y bibliotecas modernas, preservando contratos.

## Justificación inicial

A fecha 2026-07-21, .NET 10 es LTS hasta noviembre de 2028. .NET 8 y .NET 9 finalizan soporte en noviembre de 2026, por lo que no ofrecen un horizonte suficiente para una migración nueva. Esta decisión se vuelve a validar al abrir el PR.

## Alcance

- Target net10.0-windows x64.
- Generic Host y Windows Service.
- Adaptadores compatibles de DevExpress 25.2 o versión validada.
- Cliente versionado del worker Crystal net48 x64.
- Proveedor SQL moderno compatible.
- Configuración/DI/logging/health integrados.
- Nullable y analizadores de forma progresiva.
- Publicación framework-dependent/self-contained por D-006.

## Implementación

1. Confirmar matriz OS, .NET, DevExpress, Quartz, SQL y licencias.
2. Multi-targetear bibliotecas puras cuando ayude.
3. crear host net10 que consuma contratos ya probados;
4. portar adaptadores compatibles y consumir Crystal sólo mediante el contrato del worker;
5. reemplazar APIs exclusivas de .NET Framework;
6. activar nullable primero en código nuevo/extraído;
7. publicar x64 y probar en host limpio;
8. ejecutar vieja/nueva en shadow sin envío o con partición segura;
9. canary y corte controlado.

## Fuera de alcance

- Rediseñar reglas de negocio.
- Actualizar simultáneamente formato visual.
- Adoptar versiones mayores no necesarias de todas las librerías.

## Pruebas

- Suite completa en net10.
- Comparación de scheduling y resultados con net48.
- Servicio start/stop/recovery.
- SQL/TLS/SMTP/filesystem.
- DevExpress render/licencia.
- Carga, memoria, handles y larga duración.
- Publicación/instalación en Windows soportado limpio.

## Criterios de aceptación

- Host principal net10 x64 sin assemblies Crystal; los .rpt permanecen intactos en el worker net48.
- Cero dependencia de API exclusiva net48 no aislada.
- Paridad funcional aprobada.
- Servicio recupera y apaga correctamente.
- Paquetes/OS soportados durante horizonte acordado.
- Canary cumple SLO y rollback ensayado.

## Rollback

Instalación lado a lado con nombres/configuración separados. Detener nueva instancia, reconciliar leases y reactivar net48. Mantener esquema compatible y no permitir consumo simultáneo no coordinado.
