using SchedulerP360Insight.Properties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SchedulerP360Insight.Modulos
{

    public class BusinessP360Exception : Exception
    {
        public string ErrorP360Code { get; set; } = "";
        public string ErrorP360Mensaje { get; set; }
        public string ErrorP360Procedimiento { get; set; } = "";
        public BusinessP360Exception(string errorP360Code, string errorP360Mensaje, string errorP360Procedimiento)
        {
            ErrorP360Code = errorP360Code;
            ErrorP360Mensaje = errorP360Mensaje;
            ErrorP360Procedimiento = errorP360Procedimiento;
            GestionBusinessP360Exception();
        }

        public BusinessP360Exception(string errorP360Mensaje)
        {
            ErrorP360Mensaje = errorP360Mensaje;
            GestionBusinessP360Exception();
        }

        public BusinessP360Exception(string errorP360Code, string errorP360Mensaje)
        {
            ErrorP360Code = errorP360Code;
            ErrorP360Mensaje = errorP360Mensaje;
            GestionBusinessP360Exception();
        }

        private void GestionBusinessP360Exception()
        {
            ErrorP360Mensaje = ManejoCasosErrorP360(ErrorP360Code, ErrorP360Mensaje, ErrorP360Procedimiento);
        }

        private string ManejoCasosErrorP360(string p_codError, string p_mensajeError, string p_procedimiento)
        {
            // Dim v_manejoCasosError As String = ""
            bool v_registraLogAccionEnBaseDatos = false;
            bool v_registraLogAccionEnEventViewer = false;
            try
            {
                // ' Poner aqui solo los mensajes especificos de los errorP360Code especificos que podrian venir de base de datos para cambiar el errorP360Mensaje, 
                // ' por ejemplo el errorP360Code = "18456" viene directamente desde el MOTOR de la base de datos y esta ligado a una clave incorrecta del usuario rvl.pharma360.bisigma.principal_licence
                // ' Nomenclatra de errores de Pharma360, para REPORTES de Pharma360          : P360Repo_0001 / P360Repo_9999
                // ' Nomenclatra de errores de Pharma360, para DASHBOARDS  de Pharma360       : P360Dash_0001 / P360Dash_9999
                // ' Nomenclatra de errores de Pharma360, para GENERAL P360  de Pharma360     : P360Core_0001 / P360Core_9999
                // Gestion errores Que vienen desde la Base de Datos
                if (p_codError == "-2")
                {
                    p_mensajeError = "Existe una demora inusual en el proceso, servidor ocupado. ¡Por favor inténtelo de nuevo en unos minutos!"; // ERROR lentitud base de datos o lentitud consulta
                    v_registraLogAccionEnEventViewer = true;
                    v_registraLogAccionEnBaseDatos = true;
                }
                else if (p_codError == "4060" || p_codError == "17142")
                {
                    p_mensajeError = ResourceMensajesP360.mensaje_errorSQL_4060; // ERROR IMPOSIBILIDAD DE LLEGAR A BASE DE DATOS
                    v_registraLogAccionEnEventViewer = true;
                }
                else if (p_codError == "18456")
                {
                    p_mensajeError = ResourceMensajesP360.mensaje_errorSQL_18456; // ERROR IMPOSIBILIDAD DE LLEGAR A BASE DE DATOS
                    v_registraLogAccionEnEventViewer = true;
                }
                   else if (p_codError == "53")
                {
                    p_mensajeError = ResourceMensajesP360.mensaje_errorSQL_53;
                    v_registraLogAccionEnEventViewer = true;
                }
                else if (p_codError == "-2" || p_codError == "90001")
                {
                    v_registraLogAccionEnEventViewer = true;
                }
                  else if (p_codError == "2627")
                    p_mensajeError = ResourceMensajesP360.mensaje_errorSQL_2627;
                else if (p_codError == "515")
                    p_mensajeError = ResourceMensajesP360.mensaje_errorSQL_515;
                else if (p_codError == "8134")
                {
                    p_mensajeError = ResourceMensajesP360.mensaje_errorSQL_8134;
                    v_registraLogAccionEnBaseDatos = true;
                }
                else if (p_codError == "50000")
                {
                    v_registraLogAccionEnBaseDatos = true;
                }
                else if (p_codError == "8114")
                {
                    p_mensajeError = ResourceMensajesP360.mensaje_errorSQL_8114;
                    v_registraLogAccionEnBaseDatos = true;
                }
                else if (p_codError == "4815")
                {
                    p_mensajeError = ResourceMensajesP360.mensaje_errorSQL_4815 + " [" + p_mensajeError + "]";
                    v_registraLogAccionEnBaseDatos = true;
                }
                else if (p_codError == "102")
                {
                    p_mensajeError = ResourceMensajesP360.mensaje_errorSQL_102;
                    v_registraLogAccionEnBaseDatos = true;
                }
                else if (p_codError == "10001")
                {
                    //p_mensajeError = ResourceMensajesP360.mensaje_errorSQL_102;
                    p_mensajeError = "No existen datos para la consulta efectuada en P360 insightts";
                    v_registraLogAccionEnBaseDatos = true;
                }
                else
                {
                    p_mensajeError = "Código de error no gestionado. Error code: " + p_codError + ", ErrorP360: " + p_mensajeError + ". ACCION: Adicionar caso en método manejoCasosErrorP360";
                    //ModuleCapaAccesoDatos oModuleCapaAccesoDatos = new ModuleCapaAccesoDatos();
                    //oModuleCapaAccesoDatos.RegistraLogConeccionyAccion("vg_usuario", p_mensajeError);
                }
                if (v_registraLogAccionEnBaseDatos)
                {
                    string v_accion = p_mensajeError + ". PROCEDIMIENTO: " + p_procedimiento + ". ERROR: " + p_mensajeError;
                    ModuleCapaAccesoDatos oModuleCapaAccesoDatos = new ModuleCapaAccesoDatos();
                    oModuleCapaAccesoDatos.RegistraLogConeccionyAccion("vg_usuario", v_accion);
                }
                //if (v_registraLogAccionEnEventViewer)
               //     logP360("BusinessP360Exception: Error en la ejecución de un procedimiento.", " Error code: " + p_codError + ", ErrorP360:" + p_mensajeError + ", ErrorP360Procedimiento:" + p_procedimiento, "ERROR");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error scheduling job: {ex.Message}");
            }
            return p_mensajeError;
        }

    }
}
