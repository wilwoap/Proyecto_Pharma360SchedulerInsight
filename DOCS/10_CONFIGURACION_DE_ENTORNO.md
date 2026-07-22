# Configuración de entorno

## Regla

El repositorio, el binario y su archivo .config no contienen credenciales. Los valores reales se suministran al proceso mediante el mecanismo de secretos del ambiente.

## Variables

| Variable | Requerida | Uso |
|---|---|---|
| P360_CONNECTION_PRINCIPAL | Sí | Conexión principal de SQL Server |
| P360_GOOGLE_MAPS_API_KEY | No | Imagen estática del mapa en correos |
| P360_PARAMETER_PROVIDER_MODE | No | `batch` por defecto; `legacy` sólo para rollback temporal |

Si no existe P360_GOOGLE_MAPS_API_KEY, el correo incluye únicamente un enlace a la ubicación. La aplicación nunca crea variables, imprime sus valores ni incorpora una conexión predeterminada.

`P360_PARAMETER_PROVIDER_MODE` no es sensible. El valor normal es `batch`, incluso cuando la variable no existe. `legacy` conserva temporalmente la lectura histórica de parámetros, cacheada al arrancar; cualquier otro valor detiene el proceso antes de iniciar Quartz.

## Desarrollo local

Obtener los valores desde el almacén autorizado y configurarlos sólo para la sesión:

    $env:P360_CONNECTION_PRINCIPAL = "<valor obtenido del almacén autorizado>"
    $env:P360_GOOGLE_MAPS_API_KEY = "<valor obtenido del almacén autorizado>"
    $env:P360_PARAMETER_PROVIDER_MODE = "batch"

No guardar esos comandos con valores reales en scripts, historial de terminal, perfiles, capturas o documentación.

## Servicio

- Configurar las variables para la identidad que ejecuta el servicio mediante la plataforma de despliegue.
- Reiniciar el proceso después de una rotación.
- Reiniciar también después de cambiar parámetros en `T_PARAMETROS`: PR-05 usa un snapshot por vida del proceso.
- Verificar sólo presencia y conectividad; nunca mostrar el valor.
- Restringir lectura/configuración a operación y a la identidad de servicio.
- Mantener valores distintos por ambiente.

La variable de entorno es el puente inicial. D-008 decidirá si el estado final usa un almacén administrado o DPAPI/configuración protegida.

## Rotación pendiente de Gate 0

- Revocar o rotar la credencial SQL que estuvo embebida.
- Revocar o rotar la clave de mapas que estuvo embebida.
- Revisar consumo y accesos históricos.
- Limitar la nueva clave de mapas a APIs/cuotas necesarias.
- Confirmar que ninguna otra aplicación depende de esos valores antes del corte.

No se publicará el repositorio mientras esta lista no tenga propietario y evidencia.

## Diagnóstico seguro

Ante configuración ausente, comprobar:

1. identidad efectiva del proceso;
2. presencia de la variable sin imprimir su contenido;
3. reinicio posterior a la configuración;
4. permisos/red/TLS;
5. conectividad desde un cliente autorizado.

Nunca resolver el incidente agregando el secreto a App.config o Settings.settings.
