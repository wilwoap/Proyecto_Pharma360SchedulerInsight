# Gates de calidad

## Objetivo

Impedir que el programa modernice la tecnología a costa del comportamiento. Los gates aumentan gradualmente; no se exige al primer PR corregir toda la deuda heredada.

## Gate obligatorio para todo PR

- Alcance y riesgo descritos.
- Relación con una ficha de PLANES_PR.
- Main verde al comenzar.
- Build Release x64.
- Cero errores nuevos y cero advertencias nuevas.
- Pruebas afectadas en verde.
- Revisión de secretos.
- Sin bin, obj, packages, publish, certificados, archivos de usuario ni bak.
- Configuración y documentación actualizadas.
- Plan de rollback concreto.
- Evidencia de revisión de código generado si cambia.

## Gates por fase

### Desde Gate 0

- escaneo del árbol e índice para secretos;
- comprobación manual de archivos de configuración y recursos;
- propietario confirma rotación.

### Desde PR-01

- restore/build en runner Windows limpio y autorizado;
- configuración x64 explícita;
- artefacto versionado y hash;
- comparación de advertencias contra baseline.

### Desde PR-02

- unitarias y caracterización;
- ninguna llamada a producción;
- fixtures anonimizados;
- resultados publicables como artefactos sin PII.

### Desde PR-03

- restauración determinista desde fuentes autorizadas;
- análisis de vulnerabilidades/licencias;
- archivo de bloqueo o mecanismo equivalente;
- SBOM de release.

### Desde PR-07

- correlationId en rutas nuevas;
- log estructurado y redactado;
- métrica de éxito/error/duración;
- health check afectado actualizado.

### Desde PR-10

- pruebas de concurrencia e idempotencia;
- migración SQL expand/contract;
- reintento/cola muerta;
- simulación de caída y recuperación.

### Desde PR-14

- análisis nullable para código nuevo;
- analizadores acordados;
- matriz de runtimes/OS;
- publicación y ejecución del artefacto en entorno limpio.

## Política de advertencias

1. Capturar la lista de advertencias existente en PR-01.
2. Fallar CI si aparece una combinación nueva de código/proyecto/mensaje.
3. Corregir lotes pequeños con prueba.
4. Activar TreatWarningsAsErrors por proyecto cuando su baseline llegue a cero.
5. No suprimir globalmente; cada supresión necesita justificación local.

## Dependencias

Cada PR que modifique paquetes incluye:

- motivo;
- versión anterior/nueva;
- fuente y propietario;
- notas de compatibilidad;
- vulnerabilidades conocidas;
- impacto de licencia/redistribución;
- prueba de carga y camino funcional;
- rollback.

Las dependencias propietarias se prueban en un runner con licencia. No se copian paquetes o claves de licencia a un repositorio público.

## Código generado

- No aplicar refactor masivo ni formateo a Designer.cs, Settings.Designer.cs o datasets.
- Cambiar la fuente generadora y regenerar cuando sea posible.
- Separar cambios generados de lógica manuscrita en commits reconocibles.
- Revisar que el generador no reintroduzca secretos.

## Seguridad

Bloquean merge:

- secreto verificable;
- vulnerabilidad crítica/alta sin excepción vigente;
- datos de producción en fixture;
- ruta de traversal;
- HTML/cabecera no codificados en cambios nuevos;
- logging de credenciales.

Una excepción requiere propietario, razón, compensación y fecha de expiración.

## Rendimiento

Una optimización sólo se acepta con:

- escenario reproducible;
- volumen y hardware registrados;
- p50/p95/p99 o distribución apropiada;
- uso de memoria/handles cuando haya renderizado;
- comparación antes/después;
- confirmación de que no cambió la salida.

## Revisión manual obligatoria

Aunque CI pase, se requiere revisión especializada para:

- cambios de SQL y permisos;
- salida visual de reporte;
- semántica de reintento/misfire;
- secretos/licenciamiento;
- scripts de instalación/rollback.

## Definition of Done de un PR

- Código, pruebas y documentación alineados.
- Criterios de aceptación demostrados.
- Observabilidad suficiente para saber si funciona.
- No quedan pasos manuales ocultos.
- Despliegue y reversión entendidos.
- Decisiones nuevas registradas.
- Responsable de observación posterior identificado.

