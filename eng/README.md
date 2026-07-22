# Herramientas de ingeniería

Este directorio contiene controles ejecutables del repositorio:

- warnings-baseline.json congela las advertencias heredadas de Release x64.
- test-baseline.json fija el número mínimo, timeout y plataforma del arnés de caracterización.
- verify-repository.ps1 impide versionar salidas, certificados y secretos de alta confianza.
- compare-build-outputs.ps1 compara el inventario, los DLL, la configuración y el manifiesto entre dos builds.
- generate-sbom.ps1 genera y valida un SBOM SPDX 2.2 del output con Microsoft SBOM Tool 4.1.5.
- verify-dependencies.ps1 bloquea vulnerabilidades no exceptuadas, hallazgos altos/críticos y paquetes obsoletos.
- Test-SqlContracts.ps1 valida mapeo, timeout, cancelación y pooling contra una instancia LocalDB sintética y efímera; nunca usa la conexión P360.
- Test-NotificationQueueContracts.ps1 aplica dos veces la expansión PR-10 y valida claim concurrente, lease/reclaim, backoff, dead-letter, reproceso y auditoría sobre LocalDB sintético; nunca usa la conexión P360.
- dependency-exceptions.json contiene excepciones temporales exactas, justificadas y con vencimiento obligatorio.

El punto de entrada del build es build.ps1 en la raíz. La restauración normal exige los `packages.lock.json`; `-UpdateLockFile` es la operación explícita para actualizarlos. Los controles no leen ni imprimen valores de variables de entorno sensibles.
