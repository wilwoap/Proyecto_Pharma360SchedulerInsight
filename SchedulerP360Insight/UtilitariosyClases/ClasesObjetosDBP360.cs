using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SchedulerP360Insight.UtilitariosyClases
{
    public class DatosVacacionAusencia
    {
        public int IdVacacionAusencia { get; set; }
        public int CodColab { get; set; }
        public string Colaborador { get; set; }
        public int CodVisita { get; set; }
        public DateTime FechaInicioVacacionAusencia { get; set; }
        public DateTime FechaFinVacacionAusencia { get; set; }
        public string TipoVacacionAusencia { get; set; }
        public string MotivoVacacionAusencia { get; set; }
        public string EstadoVacacionAusencia { get; set; }
        public string ComentarioEstadoVacacionAusencia { get; set; }
    }

    public class DatosVisita
    {
        public int CodVisita { get; set; }
        public string Colaborador { get; set; }
        public string Supervisor { get; set; }
        public string TipoVisitado { get; set; }
        public string CategoriaEspecialidad { get; set; }
        public string NombreVisitado { get; set; }
        public string NombreContacto { get; set; }        
        public string Ciudad { get; set; }
        public string Direccion { get; set; }
        public DateTime FechaVisita { get; set; }
        public int MinutosDuracionVisita { get; set; }        
        public DateTime? FechaProximaVisita { get; set; } // Nullable si la fecha puede ser null
        public string Observaciones { get; set; }
        public string ObjetivoVisitaDescripcion { get; set; }
        public int CodPedido { get; set; } // Asumiendo int, cambia a bool si es necesario
        public bool RegistraPedido { get; set; }
        public bool RegistraCapacitaciones { get; set; }
        public bool RegistraCaras { get; set; }
        public bool RegistraExhibiciones { get; set; }
        public bool RegistraStocks { get; set; }
        public bool RegistraMuestras { get; set; }
        public string AccionesEfectuadas { get; set; }
        public string Latitud { get; set; }
        public string Longitud { get; set; }
    }
    public class ClasesObjetosDBP360
    {
        public int CodPedido { get; set; }
        public DateTime FechaPedido { get; set; }
        public DateTime FechaEntregaPedido { get; set; } // Asumiendo que solo nos interesa la fecha sin la hora
        public string DespachadorPedido { get; set; }
        public string Cliente { get; set; }
        public string NumeroFiscal { get; set; }
        public string TipoVisitado { get; set; }
        public string GrupoCliente { get; set; }
        public string CategoriaCliente { get; set; }
        public string CadenaCliente { get; set; }
        public string Ciudad { get; set; }
        public string Direccion { get; set; }
        public string NombreContacto { get; set; }
        public string Colaborador { get; set; }
        public string Observaciones { get; set; }
        public string MotivoEstadoPedido { get; set; }
        public string Latitud { get; set; }
        public string Longitud { get; set; }
        public int PedidoUnidadesReales { get; set; }
        public int PedidoUnidadesBonificadas { get; set; }
        public int UnidadesRealesDespachadas { get; set; }
        public int UnidadesBonificadasDespachadas { get; set; }
        public int SaldoUnidades { get; set; }
        public int SaldoBonificaciones { get; set; }
        public decimal PedidoValorVentaPVF { get; set; }
        public decimal PedidoValorVentaPVP { get; set; }
        public decimal PedidoValorDescuento { get; set; }
        public decimal SubtotalPedidoValorVentaPVF { get; set; }
        public decimal SubtotalPedidoValorVentaPVP { get; set; }
        public decimal SubtotalPedidoValorDescuento { get; set; }
    }
    public class DatosContactosNotificaciones
    {
        public string Contacto { get; set; }
        public string Codigo { get; set; }
        public string Nombre { get; set; }
        public string Email { get; set; }
        public string Telefono { get; set; }
        public string Direccion { get; set; }
        public string Rol { get; set; }
    }

}
