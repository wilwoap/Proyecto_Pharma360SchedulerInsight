using SchedulerP360Insight.Modulos;
using System;

public class LaboratoryConstants
{
    private string _laboratoryName;
    private string _adminEmail;
    private string _intellectualPropertyNotice;
    private string _senderEmail;
    private bool _pharma360MailSSL;
    private string _pharma360MailSMTP;
    private string _pharma360MailUser;
    private string _pharma360MailPass;
    private int _pharma360MailPort;
    private string _pharma360UrlLogo;
    private string _pharma360EmpresaPais;
    private string _pharma360EmpresaCiudad;
    private string _pharma360EmpresaDireccion;
    private string _pharma360EmpresaSitioWeb;
    private string _pharma360EmpresaEmailContacto;
    private string _pharma360EmpresaTelefonoContacto;

    public LaboratoryConstants(
        string laboratoryName,
        string adminEmail,
        string intellectualPropertyNotice,
        string senderEmail,
        bool pharma360MailSSL,
        string pharma360MailSMTP,
        string pharma360MailUser,
        string pharma360MailPass,
        int pharma360MailPort,
        string pharma360UrlLogo,
        string pharma360EmpresaPais,
        string pharma360EmpresaCiudad,
        string pharma360EmpresaSitioWeb,
        string pharma360EmpresaEmailContacto,
        string pharma360EmpresaTelefonoContacto,
        string pharma360EmpresaDireccion)
    {
        _laboratoryName = laboratoryName;
        _adminEmail = adminEmail;
        _intellectualPropertyNotice = intellectualPropertyNotice;
        _senderEmail = senderEmail;
        _pharma360MailSSL = pharma360MailSSL;
        _pharma360MailSMTP = pharma360MailSMTP;
        _pharma360MailUser = pharma360MailUser;
        _pharma360MailPass = pharma360MailPass;
        _pharma360MailPort = pharma360MailPort;
        _pharma360UrlLogo = pharma360UrlLogo;
        _pharma360EmpresaPais=pharma360EmpresaPais;
        _pharma360EmpresaCiudad=pharma360EmpresaCiudad;
        _pharma360EmpresaSitioWeb=pharma360EmpresaSitioWeb;
        _pharma360EmpresaEmailContacto=pharma360EmpresaEmailContacto;
        _pharma360EmpresaTelefonoContacto=pharma360EmpresaTelefonoContacto;
        _pharma360EmpresaDireccion=pharma360EmpresaDireccion;
    }

    public LaboratoryConstants()
    {
        string v_Pharma360_mail_ssl = string.Empty;
        ModuleCapaAccesoDatos oModuleCapaAccesoDatos = new ModuleCapaAccesoDatos();
        v_Pharma360_mail_ssl=oModuleCapaAccesoDatos.getValorParametroSistemaDB("MAIL_SSL");
        _intellectualPropertyNotice = "Powered by: Pharma360° ™ Bisigma ® Derechos Intelectuales";
        _pharma360UrlLogo = oModuleCapaAccesoDatos.getValorParametroSistemaDB("LABORATORIO_URL_LOGO");
        _laboratoryName = oModuleCapaAccesoDatos.getValorParametroSistemaDB("LABORATORIO_IMPLEMENTACION");
        _adminEmail = oModuleCapaAccesoDatos.getValorParametroSistemaDB("MAIL_ADMINISTRADOR_LABORATORIO");
        _pharma360MailSMTP = oModuleCapaAccesoDatos.getValorParametroSistemaDB("MAIL_SMTP");
        _pharma360MailUser = oModuleCapaAccesoDatos.getValorParametroSistemaDB("MAIL_USER");
        _pharma360MailPass = oModuleCapaAccesoDatos.getValorParametroSistemaDB("MAIL_PASS");
        _pharma360MailPort = Convert.ToInt32(oModuleCapaAccesoDatos.getValorParametroSistemaDB("MAIL_PORT"));
        _pharma360EmpresaPais = oModuleCapaAccesoDatos.getValorParametroSistemaDB("EMPRESA_PAIS");
        _pharma360EmpresaCiudad = oModuleCapaAccesoDatos.getValorParametroSistemaDB("EMPRESA_CIUDAD");
        _pharma360EmpresaDireccion = oModuleCapaAccesoDatos.getValorParametroSistemaDB("EMPRESA_DIRECCION");
        _pharma360EmpresaSitioWeb = oModuleCapaAccesoDatos.getValorParametroSistemaDB("EMPRESA_SITIO_WEB");
        _pharma360EmpresaEmailContacto = oModuleCapaAccesoDatos.getValorParametroSistemaDB("EMPRESA_EMAIL_CONTACTO");
        _pharma360EmpresaTelefonoContacto = oModuleCapaAccesoDatos.getValorParametroSistemaDB("EMPRESA_TELEFONO_CONTACTO");

        _senderEmail=_pharma360MailUser;
        if (v_Pharma360_mail_ssl == "1")
        {
            _pharma360MailSSL = true;
        }
        else
        {
            _pharma360MailSSL = false;
        }
    }

    public string LaboratoryName
    {
        get { return _laboratoryName; }
        set { _laboratoryName = value; }
    }

    public string AdminEmail
    {
        get { return _adminEmail; }
        set { _adminEmail = value; }
    }

    public string IntellectualPropertyNotice
    {
        get { return _intellectualPropertyNotice; }
        set { _intellectualPropertyNotice = value; }
    }

    public string SenderEmail
    {
        get { return _senderEmail; }
        set { _senderEmail = value; }
    }

    public bool Pharma360MailSSL
    {
        get { return _pharma360MailSSL; }
        set { _pharma360MailSSL = value; }
    }

    public string Pharma360MailSMTP
    {
        get { return _pharma360MailSMTP; }
        set { _pharma360MailSMTP = value; }
    }

    public string Pharma360MailUser
    {
        get { return _pharma360MailUser; }
        set { _pharma360MailUser = value; }
    }

    public string Pharma360MailPass
    {
        get { return _pharma360MailPass; }
        set { _pharma360MailPass = value; }
    }

    public int Pharma360MailPort
    {
        get { return _pharma360MailPort; }
        set { _pharma360MailPort = value; }
    }

    public string Pharma360UrlLogo
    {
        get { return _pharma360UrlLogo; }
        set { _pharma360UrlLogo = value; }
    }
    public string Pharma360EmpresaPais
    {
        get { return _pharma360EmpresaPais; }
        set { _pharma360EmpresaPais = value; }
    }

    public string Pharma360EmpresaCiudad
    {
        get { return _pharma360EmpresaCiudad; }
        set { _pharma360EmpresaCiudad = value; }
    }
    public string Pharma360EmpresaDireccion
    {
        get { return _pharma360EmpresaDireccion; }
        set { _pharma360EmpresaDireccion = value; }
    }

    public string Pharma360EmpresaSitioWeb
    {
        get { return _pharma360EmpresaSitioWeb; }
        set { _pharma360EmpresaSitioWeb = value; }
    }

    public string Pharma360EmpresaEmailContacto
    {
        get { return _pharma360EmpresaEmailContacto; }
        set { _pharma360EmpresaEmailContacto = value; }
    }

    public string Pharma360EmpresaTelefonoContacto
    {
        get { return _pharma360EmpresaTelefonoContacto; }
        set { _pharma360EmpresaTelefonoContacto = value; }
    }

}
