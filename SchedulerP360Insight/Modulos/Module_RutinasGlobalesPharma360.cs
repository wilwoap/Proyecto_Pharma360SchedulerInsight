using SchedulerP360Insight.Properties;
using System;

namespace SchedulerP360Insight.Modulos
{
    public sealed class BusinessP360Exception : Exception
    {
        public BusinessP360Exception(
            string errorP360Code,
            string errorP360Mensaje,
            string errorP360Procedimiento)
            : base(MapMessage(
                errorP360Code,
                errorP360Mensaje,
                errorP360Procedimiento))
        {
            ErrorP360Code = errorP360Code ?? string.Empty;
            ErrorP360Mensaje = Message;
            ErrorP360Procedimiento = errorP360Procedimiento ?? string.Empty;
        }

        public BusinessP360Exception(string errorP360Mensaje)
            : this(string.Empty, errorP360Mensaje, string.Empty)
        {
        }

        public BusinessP360Exception(
            string errorP360Code,
            string errorP360Mensaje)
            : this(errorP360Code, errorP360Mensaje, string.Empty)
        {
        }

        public string ErrorP360Code { get; }

        public string ErrorP360Mensaje { get; }

        public string ErrorP360Procedimiento { get; }

        private static string MapMessage(
            string errorCode,
            string message,
            string procedure)
        {
            string code = errorCode ?? string.Empty;
            string originalMessage = message ?? string.Empty;

            switch (code)
            {
                case "-2":
                    return "Existe una demora inusual en el proceso, servidor ocupado. " +
                        "¡Por favor inténtelo de nuevo en unos minutos!";
                case "4060":
                case "17142":
                    return ResourceMensajesP360.mensaje_errorSQL_4060;
                case "18456":
                    return ResourceMensajesP360.mensaje_errorSQL_18456;
                case "53":
                    return ResourceMensajesP360.mensaje_errorSQL_53;
                case "90001":
                case "50000":
                    return originalMessage;
                case "2627":
                    return ResourceMensajesP360.mensaje_errorSQL_2627;
                case "515":
                    return ResourceMensajesP360.mensaje_errorSQL_515;
                case "8134":
                    return ResourceMensajesP360.mensaje_errorSQL_8134;
                case "8114":
                    return ResourceMensajesP360.mensaje_errorSQL_8114;
                case "4815":
                    return ResourceMensajesP360.mensaje_errorSQL_4815 +
                        " [" + originalMessage + "]";
                case "102":
                    return ResourceMensajesP360.mensaje_errorSQL_102;
                case "10001":
                    return "No existen datos para la consulta efectuada en P360 insightts";
                default:
                    return "Código de error no gestionado. Error code: " + code +
                        ", ErrorP360: " + originalMessage +
                        ". ACCION: Adicionar caso en método manejoCasosErrorP360";
            }
        }
    }
}
