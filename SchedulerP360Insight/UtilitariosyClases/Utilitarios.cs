using CrystalDecisions.CrystalReports.Engine;
using CrystalDecisions.Shared;
using SchedulerP360Insight.Modulos;
using SchedulerP360Insight.Properties;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Net.Mail;
using System.Net;
using System.Resources;
using System.Threading.Tasks;
using System.IO;
using DevExpress.XtraPrinting;
using DevExpress.XtraReports.UI;
using ExportOptions = CrystalDecisions.Shared.ExportOptions;
using DevExpress.XtraReports.Serialization;
using SchedulerP360Insight.UtilitariosyClases;
using SchedulerP360Insight.Services;
using System.Globalization;
using System.Text;
using System.Linq;
using System.Diagnostics.Contracts;

namespace SchedulerP360Insight
{
    public class Utilitarios
    {
        readonly ModuleCapaAccesoDatos oModuleCapaAccesoDatos = new ModuleCapaAccesoDatos();
        readonly string currentUsername = Environment.UserName;
        private readonly LaboratoryConstants labConstants;
        private readonly IEmailTransport emailTransport;
        private readonly INotificationDeliveryStore notificationDeliveryStore;
        string accion = string.Empty;
        public static bool NotificaAdministrador { get; set; } = false;

        public Utilitarios()
            : this(new LaboratoryConstants(), null, null)
        {
        }

        public Utilitarios(
            LaboratoryConstants labConstants,
            IEmailTransport emailTransport,
            INotificationDeliveryStore notificationDeliveryStore)
        {
            this.labConstants = labConstants ?? throw new ArgumentNullException(nameof(labConstants));
            this.emailTransport = emailTransport ?? new SmtpEmailTransport(labConstants);
            this.notificationDeliveryStore = notificationDeliveryStore ??
                new SqlNotificationDeliveryStore(oModuleCapaAccesoDatos, currentUsername);
        }
        /// <summary>
        /// Configura la conexión de base de datos para todas las tablas en un documento de reporte, registrando las acciones y errores.
        /// </summary>
        /// <param name="myConnectionInfo">Información de la conexión a la base de datos.</param>
        /// <param name="myReportDocument">El documento de reporte a configurar.</param>
        public void SetDBLogonForReport(ConnectionInfo myConnectionInfo, ReportDocument myReportDocument)
        {
            // Obtiene el nombre del método que llamó a esta función para fines de registro.
            System.Diagnostics.StackFrame stackframe = new System.Diagnostics.StackFrame(1);
            string nameFuenteCaller = stackframe.GetMethod().Name;
            // Instancia para registro de acciones.
            ModuleCapaAccesoDatos oModuleCapaAccesoDatos = new ModuleCapaAccesoDatos();
            string currentUsername = Environment.UserName; // Usuario actual para registrar quién realiza la acción.
            string accion = $"Ingresa a SetDBLogonForReport para establecer conexión del reporte: {myReportDocument.Name}";
            oModuleCapaAccesoDatos.RegistraLogConeccionyAccion(currentUsername, accion);
            try
            {
                foreach (Table myTable in myReportDocument.Database.Tables)
                {
                    accion = $"Estableciendo conexión para la tabla {myTable.Name}";
                    oModuleCapaAccesoDatos.RegistraLogConeccionyAccion(currentUsername, accion);

                    TableLogOnInfo myTableLogonInfo = myTable.LogOnInfo;
                    myTableLogonInfo.ConnectionInfo = myConnectionInfo;
                    myTable.ApplyLogOnInfo(myTableLogonInfo);
                }
            }
            catch (SqlException exSql)
            {
                // Registro de errores SQL
                accion = $"Error al intentar establecimiento de conexión para la tabla: {exSql.Message}";
                oModuleCapaAccesoDatos.RegistraLogConeccionyAccion(currentUsername, accion);
                Console.WriteLine($"SQL Error in {nameFuenteCaller}: {exSql.Message}. StackTrace: {exSql.StackTrace}");
                // Considera re-lanzar la excepción si es necesario que el llamador maneje este error.
                // throw;
            }
            catch (Exception ex)
            {
                // Registro de errores generales
                accion = $"Error general al intentar establecer la conexión: {ex.Message}";
                oModuleCapaAccesoDatos.RegistraLogConeccionyAccion(currentUsername, accion);
                Console.WriteLine($"General Error in {nameFuenteCaller}: {ex.Message}. StackTrace: {ex.StackTrace}");
                // Considera re-lanzar la excepción si es necesario que el llamador maneje este error.
                // throw;
            }
        }

        /// <summary>
        /// Establece la información de conexión para un documento de reporte y todos sus subreportes.
        /// </summary>
        /// <param name="report">El documento de reporte.</param>
        public void SetConnectionInfo(ReportDocument report)
        {
            try
            {
                // Obtiene la cadena de conexión desde el archivo de configuración.
                //string connectionString = ConfigurationManager.ConnectionStrings["CadenaConeccionP360"].ConnectionString;
                string connectionString = AppConfig.ConnectionString;
                // Construye un objeto SqlConnectionStringBuilder con la cadena de conexión.
                SqlConnectionStringBuilder connStringBuilder = new SqlConnectionStringBuilder(connectionString);
                // Crea un objeto ConnectionInfo con los detalles de la conexión.
                ConnectionInfo connectionInfo = new ConnectionInfo
                {
                    DatabaseName = connStringBuilder.InitialCatalog,
                    UserID = connStringBuilder.UserID,
                    Password = connStringBuilder.Password,
                    ServerName = connStringBuilder.DataSource,
                    AllowCustomConnection = true
                };

                // Crea una instancia de Utilitarios para usar el método SetDBLogonForReport.
                Utilitarios utilitarios = new Utilitarios();
                // Establece la conexión para el reporte principal.
                utilitarios.SetDBLogonForReport(connectionInfo, report);

                // Itera a través de las secciones del reporte para establecer la conexión en los subreportes.
                foreach (Section section in report.ReportDefinition.Sections)
                {
                    foreach (ReportObject reportObject in section.ReportObjects)
                    {
                        if (reportObject.Kind == ReportObjectKind.SubreportObject)
                        {
                            SubreportObject subReportObject = (SubreportObject)reportObject;
                            ReportDocument subReportDocument = subReportObject.OpenSubreport(subReportObject.SubreportName);

                            utilitarios.SetDBLogonForReport(connectionInfo, subReportDocument);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred in SetConnectionInfo: {ex.Message}");
                // Re-lanzar la excepción permite que el llamador maneje este error de manera adecuada.
                throw;
            }
        }
        /// <summary>
        /// Obtiene información de la cola de notificaciones basada en el ID del reporte.
        /// </summary>
        /// <param name="reportId">El ID del reporte para filtrar las notificaciones.</param>
        /// <returns>Una lista de objetos InfoColaNotificaciones que contienen los detalles de las notificaciones.</returns>
        public List<InfoColaNotificaciones> GetInfoColaNotificaciones(int reportId)
        {
            // Obtiene la cadena de conexión desde el archivo de configuración.
            //string connectionString = ConfigurationManager.ConnectionStrings["CadenaConeccionP360"].ConnectionString;
            string connectionString = AppConfig.ConnectionString;
            List<InfoColaNotificaciones> notifications = new List<InfoColaNotificaciones>();

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open(); // Abre la conexión a la base de datos.

                    // Obtiene la consulta SQL desde el archivo de configuración.
                    string query = ConfigurationManager.AppSettings["P360.InfoColaNotificaciones.Query"];

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@ReportId", reportId); // Establece el parámetro para la consulta.

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read()) // Lee cada fila del resultado.
                            {
                                // Crea un nuevo objeto InfoColaNotificaciones y lo llena con los datos de la fila actual.
                                InfoColaNotificaciones notification = new InfoColaNotificaciones
                                {
                                    ColaNotificacionId = Convert.ToInt32(reader["cola_notificacion_id"]),
                                    ReportId = Convert.ToInt32(reader["report_id"]),
                                    ReportUID = Convert.ToString(reader["report_uid"]),
                                    ReportName = Convert.ToString(reader["report_name"]),
                                    ReportInsight = Convert.ToString(reader["report_insight"]),
                                    ReportType = Convert.ToString(reader["report_type"]),
                                    ReferenceEvent = Convert.ToString(reader["referencia_evento"]),
                                    ReferenceEventId = Convert.ToString(reader["referencia_evento_id"]),
                                    CodColab = Convert.ToInt32(reader["cod_colab"]),
                                    NameColab = Convert.ToString(reader["nombre_colab"]),
                                    EmailColab = Convert.ToString(reader["email_colab"]),
                                    CodSup = Convert.ToInt32(reader["cod_sup"]),
                                    NameSup = Convert.ToString(reader["nombre_sup"]),
                                    EmailSup = Convert.ToString(reader["email_sup"])
                                };

                                notifications.Add(notification); // Agrega el objeto a la lista de notificaciones.
                            }
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                // Maneja excepciones específicas de SQL Server.
                string errorMessage = $"Error al recuperar las notificaciones de la base de datos: {ex.Message}";
                throw new CustomException(errorMessage, ex);
            }
            catch (Exception ex)
            {
                // Maneja todas las otras excepciones.
                string errorMessage = $"Error al recuperar las notificaciones de la base de datos: {ex.Message}";
                throw new CustomException(errorMessage, ex);
            }
            return notifications; // Devuelve la lista de notificaciones.
        }

        /// <summary>
        /// Constructs the HTML body for an email using a specified template and dynamic data.
        /// This function fetches an HTML template based on a key, replaces common and specific placeholders
        /// with data from notification information and laboratory constants, and injects dynamic date-related data.
        /// </summary>
        /// <param name="p360notificacion">An instance of <see cref="InfoColaNotificaciones"/>, containing details about the notification.</param>
        /// <param name="labConstants">An instance of <see cref="LaboratoryConstants"/>, providing necessary constants for the email.</param>
        /// <param name="emailBodyKey">The key used to retrieve the specific HTML template from resources.</param>
        /// <returns>A string containing the fully constructed and formatted HTML email body. Returns an empty string if an error occurs.</returns>
        /// <remarks>
        /// The function supports various types of notifications by switching on the <c>ReportName</c> attribute of <paramref name="p360notificacion"/>.
        /// It handles common placeholders universally and specific placeholders based on the notification type.
        /// Date calculations are performed to include the current date, the first, and the last day of the current month.
        /// </remarks>
        /// <exception cref="Exception">Catches and suppresses exceptions, returning an empty string on failure. Future implementations should enhance error handling.</exception>
        public static string ConstruirCuerpoEmailPlantillaHTML(InfoColaNotificaciones p360notificacion, LaboratoryConstants labConstants, string emailBodyKey)
        {
            string htmlBodyEmail = "";
            try
            {
                // Inicialización del ResourceManager para acceder a las plantillas de correo (Obtención de la plantilla de correo basada en 'emailBodyKey')
                ResourceManager resourceManager = new ResourceManager(typeof(ResourcePlantillasEmail));
                htmlBodyEmail = resourceManager.GetString(emailBodyKey);
                // Reemplazo de marcadores COMUNES de todas las plantillas
                htmlBodyEmail = ReemplazarMarcadoresComunesPlantillaHTML(htmlBodyEmail, p360notificacion, labConstants);
                switch (p360notificacion.ReportUID)
                {
                    case "PVM":
                    case "PVMM":
                    case "PVG":
                    case "PVGM":
                        htmlBodyEmail = ReemplazarMarcadoresEspecificosPlantillaHTMLTipoA(htmlBodyEmail, p360notificacion);
                        break;
                    case "AURX":
                    case "AUMD":
                        htmlBodyEmail = ReemplazarMarcadoresEspecificosPlantillaHTMLTipoA(htmlBodyEmail, p360notificacion);
                        break;
                    case "RVIS": //Registro visita(Actualmente funcional)
                        htmlBodyEmail = ReemplazarMarcadoresEspecificosPlantillaHTML_RegistroVisita(htmlBodyEmail, p360notificacion);
                        break;
                    case "AVIS": //Anulación visita
                        htmlBodyEmail = ReemplazarMarcadoresEspecificosPlantillaHTML_AnulacionVisita(htmlBodyEmail, p360notificacion);
                        break;
                    case "RPED": //Registro pedido
                        htmlBodyEmail = ReemplazarMarcadoresEspecificosPlantillaHTML_RegistroPedido(htmlBodyEmail, p360notificacion);
                        break;
                    case "DPED": //Despacho pedido
                        htmlBodyEmail = ReemplazarMarcadoresEspecificosPlantillaHTML_DespachoPedido(htmlBodyEmail, p360notificacion);
                        break;
                    case "XPED": //Anulación pedido
                        htmlBodyEmail = ReemplazarMarcadoresEspecificosPlantillaHTML_AnulacionDevolucionPedido(htmlBodyEmail, p360notificacion);
                        break;
                    case "VPED": //Devolución pedido
                        htmlBodyEmail = ReemplazarMarcadoresEspecificosPlantillaHTML_AnulacionDevolucionPedido(htmlBodyEmail, p360notificacion);
                        break;
                    case "STNP": //Devolución pedido
                        htmlBodyEmail = ReemplazarMarcadoresEspecificosPlantillaHTML_SolicitudTiempoNoPromocional(htmlBodyEmail, p360notificacion);
                        break;
                    case "VTNP": //Devolución pedido
                        htmlBodyEmail = ReemplazarMarcadoresEspecificosPlantillaHTML_AprobacionDenegacionSolicitudTiempoNoPromocional(htmlBodyEmail, p360notificacion);
                        break;
                    default:
                        // Manejo opcional de casos no reconocidos
                        break;
                }
                //Retorna el HTML ya formateado
                return htmlBodyEmail;
            }
            catch (Exception ex)
            {
                return htmlBodyEmail;
            }
        }

        /// <summary>
        /// Replaces common placeholders in an HTML email template with specific data from notification details and laboratory constants.
        /// </summary>
        /// <param name="htmlBodyEmail">The HTML content of the email template before placeholder replacement.</param>
        /// <param name="p360notificacion">An instance of <see cref="InfoColaNotificaciones"/>, containing notification-specific information such as the recipient's name.</param>
        /// <param name="labConstants">An instance of <see cref="LaboratoryConstants"/>, providing necessary constant values such as laboratory name, administrator email, etc.</param>
        /// <returns>The HTML content of the email after replacing placeholders with actual data. If an exception occurs, the original HTML content is returned unchanged.</returns>
        /// <remarks>
        /// This method focuses on replacing placeholders that are common across different types of email templates, such as recipient name, laboratory name, admin email, and others.
        /// It is designed to be used as part of a larger process that prepares emails for sending by dynamically inserting relevant data into the templates.
        /// </remarks>
        /// <exception cref="Exception">Catches and suppresses any exception, returning the original HTML content if an error occurs during the replacement process.</exception>
        private static string ReemplazarMarcadoresComunesPlantillaHTML(string htmlBodyEmail, InfoColaNotificaciones p360notificacion, LaboratoryConstants labConstants)
        {
            try
            {
                return htmlBodyEmail.Replace("[NombreReporte]", p360notificacion.ReportName)
                                    .Replace("[NombreInsight]", p360notificacion.ReportInsight)
                                    .Replace("[Destinatario]", p360notificacion.NameColab)
                                    .Replace("[LaboratorioImplementacion]", labConstants.LaboratoryName)
                                    .Replace("[MailAdministradorLaboratorio]", labConstants.AdminEmail)
                                    .Replace("[FechaActualP360]", ObtenerFechaActualP360())
                                    .Replace("[UrlImagenLogo]", labConstants.Pharma360UrlLogo)
                                    .Replace("[TrademarkBisigma]", labConstants.IntellectualPropertyNotice);
            }
            catch (Exception ex)
            {
                return htmlBodyEmail;
            }
        }

        /// <summary>
        /// Replaces specific placeholders within an HTML email template with data relevant to a particular type of notification.
        /// </summary>
        /// <param name="htmlBodyEmail">The HTML content of the email template before placeholder replacement.</param>
        /// <param name="p360notificacion">An instance of <see cref="InfoColaNotificaciones"/>, containing information specific to the notification that may dictate the replacement logic.</param>
        /// <returns>The HTML content of the email after replacing specific placeholders with the designated data. If an exception occurs, the original HTML content is returned unchanged.</returns>
        /// <remarks>
        /// This method is tailored to handle specific data replacement for a certain type of email template, indicated by "Tipo A" in the method name. It should be adapted to include actual data replacement logic based on the notification's requirements.
        /// </remarks>
        /// <exception cref="Exception">Catches and suppresses any exception, returning the original HTML content if an error occurs during the replacement process.</exception>
        private static string ReemplazarMarcadoresEspecificosPlantillaHTMLTipoA(string htmlBodyEmail, InfoColaNotificaciones p360notificacion)
        {
            try
            {
                // Reemplazo de marcadores distintos de cada reporte(depende de la plantilla aplicada ya que vari en cada caso)
                // Fecha actual y cálculo del primer y último día del mes
                DateTime currentDate = DateTime.Now;
                DateTime firstDayOfMonth = new DateTime(currentDate.Year, currentDate.Month, 1);
                DateTime lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);
                string vg_periodo = $"{firstDayOfMonth:yyyy-MM-dd} al {lastDayOfMonth:yyyy-MM-dd}";
                htmlBodyEmail = htmlBodyEmail.Replace("[Periodo]", vg_periodo);
                return htmlBodyEmail;
            }
            catch (Exception ex)
            {
                return htmlBodyEmail;
            }
        }
        /// <summary>
        /// Replaces specific placeholders within an HTML email template with data relevant to a particular type of notification.
        /// </summary>
        /// <param name="htmlBodyEmail">The HTML content of the email template before placeholder replacement.</param>
        /// <param name="p360notificacion">An instance of <see cref="InfoColaNotificaciones"/>, containing information specific to the notification that may dictate the replacement logic.</param>
        /// <returns>The HTML content of the email after replacing specific placeholders with the designated data. If an exception occurs, the original HTML content is returned unchanged.</returns>
        /// <remarks>
        /// This method is tailored to handle specific data replacement for a certain type of email template, indicated by "Tipo A" in the method name. It should be adapted to include actual data replacement logic based on the notification's requirements.
        /// </remarks>
        /// <exception cref="Exception">Catches and suppresses any exception, returning the original HTML content if an error occurs during the replacement process.</exception>
        private static string ReemplazarMarcadoresEspecificosPlantillaHTML_AprobacionDenegacionSolicitudTiempoNoPromocional(string htmlBodyEmail, InfoColaNotificaciones p360notificacion)
        {
            try
            {
                // Obtiene el código de solicitud vacaciones o ausencias desde el objeto p360notificacion
                int idVacacionAusencia = int.Parse(p360notificacion.ReferenceEventId);
                DatosVacacionAusencia datosVacacionAusencia = ModuleCapaAccesoDatos.GetDatosVacacionAusenciaById(idVacacionAusencia);
                // Obtiene el código de visita desde el objeto p360notificacion
                int codigoVisita = datosVacacionAusencia.CodVisita;
                // Llama al método para obtener los datos de la visita desde la base de datos
                DatosVisita datosVisita = ModuleCapaAccesoDatos.GetDatosVisitaByCodVisitaDB(codigoVisita);
                if (datosVisita != null)
                {
                    if (datosVisita.NombreVisitado.ToUpper().Equals("PUNTO DE CONTACTO"))
                    {
                        NotificaAdministrador = true;
                    }

                    StringBuilder sb = new StringBuilder(htmlBodyEmail);
                    // Reemplazar marcador del mapa, si las coordenadas están disponibles
                    if (!string.IsNullOrEmpty(datosVisita.Latitud) && !string.IsNullOrEmpty(datosVisita.Longitud))
                    {
                        string googleMapsApiKey = AppConfig.GoogleMapsApiKey;
                        string htmlMapa = GenerarTAGImagenEstaticaMapaHTML(datosVisita.Latitud, datosVisita.Longitud, googleMapsApiKey);
                        sb.Replace("[MARCADOR_MAPA]", htmlMapa);
                    }
                    DateTime fechaVisitaFin = datosVisita.FechaVisita.AddMinutes(datosVisita.MinutosDuracionVisita);

                    // Reemplazar otros marcadores en el HTML
                    sb.Replace("[CodVisita]", datosVisita.CodVisita.ToString())
                      .Replace("[Destinatario]", datosVisita.Colaborador)
                      .Replace("[Supervisor]", datosVisita.Supervisor)
                      .Replace("[Cliente]", datosVisita.NombreVisitado)
                      .Replace("[CategoriaEspecialidad]", datosVisita.CategoriaEspecialidad)
                      .Replace("[Direccion]", datosVisita.Direccion)
                      .Replace("[Fecha_visita]", datosVisita.FechaVisita.ToString("dd/MMMM/yyyy hh:mm tt", new CultureInfo("es-ES")))
                      .Replace("[MinutosDuracionVisita]", datosVisita.MinutosDuracionVisita.ToString())
                      .Replace("[Ciudad]", datosVisita.Ciudad)
                      .Replace("[Contacto]", datosVisita.NombreContacto)
                      .Replace("[Observaciones]", datosVisita.Observaciones)
                      .Replace("[ObjetivoVisita]", datosVisita.ObjetivoVisitaDescripcion)
                      .Replace("[AccionesComercialesVisita]", datosVisita.AccionesEfectuadas)
                      .Replace("[Fecha_visita_fin]", fechaVisitaFin.ToString("dd/MMMM/yyyy hh:mm tt", new CultureInfo("es-ES")))
                      .Replace("[Estado_solicitud]", datosVacacionAusencia.EstadoVacacionAusencia)
                      .Replace("[Observacion_estado_solicitud]", datosVacacionAusencia.ComentarioEstadoVacacionAusencia)
                      .Replace("[ID_solicitud]", datosVacacionAusencia.IdVacacionAusencia.ToString());

                    // Devuelve la cadena modificada
                    return sb.ToString();
                }
                else
                {
                    Console.WriteLine($"No se encontraron datos para el código de visita {codigoVisita} proporcionado.");
                    return htmlBodyEmail;
                }
            }
            catch (Exception ex)
            {
                // Considera registrar el error
                Console.WriteLine($"Error al reemplazar marcadores en la plantilla: {ex.Message}");
                return htmlBodyEmail;
            }
        }

        /// <summary>
        /// Replaces specific placeholders within an HTML email template with data relevant to a particular type of notification.
        /// </summary>
        /// <param name="htmlBodyEmail">The HTML content of the email template before placeholder replacement.</param>
        /// <param name="p360notificacion">An instance of <see cref="InfoColaNotificaciones"/>, containing information specific to the notification that may dictate the replacement logic.</param>
        /// <returns>The HTML content of the email after replacing specific placeholders with the designated data. If an exception occurs, the original HTML content is returned unchanged.</returns>
        /// <remarks>
        /// This method is tailored to handle specific data replacement for a certain type of email template, indicated by "Tipo A" in the method name. It should be adapted to include actual data replacement logic based on the notification's requirements.
        /// </remarks>
        /// <exception cref="Exception">Catches and suppresses any exception, returning the original HTML content if an error occurs during the replacement process.</exception>
        private static string ReemplazarMarcadoresEspecificosPlantillaHTML_SolicitudTiempoNoPromocional(string htmlBodyEmail, InfoColaNotificaciones p360notificacion)
        {
            try
            {
                // Obtiene el código de solicitud vacaciones o ausencias desde el objeto p360notificacion
                int idVacacionAusencia = int.Parse(p360notificacion.ReferenceEventId);
                DatosVacacionAusencia datosVacacionAusencia = ModuleCapaAccesoDatos.GetDatosVacacionAusenciaById(idVacacionAusencia);
                // Obtiene el código de visita desde el objeto p360notificacion
                int codigoVisita = datosVacacionAusencia.CodVisita;
                // Llama al método para obtener los datos de la visita desde la base de datos
                DatosVisita datosVisita = ModuleCapaAccesoDatos.GetDatosVisitaByCodVisitaDB(codigoVisita);
                if (datosVisita != null)
                {
                    if (datosVisita.NombreVisitado.ToUpper().Equals("PUNTO DE CONTACTO"))
                    {
                        NotificaAdministrador = true;
                    }

                    StringBuilder sb = new StringBuilder(htmlBodyEmail);
                    // Reemplazar marcador del mapa, si las coordenadas están disponibles
                    if (!string.IsNullOrEmpty(datosVisita.Latitud) && !string.IsNullOrEmpty(datosVisita.Longitud))
                    {
                        string googleMapsApiKey = AppConfig.GoogleMapsApiKey;
                        string htmlMapa = GenerarTAGImagenEstaticaMapaHTML(datosVisita.Latitud, datosVisita.Longitud, googleMapsApiKey);
                        sb.Replace("[MARCADOR_MAPA]", htmlMapa);
                    }
                    DateTime fechaVisitaFin = datosVisita.FechaVisita.AddMinutes(datosVisita.MinutosDuracionVisita);

                    // Reemplazar otros marcadores en el HTML
                    sb.Replace("[CodVisita]", datosVisita.CodVisita.ToString())
                      .Replace("[Destinatario]", datosVisita.Colaborador)
                      .Replace("[Supervisor]", datosVisita.Supervisor)
                      .Replace("[Cliente]", datosVisita.NombreVisitado)
                      .Replace("[CategoriaEspecialidad]", datosVisita.CategoriaEspecialidad)
                      .Replace("[Direccion]", datosVisita.Direccion)
                      .Replace("[Fecha_visita]", datosVisita.FechaVisita.ToString("dd/MMMM/yyyy hh:mm tt", new CultureInfo("es-ES")))
                      .Replace("[MinutosDuracionVisita]", datosVisita.MinutosDuracionVisita.ToString())
                      .Replace("[Ciudad]", datosVisita.Ciudad)
                      .Replace("[Contacto]", datosVisita.NombreContacto)
                      .Replace("[Observaciones]", datosVisita.Observaciones)
                      .Replace("[ObjetivoVisita]", datosVisita.ObjetivoVisitaDescripcion)
                      .Replace("[AccionesComercialesVisita]", datosVisita.AccionesEfectuadas)
                      .Replace("[Fecha_visita_fin]", fechaVisitaFin.ToString("dd/MMMM/yyyy hh:mm tt", new CultureInfo("es-ES")))
                      .Replace("[ID_solicitud]", datosVacacionAusencia.IdVacacionAusencia.ToString());

                    // Devuelve la cadena modificada
                    return sb.ToString();
                }
                else
                {
                    Console.WriteLine($"No se encontraron datos para el código de visita {codigoVisita} proporcionado.");
                    return htmlBodyEmail;
                }
            }
            catch (Exception ex)
            {
                // Considera registrar el error
                Console.WriteLine($"Error al reemplazar marcadores en la plantilla: {ex.Message}");
                return htmlBodyEmail;
            }
        }

        /// <summary>
        /// Replaces specific placeholders within an HTML email template with data relevant to a particular type of notification.
        /// </summary>
        /// <param name="htmlBodyEmail">The HTML content of the email template before placeholder replacement.</param>
        /// <param name="p360notificacion">An instance of <see cref="InfoColaNotificaciones"/>, containing information specific to the notification that may dictate the replacement logic.</param>
        /// <returns>The HTML content of the email after replacing specific placeholders with the designated data. If an exception occurs, the original HTML content is returned unchanged.</returns>
        /// <remarks>
        /// This method is tailored to handle specific data replacement for a certain type of email template, indicated by "Tipo A" in the method name. It should be adapted to include actual data replacement logic based on the notification's requirements.
        /// </remarks>
        /// <exception cref="Exception">Catches and suppresses any exception, returning the original HTML content if an error occurs during the replacement process.</exception>
        private static string ReemplazarMarcadoresEspecificosPlantillaHTML_RegistroVisita(string htmlBodyEmail, InfoColaNotificaciones p360notificacion)
        {
            try
            {
                // Obtiene el código de visita desde el objeto p360notificacion
                int codigoVisita = int.Parse(p360notificacion.ReferenceEventId);
                // Llama al método para obtener los datos de la visita desde la base de datos
                DatosVisita datosVisita = ModuleCapaAccesoDatos.GetDatosVisitaByCodVisitaDB(codigoVisita);
                if (datosVisita != null)
                {
                    if (datosVisita.NombreVisitado.ToUpper().Equals("PUNTO DE CONTACTO"))
                    {
                        NotificaAdministrador = true;
                    }

                    StringBuilder sb = new StringBuilder(htmlBodyEmail);
                    // Reemplazar marcador del mapa, si las coordenadas están disponibles
                    if (!string.IsNullOrEmpty(datosVisita.Latitud) && !string.IsNullOrEmpty(datosVisita.Longitud))
                    {
                        string googleMapsApiKey = AppConfig.GoogleMapsApiKey;
                        string htmlMapa = GenerarTAGImagenEstaticaMapaHTML(datosVisita.Latitud, datosVisita.Longitud, googleMapsApiKey);
                        sb.Replace("[MARCADOR_MAPA]", htmlMapa);
                    }
                    // Reemplazar otros marcadores en el HTML
                    sb.Replace("[CodVisita]", datosVisita.CodVisita.ToString())
                      .Replace("[Destinatario]", datosVisita.Colaborador)
                      .Replace("[Cliente]", datosVisita.NombreVisitado)
                      .Replace("[CategoriaEspecialidad]", datosVisita.CategoriaEspecialidad)
                      .Replace("[Direccion]", datosVisita.Direccion)
                      .Replace("[Fecha_visita]", datosVisita.FechaVisita.ToString("dd/MMMM/yyyy hh:mm tt", new CultureInfo("es-ES")))
                      .Replace("[Ciudad]", datosVisita.Ciudad)
                      .Replace("[Contacto]", datosVisita.NombreContacto)
                      .Replace("[Observaciones]", datosVisita.Observaciones)
                      .Replace("[ObjetivoVisita]", datosVisita.ObjetivoVisitaDescripcion)
                      .Replace("[AccionesComercialesVisita]", datosVisita.AccionesEfectuadas);

                    // Devuelve la cadena modificada
                    return sb.ToString();
                }
                else
                {
                    Console.WriteLine($"No se encontraron datos para el código de visita {codigoVisita} proporcionado.");
                    return htmlBodyEmail;
                }
            }
            catch (Exception ex)
            {
                // Considera registrar el error
                Console.WriteLine($"Error al reemplazar marcadores en la plantilla: {ex.Message}");
                return htmlBodyEmail;
            }
        }
        /// <summary>
        /// Replaces specific placeholders within an HTML email template with data relevant to a particular type of notification.
        /// </summary>
        /// <param name="htmlBodyEmail">The HTML content of the email template before placeholder replacement.</param>
        /// <param name="p360notificacion">An instance of <see cref="InfoColaNotificaciones"/>, containing information specific to the notification that may dictate the replacement logic.</param>
        /// <returns>The HTML content of the email after replacing specific placeholders with the designated data. If an exception occurs, the original HTML content is returned unchanged.</returns>
        /// <remarks>
        /// This method is tailored to handle specific data replacement for a certain type of email template, indicated by "Tipo A" in the method name. It should be adapted to include actual data replacement logic based on the notification's requirements.
        /// </remarks>
        /// <exception cref="Exception">Catches and suppresses any exception, returning the original HTML content if an error occurs during the replacement process.</exception>
        private static string ReemplazarMarcadoresEspecificosPlantillaHTML_AnulacionVisita(string htmlBodyEmail, InfoColaNotificaciones p360notificacion)
        {
            try
            {
                // Obtiene el código de visita desde el objeto p360notificacion
                int codigoVisita = int.Parse(p360notificacion.ReferenceEventId);
                // Llama al método para obtener los datos de la visita desde la base de datos
                DatosVisita datosVisita = ModuleCapaAccesoDatos.GetDatosVisitaByCodVisitaDB(codigoVisita);
                if (datosVisita != null)
                {
                    StringBuilder sb = new StringBuilder(htmlBodyEmail);

                    // Reemplazar marcador del mapa, si las coordenadas están disponibles
                    if (!string.IsNullOrEmpty(datosVisita.Latitud) && !string.IsNullOrEmpty(datosVisita.Longitud))
                    {
                        string googleMapsApiKey = AppConfig.GoogleMapsApiKey;
                        string htmlMapa = GenerarTAGImagenEstaticaMapaHTML(datosVisita.Latitud, datosVisita.Longitud, googleMapsApiKey);
                        sb.Replace("[MARCADOR_MAPA]", htmlMapa);
                    }
                    // Reemplazar otros marcadores en el HTML
                    sb.Replace("[CodVisita]", datosVisita.CodVisita.ToString())
                      .Replace("[Destinatario]", datosVisita.Colaborador)
                      .Replace("[Cliente]", datosVisita.NombreVisitado)
                      .Replace("[CategoriaEspecialidad]", datosVisita.CategoriaEspecialidad)
                      .Replace("[Direccion]", datosVisita.Direccion)
                      .Replace("[Fecha_visita]", datosVisita.FechaVisita.ToString("dd/MMMM/yyyy hh:mm tt", new CultureInfo("es-ES")))
                      .Replace("[Ciudad]", datosVisita.Ciudad)
                      .Replace("[Contacto]", datosVisita.NombreContacto)
                      .Replace("[Observaciones]", datosVisita.Observaciones)
                      .Replace("[ObjetivoVisita]", datosVisita.ObjetivoVisitaDescripcion);

                    // Devuelve la cadena modificada
                    return sb.ToString();
                }
                else
                {
                    Console.WriteLine($"No se encontraron datos para el código de visita {codigoVisita} proporcionado.");
                    return htmlBodyEmail;
                }
            }
            catch (Exception ex)
            {
                // Considera registrar el error
                Console.WriteLine($"Error al reemplazar marcadores en la plantilla: {ex.Message}");
                return htmlBodyEmail;
            }
        }
        /// <summary>
        /// Replaces specific placeholders within an HTML email template with data relevant to a particular type of notification.
        /// </summary>
        /// <param name="htmlBodyEmail">The HTML content of the email template before placeholder replacement.</param>
        /// <param name="p360notificacion">An instance of <see cref="InfoColaNotificaciones"/>, containing information specific to the notification that may dictate the replacement logic.</param>
        /// <returns>The HTML content of the email after replacing specific placeholders with the designated data. If an exception occurs, the original HTML content is returned unchanged.</returns>
        /// <remarks>
        /// This method is tailored to handle specific data replacement for a certain type of email template, indicated by "Tipo A" in the method name. It should be adapted to include actual data replacement logic based on the notification's requirements.
        /// </remarks>
        /// <exception cref="Exception">Catches and suppresses any exception, returning the original HTML content if an error occurs during the replacement process.</exception>
        private static string ReemplazarMarcadoresEspecificosPlantillaHTML_RegistroPedido(string htmlBodyEmail, InfoColaNotificaciones p360notificacion)
        {
            try
            {
                // Obtiene el código de pedido desde el objeto p360notificacion
                int codigoPedido = int.Parse(p360notificacion.ReferenceEventId);
                // Llama al método para obtener los datos del pedido desde la base de datos
                ClasesObjetosDBP360 datosPedido = ModuleCapaAccesoDatos.GetDatosPedidoByCodPedidoDB(codigoPedido);
                if (datosPedido != null)
                {
                    StringBuilder sb = new StringBuilder(htmlBodyEmail);

                    // Reemplazar marcador del mapa, si las coordenadas están disponibles
                    if (!string.IsNullOrEmpty(datosPedido.Latitud) && !string.IsNullOrEmpty(datosPedido.Longitud))
                    {
                        string googleMapsApiKey = AppConfig.GoogleMapsApiKey;
                        string htmlMapa = GenerarTAGImagenEstaticaMapaHTML(datosPedido.Latitud, datosPedido.Longitud, googleMapsApiKey);
                        sb.Replace("[MARCADOR_MAPA]", htmlMapa);
                    }
                    // Reemplazar otros marcadores en el HTML
                    sb.Replace("[CodPedido]", datosPedido.CodPedido.ToString())
                      .Replace("[Destinatario]", datosPedido.Colaborador)
                      .Replace("[Cliente]", datosPedido.Cliente)
                      .Replace("[CategoriaEspecialidad]", datosPedido.CategoriaCliente)
                      .Replace("[Direccion]", datosPedido.Direccion)
                      .Replace("[Fecha_pedido]", datosPedido.FechaPedido.ToString("dd/MMMM/yyyy hh:mm tt", new CultureInfo("es-ES")))
                      .Replace("[Fecha_acordada]", datosPedido.FechaEntregaPedido.ToString("dd/MMMM/yyyy hh:mm tt", new CultureInfo("es-ES")))
                      .Replace("[Ciudad]", datosPedido.Ciudad)
                      .Replace("[Contacto]", datosPedido.NombreContacto)
                      .Replace("[Observaciones]", datosPedido.Observaciones)
                      .Replace("[Despachador_pedido]", datosPedido.DespachadorPedido)
                      .Replace("[Unidades]", datosPedido.PedidoUnidadesReales.ToString())
                      .Replace("[Bonificaciones]", datosPedido.PedidoUnidadesBonificadas.ToString())
                      .Replace("[Total_solicitados]", (datosPedido.PedidoUnidadesReales + datosPedido.PedidoUnidadesBonificadas).ToString())
                      .Replace("[Unidades_despachadas]", datosPedido.UnidadesRealesDespachadas.ToString())
                      .Replace("[Bonificaciones_despachadas]", datosPedido.UnidadesBonificadasDespachadas.ToString())
                      .Replace("[Total_despachados]", (datosPedido.UnidadesRealesDespachadas + datosPedido.UnidadesBonificadasDespachadas).ToString())
                      .Replace("[Saldo_unidades]", datosPedido.SaldoUnidades.ToString())
                      .Replace("[Saldo_bonificaciones]", datosPedido.SaldoBonificaciones.ToString())
                      .Replace("[Total_saldo]", (datosPedido.SaldoUnidades + datosPedido.SaldoBonificaciones).ToString());
                    // Devuelve la cadena modificada
                    return sb.ToString();
                }
                else
                {
                    Console.WriteLine($"No se encontraron datos para el código de pedido {codigoPedido} proporcionado.");
                    return htmlBodyEmail;
                }
            }
            catch (Exception ex)
            {
                // Considera registrar el error
                Console.WriteLine($"Error al reemplazar marcadores en la plantilla: {ex.Message}");
                return htmlBodyEmail;
            }
        }

        /// <summary>
        /// Replaces specific placeholders within an HTML email template with data relevant to a particular type of notification.
        /// </summary>
        /// <param name="htmlBodyEmail">The HTML content of the email template before placeholder replacement.</param>
        /// <param name="p360notificacion">An instance of <see cref="InfoColaNotificaciones"/>, containing information specific to the notification that may dictate the replacement logic.</param>
        /// <returns>The HTML content of the email after replacing specific placeholders with the designated data. If an exception occurs, the original HTML content is returned unchanged.</returns>
        /// <remarks>
        /// This method is tailored to handle specific data replacement for a certain type of email template, indicated by "Tipo A" in the method name. It should be adapted to include actual data replacement logic based on the notification's requirements.
        /// </remarks>
        /// <exception cref="Exception">Catches and suppresses any exception, returning the original HTML content if an error occurs during the replacement process.</exception>
        private static string ReemplazarMarcadoresEspecificosPlantillaHTML_DespachoPedido(string htmlBodyEmail, InfoColaNotificaciones p360notificacion)
        {
            try
            {
                string codigoUnicoDespacho_CUD = p360notificacion.ReferenceEventId;
                int codigoPedido = ModuleCapaAccesoDatos.GetCodPedidoPorCodUnicoDespachoCUD(codigoUnicoDespacho_CUD);
                // Obtiene el código de pedido desde el objeto p360notificacion
                //int codigoPedido = int.Parse(p360notificacion.ReferenceEventId);
                // Llama al método para obtener los datos del pedido desde la base de datos
                ClasesObjetosDBP360 datosPedido = ModuleCapaAccesoDatos.GetDatosPedidoByCodPedidoDB(codigoPedido);
                if (datosPedido != null)
                {
                    StringBuilder sb = new StringBuilder(htmlBodyEmail);

                    // Reemplazar marcador del mapa, si las coordenadas están disponibles
                    if (!string.IsNullOrEmpty(datosPedido.Latitud) && !string.IsNullOrEmpty(datosPedido.Longitud))
                    {
                        string googleMapsApiKey = AppConfig.GoogleMapsApiKey;
                        string htmlMapa = GenerarTAGImagenEstaticaMapaHTML(datosPedido.Latitud, datosPedido.Longitud, googleMapsApiKey);
                        sb.Replace("[MARCADOR_MAPA]", htmlMapa);
                    }
                    // Reemplazar otros marcadores en el HTML
                    sb.Replace("[CodPedido]", datosPedido.CodPedido.ToString())
                      .Replace("[CodDespacho]", codigoUnicoDespacho_CUD.ToString())
                      .Replace("[Destinatario]", datosPedido.Colaborador)
                      .Replace("[Cliente]", datosPedido.Cliente)
                      .Replace("[CategoriaEspecialidad]", datosPedido.CategoriaCliente)
                      .Replace("[Direccion]", datosPedido.Direccion)
                      .Replace("[Fecha_pedido]", datosPedido.FechaPedido.ToString("dd/MMMM/yyyy hh:mm tt", new CultureInfo("es-ES")))
                      .Replace("[Fecha_acordada]", datosPedido.FechaEntregaPedido.ToString("dd/MMMM/yyyy hh:mm tt", new CultureInfo("es-ES")))
                      .Replace("[Ciudad]", datosPedido.Ciudad)
                      .Replace("[Contacto]", datosPedido.NombreContacto)
                      .Replace("[Observaciones]", datosPedido.Observaciones)
                      .Replace("[Despachador_pedido]", datosPedido.DespachadorPedido)
                      .Replace("[Unidades]", datosPedido.PedidoUnidadesReales.ToString())
                      .Replace("[Bonificaciones]", datosPedido.PedidoUnidadesBonificadas.ToString())
                      .Replace("[Total_solicitados]", (datosPedido.PedidoUnidadesReales + datosPedido.PedidoUnidadesBonificadas).ToString())
                      .Replace("[Unidades_despachadas]", datosPedido.UnidadesRealesDespachadas.ToString())
                      .Replace("[Bonificaciones_despachadas]", datosPedido.UnidadesBonificadasDespachadas.ToString())
                      .Replace("[Total_despachados]", (datosPedido.UnidadesRealesDespachadas + datosPedido.UnidadesBonificadasDespachadas).ToString())
                      .Replace("[Saldo_unidades]", datosPedido.SaldoUnidades.ToString())
                      .Replace("[Saldo_bonificaciones]", datosPedido.SaldoBonificaciones.ToString())
                      .Replace("[Total_saldo]", (datosPedido.SaldoUnidades + datosPedido.SaldoBonificaciones).ToString());
                    // Devuelve la cadena modificada
                    return sb.ToString();
                }
                else
                {
                    Console.WriteLine($"No se encontraron datos para el código de pedido {codigoPedido} proporcionado.");
                    return htmlBodyEmail;
                }
            }
            catch (Exception ex)
            {
                // Considera registrar el error
                Console.WriteLine($"Error al reemplazar marcadores en la plantilla: {ex.Message}");
                return htmlBodyEmail;
            }
        }

        /// <summary>
        /// Replaces specific placeholders within an HTML email template with data relevant to a particular type of notification.
        /// </summary>
        /// <param name="htmlBodyEmail">The HTML content of the email template before placeholder replacement.</param>
        /// <param name="p360notificacion">An instance of <see cref="InfoColaNotificaciones"/>, containing information specific to the notification that may dictate the replacement logic.</param>
        /// <returns>The HTML content of the email after replacing specific placeholders with the designated data. If an exception occurs, the original HTML content is returned unchanged.</returns>
        /// <remarks>
        /// This method is tailored to handle specific data replacement for a certain type of email template, indicated by "Tipo A" in the method name. It should be adapted to include actual data replacement logic based on the notification's requirements.
        /// </remarks>
        /// <exception cref="Exception">Catches and suppresses any exception, returning the original HTML content if an error occurs during the replacement process.</exception>
        private static string ReemplazarMarcadoresEspecificosPlantillaHTML_AnulacionDevolucionPedido(string htmlBodyEmail, InfoColaNotificaciones p360notificacion)
        {
            try
            {
                // Obtiene el código de pedido desde el objeto p360notificacion
                int codigoPedido = int.Parse(p360notificacion.ReferenceEventId);
                // Llama al método para obtener los datos del pedido desde la base de datos
                ClasesObjetosDBP360 datosPedido = ModuleCapaAccesoDatos.GetDatosPedidoByCodPedidoDB(codigoPedido);
                if (datosPedido != null)
                {
                    StringBuilder sb = new StringBuilder(htmlBodyEmail);
                    // Reemplazar otros marcadores en el HTML
                    sb.Replace("[CodPedido]", datosPedido.CodPedido.ToString())
                      .Replace("[Destinatario]", datosPedido.Colaborador)
                      .Replace("[Cliente]", datosPedido.Cliente)
                      .Replace("[CategoriaEspecialidad]", datosPedido.CategoriaCliente)
                      .Replace("[Direccion]", datosPedido.Direccion)
                      .Replace("[Fecha_pedido]", datosPedido.FechaPedido.ToString("dd/MMMM/yyyy hh:mm tt", new CultureInfo("es-ES")))
                      .Replace("[Fecha_acordada]", datosPedido.FechaEntregaPedido.ToString("dd/MMMM/yyyy hh:mm tt", new CultureInfo("es-ES")))
                      .Replace("[Ciudad]", datosPedido.Ciudad)
                      .Replace("[Contacto]", datosPedido.NombreContacto)
                      .Replace("[Observaciones]", datosPedido.Observaciones)
                      .Replace("[Motivo_devolucion_cancelacion]", datosPedido.MotivoEstadoPedido)
                      .Replace("[Despachador_pedido]", datosPedido.DespachadorPedido)
                      .Replace("[Unidades]", datosPedido.PedidoUnidadesReales.ToString())
                      .Replace("[Bonificaciones]", datosPedido.PedidoUnidadesBonificadas.ToString())
                      .Replace("[Total_solicitados]", (datosPedido.PedidoUnidadesReales + datosPedido.PedidoUnidadesBonificadas).ToString())
                      .Replace("[Unidades_despachadas]", datosPedido.UnidadesRealesDespachadas.ToString())
                      .Replace("[Bonificaciones_despachadas]", datosPedido.UnidadesBonificadasDespachadas.ToString())
                      .Replace("[Total_despachados]", (datosPedido.UnidadesRealesDespachadas + datosPedido.UnidadesBonificadasDespachadas).ToString())
                      .Replace("[Saldo_unidades]", datosPedido.SaldoUnidades.ToString())
                      .Replace("[Saldo_bonificaciones]", datosPedido.SaldoBonificaciones.ToString())
                      .Replace("[Total_saldo]", (datosPedido.SaldoUnidades + datosPedido.SaldoBonificaciones).ToString());
                    // Devuelve la cadena modificada
                    return sb.ToString();
                }
                else
                {
                    Console.WriteLine($"No se encontraron datos para el código de pedido {codigoPedido} proporcionado.");
                    return htmlBodyEmail;
                }
            }
            catch (Exception ex)
            {
                // Considera registrar el error
                Console.WriteLine($"Error al reemplazar marcadores en la plantilla: {ex.Message}");
                return htmlBodyEmail;
            }
        }

        private static string GenerarTAGImagenEstaticaMapaHTML(string lat, string lon, string API_KEY)
        {
            string htmlResultado = "";
            try
            {
                string baseUrl = "https://www.google.com/maps?q=";
                string baseImageUrl = "https://maps.googleapis.com/maps/api/staticmap?";
                string iconUbicacionAccionComercial = "https://upload.wikimedia.org/wikipedia/commons/f/f9/Map-icon.svg";
                String zoomMapa = "17";
                string tamanioImagenMapa = "600x300";
                // Construir la URL del enlace de Google Maps
                string enlaceMapa = $"{baseUrl}{lat},{lon}";

                if (string.IsNullOrWhiteSpace(API_KEY))
                {
                    return $"<a href=\"{enlaceMapa}\" target=\"_blank\">Ver ubicación en Google Maps</a>";
                }

                // Construir la URL de la imagen del mapa estático de Google Maps
                string srcImagenMapa = $"{baseImageUrl}center={lat},{lon}&zoom={zoomMapa}&size={tamanioImagenMapa}&markers=icon:{iconUbicacionAccionComercial}|{lat},{lon}&key={API_KEY}";
                // Construir el HTML con el enlace y la imagen
                htmlResultado = $"<a href=\"{enlaceMapa}\" target=\"_blank\">" +
                                       $"<img src=\"{srcImagenMapa}\" alt=\"Ubicación en Mapa con Marcador Personalizado\">" +
                                       "</a>";
                return htmlResultado;
            }
            catch
            {
                return htmlResultado;
            }
        }

        /// <summary>
        /// Retrieves the current date formatted as "yyyy-MM-dd".
        /// </summary>
        /// <returns>A string representation of the current date in "yyyy-MM-dd" format.</returns>
        /// <remarks>
        /// This method is designed to provide a standardized date format across the application, specifically tailored for Pharma360's requirements.
        /// The format "yyyy-MM-dd" ensures compatibility with international date formats and is commonly used in database and API integrations.
        /// </remarks>
        private static string ObtenerFechaActualP360()
        {
            // Implementa la lógica para obtener la fecha actual según tus requisitos
            return DateTime.Now.ToString("dd MMMM yyyy hh:mm tt");
        }

        /// <summary>
        /// Envía un reporte por correo electrónico con un archivo adjunto. El correo se personaliza basándose en los parámetros proporcionados y la información contenida en el objeto p360notificacion.
        /// </summary>
        /// <param name="attachmentPath">Ruta del archivo adjunto que se incluirá en el correo electrónico. No puede estar vacío.</param>
        /// <param name="reportName">Nombre del reporte para personalización del correo.</param>
        /// <param name="emailSubject">Asunto del correo electrónico. Se busca una clave de recurso y se personaliza basado en el reporte y el destinatario.</param>
        /// <param name="emailBodyKey">Clave para buscar el cuerpo del correo en los recursos. Se personaliza similar al asunto.</param>
        /// <param name="p360notificacion">Objeto conteniendo información del destinatario y detalles adicionales necesarios para el envío del correo.</param>
        /// <param name="reportSendMailCopySupervisor">Indica si se debe enviar una copia del correo al supervisor del destinatario.</param>
        /// <returns>Una tarea que representa la operación asincrónica de envío de correo.</returns>
        /// <exception cref="ArgumentNullException">Se lanza cuando el camino del archivo adjunto o el correo electrónico del destinatario están vacíos, o si el correo electrónico del destinatario no es válido.</exception>
        /// <remarks>
        /// Asegura que las constantes y configuraciones necesarias para la operación SMTP estén definidas en labConstants.
        /// La función puede lanzar excepciones adicionales relacionadas con la operación de red o fallos en el envío de correo, que deben ser manejadas por el llamador.
        /// </remarks>
        public async Task SendReportbyEmailWithAttachmentAsync(string attachmentPath, string emailSubject, string emailBodyKey, InfoColaNotificaciones p360notificacion, Boolean reportSendMailCopySupervisor)
        {
            if (string.IsNullOrEmpty(attachmentPath))
            {
                throw new ArgumentNullException(nameof(attachmentPath));
            }
            if (string.IsNullOrEmpty(p360notificacion.EmailColab))
            {
                throw new ArgumentNullException(nameof(p360notificacion.EmailColab));
            }
            if (!IsValidEmail(p360notificacion.EmailColab))
            {
                throw new ArgumentNullException(nameof(p360notificacion.EmailColab));
            }
            int colaNotificacionId = 0;
            StringBuilder destinatariosInfo = new StringBuilder();
            string emailSubjectEmail;
            try
            {
                colaNotificacionId = p360notificacion.ColaNotificacionId;
                emailSubjectEmail = emailSubject.Replace("[COLABORADOR_RECIBE]", p360notificacion.NameColab);
                emailSubjectEmail = emailSubjectEmail.Replace("[REPORT_NAME]", p360notificacion.ReportName);
                // Configure email settings and create message
                using (var message = new MailMessage(labConstants.SenderEmail, p360notificacion.EmailColab))
                using (var attachment = new Attachment(attachmentPath))
                {
                    // Inicio: Setea los destinatarios en función de la configuración de cada tipo de reporte.
                    if (reportSendMailCopySupervisor)
                    {
                        message.CC.Add(p360notificacion.EmailSup);
                        accion = $"Report: '{attachmentPath}' Cid=('{colaNotificacionId}') --> Sent to: {p360notificacion.EmailColab} with copy to: {p360notificacion.EmailSup}";
                    }
                    else
                    {
                        accion = $"Report: '{attachmentPath}' Cid=('{colaNotificacionId}') --> Sent to: {p360notificacion.EmailColab}";
                    }
                    // Envía a contactos adicionales asociados con el reporte y el evento especifico evento(si es que están configurados)
                    List<DatosContactosNotificaciones> contactosNotificaciones = notificationDeliveryStore.GetAdditionalContacts(p360notificacion.ReportId, p360notificacion.ReferenceEventId);
                    if (contactosNotificaciones.Count > 0)
                    {
                        foreach (var contacto in from contacto in contactosNotificaciones
                                                 where IsValidEmail(contacto.Email)// Asume que tienes un método para validar el correo electrónico
                                                 select contacto)
                        {
                            message.CC.Add(contacto.Email);
                            // Añadir la información del contacto a la cadena
                            if (destinatariosInfo.Length > 0) destinatariosInfo.Append(" / ");// Separa los contactos con " / " si ya hay contenido
                            destinatariosInfo.Append($"{contacto.Email} ({contacto.Nombre})");
                        }
                        accion = $"Report: '{attachmentPath}' Cid=('{colaNotificacionId}') --> Sent to: {p360notificacion.EmailColab} with aditional copy to: {destinatariosInfo}";
                    }
                    // Fin: Setea los destinatarios en función de la configuración de cada tipo de reporte.
                    // set the message body as HTML
                    message.IsBodyHtml = true;
                    message.Subject = emailSubjectEmail;
                    message.Body = ConstruirCuerpoEmailPlantillaHTML(p360notificacion, labConstants, emailBodyKey);
                    // add attachment
                    message.Attachments.Add(attachment);
                    await emailTransport.SendAsync(message);
                    Console.WriteLine(accion);
                    notificationDeliveryStore.Log(accion);
                }
                // Actualizacion de bandera de envio de notificacion
                notificationDeliveryStore.MarkSent(colaNotificacionId);
            }
            catch (SmtpFailedRecipientException ex)
            {
                // Error específico para un destinatario fallido
                accion = $"Cid=['{colaNotificacionId}']. Error sending email to {ex.FailedRecipient}. Código de error:{ex.StatusCode}. Detalle error: {ex.Message}";
                Console.WriteLine(accion);
                notificationDeliveryStore.Log(accion);
            }
            catch (SmtpException ex)
            {
                // Error general de SMTP
                accion = $"Cid=['{colaNotificacionId}']. Código de error: {ex.StatusCode}. Detalle error: {ex.Message}";
                Console.WriteLine(accion);
                notificationDeliveryStore.Log(accion);
            }
            catch (ConfigurationErrorsException ex)
            {
                // Error en la configuración de la aplicación
                accion = $"Cid=('{colaNotificacionId}'). Error sending email: Configuration error: {ex.Message}";
                Console.WriteLine(accion);
                notificationDeliveryStore.Log(accion);
            }
            catch (FormatException ex)
            {
                // Error de formato de datos
                accion = $"Cid=('{colaNotificacionId}'). Error sending email: Format error: {ex.Message}";
                Console.WriteLine(accion);
                notificationDeliveryStore.Log(accion);
            }
            catch (Exception ex)
            {
                accion = $"Cid=['{colaNotificacionId}']. Error sending email: {ex.Message}";
                Console.WriteLine(accion);
                notificationDeliveryStore.Log(accion);
            }
        }

        /// <summary>
        /// Envía un correo electrónico personalizado relacionado con un reporte sin incluir un archivo adjunto. 
        /// La personalización se realiza a través de parámetros y la información del objeto p360notificacion.
        /// </summary>
        /// <param name="emailSubject">El asunto del correo electrónico, que se personaliza mediante reemplazo de tokens específicos.</param>
        /// <param name="emailBodyKey">La clave para buscar el cuerpo del correo electrónico dentro de un conjunto de recursos, permitiendo la personalización del contenido del mensaje.</param>
        /// <param name="p360notificacion">Objeto que contiene información del destinatario y otros detalles necesarios para la personalización y envío del correo electrónico.</param>
        /// <param name="reportSendMailCopySupervisor">Booleano que indica si se debe enviar una copia del correo al supervisor del destinatario.</param>
        /// <returns>Una tarea que representa la operación asincrónica de envío del correo electrónico.</returns>
        /// <exception cref="ArgumentNullException">Lanza una excepción si el correo electrónico del destinatario está vacío o es inválido.</exception>
        /// <remarks>
        /// El método valida la dirección de correo electrónico del destinatario antes de proceder con la configuración y envío del correo electrónico.
        /// Utiliza ResourceManager para acceder a las plantillas de correo electrónico y personaliza el mensaje usando información del objeto p360notificacion.
        /// La función intenta enviar el correo y registra la acción, gestionando cualquier excepción que pueda ocurrir durante el proceso.
        /// </remarks>
        public async Task SendReportbyEmailWithOutAttachmentAsync(string emailSubject, string emailBodyKey, InfoColaNotificaciones p360notificacion, Boolean reportSendMailCopySupervisor)
        {
            if (string.IsNullOrEmpty(p360notificacion.EmailColab))
            {
                throw new ArgumentNullException(nameof(p360notificacion.EmailColab));
            }
            if (!IsValidEmail(p360notificacion.EmailColab))
            {
                throw new ArgumentNullException(nameof(p360notificacion.EmailColab));
            }
            int colaNotificacionId = 0;
            StringBuilder destinatariosInfo = new StringBuilder();
            string emailSubjectEmail;
            try
            {
                colaNotificacionId = p360notificacion.ColaNotificacionId;
                emailSubjectEmail = emailSubject.Replace("[COLABORADOR_RECIBE]", p360notificacion.NameColab);
                emailSubjectEmail = emailSubjectEmail.Replace("[REPORT_NAME]", p360notificacion.ReportName);
                // Configure email settings and create message
                using (var message = new MailMessage(labConstants.SenderEmail, p360notificacion.EmailColab))
                {
                    // Inicio: Setea los destinatarios en función de la configuración de cada tipo de reporte.
                    if (reportSendMailCopySupervisor)
                    {
                        message.CC.Add(p360notificacion.EmailSup);
                        accion = $"Report: '{p360notificacion.ReportName}' Cid=('{colaNotificacionId}') --> Sent to: {p360notificacion.EmailColab} with copy to: {p360notificacion.EmailSup}";
                    }
                    else
                    {
                        accion = $"Report: '{p360notificacion.ReportName}' Cid=('{colaNotificacionId}') --> Sent to: {p360notificacion.EmailColab}";
                    }
                    // Envía a contactos adicionales asociados con el reporte y el evento especifico evento(si es que están configurados)
                    List<DatosContactosNotificaciones> contactosNotificaciones = notificationDeliveryStore.GetAdditionalContacts(p360notificacion.ReportId, p360notificacion.ReferenceEventId);
                    if (contactosNotificaciones.Count > 0)
                    {
                        foreach (var contacto in from contacto in contactosNotificaciones
                                                 where IsValidEmail(contacto.Email)// Asume que tienes un método para validar el correo electrónico
                                                 select contacto)
                        {
                            message.CC.Add(contacto.Email);
                            // Añadir la información del contacto a la cadena
                            if (destinatariosInfo.Length > 0) destinatariosInfo.Append(" / ");// Separa los contactos con " / " si ya hay contenido
                            destinatariosInfo.Append($"{contacto.Email} ({contacto.Nombre})");
                        }
                        accion = $"Report: '{p360notificacion.ReportName}' Cid=('{colaNotificacionId}') --> Sent to: {p360notificacion.EmailColab} with aditional copy to: {destinatariosInfo}";
                    }
                    // Fin: Setea los destinatarios en función de la configuración de cada tipo de reporte.
                    // set the message body as HTML
                    message.IsBodyHtml = true;
                    message.Subject = emailSubjectEmail;
                    message.Body = ConstruirCuerpoEmailPlantillaHTML(p360notificacion, labConstants, emailBodyKey);
                    // Envia notificacion al administrador si es que es una visita y dicha visita es a 'Punto de contacto'
                    if (NotificaAdministrador)
                    {
                        message.Bcc.Add(labConstants.AdminEmail);
                        if (destinatariosInfo.Length > 0) destinatariosInfo.Append(" / ");// Separa los contactos con " / " si ya hay contenido
                        destinatariosInfo.Append($"{labConstants.AdminEmail} ({"Email administrador"})");
                        accion = accion + $". With extra-aditional Bcc copy to: {labConstants.AdminEmail}";
                    }
                    // add attachment
                    await emailTransport.SendAsync(message);
                    Console.WriteLine(accion);
                    notificationDeliveryStore.Log(accion);
                }
                // Actualizacion de bandera de envio de notificacion
                notificationDeliveryStore.MarkSent(colaNotificacionId);
                NotificaAdministrador = false;
            }
            catch (SmtpFailedRecipientException ex)
            {
                // Error específico para un destinatario fallido
                accion = $"Cid=['{colaNotificacionId}']. Error sending email to {ex.FailedRecipient}. Código de error:{ex.StatusCode}. Detalle error: {ex.Message}";
                Console.WriteLine(accion);
                notificationDeliveryStore.Log(accion);
            }
            catch (SmtpException ex)
            {
                // Error general de SMTP
                accion = $"Cid=['{colaNotificacionId}']. Código de error: {ex.StatusCode}. Detalle error: {ex.Message}";
                Console.WriteLine(accion);
                notificationDeliveryStore.Log(accion);
            }
            catch (Exception ex)
            {
                accion = $"Cid=['{colaNotificacionId}']. Error sending email: {ex.Message}";
                Console.WriteLine(accion);
                notificationDeliveryStore.Log(accion);
            }
        }

        /// <summary>
        /// Verifica si la cadena proporcionada es una dirección de correo electrónico válida.
        /// </summary>
        /// <param name="email">La dirección de correo electrónico a verificar.</param>
        /// <returns>Verdadero si la cadena es una dirección de correo electrónico válida; de lo contrario, falso.</returns>
        /// <remarks>
        /// Este método intenta crear una instancia de <see cref="MailAddress"/> con la cadena proporcionada.
        /// Si se crea la instancia sin lanzar una excepción, y la dirección de correo normalizada
        /// coincide con la entrada original, se considera que el correo electrónico es válido.
        /// Este método puede no ser el más eficiente para verificaciones de correo electrónico en
        /// un bucle o en operaciones de alto rendimiento debido al uso de manejo de excepciones.
        /// </remarks>
        private static bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Obtiene la fecha y hora actuales en el formato específico "yyyy-MM-dd HH:mm:ss.fff".
        /// </summary>
        /// <returns>Una cadena de texto que representa la fecha y hora actuales en el formato "yyyy-MM-dd HH:mm:ss.fff".</returns>
        /// <exception cref="Exception">Lanza una excepción si ocurre un error al obtener o formatear la fecha y hora actuales.</exception>
        /// <remarks>
        /// Este método utiliza <see cref="DateTime.Now"/> para obtener la fecha y hora actuales y las formatea
        /// en una cadena de texto según el formato especificado. Cualquier error durante la obtención o el formateo
        /// resultará en una excepción que incluye detalles del método que la originó.
        /// </remarks>
        string obtieneFechaActualP360()
        {
            try
            {
                DateTime fechaActual = DateTime.Now;
                string fechaActualString = string.Format("{0:yyyy-MM-dd HH:mm:ss.fff}", fechaActual);
                return fechaActualString;
            }
            catch (Exception ex)
            {
                // If an exception is thrown, re-throw it with additional information
                throw new Exception(ex.Message + " Method: " + System.Reflection.MethodBase.GetCurrentMethod().ToString());
            }
        }

        /// <summary>
        /// Obtiene el primer y último día del mes actual.
        /// </summary>
        /// <returns>
        /// Una tupla que contiene dos valores <see cref="DateTime"/>: el primero es el primer día del mes actual,
        /// y el segundo es el último día del mes actual.
        /// </returns>
        /// <remarks>
        /// Este método calcula dinámicamente el primer y último día del mes basado en la fecha y hora actuales.
        /// Se usa <see cref="DateTime.Now"/> para obtener la fecha actual, y a partir de ahí, se calculan el primer
        /// y último día del mes. Este método puede ser especialmente útil para generar informes mensuales o
        /// calcular rangos de fechas dentro del mes actual.
        /// </remarks>
        public (DateTime, DateTime) GetFirstAndLastDayOfMonth()
        {
            DateTime currentDate = DateTime.Now;
            DateTime firstDayOfMonth = new DateTime(currentDate.Year, currentDate.Month, 1);
            DateTime lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);
            return (firstDayOfMonth, lastDayOfMonth);
        }

        /// <summary>
        /// Exporta un reporte especificado a un archivo PDF en la ubicación de salida dada.
        /// </summary>
        /// <param name="outputPath">La ruta de archivo completa donde se guardará el PDF exportado.</param>
        /// <param name="report">El documento de reporte (<see cref="ReportDocument"/>) que será exportado.</param>
        /// <remarks>
        /// Configura las opciones de exportación del reporte para generar un PDF y lo guarda en el disco en la ubicación especificada.
        /// Registra la acción de exportación utilizando un módulo de acceso a datos.
        /// </remarks>
        /// <exception cref="InvalidCastException">Se lanza si ocurre un error al configurar las opciones de exportación del reporte.</exception>
        /// <exception cref="IOException">Se lanza si ocurre un error de E/S al intentar guardar el archivo PDF en el disco.</exception>
        public void ExportReportToPdf(string outputPath, ReportDocument report)
        {
            try
            {
                ExportOptions exportOptions = report.ExportOptions;
                exportOptions.ExportFormatType = ExportFormatType.PortableDocFormat;
                exportOptions.ExportDestinationType = ExportDestinationType.DiskFile;
                exportOptions.DestinationOptions = new DiskFileDestinationOptions
                {
                    DiskFileName = outputPath
                };

                accion = $"Iniciando exportación Crystal PDF. Archivo destino: '{outputPath}'";
                Console.WriteLine(accion);
                oModuleCapaAccesoDatos.RegistraLogConeccionyAccion(currentUsername, accion);

                report.Export();

                accion = $"Report: '{outputPath}' --> Generated and saved";
                Console.WriteLine(accion);
                oModuleCapaAccesoDatos.RegistraLogConeccionyAccion(currentUsername, accion);
            }
            catch (CrystalDecisions.CrystalReports.Engine.LogOnException ex)
            {
                accion =
                    "ERROR CRYSTAL LOGON AL EXPORTAR PDF. " +
                    "El fallo ocurrió en report.Export(), no necesariamente en la conexión .NET. " +
                    "Crystal Reports puede estar usando el proveedor OLE DB guardado dentro del .rpt. " +
                    "Validar en este equipo si existe Microsoft OLE DB Driver 18 x64 además de cualquier versión 19. " +
                    "Si el .rpt usa Provider=MSOLEDBSQL, la versión 19 puede exigir Encrypt=Mandatory/certificado válido por defecto. " +
                    "En servidores con certificado autofirmado puede fallar con 'No se pudo conectar con la base de datos'. " +
                    "Comparar contra un equipo funcional la existencia de msoledbsql.dll, msoledbsql19.dll y las versiones instaladas de Microsoft OLE DB Driver for SQL Server. " +
                    $"Archivo destino: '{outputPath}'. Detalle Crystal: '{ex.Message}'";

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(accion);
                Console.ResetColor();

                oModuleCapaAccesoDatos.RegistraLogConeccionyAccion(currentUsername, accion);

                Exception inner = ex.InnerException;
                int nivel = 1;

                while (inner != null)
                {
                    accion = $"Detalle interno Crystal LogOnException. Nivel='{nivel}', Tipo='{inner.GetType().FullName}', Mensaje='{inner.Message}'";
                    Console.WriteLine(accion);
                    oModuleCapaAccesoDatos.RegistraLogConeccionyAccion(currentUsername, accion);

                    inner = inner.InnerException;
                    nivel++;
                }

                throw;
            }
            catch (InvalidCastException e)
            {
                accion = $"Invalid cast exception occurred al exportar Crystal PDF. Archivo destino: '{outputPath}'. Detalle: {e.Message}";
                Console.WriteLine(accion);
                oModuleCapaAccesoDatos.RegistraLogConeccionyAccion(currentUsername, accion);

                throw;
            }
            catch (IOException e)
            {
                accion = $"I/O exception occurred al exportar Crystal PDF. Validar permisos/ruta de salida. Archivo destino: '{outputPath}'. Detalle: {e.Message}";
                Console.WriteLine(accion);
                oModuleCapaAccesoDatos.RegistraLogConeccionyAccion(currentUsername, accion);

                throw;
            }
            catch (Exception ex)
            {
                accion =
                    $"Error general al exportar Crystal PDF. Archivo destino: '{outputPath}'. " +
                    $"Tipo='{ex.GetType().FullName}', Mensaje='{ex.Message}'";

                Console.WriteLine(accion);
                oModuleCapaAccesoDatos.RegistraLogConeccionyAccion(currentUsername, accion);

                Exception inner = ex.InnerException;
                int nivel = 1;

                while (inner != null)
                {
                    accion = $"Detalle interno exportación Crystal. Nivel='{nivel}', Tipo='{inner.GetType().FullName}', Mensaje='{inner.Message}'";
                    Console.WriteLine(accion);
                    oModuleCapaAccesoDatos.RegistraLogConeccionyAccion(currentUsername, accion);

                    inner = inner.InnerException;
                    nivel++;
                }

                throw;
            }
        }

        public string generaTag(MemoryStream reporteEnMemoria, XtraReport report)
        {
            //Este metodo no se lo utiliza pero se lo deja por que es importantisimo para usarlo en algun momento para:
            // 1) Incrustar una imagen en un html/
            // 2) Grabar la imagen en un directorio.
            string htmlImageTag;
            using (reporteEnMemoria)
            {
                // Ajusta las opciones de exportación si es necesario
                ImageExportOptions options = new ImageExportOptions(System.Drawing.Imaging.ImageFormat.Png)
                {
                    ExportMode = ImageExportMode.SingleFile, // O SingleFilePageByPage si necesitas varias páginas
                    Resolution = 96 // Ajusta esto según tus necesidades
                };
                report.ExportToImage(reporteEnMemoria, options);

                // 1) Ahora el MemoryStream 'ms' contiene tu imagen. Procede a convertirlo a Base64 para incrustarlo en HTML.
                string base64String = Convert.ToBase64String(reporteEnMemoria.ToArray());
                htmlImageTag = $"<img src=\"data:image/png;base64,{base64String}\" alt=\"Reporte\" />";

                // 2) grabar la imagen en un rirectorio

                string reportPathOutput = "c:/temp/directorio/";
                string imagePath = Path.Combine(reportPathOutput, "NombreDeLaImagen.png");
                File.WriteAllBytes(imagePath, reporteEnMemoria.ToArray()); // Usa el mismo MemoryStream para guardar la imagen en disco
            }
            return htmlImageTag;
        }
    }
}
