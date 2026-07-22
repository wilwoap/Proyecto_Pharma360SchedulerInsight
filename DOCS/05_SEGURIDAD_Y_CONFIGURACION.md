# Seguridad y configuración

## Objetivo

Eliminar secretos del código y reducir las rutas por las que datos, archivos o mensajes pueden producir acceso no autorizado. Este plan comienza antes del primer commit.

## Gate 0: respuesta inmediata

### Valores afectados

La revisión encontró:

- una clave de Google Maps en SchedulerP360Insight/App.config;
- una cadena SQL con usuario y contraseña en SchedulerP360Insight/Properties/Settings.settings;
- el mismo valor SQL materializado por el generador en Properties/Settings.Designer.cs.

Los valores no se reproducen en esta documentación.

### Secuencia obligatoria

1. Identificar al propietario de cada credencial y su ámbito.
2. Revocar o rotar la cuenta SQL y la clave de API. No basta con borrarlas del archivo.
3. Verificar si se reutilizan en otra aplicación antes de rotar de forma coordinada.
4. Sustituir los valores fuente por marcadores no sensibles y regenerar Settings.Designer.cs.
5. Limpiar artefactos de compilación/publicación que hayan embebido los valores.
6. Escanear archivos rastreables, ignorados relevantes, índice Git y artefactos que vayan a publicarse.
7. comprobar que el binario/config de release no contiene los valores antiguos;
8. crear el primer commit sólo después de la aprobación del propietario de seguridad.

Como no existe historial ni remoto, no se requiere una reescritura de historia. Si entretanto se publica un commit, se debe tratar como incidente y aplicar limpieza de historia coordinada, además de la rotación.

## Fuente de secretos objetivo

Orden de preferencia:

1. almacén de secretos administrado por la organización con identidad de servicio;
2. secreto protegido por Windows/DPAPI y ACL de la cuenta de servicio;
3. configuración cifrada de .NET Framework como puente temporal;
4. variable de entorno protegida como transición, documentando quién puede leerla.

Nunca:

- secreto en App.config, Settings.settings, código, recurso, JobDataMap o argumentos de proceso;
- secreto en logs, mensajes de error, capturas o documentación;
- valor real en fixtures de prueba;
- creación interactiva de una variable de máquina desde la aplicación.

La aplicación debe recibir un descriptor/configuración, validar su presencia y fallar sin imprimir el valor.

## Clave de mapas

La clave actual llega a una URL de imagen incluida en un correo. Los clientes o proxies de correo pueden solicitar esa URL, por lo que el receptor puede observarla y restricciones simples por IP/referrer pueden ser ineficaces.

Diseño preferido:

- generar u obtener la imagen en el servidor;
- insertar la imagen como contenido adjunto o servirla mediante una URL temporal controlada;
- mantener la llamada a Google y su clave fuera del HTML enviado;
- limitar APIs, cuota, presupuesto y alertas;
- registrar consumo, no la URL con credencial.

Hasta implementar este diseño, la funcionalidad debe usar una clave separada, de mínimo ámbito y con alertas de uso.

## SQL

- Una identidad por aplicación y entorno.
- Sin permisos de administración, creación de objetos ni lectura global.
- Conceder ejecución/lectura/escritura sólo sobre objetos inventariados.
- Cifrado de transporte validado; no aceptar certificados sin validación.
- Parámetros con tipo/tamaño explícito; evitar AddWithValue.
- Timeouts finitos por clase de operación.
- No incluir connection strings en excepción o log.
- Revisar procedimientos dinámicos y privilegios efectivos con el DBA.

La opción actual Encrypt=False se debe tratar como riesgo hasta confirmar topología y controles de red. El cambio requiere certificado SQL válido y prueba en cada entorno.

## HTML y correo

- Codificar como HTML todo valor procedente de SQL, configuración o usuario.
- Validar por separado atributos, URL y texto; una codificación no sirve para todos los contextos.
- Mantener una lista permitida de esquemas URL y dominios cuando corresponda.
- Validar direcciones To/CC/BCC y límites de destinatarios.
- Rechazar saltos de línea en cabeceras.
- Definir límite de cuerpo y adjunto antes de construir el mensaje.
- No registrar cuerpos completos ni listas de destinatarios salvo necesidad auditada.
- Aplicar TLS con validación de certificado; no degradar silenciosamente.

## Archivos y reportes

- Definir una raíz de salida fija por entorno.
- Obtener ruta absoluta y comprobar que permanece bajo esa raíz.
- Sanear nombre, extensión y longitud.
- Crear archivos con nombre no predecible o identificador de operación.
- Evitar sobrescritura accidental.
- Aplicar permisos de mínimo privilegio.
- Borrar temporales en finally y ejecutar una reconciliación periódica.
- Definir retención y eliminación segura con el propietario de datos.

## Cadena de suministro

Acciones:

- congelar y documentar la dependencia Crystal actual hasta PR-13; allí sustituir la procedencia no oficial por medios SAP o un paquete interno firmado y trazable, sin modificar los .rpt;
- verificar derecho de redistribución de SAP y DevExpress;
- eliminar paquetes sin uso confirmado;
- actualizar o retirar log4net 2.0.12;
- generar inventario SBOM por release;
- fijar versiones y hashes/fuentes autorizadas;
- escanear vulnerabilidades en cada PR y diariamente;
- documentar excepciones con propietario, fecha de expiración y compensación.

No se debe actualizar todo el grafo en un único PR. Cada lote necesita prueba de carga de ensamblados, renderizado y licencia.

## Logging y privacidad

Campos permitidos sugeridos:

- timestamp UTC;
- nivel;
- serviceVersion;
- environment;
- correlationId;
- scheduleId, reportId y notificationId opacados si contienen significado sensible;
- operación;
- duración;
- resultado y código de error.

Campos prohibidos:

- credenciales y connection strings;
- claves o tokens;
- contenido completo de consultas con parámetros;
- cuerpo HTML;
- datos personales no necesarios;
- rutas con información sensible.

La tabla SQL de log no debe ser el único destino. Si SQL falla, el sistema necesita un canal local/central independiente.

## Criterios de aceptación de seguridad

- Escaneo sin secretos verificables antes del primer commit.
- Credenciales antiguas revocadas o rotadas con evidencia.
- Ninguna credencial en binarios, archivos de configuración ni logs de release.
- Cero vulnerabilidades críticas/altas sin excepción aprobada.
- Dependencias propietarias trazables a proveedor/paquete interno autorizado.
- Pruebas de traversal, HTML injection, cabeceras de correo y redacción.
- Cuenta de servicio y cuenta SQL con permisos documentados.
