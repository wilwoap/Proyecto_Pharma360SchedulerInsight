# Fichas de implementación

Cada archivo define un incremento revisable. Los números expresan dependencia lógica; pueden cambiarse sólo actualizando el roadmap y las referencias.

| Ficha | Estado inicial |
|---|---|
| GATE-00-SANEAMIENTO-PRECOMMIT.md | Completado |
| PR-01-BUILD-REPRODUCIBLE-X64.md | Validado |
| PR-02-PRUEBAS-DE-CARACTERIZACION.md | Validado |
| PR-03-SDK-PACKAGEREFERENCE-NET48.md | Validado |
| PR-04-DEPENDENCIAS.md | Validado |
| PR-05-CONFIGURACION-Y-COMPOSICION.md | Validado |
| PR-06-CICLO-DE-VIDA-DE-SERVICIO.md | Validado; adaptador de servicio pendiente de D-006 |
| PR-07-OBSERVABILIDAD-Y-SALUD.md | Núcleo neutral validado; plataforma/alertas pendientes de D-010 |
| PR-08-ACCESO-A-DATOS-RESILIENTE.md | Propuesto |
| PR-09-QUARTZ-ENDURECIDO.md | Propuesto |
| PR-10-COLA-IDEMPOTENTE.md | Bloqueado por D-003 |
| PR-11-PIPELINE-DE-REPORTES.md | Propuesto |
| PR-12-CORREO-Y-PLANTILLAS.md | Propuesto |
| PR-13-AISLAMIENTO-DE-CRYSTAL.md | Propuesto; no modifica archivos .rpt |
| PR-14-MIGRACION-NET10.md | Bloqueado por PR-13 |
| PR-15-DESPLIEGUE-Y-OPTIMIZACION.md | Propuesto |

## Campos de una ficha

- Propósito: resultado único.
- Alcance: trabajo autorizado.
- Fuera de alcance: protección contra crecimiento accidental.
- Implementación: orden recomendado.
- Pruebas/evidencia: cómo demostrarlo.
- Aceptación: condición binaria para merge.
- Rollback: reversión práctica.
- Riesgos/dependencias: decisiones o coordinación.

Una ficha no sustituye la descripción del PR; se actualiza con las decisiones y evidencia reales.
