using DevExpress.XtraReports.UI;
using SchedulerP360Insight.DS_V_REPORTE_HISTORICO_PEDIDOSTableAdapters;
using SchedulerP360Insight.Modulos;
using System;
using System.Data.SqlClient;
using System.Drawing.Printing;
using System.Reflection;
using System.Windows.Forms;

namespace SchedulerP360Insight.P360Reports
{
    public partial class XtraReportPedidosP360 : XtraReport
    {
        private readonly LaboratoryConstants labConstants;
        private DS_V_REPORTE_HISTORICO_PEDIDOS oDS_V_REPORTE_HISTORICO_PEDIDOS = new DS_V_REPORTE_HISTORICO_PEDIDOS();
        private int v_cod_pedido;

        public XtraReportPedidosP360()
            : this(null)
        {
        }

        public XtraReportPedidosP360(LaboratoryConstants labConstants)
        {
            this.labConstants = labConstants;
            InitializeComponent();
        }

        /// <summary>
        /// Carga los datos del reporte utilizando el parámetro "p_cod_pedido" y la cadena de conexión dinámica.
        /// </summary>
        private void funcionCargadatosreporte()
        {
            try
            {
                // Validar que el parámetro exista
                if (Parameters["p_cod_pedido"].Value == null)
                {
                    throw new BusinessP360Exception("0", "El parámetro 'p_cod_pedido' es nulo.", MethodBase.GetCurrentMethod().Name);
                }

                // Convertir el parámetro a entero
                v_cod_pedido = Convert.ToInt32(Parameters["p_cod_pedido"].Value);

                // Crear el TableAdapter y asignarle la cadena de conexión dinámica
                V_REPORTE_HISTORICO_PEDIDOSTableAdapter oV_REPORTE_HISTORICO_PEDIDOSTableAdapter = new V_REPORTE_HISTORICO_PEDIDOSTableAdapter();
                oV_REPORTE_HISTORICO_PEDIDOSTableAdapter.Connection.ConnectionString = AppConfig.ConnectionString;

                // Llenar el DataSet usando el código del pedido
                oV_REPORTE_HISTORICO_PEDIDOSTableAdapter.FillByCodPedido(oDS_V_REPORTE_HISTORICO_PEDIDOS.V_REPORTE_HISTORICO_PEDIDOS, v_cod_pedido);

                // Asignar el DataSource del reporte
                this.DataSource = oDS_V_REPORTE_HISTORICO_PEDIDOS;

                // Configurar los valores generales de la empresa
                ConfiguraValoresGeneralesEmpresa();
            }
            catch (SqlException exSql)
            {
                string errorMsg = $"Error SQL (Número {exSql.Number}) en {MethodBase.GetCurrentMethod().Name}: {exSql.Message}";
                // Aquí puedes registrar el error (por ejemplo, en un log) antes de lanzar la excepción
                this.Dispose();
                throw new BusinessP360Exception(exSql.Number.ToString(), errorMsg, exSql.Procedure);
            }
            catch (BusinessP360Exception be)
            {
                // Registrar o manejar el error según tu lógica de negocio
                this.Dispose();
                throw;
            }
            catch (Exception ex)
            {
                string errorMsg = $"Excepción en {MethodBase.GetCurrentMethod().Name}: {ex.Message}";
                this.Dispose();
                throw new Exception(errorMsg, ex);
            }
        }

        /// <summary>
        /// Evento BeforePrint: se invoca justo antes de que se imprima el reporte.
        /// </summary>
        private void XtraReportPedidosP360_BeforePrint(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                // Cargar los datos para el reporte
                funcionCargadatosreporte();
            }
            catch (Exception ex)
            {
                // Manejo de errores: se muestra un mensaje de error y se cancela la impresión
                MessageBox.Show("Error al cargar los datos para el reporte:\n" + ex.Message,
                                "Error en Reporte",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                this.DataSource = null;
                e.Cancel = true;
            }
        }

        /// <summary>
        /// Configura los valores generales de la empresa en el reporte.
        /// </summary>
        private void ConfiguraValoresGeneralesEmpresa()
        {
            try
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
            catch (Exception ex)
            {
                throw new Exception("Error configurando los valores generales de la empresa: " + ex.Message, ex);
            }
        }
    }
}
