# PR-06 — Ciclo de vida no interactivo

Estado: propuesto. Dependencia: PR-05 y decisión D-006.

## Propósito

Permitir ejecución desatendida con arranque validado, cancelación y apagado ordenado.

## Alcance

- Separar RunAsync del Main.
- Eliminar Console.ReadKey, prompts, MessageBox y Environment.Exit en lógica.
- Propagar cancelación.
- Coordinar start/standby/shutdown de Quartz.
- Modo consola diagnóstico y host Windows Service net48 si se aprueba.
- Códigos de salida y eventos de ciclo de vida.

## Implementación

1. Definir IApplicationLifetime interno.
2. Hacer Main mínimo: composición, ejecución y código de salida.
3. Fallar antes de iniciar si configuración es inválida.
4. Registrar handler de parada del host.
5. detener nuevos triggers y esperar jobs con timeout;
6. liberar scheduler/renderers/recursos;
7. crear adaptador ServiceBase o mecanismo aprobado;
8. mantener modo consola sólo con bandera explícita.

## Fuera de alcance

- Migrar a .NET 10/Generic Host.
- Cambiar semántica de jobs.
- Instalar automáticamente el servicio.

## Casos de prueba

- Inicio correcto.
- Configuración ausente.
- Ctrl+C/parada de servicio sin jobs.
- Parada con job corto/largo/no cancelable.
- Timeout de apagado.
- Segunda instancia si está prohibida.
- Cero UI en cuenta no interactiva.

## Criterios de aceptación

- Funciona sin escritorio/sesión.
- Ningún camino solicita input.
- Parada termina dentro del presupuesto o deja estado recuperable.
- Recursos liberados y Quartz apagado.
- Código de salida diferencia configuración, dependencia y fallo no controlado.
- Runbook actualizado.

## Rollback

Desplegar lado a lado y conservar el lanzador anterior durante canary. Si falla el host de servicio, detenerlo y reactivar el mecanismo anterior sin compartir simultáneamente la misma cola.

