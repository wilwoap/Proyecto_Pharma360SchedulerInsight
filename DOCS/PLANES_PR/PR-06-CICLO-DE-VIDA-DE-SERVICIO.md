# PR-06 — Ciclo de vida no interactivo

Estado: núcleo validado localmente el 2026-07-22. Dependencia de PR-05 satisfecha; D-006 sólo mantiene pendiente el adaptador y empaquetado de Windows Service.

## Propósito

Permitir ejecución desatendida con arranque validado, cancelación y apagado ordenado.

## Alcance

- Separar RunAsync del Main.
- Eliminar Console.ReadKey, prompts, MessageBox y Environment.Exit en lógica.
- Propagar cancelación.
- Coordinar start/standby/shutdown de Quartz.
- Modo consola no interactivo y frontera apta para un futuro host Windows Service net48.
- Códigos de salida y eventos de ciclo de vida.

## Implementación

1. Se definió `IApplicationLifetime` y un adaptador de consola para `Ctrl+C`/`ProcessExit`.
2. `Main` quedó reducido a composición, ejecución y código de salida.
3. La configuración inválida falla antes de crear o iniciar Quartz.
4. La carga SQL y las operaciones Quartz reciben cancelación.
5. La parada ejecuta `Standby` y `Shutdown(true)` dentro de 1–900 segundos, 30 por defecto.
6. El timeout fuerza `Shutdown(false)` y devuelve código 4.
7. Se conservaron temporalmente ejecución sin argumentos y `--console`; `--help` no carga dependencias.
8. `ServiceBase` y el instalador se difieren hasta cerrar D-006.

## Fuera de alcance

- Migrar a .NET 10/Generic Host.
- Cambiar semántica de jobs.
- Implementar o instalar automáticamente el servicio antes de D-006.
- Definir persistencia o exclusión multiinstancia antes de D-009.

## Casos de prueba

- Inicio, espera y parada en orden.
- Fallo de registro antes del inicio y limpieza forzada.
- Cancelación del llamador y señal idempotente del lifetime.
- Parada ordenada y timeout con fallback sin espera.
- Opciones predeterminadas, valor configurado y rango inválido.
- Ayuda/argumentos inválidos sin configuración ni red.
- Adaptador contra una instancia real de Quartz con `RAMJobStore`.
- Escaneo de cero UI/input en todo el código de producción.

## Criterios de aceptación

- [x] El proceso no requiere escritorio ni sesión interactiva.
- [x] Ningún camino solicita input ni muestra `MessageBox`.
- [x] La parada ordenada está acotada y tiene fallback explícito.
- [x] Quartz se pone en standby y se apaga.
- [x] Los códigos distinguen configuración, dependencia, fallo no controlado y timeout.
- [x] Runbook y configuración actualizados.
- [ ] Adaptador/instalador Windows Service, pendiente de D-006.

## Evidencia de validación

- build Release x64 y 45/45 pruebas aprobadas;
- `--help` devuelve 0 y un argumento desconocido devuelve 2;
- escaneo sin llamadas interactivas;
- diff de `.rpt` vacío;
- detalle operativo en `DOCS/16_CICLO_DE_VIDA_NO_INTERACTIVO.md`.

## Rollback

Conservar el release anterior lado a lado. Detener la candidata, preservar logs/estado de cola y reactivar el mecanismo anterior sin compartir simultáneamente la misma cola. La ejecución sin argumentos permanece disponible durante la transición del lanzador.
