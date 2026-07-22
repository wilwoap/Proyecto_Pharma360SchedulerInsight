using DevExpress.XtraReports.UI;
using SchedulerP360Insight.DS_SP_GetFactReporteSELLINPeriodosTableAdapters;
using SchedulerP360Insight.Modulos;
using System;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Reflection;

namespace SchedulerP360Insight.P360Reports
{
    public partial class XtraReport1 : XtraReport
    {
        public XtraReport1()
        {
            InitializeComponent();
        }

        private void XtraReport1_BeforePrint(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                funcionCargadatosreporte();
            }
            catch (Exception ex)
            {
                e.Cancel = true;
                throw new InvalidOperationException(
                    "Error no interactivo al cargar los datos del reporte.",
                    ex);
            }
        }

        private void funcionCargadatosreporte()
        {
            try
            {
                // Crear la tabla de filtros mediante el módulo correspondiente
                DataTable tablaFiltros = new Module_FiltrosReportes().CrearTablaFiltroReportes();

                // Agregar un registro de filtro (por ejemplo, el token)
                DataRow row = tablaFiltros.NewRow();
                row["token"] = "1234567";
                tablaFiltros.Rows.Add(row);

                // Crear el DataSet y el TableAdapter
                DS_SP_GetFactReporteSELLINPeriodos dataSet = new DS_SP_GetFactReporteSELLINPeriodos();
                SP_GetFactReporteSELLINPeriodosTableAdapter tableAdapter = new SP_GetFactReporteSELLINPeriodosTableAdapter();

                // Sobrescribir la cadena de conexión del TableAdapter con la cadena dinámica
                tableAdapter.Connection.ConnectionString = AppConfig.ConnectionString;

                // Obtener la fecha de corte (aquí se usa la fecha actual)
                DateTime fechaCorte = DateTime.Now;

                // Llenar el DataSet con los datos del reporte
                tableAdapter.Fill(dataSet.SP_GetFactReporteSELLINPeriodos,
                                  fechaCorte,
                                  "MAT",
                                  string.Empty,
                                  "linea,region,fecha_ord",
                                  tablaFiltros);

                // Asignar el DataSource del reporte
                this.DataSource = dataSet;
            }
            catch (SqlException exSql)
            {
                string errorMsg = $"Error SQL (Código {exSql.Number}) en {MethodBase.GetCurrentMethod().Name}: {exSql.Message}";
                // Aquí podrías registrar el error en un log, si es necesario
                this.Dispose();
                throw new BusinessP360Exception(exSql.Number.ToString(), errorMsg, exSql.Procedure);
            }
            catch (BusinessP360Exception)
            {
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
    }
}
