# PR-02 — Pruebas de caracterización

Estado: validado localmente el 2026-07-21. Dependencia PR-01 satisfecha.

## Propósito

Capturar el comportamiento crítico actual antes de extraer/refactorizar componentes.

## Alcance

- Proyecto de pruebas compatible con net48.
- Seams mínimos para reloj, datos, SMTP, archivos y renderizado.
- Fixtures sintéticos/anonimizados.
- SMTP sink.
- Caracterización de arranque, tipos de job, plantillas y rutas.
- Golden masters iniciales para al menos un reporte de cada tecnología.

## Implementación

1. Seleccionar framework de pruebas compatible con runner/licencia.
2. Extraer sólo los seams necesarios, sin rediseño amplio.
3. Crear builders de datos de programación/notificación.
4. Sustituir reloj, identificadores y filesystem.
5. Capturar comportamiento válido y errores actuales.
6. Preparar integración SQL desechable y SMTP sink.
7. Definir normalización de PDF.
8. Publicar resultados anonimizados en CI.

## Fuera de alcance

- Corregir todos los comportamientos capturados.
- Cobertura porcentual global.
- Concurrencia/idempotencia final.
- Conversión de reportes.

## Casos obligatorios

- Tres tipos de report_type.
- Cron válido/inválido.
- Configuración ausente.
- Report UID conocido/desconocido.
- PDF no vacío.
- HTML con caracteres especiales.
- SMTP success/failure sin envío real.
- Ruta válida/traversal.
- SQL timeout/error simulado.

## Criterios de aceptación

- Las pruebas corren sin producción ni Internet.
- Un fallo intencional en cada ruta crítica es detectado.
- Fixtures no contienen PII/secretos.
- Golden master tiene aprobador y método reproducible.
- Cero cambio visible no aprobado.
- El catálogo de 06_ESTRATEGIA_DE_PRUEBAS.md indica cobertura inicial y pendientes.

## Rollback

Las pruebas y seams deben ser aditivos. Si un seam cambia comportamiento, revertirlo y elegir una frontera menor; no conservar una abstracción sin prueba.

## Evidencia

- MSTest.Sdk 4.3.2, net48 y x64.
- 22 pruebas descubiertas y ejecutadas; 22 correctas, cero omitidas y cero fallidas.
- Tiempo local aproximado del arnés: un segundo; timeout global: 60 segundos.
- TRX generado bajo artifacts/test-results y excluido de Git.
- Cinco binarios Crystal protegidos por SHA-256 sin carga ni modificación.
- Un reporte DevExpress representativo protegido por hash de código, diseñador y recursos.
- Cero acceso a SQL, SMTP externo o Internet durante las pruebas.
- Dos deudas capturadas: HTML sin codificar y clave de plantilla inexistente que retorna null.

La generación de PDFs reales queda pendiente de un fixture SQL anonimizado y de aprobación visual; no se usarán datos productivos para cerrar ese punto.
