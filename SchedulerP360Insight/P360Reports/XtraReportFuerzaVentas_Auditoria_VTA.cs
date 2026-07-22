using SchedulerP360Insight.DS_SP_GetFactAuditVTAReporteGraficoTableAdapters;
using SchedulerP360Insight.DS_SP_GetFactAuditVTAReporteStandardTableAdapters;
using SchedulerP360Insight.Modulos;
using System;
using System.Drawing;
using System.Data.SqlClient;
using System.Globalization;
using DevExpress.XtraPivotGrid;
using System.Configuration;
using System.Reflection;

namespace SchedulerP360Insight.P360Reports
{
    public partial class XtraReportFuerzaVentas_Auditoria_VTA : DevExpress.XtraReports.UI.XtraReport
    {
        // Variables para parámetros y datasets
        DateTime v_fechaCorteReporte;
        string v_colaborador;
        string v_periodo;
        DS_SP_GetFactAuditVTAReporteGrafico oDS_SP_GetFactAuditVTAReporteGrafico = new DS_SP_GetFactAuditVTAReporteGrafico();
        DS_SP_GetFactAuditVTAReporteStandard oDS_SP_GetFactAuditVTAReporteStandard = new DS_SP_GetFactAuditVTAReporteStandard();

        public XtraReportFuerzaVentas_Auditoria_VTA()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Evento BeforePrint del reporte: se invoca justo antes de imprimir.
        /// Se encarga de cargar los datos, configurar títulos dinámicos y colores de las series.
        /// </summary>
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

        /// <summary>
        /// Carga los datos para el reporte usando los parámetros proporcionados y sobrescribe
        /// la cadena de conexión de los TableAdapters para usar la configuración dinámica.
        /// </summary>
        private void funcionCargadatosreporte()
        {
            try
            {
                // Asignar parámetros recibidos en el reporte
                v_fechaCorteReporte = (DateTime)Parameters["p_fechaCorteReporte"].Value;
                v_periodo = (string)Parameters["p_periodo"].Value;
                v_colaborador = (string)Parameters["p_colaborador"].Value;

                // Crear instancias de los TableAdapters
                SP_GetFactAuditVTAReporteGraficoTableAdapter adapterGrafico = new SP_GetFactAuditVTAReporteGraficoTableAdapter();
                SP_GetFactAuditVTAReporteStandardTableAdapter adapterStandard = new SP_GetFactAuditVTAReporteStandardTableAdapter();

                // Sobrescribir la cadena de conexión de cada TableAdapter
                adapterGrafico.Connection.ConnectionString = AppConfig.ConnectionString;
                adapterStandard.Connection.ConnectionString = AppConfig.ConnectionString;

                // Llenar los datasets usando los parámetros
                adapterGrafico.Fill(oDS_SP_GetFactAuditVTAReporteGrafico.SP_GetFactAuditVTAReporteGrafico,
                                    v_fechaCorteReporte, v_colaborador, v_periodo);

                adapterStandard.Fill(oDS_SP_GetFactAuditVTAReporteStandard.SP_GetFactAuditVTAReporteStandard,
                                     v_fechaCorteReporte, v_colaborador, v_periodo);

                // Asignar los DataSources a los controles si hay datos
                if (oDS_SP_GetFactAuditVTAReporteGrafico.SP_GetFactAuditVTAReporteGrafico.Rows.Count > 0 &&
                    oDS_SP_GetFactAuditVTAReporteStandard.SP_GetFactAuditVTAReporteStandard.Rows.Count > 0)
                {
                    XrChart_RX.DataSource = oDS_SP_GetFactAuditVTAReporteGrafico;
                    XrPivotGrid_RX.DataSource = oDS_SP_GetFactAuditVTAReporteStandard;

                    // Configurar etiqueta para el colaborador
                    XrRichTextColaborador.Text = "Colaborador: " +
                        oDS_SP_GetFactAuditVTAReporteStandard.SP_GetFactAuditVTAReporteStandard.Rows[0]["colaborador"].ToString();
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

        /// <summary>
        /// Configura los títulos dinámicos del reporte usando la fecha de corte.
        /// </summary>
        /// <param name="p_fechaCorteReporte">Fecha de corte para el reporte.</param>
        public void funcionSeteaTitulosDinamicosCampos(DateTime p_fechaCorteReporte)
        {
            try
            {
                // Configuración del título con la fecha de corte
                XrLabelFechaCorteReporte.Text = string.Format("{0} del {1}",
                    CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(p_fechaCorteReporte.Month),
                    p_fechaCorteReporte.Year);
            }
            catch (Exception ex)
            {
                // Puedes registrar este error o mostrar un mensaje, según lo requieras
                throw new Exception("Error configurando los títulos dinámicos: " + ex.Message, ex);
            }
        }

        /// <summary>
        /// Establece los colores de las series del gráfico usando los valores de AppSettings.
        /// </summary>
        private void funcionEstableceColoresSeries()
        {
            try
            {
                string colorMercado = ConfigurationManager.AppSettings["Color_ChartAUDITS_SerieDataMercado_DashboardsandReportsP360"];
                string colorLaboratorio = ConfigurationManager.AppSettings["Color_ChartAUDITS_SerieDataLaboratorio_DashboardsandReportsP360"];
                XrChart_RX.GetSeriesByName("RX Mdo MTD").View.Color = Color.FromName(colorMercado);
                XrChart_RX.GetSeriesByName("RX Lab MTD").View.Color = Color.FromName(colorLaboratorio);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    "No fue posible aplicar los colores configurados al reporte: " +
                    ex.Message);
            }
        }

        /// <summary>
        /// Maneja el cálculo personalizado en el PivotGrid para generar resúmenes.
        /// </summary>
        private void XrPivotGrid_RX_CustomSummary(object sender, DevExpress.XtraReports.UI.PivotGrid.PivotGridCustomSummaryEventArgs e)
        {
            try
            {
                // Se crea el DrillDown DataSource para iterar los datos
                PivotDrillDownDataSource ds = e.CreateDrillDownDataSource();
                int i;
                decimal q0L = 0, q1L = 0, q0M = 0, q1M = 0, crecL, crecM, partcq0, partcq1, IE;

                // Verificar que el DataField sea uno de los esperados
                if (!(e.DataField == rxCumpLab || e.DataField == rxCumpMdo || e.DataField == rxIE ||
                      e.DataField == rxParticq0 || e.DataField == rxParticq1))
                {
                    return;
                }
                else
                {
                    // Configurar formato de celdas según el DataField
                    if (e.DataField != rxIE)
                        e.DataField.CellFormat.FormatString = "p2";
                    else
                        e.DataField.CellFormat.FormatString = "n2";

                    e.DataField.SummaryType = DevExpress.Data.PivotGrid.PivotSummaryType.Custom;
                    e.DataField.CellFormat.FormatType = DevExpress.Utils.FormatType.Numeric;

                    // Iterar sobre los datos
                    for (i = 0; i < ds.RowCount; i++)
                    {
                        PivotDrillDownDataRow row = ds[i];
                        q0L += Convert.ToDecimal(row[rx_fieldventaslabq0]);
                        q1L += Convert.ToDecimal(row[rx_fieldventaslabq1]);
                        q0M += Convert.ToDecimal(row[rx_fieldventasmdoq0]);
                        q1M += Convert.ToDecimal(row[rx_fieldventasmdoq1]);
                    }
                    // Calcular crecimientos y participaciones
                    crecL = (q0L == 0 && q1L == 0) ? 0 :
                            (q0L != 0 && q1L == 0) ? 100 :
                            (q0L != 0 && q1L != 0) ? ((q0L / q1L) - 1) * 100 : 0;

                    crecM = (q0M == 0 && q1M == 0) ? 0 :
                            (q0M != 0 && q1M == 0) ? 100 :
                            (q0M != 0 && q1M != 0) ? ((q0M / q1M) - 1) * 100 : 0;

                    partcq0 = (q0M == 0 && q0L == 0) ? 0 :
                              (q0L != 0 && q0M == 0) ? 100 :
                              (q0M != 0 && q0L != 0) ? (q0L / q0M) : 0;

                    partcq1 = (q1M == 0 && q1L == 0) ? 0 :
                              (q1L != 0 && q1M == 0) ? 100 :
                              (q1M != 0 && q1L != 0) ? (q1L / q1M) : 0;

                    IE = (crecL == 0 && crecM == 0) ? 100 : (crecM != -100 ? ((100 + crecL) / (100 + crecM)) * 100 : 0);

                    // Asignar el valor calculado según el DataField
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
                // En este bloque se podría registrar el error para revisión sin interrumpir la operación
                // Por ejemplo: LogError("Error en CustomSummary: " + ex.Message);
            }
        }
    }
}
