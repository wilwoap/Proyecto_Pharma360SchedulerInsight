using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualBasic;
using System.Data.OleDb;
using System.Data.SqlClient;
using System.Data;
using System.Configuration;
using DevExpress.CodeParser;
using Dapper;
using SchedulerP360Insight.UtilitariosyClases;

namespace SchedulerP360Insight.Modulos
{
    public class ModuleCapaAccesoDatos
    {
        private readonly int commandTimeoutP360 = 0;
        private readonly Func<string> connectionStringProvider;

        public ModuleCapaAccesoDatos()
            : this(() => AppConfig.ConnectionString)
        {
        }

        public ModuleCapaAccesoDatos(string connectionString)
            : this(() => connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException(
                    "La cadena de conexión es obligatoria.",
                    nameof(connectionString));
            }
        }

        private ModuleCapaAccesoDatos(Func<string> connectionStringProvider)
        {
            this.connectionStringProvider = connectionStringProvider ??
                throw new ArgumentNullException(nameof(connectionStringProvider));
        }

        private string ConnectionString
        {
            get
            {
                string value = connectionStringProvider();
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new InvalidOperationException(
                        "La cadena de conexión no está configurada.");
                }

                return value;
            }
        }

        /// <summary>
        /// Registra la información necesaria en la cola de envío de notificaciones para eventos asíncronos
        /// que aún no están en la cola. Este proceso se lleva a cabo a través del procedimiento almacenado
        /// 'P360Insight.SP_RegistrarInformacionColaNotificacionesEventosAsincronos' en la base de datos 'P360Insight'.
        /// </summary>
        /// <remarks>
        /// Los eventos asíncronos a los que se hace referencia pueden incluir, pero no se limitan a:
        /// <list type="bullet">
        ///     <item>
        ///         <description>report_id = 1: Relacionado con el proceso de generación de cola para un tipo específico de reporte.</description>
        ///     </item>
        /// </list>
        /// Cada 'report_id' representa un proceso de generación de cola distinto y tiene asociada una lógica específica para
        /// determinar cómo se llenará la cola de notificaciones. Esta lógica debe estar definida y ejecutarse dentro del
        /// procedimiento almacenado mencionado, permitiendo llenar las colas de notificaciones basándose en criterios específicos
        /// para cada tipo de evento asíncrono.
        /// <para>
        /// Nota especial: Dado que estos eventos asíncronos llenan colas de receptores de correo según distintas lógicas,
        /// es importante que dicha lógica de cómo se llenarán estas colas sea ejecutada correctamente en el procedimiento almacenado.
        /// Esto asegura que las notificaciones sean enviadas a los destinatarios correctos basándose en el tipo de evento asíncrono y
        /// las reglas de negocio asociadas.
        /// </para>
        /// <para>
        /// Adicionalmente, dentro de la lógica del procedimiento almacenado se determina si se insertará o no la cola dependiendo del ID del reporte,
        /// lo que permite una gestión dinámica y flexible de las notificaciones basadas en las necesidades específicas de cada evento.
        /// </para>
        /// </remarks>
        /// <param name="reportId">El identificador del reporte asociado al evento asíncrono.</param>
        /// <param name="usuario">El nombre de usuario que realiza el registro, utilizado para el seguimiento y la auditoría.</param>
        public void RegistrarInformacionColaNotificacionesEventosAsincronos(string reportUID, string usuario)
        {
            try
            {
                using (SqlConnection cnSQL = new SqlConnection(ConnectionString))
                using (SqlCommand cmSQL = new SqlCommand("P360Insight.SP_RegistrarInformacionColaNotificacionesEventosAsincronos", cnSQL))
                {
                    cnSQL.Open();
                    cmSQL.CommandTimeout = commandTimeoutP360;
                    cmSQL.CommandType = CommandType.StoredProcedure;
                    // Agregar los parámetros necesarios para el procedimiento almacenado
                    cmSQL.Parameters.Add("@p_reportUID", SqlDbType.VarChar).Value = reportUID;
                    cmSQL.Parameters.Add("@p_usuario", SqlDbType.VarChar).Value = usuario;
                    // Ejecutar el procedimiento almacenado
                    cmSQL.ExecuteNonQuery();
                }
            }
            catch (SqlException exSql)
            {
                // El SP retorna 50000 para cuando el id del reporte no genera cola, genera una excepcion pero no se debe hacer nada en el scheduler
                // mas bien si es que es DISTINTO a 50000 significa que algo extraño puede estar ucediendo y se imprime en consola
                if (exSql.Number != 50000) 
                {
                    WriteSafeFailure(
                        "RegistrarInformacionColaNotificaciones",
                        exSql);
                }
                // throw; No hacer el throw para manejarlo aqui
            }
        }

        public int getCodigoFicheroVigente()
        {
            System.Diagnostics.StackFrame stackframe = new System.Diagnostics.StackFrame(1);
            string nameFuenteCaller = stackframe.GetMethod().Name;
            int codigoFichero = 0;
            try
            {
                string query = "SELECT cod_fichero FROM prescr.T_FICHERO WHERE vigente = 1";
                using (SqlConnection cnSQL = new SqlConnection(ConnectionString))
                using (SqlCommand cmSQL = new SqlCommand(query, cnSQL))
                {
                    cnSQL.Open();
                    cmSQL.CommandTimeout = commandTimeoutP360;
                    using (SqlDataReader drSQL = cmSQL.ExecuteReader())
                    {
                        if (!drSQL.HasRows)
                            throw new Exception("Ha ocurrido un error al consultar el código del fichero vigente.");
                        while (drSQL.Read())
                            codigoFichero = drSQL.GetInt32(drSQL.GetOrdinal("cod_fichero"));
                    }
                }
                return codigoFichero;
            }
            catch (SqlException exSql)
            {
                WriteSafeFailure(nameFuenteCaller, exSql);
                return codigoFichero;
            }
            catch (Exception ex)
            {
                codigoFichero = 0;
                throw new Exception("Ha ocurrido un error al consultar el código del fichero vigente.", ex);
                return codigoFichero;
            }
        }


        public string getValorParametroSistemaDB(string p_nombreParametro)
        {
            System.Diagnostics.StackFrame stackframe = new System.Diagnostics.StackFrame(1);
            string nameFuenteCaller = stackframe.GetMethod().Name;
            string v_valorParametro = string.Empty;
            try
            {
                string query = "SELECT VALOR as VALOR FROM T_PARAMETROS where parametro=@nombreParametro";
                using (SqlConnection cnSQL = new SqlConnection(ConnectionString))
                using (SqlCommand cmSQL = new SqlCommand(query, cnSQL))
                {
                    cnSQL.Open();
                    cmSQL.CommandTimeout = commandTimeoutP360;
                    cmSQL.Parameters.AddWithValue("@nombreParametro", p_nombreParametro);
                    using (SqlDataReader drSQL = cmSQL.ExecuteReader())
                    {
                        if (!drSQL.HasRows)
                            throw new Exception("Ha ocurrido un error en la consulta del parámetro: " + p_nombreParametro + " desde la base de datos. ");
                        while (drSQL.Read())
                            v_valorParametro = drSQL.GetString(drSQL.GetOrdinal("VALOR"));
                    }
                }
                return v_valorParametro;
            }
            catch (SqlException exSql)
            {
                WriteSafeFailure(nameFuenteCaller, exSql);
                return v_valorParametro;
            }
            catch (Exception ex)
            {
                v_valorParametro = string.Empty;
                throw new Exception("Ha ocurrido un error en la consulta del parámetro: " + p_nombreParametro + " desde la base de datos. ", ex);
                return v_valorParametro;
            }
        }
        public static int GetCodPedidoPorCodUnicoDespachoCUD(string p_codigoUnicoDespacho_CUD)
        {
            string query = "SELECT DISTINCT cod_pedido FROM mobile.T_DESPACHOSPEDIDO_PED0004 WHERE CUD = @p_codigoUnicoDespacho_CUD";
            try
            {
                using (SqlConnection cnSQL = new SqlConnection(AppConfig.ConnectionString))
                using (SqlCommand cmSQL = new SqlCommand(query, cnSQL))
                {
                    cnSQL.Open();
                    cmSQL.CommandTimeout = 300; // woap ajustar Asegúrate de que commandTimeoutP360 esté definido correctamente
                    cmSQL.Parameters.Add("@p_codigoUnicoDespacho_CUD", SqlDbType.VarChar).Value = p_codigoUnicoDespacho_CUD;

                    using (SqlDataReader drSQL = cmSQL.ExecuteReader())
                    {
                        if (!drSQL.HasRows)
                            throw new Exception($"No se encontró un código unico de despacho (CUD) con el código: {p_codigoUnicoDespacho_CUD}.");

                        drSQL.Read(); // Lee el primer registro, asumiendo que solo habrá un valor de cod_pedido para un cod_despacho
                        return drSQL.GetInt32(drSQL.GetOrdinal("cod_pedido"));
                    }
                }
            }
            catch (SqlException exSql)
            {
                WriteSafeFailure("GetCodPedidoPorCud", exSql);
                throw; // Considera si deseas propagar la excepción o manejarla de manera diferente
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al consultar el código de pedido para el código de despacho: {p_codigoUnicoDespacho_CUD}.", ex);
            }
        }

        public bool actualizaEstadoColaNotificacionaEnviado(int ColaNotificacionId)
        {
            string query = @"
            UPDATE P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES
            SET
            enviado = 1,
            intentos_envio  =   intentos_envio  +   1,
            fecha_envio = GETDATE()
            WHERE cola_notificacion_id = @ColaNotificacionId";
            try
            {
                using (SqlConnection cnSQL = new SqlConnection(ConnectionString))
                {
                    using (SqlCommand cmSQL = new SqlCommand(query, cnSQL))
                    {
                        cmSQL.Parameters.AddWithValue("@ColaNotificacionId", ColaNotificacionId);
                        cnSQL.Open();
                        int filasAfectadas = cmSQL.ExecuteNonQuery();
                        // Si filasAfectadas es mayor que 0, entonces la actualización fue exitosa.
                        return filasAfectadas > 0;
                    }
                }
            }
            catch (SqlException exSql)
            {
                WriteSafeFailure("ActualizarEstadoNotificacion", exSql);
                return false;
            }
            catch (Exception ex)
            {
                WriteSafeFailure("ActualizarEstadoNotificacion", ex);
                return false;
            }
        }
        public void RegistraLogConeccionyAccion(string v_usuario, string accion)
        {
            string source = new System.Diagnostics.StackFrame(1)
                .GetMethod()
                .Name;
            TryRegistraLogCore(v_usuario, accion, source);
        }

        public bool TryRegistraLogConeccionyAccion(
            string v_usuario,
            string accion)
        {
            string source = new System.Diagnostics.StackFrame(1)
                .GetMethod()
                .Name;
            return TryRegistraLogCore(v_usuario, accion, source);
        }

        private bool TryRegistraLogCore(
            string username,
            string action,
            string source)
        {
            try
            {
                using (SqlConnection cnSQL = new SqlConnection(ConnectionString))
                {
                    cnSQL.Open();
                    using (SqlCommand cmSQL = new SqlCommand("INSERT INTO DBO.T_LOG_CONECCIONYACCIONES (USUARIO, IP, ACCION, FUENTE, ORIGEN) VALUES (@col_usuario, @col_ip, @col_accion, @col_fuente, @col_origen)", cnSQL))
                    {
                        cmSQL.Parameters.AddWithValue(
                            "@col_usuario",
                            username ?? string.Empty);
                        cmSQL.Parameters.AddWithValue("@col_ip", GetIPAddress());
                        cmSQL.Parameters.AddWithValue(
                            "@col_accion",
                            (action ?? string.Empty).Replace("'", " "));
                        cmSQL.Parameters.AddWithValue("@col_fuente", "schedulerP360");
                        cmSQL.Parameters.AddWithValue("@col_origen", source);
                        cmSQL.ExecuteNonQuery();
                    }
                }

                return true;
            }
            catch (SqlException exSql)
            {
                WriteSafeFailure(source, exSql);
                return false;
            }
            catch (Exception ex)
            {
                WriteSafeFailure(source, ex);
                return false;
            }
        }
        public string GetIPAddress()
        {
            try
            {
                System.Net.IPHostEntry h = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
                return h.AddressList.GetValue(0).ToString() + "(" + System.Net.Dns.GetHostName() + ")";
            }
            catch (Exception ex)
            {
                WriteSafeFailure("GetIPAddress", ex);
                return null;
            }
        }
        public static DatosVacacionAusencia GetDatosVacacionAusenciaById(int idVacacionAusencia)
        {
            System.Diagnostics.StackFrame stackframe = new System.Diagnostics.StackFrame(1);
            string nameFuenteCaller = stackframe.GetMethod().Name;
            DatosVacacionAusencia datos = null;

            string sql = @"
        SELECT 
            id_vacacion_ausencia AS IdVacacionAusencia, 
            cod_colab AS CodColab, 
            colaborador AS Colaborador, 
            cod_visita AS CodVisita, 
            fecha_inicio_vacacion_ausencia AS FechaInicioVacacionAusencia, 
            fecha_fin_vacacion_ausencia AS FechaFinVacacionAusencia, 
            tipo_vacacion_ausencia AS TipoVacacionAusencia, 
            motivo_vacacion_ausencia AS MotivoVacacionAusencia, 
            estado_vacacion_ausencia AS EstadoVacacionAusencia, 
            comentario_estado_vacacion_ausencia AS ComentarioEstadoVacacionAusencia
        FROM calendarsP360.V_DIAS_VACACIONES_AUSENCIAS
        WHERE id_vacacion_ausencia = @Id";

            try
            {
                using (var cnSQL = new SqlConnection(AppConfig.ConnectionString))
                {
                    cnSQL.Open();
                    datos = cnSQL.QueryFirstOrDefault<DatosVacacionAusencia>(sql, new { Id = idVacacionAusencia });
                }
            }
            catch (SqlException exSql)
            {
                WriteSafeFailure(nameFuenteCaller, exSql);
            }
            catch (Exception ex)
            {
                WriteSafeFailure(nameFuenteCaller, ex);
            }

            return datos;
        }

        public static DatosVisita GetDatosVisitaByCodVisitaDB(int codigoVisita)
        {
            System.Diagnostics.StackFrame stackframe = new System.Diagnostics.StackFrame(1);
            string nameFuenteCaller = stackframe.GetMethod().Name;
            DatosVisita datosVisita = null;
            try
            {
                string sql = @"
                SELECT 
                    cod_visita AS CodVisita, 
                    colaborador AS Colaborador, 
                    supervisor AS Supervisor,
                    tipo_visitado AS TipoVisitado, 
                    categoria_especialidad_visitado AS CategoriaEspecialidad, 
                    nombre_visitado AS NombreVisitado, 
                    nombre_contacto AS NombreContacto,
                    ciudad AS Ciudad, 
                    direccion AS Direccion, 
                    fecha_visita AS FechaVisita, 
                    fecha_proxima_visita AS FechaProximaVisita, 
                    minutos_duracion_visita AS MinutosDuracionVisita,
                    observaciones AS Observaciones, 
                    objetivo_visita_descripcion AS ObjetivoVisitaDescripcion,
                    cod_pedido AS CodPedido,
                    registraPedido AS RegistraPedido,
                    registraCapacitaciones AS RegistraCapacitaciones,
                    registraCaras AS RegistraCaras,
                    registraExhibiciones AS RegistraExhibiciones,
                    registraStocks AS RegistraStocks,
                    registraMuestras AS RegistraMuestras,
                    AccionesEfectuadas=
                      'Proceso de visita ' +
                      CASE WHEN registraPedido = 1 THEN '| Toma de pedido' ELSE '' END +
                      CASE WHEN registraCapacitaciones = 1 THEN '| Registro de capacitaciones ' ELSE '' END +
                      CASE WHEN registraCaras = 1 THEN '| Registro de caras ' ELSE '' END +
                      CASE WHEN registraExhibiciones = 1 THEN '| Registro de exhibiciones ' ELSE '' END +
                      CASE WHEN registraStocks = 1 THEN '| Levantamiento de stocks  ' ELSE '' END +
                      CASE WHEN registraMuestras = 1 THEN '| Entrega de muestras' ELSE ''
                      END,
                    latitud AS Latitud, 
                    longitud AS Longitud
                FROM 
                    mobile.V_REPORTE_HISTORICO_VISITA
                WHERE cod_visita = @CodigoVisita";
                using (var cnSQL = new SqlConnection(AppConfig.ConnectionString))
                {
                    cnSQL.Open();
                    datosVisita = cnSQL.QueryFirstOrDefault<DatosVisita>(sql, new { CodigoVisita = codigoVisita });
                }
            }
            catch (SqlException exSql)
            {
                WriteSafeFailure(nameFuenteCaller, exSql);
            }
            catch (Exception ex)
            {
                WriteSafeFailure(nameFuenteCaller, ex);
            }
            return datosVisita;
        }
        public static ClasesObjetosDBP360 GetDatosPedidoByCodPedidoDB(int codigoPedido)
        {
            System.Diagnostics.StackFrame stackframe = new System.Diagnostics.StackFrame(1);
            string nameFuenteCaller = stackframe.GetMethod().Name;
            ClasesObjetosDBP360 DatosPedido = null;
            try
            {
                string sql = @"
               SELECT 
                    cod_pedido AS CodPedido, 
                    fecha_pedido AS FechaPedido, 
                    fecha_entrega_pedido AS FechaEntregaPedido, 
                    despachador_pedido AS DespachadorPedido, 
                    cliente AS Cliente,
                    numero_fiscal AS NumeroFiscal,
                    tipo_visitado AS TipoVisitado, 
                    grupo_cliente AS GrupoCliente, 
                    categoria_cliente AS CategoriaCliente, 
                    cadena_cliente AS CadenaCliente,
                    ciudad AS Ciudad, 
                    direccion AS Direccion, 
                    nombre_contacto AS NombreContacto, 
                    colaborador AS Colaborador,
                    observaciones AS Observaciones, 
                    motivo_estado_pedido AS MotivoEstadoPedido,
                    latitud AS Latitud, 
                    longitud AS Longitud, 
                    pedido_unidades_reales AS PedidoUnidadesReales, 
                    pedido_unidades_bonificadas AS PedidoUnidadesBonificadas, 
                    unidades_reales_despachadas AS UnidadesRealesDespachadas, 
                    unidades_bonificadas_despachadas AS UnidadesBonificadasDespachadas, 
                    saldo_unidades AS SaldoUnidades, 
                    saldo_bonificaciones AS SaldoBonificaciones, 
                    pedido_valor_venta_pvf AS PedidoValorVentaPVF, 
                    pedido_valor_venta_pvp AS PedidoValorVentaPVP, 
                    pedido_valor_descuento AS PedidoValorDescuento, 
                    subtotal_pedido_valor_venta_pvf AS SubtotalPedidoValorVentaPVF, 
                    subtotal_pedido_valor_venta_pvp AS SubtotalPedidoValorVentaPVP, 
                    subtotal_pedido_valor_descuento AS SubtotalPedidoValorDescuento
                FROM 
                    mobile.V_REPORTE_HISTORICO_PEDIDOS_RESUMIDO
                WHERE cod_pedido = @CodigoPedido";
                using (var cnSQL = new SqlConnection(AppConfig.ConnectionString))
                {
                    cnSQL.Open();
                    DatosPedido = cnSQL.QueryFirstOrDefault<ClasesObjetosDBP360>(sql, new { CodigoPedido = codigoPedido });
                }
            }
            catch (SqlException exSql)
            {
                WriteSafeFailure(nameFuenteCaller, exSql);
            }
            catch (Exception ex)
            {
                WriteSafeFailure(nameFuenteCaller, ex);
            }
            return DatosPedido;
        }
        public List<DatosContactosNotificaciones> GetDataContactosNotificacionesxReporteyEvento(int reportId, string referenciaEventoId)
        {
            List<DatosContactosNotificaciones> contactosNotificaciones = new List<DatosContactosNotificaciones>();
            try
            {
                using (var cnSQL = new SqlConnection(ConnectionString))
                {
                    cnSQL.Open();
                    var parametros = new { p_report_id = reportId, p_referencia_evento_id = referenciaEventoId };
                    contactosNotificaciones = cnSQL.Query<DatosContactosNotificaciones>("[P360Insight].[GetDataContactosNotificaciones]", parametros, commandType: CommandType.StoredProcedure).ToList();
                }
            }
            catch (SqlException exSql)
            {
                WriteSafeFailure("GetContactosNotificaciones", exSql);
            }
            catch (Exception ex)
            {
                WriteSafeFailure("GetContactosNotificaciones", ex);
            }
            return contactosNotificaciones;
        }

        private static void WriteSafeFailure(
            string operation,
            Exception error)
        {
            SqlException sqlError = error as SqlException;
            string sqlCode = sqlError == null
                ? string.Empty
                : ", sql_code=" + sqlError.Number;
            Console.Error.WriteLine(
                "Fallo de acceso a datos. operation=" + operation +
                ", category=" + error.GetType().Name + sqlCode + ".");
        }

    }
}

namespace SchedulerP360Insight.Modulos
{
    public class Module_FiltrosReportes
    {
        private readonly DataTable tablaFiltrosSeleccionadosenUserControlAnaliticsBIP360;

        public Module_FiltrosReportes()
        {
            Console.WriteLine("Entro a contructor");
            this.tablaFiltrosSeleccionadosenUserControlAnaliticsBIP360=CrearTablaFiltroReportes();
        }

        public DataTable CrearTablaFiltroReportes()
        {
            DataTable tablaFiltros = new DataTable("filtrosReportes");
            try
            {
            
            DataColumn[] cols =
            {
            new DataColumn("token", typeof(int)),
            new DataColumn("fecha", typeof(DateTime)),
            new DataColumn("anio", typeof(int)),
            new DataColumn("mes", typeof(string)),
            new DataColumn("cod_linea", typeof(string)),
            new DataColumn("linea", typeof(string)),
            new DataColumn("producto", typeof(string)),
            new DataColumn("cod_item", typeof(string)),
            new DataColumn("item", typeof(string)),
            new DataColumn("cod_rep", typeof(string)),
            new DataColumn("representante", typeof(string)),
            new DataColumn("supervisor", typeof(string)),
            new DataColumn("cod_poblacion", typeof(string)),
            new DataColumn("poblacion", typeof(string)),
            new DataColumn("ciudad", typeof(string)),
            new DataColumn("region", typeof(string)),
            new DataColumn("provincia", typeof(string)),
            new DataColumn("codigo_campana", typeof(int)),
            new DataColumn("opcion_comparacion", typeof(string)),
            new DataColumn("agrupar_cubo_gerencial", typeof(int))
        };
                tablaFiltros.Columns.AddRange(cols);
                return tablaFiltros;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    "Fallo al construir tabla de filtros. Categoría: " +
                    ex.GetType().Name);
                return tablaFiltros;
            }
        }

    }
}
