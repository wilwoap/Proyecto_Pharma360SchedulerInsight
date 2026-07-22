# Guía de trabajo por PR

## Antes de empezar

1. Elegir una ficha de PLANES_PR con dependencias completadas.
2. Resolver las decisiones pendientes que cambien el resultado.
3. Actualizar main y demostrar gates verdes.
4. Crear una rama corta con nombre tipo modernizacion/pr-XX-descripcion.
5. Registrar baseline específica del comportamiento a tocar.

No comenzar desde el estado actual hasta completar Gate 0.

## Durante el cambio

- Mantener una sola intención.
- Introducir un seam antes de reemplazar una dependencia.
- Escribir primero la prueba de caracterización o fallo.
- Evitar formateo/renombre no relacionado.
- No editar diseñador generado salvo que sea inevitable y explicarlo.
- Mantener compatibilidad SQL expand/contract.
- No llamar servicios productivos.
- Añadir cancelación, timeout y logging en fronteras nuevas.
- Actualizar la ficha y decisiones en el mismo PR.

## Estructura de commits

Commits pequeños y revisables:

- test: caracteriza comportamiento X;
- refactor: extrae contrato Y sin cambiar resultado;
- fix/feat: implementa cambio;
- docs: actualiza operación/decisión.

No mezclar artefactos generados con lógica si pueden separarse. No incluir binarios, packages, publish, PFX, archivos de usuario ni secretos.

## Descripción del PR

Copiar y completar:

### Objetivo

Una frase que explique el resultado.

### No incluido

Lista explícita de problemas cercanos que quedan fuera.

### Riesgo

Escenarios que podrían romperse y clasificación.

### Evidencia antes/después

Comandos, pruebas, métricas y salida anonimizada.

### Seguridad y datos

Secretos, PII, permisos, nuevas dependencias y migraciones.

### Despliegue

Orden, configuración y compatibilidad.

### Rollback

Pasos y condición para activarlo.

### Checklist

- [ ] Ficha y decisiones vinculadas.
- [ ] Release x64 compila.
- [ ] Sin advertencias nuevas.
- [ ] Pruebas afectadas pasan.
- [ ] Secret scan pasa.
- [ ] Dependencias/licencias revisadas.
- [ ] Sin artefactos ni datos sensibles.
- [ ] Observabilidad actualizada.
- [ ] Documentación actualizada.
- [ ] Rollback probado o justificado.

## Revisión

El autor entrega evidencia, no sólo afirma “funciona”. El revisor comprueba:

- que la prueba habría detectado una regresión;
- que el error no se oculta;
- que timeout/cancelación/Dispose son correctos;
- que no hay estado global nuevo;
- que logs no contienen datos sensibles;
- que rutas y HTML se validan por contexto;
- que SQL es compatible y parametrizado;
- que el rollback no depende del mismo componente fallido.

Cambios de reporte requieren aprobador funcional. Cambios de SQL/permisos requieren DBA. Seguridad/licencias requieren propietario correspondiente.

## Merge y despliegue

- Merge sólo con gates verdes y aprobaciones.
- Preferir historial lineal/squash según estándar del equipo.
- Etiquetar artefacto con versión y commit.
- Desplegar primero a preproducción/canary.
- Observar durante ventana acordada.
- Actualizar estado de ficha: Validado o Desplegado.

## Manejo de hallazgos fuera de alcance

Registrar en la sección de riesgos o crear una ficha futura. Sólo ampliar el PR si el hallazgo bloquea seguridad/corrección y no puede aislarse; en ese caso actualizar objetivo, riesgo y revisores antes de continuar.

