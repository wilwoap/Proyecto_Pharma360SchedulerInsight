# Inventario y línea base

Fecha de observación: 2026-07-21.

## Estado del repositorio

| Elemento | Estado observado |
|---|---|
| Rama | main |
| Historial | Sin commits |
| Remoto | No configurado |
| Índice | Aproximadamente 90 altas preparadas, correspondientes a la importación inicial |
| Solución | SchedulerP360Insight.sln |
| Proyecto | SchedulerP360Insight/SchedulerP360Insight.csproj |
| Tipo | Ejecutable C# clásico, no SDK |
| Framework | .NET Framework 4.8 |
| Gestión de paquetes | packages.config y referencias directas |

Los archivos bin, obj, packages, publish, archivos de usuario, copias bak y un certificado PFX local están ignorados. No deben eliminarse como parte de la valoración; sí deben inventariarse y gestionarse fuera del código fuente.

## Componentes funcionales observados

    SchedulerP360Insight.cs
        carga configuración y trabajos desde SQL
        crea triggers Cron en Quartz
        mantiene vivo el proceso de consola

    Jobs/
        P360HtmlReportJob
        P360DevExpressReportsReportJob
        P360CrystalReportsReportJob

    UtilitariosyClases/Utilitarios.cs
        correo, plantillas HTML, Crystal, archivos y cola

    Modulos/ModuleCapaAccesoDatos.cs
        ADO.NET, Dapper y registro en SQL

    Modulos/LaboratoryConstants.cs
        parámetros operativos y SMTP desde SQL

    P360Reports/
        reportes Crystal y DevExpress

## Tamaño aproximado

| Métrica | Valor |
|---|---:|
| Archivos C# | 47 |
| Líneas C# | 34.552 |
| Archivos generados/diseñador | 18 |
| Líneas generadas | 29.750 |
| Archivos manuscritos | 29 |
| Líneas manuscritas | 4.802 |
| Proporción generada | 86,1 % |
| Reportes Crystal .rpt | 5 |
| Reportes DevExpress principales | 6 |
| Proyectos de pruebas | 0 |

La concentración de código manuscrito está principalmente en Utilitarios.cs, ModuleCapaAccesoDatos.cs, el punto de entrada y los tres trabajos Quartz.

## Dependencias relevantes

| Dependencia | Versión observada | Uso/riesgo |
|---|---:|---|
| SAP Crystal Reports runtime/assemblies | 13.0.4000; paquetes 13.0.4003 | Sólo .NET Framework; referencias locales y paquetes de tercero |
| DevExpress | 25.2.8 | Reportes y WinForms; compatible con runtimes modernos según el proveedor |
| Quartz | 3.6.2 | Planificación; muy atrasado respecto a la rama estable actual |
| Dapper | 2.1.35 | Acceso a datos |
| log4net | 2.0.12 | Sin uso manuscrito encontrado; asociado a dependencias Crystal y marcado vulnerable |
| Newtonsoft.Json | 13.0.1 | Sin uso manuscrito encontrado |
| Microsoft.Extensions.* | 2.1.1 | Sin uso manuscrito encontrado; conflicto con referencias 8.0 requeridas por DevExpress |

Antes de retirar una dependencia aparentemente no usada se debe inspeccionar el grafo restaurado, archivos generados, reflexión y requisitos de licenciamiento.

## Resultado de compilación

Comando base:

    msbuild SchedulerP360Insight.sln /t:Rebuild /p:Configuration=Release /p:Platform=x64 /m /v:minimal

Resultado:

- cero errores;
- advertencias de conflicto para Microsoft.Extensions.Configuration.Abstractions y System.Memory;
- advertencias de código no alcanzable, campos no usados y variables de excepción no usadas;
- artefacto ejecutable generado localmente.

Comando comparativo:

    msbuild SchedulerP360Insight.sln /t:Rebuild /p:Configuration=Release /p:Platform=AnyCPU /m /v:minimal

Resultado:

- cero errores;
- 36 advertencias;
- varias advertencias MSB3270/MSB3187 por mezclar AnyCPU con ensamblados AMD64 de Crystal.

Conclusión: x64 debe ser la única plataforma soportada hasta que el conjunto de dependencias cambie. Una compilación exitosa en la estación actual no prueba que otra máquina pueda restaurar y compilar, porque existen rutas a instalaciones locales de SAP y DevExpress.

## Configuración y datos externos

Objetos SQL identificados en el código:

- P360Insight.V_SCHEDULED_REPORTS;
- P360Insight.V_SCHEDULED_REPORTS_COLA_NOTIFICACIONES;
- P360Insight.SP_RegistrarInformacionColaNotificacionesEventosAsincronos;
- P360Insight.GetDataContactosNotificaciones;
- P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES;
- DBO.T_LOG_CONECCIONYACCIONES;
- DBO.T_PARAMETROS;
- prescr.T_FICHERO;
- varias vistas/tablas de agenda, movilidad y datasets de reportes.

El origen principal de conexión en tiempo de ejecución es la variable de entorno de máquina o usuario P360_CONNECTION_PRINCIPAL. También hay valores de conexión generados en Properties/Settings.Designer.cs a partir de Properties/Settings.settings. Estos últimos no deben contener credenciales.

## Comandos de reproducción

Desde la raíz del repositorio:

    git status --short
    git branch --show-current
    git remote -v
    rg --files
    msbuild SchedulerP360Insight.sln /t:Rebuild /p:Configuration=Release /p:Platform=x64 /m /v:minimal

El análisis de vulnerabilidades mediante dotnet list package no es aplicable todavía a packages.config. Se incorporará tras migrar a PackageReference; hasta entonces se debe usar una herramienta que entienda packages.config y validar manualmente los paquetes críticos.

## Límites de esta línea base

- No se inició el ejecutable.
- No se conectó a SQL ni SMTP.
- No se renderizaron reportes.
- No se validaron permisos del usuario de servicio.
- No se inspeccionaron definiciones internas de vistas y procedimientos almacenados.
- No se comprobó el entorno de producción.
- No se hizo prueba de carga ni medición de memoria.

Estos límites deben resolverse mediante PR-02 y el inventario operativo previo al primer despliegue.

