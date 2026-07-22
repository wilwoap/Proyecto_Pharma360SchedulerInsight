# Herramientas de ingeniería

Este directorio contiene controles ejecutables del repositorio:

- warnings-baseline.json congela las advertencias heredadas de Release x64.
- test-baseline.json fija el número mínimo, timeout y plataforma del arnés de caracterización.
- verify-repository.ps1 impide versionar salidas, certificados y secretos de alta confianza.
- compare-build-outputs.ps1 compara el inventario, los DLL, la configuración y el manifiesto entre dos builds.

El punto de entrada del build es build.ps1 en la raíz. La restauración normal exige los `packages.lock.json`; `-UpdateLockFile` es la operación explícita para actualizarlos. Los controles no leen ni imprimen valores de variables de entorno sensibles.
