using System;
using System.Collections.Generic;
using System.Globalization;

public sealed class LaboratoryConstants
{
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
        string pharma360EmpresaDireccion,
        string googleMapsApiKey = null)
    {
        if (pharma360MailPort < 1 || pharma360MailPort > 65535)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pharma360MailPort),
                "El puerto SMTP debe estar entre 1 y 65535.");
        }

        LaboratoryName = laboratoryName;
        AdminEmail = adminEmail;
        IntellectualPropertyNotice = intellectualPropertyNotice;
        SenderEmail = senderEmail;
        Pharma360MailSSL = pharma360MailSSL;
        Pharma360MailSMTP = pharma360MailSMTP;
        Pharma360MailUser = pharma360MailUser;
        Pharma360MailPass = pharma360MailPass;
        Pharma360MailPort = pharma360MailPort;
        Pharma360UrlLogo = pharma360UrlLogo;
        Pharma360EmpresaPais = pharma360EmpresaPais;
        Pharma360EmpresaCiudad = pharma360EmpresaCiudad;
        Pharma360EmpresaSitioWeb = pharma360EmpresaSitioWeb;
        Pharma360EmpresaEmailContacto = pharma360EmpresaEmailContacto;
        Pharma360EmpresaTelefonoContacto = pharma360EmpresaTelefonoContacto;
        Pharma360EmpresaDireccion = pharma360EmpresaDireccion;
        GoogleMapsApiKey = string.IsNullOrWhiteSpace(googleMapsApiKey)
            ? null
            : googleMapsApiKey;
    }

    public string LaboratoryName { get; }

    public string AdminEmail { get; }

    public string IntellectualPropertyNotice { get; }

    public string SenderEmail { get; }

    public bool Pharma360MailSSL { get; }

    public string Pharma360MailSMTP { get; }

    public string Pharma360MailUser { get; }

    public string Pharma360MailPass { get; }

    public int Pharma360MailPort { get; }

    public string Pharma360UrlLogo { get; }

    public string Pharma360EmpresaPais { get; }

    public string Pharma360EmpresaCiudad { get; }

    public string Pharma360EmpresaDireccion { get; }

    public string Pharma360EmpresaSitioWeb { get; }

    public string Pharma360EmpresaEmailContacto { get; }

    public string Pharma360EmpresaTelefonoContacto { get; }

    public string GoogleMapsApiKey { get; }

    public static LaboratoryConstants FromParameters(
        IReadOnlyDictionary<string, string> values,
        string googleMapsApiKey)
    {
        if (values == null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        string sslValue = GetRequired(values, "MAIL_SSL");
        bool enableSsl;
        if (sslValue == "1")
        {
            enableSsl = true;
        }
        else if (sslValue == "0")
        {
            enableSsl = false;
        }
        else
        {
            throw new InvalidOperationException(
                "El parámetro 'MAIL_SSL' sólo admite '0' o '1'.");
        }

        int mailPort;
        string portValue = GetRequired(values, "MAIL_PORT");
        if (!int.TryParse(
            portValue,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out mailPort) ||
            mailPort < 1 ||
            mailPort > 65535)
        {
            throw new InvalidOperationException(
                "El parámetro 'MAIL_PORT' debe ser un entero entre 1 y 65535.");
        }

        string mailUser = GetRequired(values, "MAIL_USER");
        return new LaboratoryConstants(
            GetRequired(values, "LABORATORIO_IMPLEMENTACION"),
            GetRequired(values, "MAIL_ADMINISTRADOR_LABORATORIO"),
            "Powered by: Pharma360° ™ Bisigma ® Derechos Intelectuales",
            mailUser,
            enableSsl,
            GetRequired(values, "MAIL_SMTP"),
            mailUser,
            GetRequired(values, "MAIL_PASS", allowEmpty: true),
            mailPort,
            GetRequired(values, "LABORATORIO_URL_LOGO", allowEmpty: true),
            GetRequired(values, "EMPRESA_PAIS", allowEmpty: true),
            GetRequired(values, "EMPRESA_CIUDAD", allowEmpty: true),
            GetRequired(values, "EMPRESA_SITIO_WEB", allowEmpty: true),
            GetRequired(values, "EMPRESA_EMAIL_CONTACTO", allowEmpty: true),
            GetRequired(values, "EMPRESA_TELEFONO_CONTACTO", allowEmpty: true),
            GetRequired(values, "EMPRESA_DIRECCION", allowEmpty: true),
            googleMapsApiKey);
    }

    public override string ToString()
    {
        return "LaboratoryConstants { " +
            "Identity=configured, " +
            "Smtp=[REDACTED], " +
            "SmtpCredential=[REDACTED], " +
            "GoogleMapsApiKey=" +
            (GoogleMapsApiKey == null ? "absent" : "[REDACTED]") + " }";
    }

    private static string GetRequired(
        IReadOnlyDictionary<string, string> values,
        string name,
        bool allowEmpty = false)
    {
        string value;
        if (!values.TryGetValue(name, out value) ||
            value == null ||
            (!allowEmpty && string.IsNullOrWhiteSpace(value)))
        {
            throw new InvalidOperationException(
                "El parámetro requerido '" + name + "' no está configurado.");
        }

        return value;
    }
}
