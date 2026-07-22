using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SchedulerP360Insight
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // TODO: esta línea de código carga datos en la tabla 'dS_V_REPORTE_HISTORICO_PEDIDOS.V_REPORTE_HISTORICO_PEDIDOS' Puede moverla o quitarla según sea necesario.
            this.v_REPORTE_HISTORICO_PEDIDOSTableAdapter.Fill(this.dS_V_REPORTE_HISTORICO_PEDIDOS.V_REPORTE_HISTORICO_PEDIDOS);
          //  this.v_REPORTE_HISTORICO_PEDIDOSTableAdapter.FillByCodPedido(this.dS_V_REPORTE_HISTORICO_PEDIDOS.V_REPORTE_HISTORICO_PEDIDOS, 1063);

        }
    }
}
