# Herramientas de ingeniería

Este directorio contiene controles ejecutables del repositorio:

- warnings-baseline.json congela las advertencias heredadas de Release x64.
- verify-repository.ps1 impide versionar salidas, certificados y secretos de alta confianza.

El punto de entrada del build es build.ps1 en la raíz. Los controles no leen ni imprimen valores de variables de entorno sensibles.
