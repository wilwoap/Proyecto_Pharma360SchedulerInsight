using DevExpress.XtraReports.UI;
using SchedulerP360Insight.DS_V_REPORTE_HISTORICO_DESPACHOPEDIDOSTableAdapters;
using SchedulerP360Insight.Modulos;
using System;
using System.Data.SqlClient;
using System.Drawing;

namespace SchedulerP360Insight.P360Reports
{
    public partial class XtraReportDespachosPedidosP360 : XtraReport
    {
        private readonly LaboratoryConstants labConstants;
        DS_V_REPORTE_HISTORICO_DESPACHOPEDIDOS oDS_V_REPORTE_HISTORICO_DESPACHOPEDIDOS = new DS_V_REPORTE_HISTORICO_DESPACHOPEDIDOS();
        int v_cod_pedido;

        public XtraReportDespachosPedidosP360()
            : this(null)
        {
        }

        public XtraReportDespachosPedidosP360(
            LaboratoryConstants labConstants)
        {
            this.labConstants = labConstants;
            InitializeComponent();
        }

        /// <summary>
        /// Método que carga los datos del reporte utilizando el parámetro "p_cod_pedido" y
        /// asigna la cadena de conexión dinámica al TableAdapter.
        /// </summary>
        private void funcionCargadatosreporte()
        {
            try
            {
                // Obtener el parámetro del reporte y convertirlo a entero
                v_cod_pedido = (int)Parameters["p_cod_pedido"].Value;

                // Crear el TableAdapter
                V_REPORTE_HISTORICO_DESPACHOPEDIDOSTableAdapter adapter = new V_REPORTE_HISTORICO_DESPACHOPEDIDOSTableAdapter();

                // Asignar la cadena de conexión dinámica (obtenida en AppConfig)
                adapter.Connection.ConnectionString = AppConfig.ConnectionString;

                // Llenar el dataset utilizando el código del pedido
                adapter.FillByCodPedido(oDS_V_REPORTE_HISTORICO_DESPACHOPEDIDOS.V_REPORTE_HISTORICO_DESPACHOPEDIDOS, v_cod_pedido);

                // Asignar el DataSource del reporte
                this.DataSource = oDS_V_REPORTE_HISTORICO_DESPACHOPEDIDOS;

                // Configurar los valores generales de la empresa en el reporte
                ConfiguraValoresGeneralesEmpresa();
            }
            catch (SqlException exSql)
            {
                // Disponer recursos y lanzar excepción de negocio
                this.Dispose();
                throw new BusinessP360Exception(exSql.Number.ToString(), exSql.Message, exSql.Procedure);
            }
            catch (BusinessP360Exception be)
            {
                this.Dispose();
                throw;
            }
            catch (Exception ex)
            {
                this.Dispose();
                throw new Exception(ex.Message + System.Reflection.MethodBase.GetCurrentMethod().ToString(), ex);
            }
        }

        /// <summary>
        /// Evento BeforePrint del reporte: se invoca antes de imprimir el reporte.
        /// Llama a la función de carga de datos.
        /// </summary>
        private void XtraReportDespachosPedidosP360_BeforePrint(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                funcionCargadatosreporte();
            }
            catch (Exception ex)
            {
                // Aquí podrías agregar notificación al usuario o logueo del error.
                System.Windows.Forms.MessageBox.Show("Error al cargar el reporte: " + ex.Message,
                                                     "Error",
                                                     System.Windows.Forms.MessageBoxButtons.OK,
                                                     System.Windows.Forms.MessageBoxIcon.Error);
                e.Cancel = true;
                this.Dispose();
            }
        }

        /// <summary>
        /// Configura los valores generales de la empresa en el reporte.
        /// </summary>
        private void ConfiguraValoresGeneralesEmpresa()
        {
            LaboratoryConstants settings =
                labConstants ?? AppConfig.LaboratoryConstants;
            vendorName.Text = settings.LaboratoryName;
            vendorAddress.Text = settings.Pharma360EmpresaDireccion;
            vendorCity.Text = settings.Pharma360EmpresaCiudad;
            vendorCountry.Text = settings.Pharma360EmpresaPais;
            vendorWebsite.Text = settings.Pharma360EmpresaSitioWeb;
            vendorEmail.Text = settings.Pharma360EmpresaEmailContacto;
            vendorPhone.Text = settings.Pharma360EmpresaTelefonoContacto;
            vendorLogo.ImageUrl = settings.Pharma360UrlLogo;
        }
    }
}
