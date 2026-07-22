# Modernización de Scheduler P360 Insight

Estado del expediente: valoración y Gate 0 completados. PR-01 a PR-04 validados; PR-05 es el siguiente incremento.

Este directorio es la fuente de verdad del programa de modernización. El objetivo es conservar el comportamiento funcional mientras se reduce, mediante cambios pequeños y reversibles, el riesgo de seguridad, operación y mantenimiento.

## Lectura recomendada

1. [00_RESUMEN_EJECUTIVO.md](00_RESUMEN_EJECUTIVO.md): decisión propuesta, prioridades y límites.
2. [01_INVENTARIO_Y_LINEA_BASE.md](01_INVENTARIO_Y_LINEA_BASE.md): evidencia observada y comandos de reproducción.
3. [02_VALORACION_INTEGRAL.md](02_VALORACION_INTEGRAL.md): evaluación por dimensión y hallazgos.
4. [03_ARQUITECTURA_OBJETIVO.md](03_ARQUITECTURA_OBJETIVO.md): estado objetivo y estrategia de transición.
5. [04_ROADMAP_POR_PRS.md](04_ROADMAP_POR_PRS.md): secuencia, dependencias y criterio de avance.
6. [05_SEGURIDAD_Y_CONFIGURACION.md](05_SEGURIDAD_Y_CONFIGURACION.md): tratamiento de secretos y superficies de ataque.
7. [06_ESTRATEGIA_DE_PRUEBAS.md](06_ESTRATEGIA_DE_PRUEBAS.md): caracterización, integración y regresión de reportes.
8. [07_GATES_DE_CALIDAD.md](07_GATES_DE_CALIDAD.md): controles obligatorios por PR.
9. [08_OPERACION_DESPLIEGUE_Y_ROLLBACK.md](08_OPERACION_DESPLIEGUE_Y_ROLLBACK.md): ejecución, observabilidad y reversión.
10. [09_DECISIONES_Y_PREGUNTAS_ABIERTAS.md](09_DECISIONES_Y_PREGUNTAS_ABIERTAS.md): decisiones que requieren contexto del negocio u operación.
11. [10_CONFIGURACION_DE_ENTORNO.md](10_CONFIGURACION_DE_ENTORNO.md): variables requeridas y operación segura.
12. [11_BUILD_Y_RUNNER.md](11_BUILD_Y_RUNNER.md): build canónico x64 y preparación del runner Windows.
13. [12_ARNES_DE_CARACTERIZACION.md](12_ARNES_DE_CARACTERIZACION.md): pruebas aisladas, fixtures y deudas observadas.
14. [13_SDK_STYLE_Y_PACKAGEREFERENCE.md](13_SDK_STYLE_Y_PACKAGEREFERENCE.md): formato SDK, restauración bloqueada y compatibilidad del output.
15. [14_DEPENDENCIAS_Y_SBOM.md](14_DEPENDENCIAS_Y_SBOM.md): grafo actualizado, excepción limitada de log4net y SBOM SPDX.
16. [GUIA_DE_TRABAJO_POR_PR.md](GUIA_DE_TRABAJO_POR_PR.md): instrucciones para desarrollar y revisar cada cambio.
17. [PLANES_PR/README.md](PLANES_PR/README.md): índice de las fichas de implementación.

## Principios no negociables

- No cambiar reglas de negocio y plataforma a la vez.
- No publicar el estado actual mientras existan secretos o credenciales embebidos.
- Tratar Crystal Reports como una caja negra: no modificar, convertir ni rediseñar archivos .rpt dentro de este programa.
- No enviar correo ni modificar datos de producción desde pruebas automatizadas.
- Cada PR debe incluir evidencia, telemetría útil y una ruta de reversión.
- Las migraciones de base de datos deben ser compatibles hacia atrás durante el despliegue.
- Un cambio de infraestructura no se considera terminado hasta probar arranque, parada y recuperación.

## Convenciones de estado

- Propuesto: diseño documentado, aún sin implementación.
- En curso: rama o PR activo con responsable.
- Validado: criterios de aceptación demostrados en un entorno controlado.
- Desplegado: operando con observación posterior al cambio.

Las fichas se actualizarán en el mismo PR que cambie su estado. Una decisión nueva se registra en 09_DECISIONES_Y_PREGUNTAS_ABIERTAS.md antes de aplicarla.
