using SchedulerP360Insight.DS_SP_GetFactAuditRXReporteGraficoTableAdapters;
using SchedulerP360Insight.DS_SP_GetFactAuditRXReporteStandardTableAdapters;
using SchedulerP360Insight.Modulos;
using System;
using System.Drawing;
using System.Data.SqlClient;
using System.Globalization;
using DevExpress.XtraPivotGrid;
using System.Configuration;
using DevExpress.CodeParser;
using System.Reflection;

namespace SchedulerP360Insight.P360Reports
{
    public partial class XtraReportFuerzaVentas_Auditoria_RX : DevExpress.XtraReports.UI.XtraReport
    {
        DateTime v_fechaCorteReporte;
        string v_colaborador;
        string v_periodo;
        DS_SP_GetFactAuditRXReporteGrafico oDS_SP_GetFactAuditRXReporteGrafico = new DS_SP_GetFactAuditRXReporteGrafico();
        DS_SP_GetFactAuditRXReporteStandard oDS_SP_GetFactAuditRXReporteStandard = new DS_SP_GetFactAuditRXReporteStandard();

        public XtraReportFuerzaVentas_Auditoria_RX()
        {
            InitializeComponent();
        }

        private void XtraReportFuerzaVentas_sinDDD_BeforePrint(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                funcionCargadatosreporte();
                funcionSeteaTitulosDinamicosCampos(v_fechaCorteReporte);
                funcionEstableceColoresSeries();
            }
            catch (BusinessP360Exception)
            {
                e.Cancel = true;
                this.Dispose();
                throw;
            }
            catch (Exception ex)
            {
                e.Cancel = true;
                this.Dispose();
                throw new InvalidOperationException(
                    "Error no interactivo al preparar el reporte.",
                    ex);
            }
        }

        private void funcionCargadatosreporte()
        {
            try
            {
                // Obtener los parámetros del reporte
                v_fechaCorteReporte = (DateTime)Parameters["p_fechaCorteReporte"].Value;
                v_periodo = (string)Parameters["p_periodo"].Value;
                v_colaborador = (string)Parameters["p_colaborador"].Value;

                // Crear instancias de los TableAdapters
                SP_GetFactAuditRXReporteGraficoTableAdapter adapterGrafico = new SP_GetFactAuditRXReporteGraficoTableAdapter();
                SP_GetFactAuditRXReporteStandardTableAdapter adapterStandard = new SP_GetFactAuditRXReporteStandardTableAdapter();

                // Asignar la cadena de conexión dinámica a cada TableAdapter
                adapterGrafico.Connection.ConnectionString = AppConfig.ConnectionString;
                adapterStandard.Connection.ConnectionString = AppConfig.ConnectionString;

                // Llenar los datasets con los datos del reporte
                adapterGrafico.Fill(oDS_SP_GetFactAuditRXReporteGrafico.SP_GetFactAuditRXReporteGrafico,
                                      v_fechaCorteReporte, v_colaborador, v_periodo);
                adapterStandard.Fill(oDS_SP_GetFactAuditRXReporteStandard.SP_GetFactAuditRXReporteStandard,
                                     v_fechaCorteReporte, v_colaborador, v_periodo);

                // Si hay datos, asignarlos a los controles; de lo contrario, notificar al usuario
                if (oDS_SP_GetFactAuditRXReporteGrafico.SP_GetFactAuditRXReporteGrafico.Rows.Count > 0 &&
                    oDS_SP_GetFactAuditRXReporteStandard.SP_GetFactAuditRXReporteStandard.Rows.Count > 0)
                {
                    XrChart_RX.DataSource = oDS_SP_GetFactAuditRXReporteGrafico;
                    XrPivotGrid_RX.DataSource = oDS_SP_GetFactAuditRXReporteStandard;
                    XrRichTextColaborador.Text = "Colaborador: " +
                        oDS_SP_GetFactAuditRXReporteStandard.SP_GetFactAuditRXReporteStandard.Rows[0]["colaborador"].ToString();
                }
                else
                {
                    this.DataSource = null;
                    throw new BusinessP360Exception(
                        "10001",
                        "No se encontraron datos para los parámetros especificados.",
                        MethodBase.GetCurrentMethod().Name);
                }
            }
            catch (SqlException exSql)
            {
                string errorMsg = $"Error SQL (Código {exSql.Number}) en {MethodBase.GetCurrentMethod().Name}: {exSql.Message}";
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

        public void funcionSeteaTitulosDinamicosCampos(DateTime p_fechaCorteReporte)
        {
            try
            {
                XrLabelFechaCorteReporte.Text = string.Format("{0} del {1}",
                    CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(p_fechaCorteReporte.Month),
                    p_fechaCorteReporte.Year);
            }
            catch (Exception ex)
            {
                throw new Exception("Error configurando los títulos dinámicos: " + ex.Message, ex);
            }
        }

        private void funcionEstableceColoresSeries()
        {
            try
            {
                string colorMercado = ConfigurationManager.AppSettings["Color_ChartAUDITS_SerieDataMercado_DashboardsandReportsP360"];
                string colorLaboratorio = ConfigurationManager.AppSettings["Color_ChartAUDITS_SerieDataLaboratorio_DashboardsandReportsP360"];
                XrChart_RX.GetSeriesByName("RX Lab MTD").View.Color = Color.FromName(colorLaboratorio);
                XrChart_RX.GetSeriesByName("RX Mdo MTD").View.Color = Color.FromName(colorMercado);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    "No fue posible aplicar los colores configurados al reporte: " +
                    ex.Message);
            }
        }

        private void XrPivotGrid_RX_CustomSummary(object sender, DevExpress.XtraReports.UI.PivotGrid.PivotGridCustomSummaryEventArgs e)
        {
            try
            {
                PivotDrillDownDataSource ds = e.CreateDrillDownDataSource();
                int i;
                decimal q0L = 0, q1L = 0, q0M = 0, q1M = 0, crecL, crecM, partcq0, partcq1, IE;

                // Solo se procesa si el DataField es uno de los esperados
                if (!(e.DataField == rxCumpLab || e.DataField == rxCumpMdo || e.DataField == rxIE ||
                      e.DataField == rxParticq0 || e.DataField == rxParticq1))
                {
                    return;
                }
                else
                {
                    // Configurar formato de celda
                    e.DataField.CellFormat.FormatString = (e.DataField == rxIE) ? "n2" : "p2";
                    e.DataField.SummaryType = DevExpress.Data.PivotGrid.PivotSummaryType.Custom;
                    e.DataField.CellFormat.FormatType = DevExpress.Utils.FormatType.Numeric;

                    // Iterar sobre el DrillDown DataSource para acumular los valores
                    for (i = 0; i < ds.RowCount; i++)
                    {
                        PivotDrillDownDataRow row = ds[i];
                        q0L += Convert.ToDecimal(row[rx_fieldventaslabq0]);
                        q1L += Convert.ToDecimal(row[rx_fieldventaslabq1]);
                        q0M += Convert.ToDecimal(row[rx_fieldventasmdoq0]);
                        q1M += Convert.ToDecimal(row[rx_fieldventasmdoq1]);
                    }
                    // Calcular crecL
                    crecL = (q0L == 0 && q1L == 0) ? 0 :
                            (q0L != 0 && q1L == 0) ? 100 :
                            (q0L != 0 && q1L != 0) ? ((q0L / q1L) - 1) * 100 : 0;
                    // Calcular crecM
                    crecM = (q0M == 0 && q1M == 0) ? 0 :
                            (q0M != 0 && q1M == 0) ? 100 :
                            (q0M != 0 && q1M != 0) ? ((q0M / q1M) - 1) * 100 : 0;
                    // Calcular partcq0
                    partcq0 = (q0M == 0 && q0L == 0) ? 0 :
                              (q0L != 0 && q0M == 0) ? 100 :
                              (q0M != 0 && q0L != 0) ? (q0L / q0M) : 0;
                    // Calcular partcq1
                    partcq1 = (q1M == 0 && q1L == 0) ? 0 :
                              (q1L != 0 && q1M == 0) ? 100 :
                              (q1M != 0 && q1L != 0) ? (q1L / q1M) : 0;
                    // Calcular IE
                    IE = (crecL == 0 && crecM == 0) ? 100 : (crecM != -100 ? ((100 + crecL) / (100 + crecM)) * 100 : 0);

                    // Asignar el valor personalizado según el DataField
                    if (e.DataField == rxCumpLab)
                        e.CustomValue = Math.Round(crecL / 100, 4);
                    else if (e.DataField == rxCumpMdo)
                        e.CustomValue = Math.Round(crecM / 100, 4);
                    else if (e.DataField == rxIE)
                        e.CustomValue = Math.Round(IE, 4);
                    else if (e.DataField == rxParticq0)
                        e.CustomValue = Math.Round(partcq0, 4);
                    else if (e.DataField == rxParticq1)
                        e.CustomValue = Math.Round(partcq1, 4);
                }
            }
            catch (Exception ex)
            {
                // En caso de error en el cálculo, se registra el error en la consola
                Console.WriteLine("Error en CustomSummary: " + ex.Message);
            }
        }
    }
}
