# PR-05 — Configuración, composición y caché

Estado: propuesto. Dependencias: PR-02; preferible PR-03.

## Propósito

Eliminar I/O en constructores y estado global, validando una configuración inmutable y reduciendo lecturas repetidas.

## Alcance

- Composition root en el punto de entrada.
- Contratos para conexión, parámetros y opciones.
- Sustitución gradual de AppConfig.ConnectionString estático.
- LaboratoryConstants convertido en snapshot validado.
- Carga de parámetros en bloque y caché con política explícita.
- BusinessP360Exception sin efectos secundarios.

## Implementación

1. Caracterizar valores, defaults y errores actuales.
2. Definir modelos de opciones sin secretos imprimibles.
3. Introducir IParameterSnapshotProvider.
4. Cargar parámetros requeridos en una operación SQL o lote.
5. Validar presencia, tipo/rango y coherencia al arrancar.
6. Inyectar snapshot/servicios en jobs y reportes.
7. retirar construcciones repetidas de LaboratoryConstants;
8. separar creación de error y escritura de log;
9. definir refresh: reinicio, TTL o señal controlada.

## Fuera de alcance

- Cambiar valores/reglas de negocio.
- Nuevo proveedor de secretos definitivo si D-008 sigue abierto.
- Refactor completo de acceso a datos.

## Pruebas y métricas

- Configuración completa/incompleta/mal tipada.
- Refresh concurrente y fallo durante refresh.
- Sin valor sensible en ToString/log.
- Conteo de consultas: objetivo máximo una carga por snapshot, frente a múltiples consultas por instancia.
- Compatibilidad de valores resultantes.

## Criterios de aceptación

- Ningún constructor nuevo realiza I/O.
- Jobs no reciben connection strings/secretos en JobDataMap.
- Cero dependencia nueva del estado global.
- Snapshot inmutable y validado antes del scheduler.
- Reducción de consultas demostrada.
- Existe modo de compatibilidad/reversión durante despliegue.

## Rollback

Conservar temporalmente un adaptador LegacyParameterProvider seleccionable por configuración no sensible. Retirarlo en un PR posterior cuando el nuevo proveedor lleve una ventana estable.

