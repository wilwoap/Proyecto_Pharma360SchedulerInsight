using DevExpress.XtraReports.UI;
using SchedulerP360Insight.DS_SP_GetFactReporteSELLINPeriodosTableAdapters;
using SchedulerP360Insight.Modulos;
using System;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Globalization;
using System.Reflection;

namespace SchedulerP360Insight.P360Reports
{
    public partial class XtraReport_Estategico1 : XtraReport
    {
        DateTime v_fechaCorteReporte = default(DateTime);
        int mesestranscurridos;
        int mesesresantes = default(int);

        DS_SP_GetFactReporteSELLINPeriodos oDS_SP_GetFactReporteSELLINPeriodos_MAT = new DS_SP_GetFactReporteSELLINPeriodos();
        DS_SP_GetFactReporteSELLINPeriodos oDS_SP_GetFactReporteSELLINPeriodos_MES = new DS_SP_GetFactReporteSELLINPeriodos();
        DS_SP_GetFactReporteSELLINPeriodos oDS_SP_GetFactReporteSELLINPeriodos_YTD = new DS_SP_GetFactReporteSELLINPeriodos();
        DS_SP_GetFactReporteSELLINPeriodos oDS_SP_GetFactReporteSELLINPeriodos_MAT_xRegionxLineaxFecha = new DS_SP_GetFactReporteSELLINPeriodos();
        DS_SP_GetFactReporteSELLINPeriodos oDS_SP_GetFactReporteSELLINPeriodos_YTD_xLineaxRegionxFecha = new DS_SP_GetFactReporteSELLINPeriodos();
        DS_SP_GetFactReporteSELLINPeriodos oDS_SP_GetFactReporteSELLINPeriodos_YTG_xLineaxRegion = new DS_SP_GetFactReporteSELLINPeriodos();
        DS_SP_GetFactReporteSELLINPeriodos oDS_SP_GetFactReporteSELLINPeriodos_YTG_xfecha = new DS_SP_GetFactReporteSELLINPeriodos();

        DataTable oDataTable;

        public XtraReport_Estategico1()
        {
            InitializeComponent();
        }

        private void XtraReport_Estategico1_BeforePrint(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                funcionCargadatosreporte();
                funcionSeteaTitulosDinamicosCampos(v_fechaCorteReporte);
                // Puedes agregar aquí otras funciones (como colores, cabecera, etc.)
            }
            catch (Exception ex)
            {
                // Si ocurre algún error, se notifica al usuario y se cancela la impresión
                System.Windows.Forms.MessageBox.Show("Error en el reporte: " + ex.Message,
                                                      "Error",
                                                      System.Windows.Forms.MessageBoxButtons.OK,
                                                      System.Windows.Forms.MessageBoxIcon.Error);
                e.Cancel = true;
                this.Dispose();
            }
        }

        private void funcionCargadatosreporte()
        {
            try
            {
                // Crear la tabla de filtros (por ejemplo, para enviar parámetros a la consulta)
                DataTable tablaFiltros = new Module_FiltrosReportes().CrearTablaFiltroReportes();
                DataRow row = tablaFiltros.NewRow();
                row["token"] = "1234567";
                tablaFiltros.Rows.Add(row);

                // Asignar la fecha de corte (aquí se usa la fecha actual)
                v_fechaCorteReporte = DateTime.Now;

                // Se instancia el TableAdapter y se asigna la cadena de conexión dinámica
                SP_GetFactReporteSELLINPeriodosTableAdapter tableAdapter = new SP_GetFactReporteSELLINPeriodosTableAdapter();
                tableAdapter.Connection.ConnectionString = AppConfig.ConnectionString;

                // Llenar los DataSets con los parámetros correspondientes
                tableAdapter.Fill(oDS_SP_GetFactReporteSELLINPeriodos_MAT_xRegionxLineaxFecha.SP_GetFactReporteSELLINPeriodos,
                                  v_fechaCorteReporte, "MAT", string.Empty, "linea,region,fecha_ord", tablaFiltros);
                tableAdapter.Fill(oDS_SP_GetFactReporteSELLINPeriodos_YTD_xLineaxRegionxFecha.SP_GetFactReporteSELLINPeriodos,
                                  v_fechaCorteReporte, "YTD", string.Empty, "linea,region", tablaFiltros);
                tableAdapter.Fill(oDS_SP_GetFactReporteSELLINPeriodos_YTG_xLineaxRegion.SP_GetFactReporteSELLINPeriodos,
                                  v_fechaCorteReporte, "YTG", string.Empty, "linea,region", tablaFiltros);

                // Asignar DataSources a los controles según la existencia de datos
                if (oDS_SP_GetFactReporteSELLINPeriodos_MAT_xRegionxLineaxFecha.Tables[0].Rows.Count > 0)
                {
                    XrPivotGrid_VentaDirecta_MAT_Valores.DataSource = oDS_SP_GetFactReporteSELLINPeriodos_MAT_xRegionxLineaxFecha;
                    XrPivotGrid_VentaDirecta_MAT_Unidades.DataSource = oDS_SP_GetFactReporteSELLINPeriodos_MAT_xRegionxLineaxFecha;
                    XrChart_VentaDirectaMes_lineal_Valores.DataSource = oDS_SP_GetFactReporteSELLINPeriodos_MAT_xRegionxLineaxFecha;
                    XrChart_VentaDirectaMes_lineal_Unidades.DataSource = oDS_SP_GetFactReporteSELLINPeriodos_MAT_xRegionxLineaxFecha;
                }

                if (oDS_SP_GetFactReporteSELLINPeriodos_YTG_xLineaxRegion.Tables[0].Rows.Count > 0)
                {
                    XrPivotGrid_VentaDirecta_YTG_Linea_Valores.DataSource = oDS_SP_GetFactReporteSELLINPeriodos_YTG_xLineaxRegion;
                    XrPivotGrid_VentaDirecta_YTG_Linea_Unidades.DataSource = oDS_SP_GetFactReporteSELLINPeriodos_YTG_xLineaxRegion;
                    XrPivotGrid_VentaDirecta_YTG_Region_Valores.DataSource = oDS_SP_GetFactReporteSELLINPeriodos_YTG_xLineaxRegion;
                    XrPivotGrid_VentaDirecta_YTG_Region_Unidades.DataSource = oDS_SP_GetFactReporteSELLINPeriodos_YTG_xLineaxRegion;
                    XrChart_VentaDirecta_YTG_Linea_Valores.DataSource = oDS_SP_GetFactReporteSELLINPeriodos_YTG_xLineaxRegion;
                    XrChart_VentaDirecta_YTG_Region_Valores.DataSource = oDS_SP_GetFactReporteSELLINPeriodos_YTG_xLineaxRegion;
                    XrChart_VentaDirecta_YTG_Linea_Unidades.DataSource = oDS_SP_GetFactReporteSELLINPeriodos_YTG_xLineaxRegion;
                    XrChart_VentaDirecta_YTG_Region_Unidades.DataSource = oDS_SP_GetFactReporteSELLINPeriodos_YTG_xLineaxRegion;
                }

                if (oDS_SP_GetFactReporteSELLINPeriodos_YTD_xLineaxRegionxFecha.Tables[0].Rows.Count > 0)
                {
                    XrPivotGrid_VentaDirecta_YTD1_Valores.DataSource = oDS_SP_GetFactReporteSELLINPeriodos_YTD_xLineaxRegionxFecha;
                    XrPivotGrid_VentaDirecta_YTD1_Unidades.DataSource = oDS_SP_GetFactReporteSELLINPeriodos_YTD_xLineaxRegionxFecha;
                    XrPivotGrid_VentaDirecta_MES_Valores.DataSource = oDS_SP_GetFactReporteSELLINPeriodos_YTD_xLineaxRegionxFecha;
                    XrPivotGrid_VentaDirecta_MES_Unidades.DataSource = oDS_SP_GetFactReporteSELLINPeriodos_YTD_xLineaxRegionxFecha;
                }
            }
            catch (SqlException exSql)
            {
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
                throw new Exception(ex.Message + MethodBase.GetCurrentMethod().ToString(), ex);
            }
        }

        private void funcionSeteaTitulosDinamicosCampos(DateTime p_fechaCorteReporte)
        {
            int anioActualConsulta;
            int anioAnteriorConsulta;
            string v_mesActual = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(p_fechaCorteReporte.Month).ToUpper();

            try
            {
                XrLabelFechaCorteReporte.Text = $"{CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(p_fechaCorteReporte.Month)} del {p_fechaCorteReporte.Year}";
                anioActualConsulta = p_fechaCorteReporte.Year;
                anioAnteriorConsulta = p_fechaCorteReporte.Year - 1;
                XrRichTextTituloXrPivotGrid_VentaDirecta_YTG_LINEA_VALORES.Text += anioActualConsulta.ToString();
                XrRichTextTituloXrPivotGrid_VentaDirecta_YTG_REGION_Valores.Text += anioActualConsulta.ToString();
                XrRichTextTituloXrPivotGrid_VentaDirecta_YTG_LINEA_UNIDADES.Text += anioActualConsulta.ToString();
                XrRichTextTituloXrPivotGrid_VentaDirecta_YTG_REGION_Unidades.Text += anioActualConsulta.ToString();
                XrPivotfield_PlanTodoAnio_xLinea_Valores.Caption = $"Plan {anioActualConsulta}";
                XrPivotfield_PlanTodoAnio_xLinea_Unidades.Caption = $"Plan {anioActualConsulta}";
                XrPivotfield_PlanTodoAnio_xRegion_Valores.Caption = $"Plan {anioActualConsulta}";
            }
            catch (Exception ex)
            {
                // Si es necesario, se puede registrar el error o mostrar un mensaje
            }
        }
    }
}
