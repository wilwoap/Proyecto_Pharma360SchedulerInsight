using SchedulerP360Insight.Modulos;
using SchedulerP360Insight.Observability;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SchedulerP360Insight.Hosting
{
    public interface IAuditEventSink
    {
        void Write(
            string eventName,
            IReadOnlyDictionary<string, string> fields);
    }

    public sealed class LegacyApplicationEventSink : IApplicationEventSink
    {
        private readonly IOperationalTelemetry telemetry;
        private readonly IAuditEventSink audit;

        public LegacyApplicationEventSink(
            IOperationalTelemetry telemetry,
            IAuditEventSink audit)
        {
            this.telemetry = telemetry ??
                throw new ArgumentNullException(nameof(telemetry));
            this.audit = audit ?? throw new ArgumentNullException(nameof(audit));
        }

        public void Write(
            string eventName,
            IReadOnlyDictionary<string, string> fields = null)
        {
            telemetry.Write(
                TelemetryLevels.Information,
                eventName,
                fields: fields);
            try
            {
                audit.Write(eventName, fields);
            }
            catch (Exception auditError)
            {
                telemetry.Write(
                    TelemetryLevels.Warning,
                    "audit.write.failed",
                    fields: new Dictionary<string, string>
                    {
                        ["audit_sink"] = "sql",
                        ["failure_category"] = auditError.GetType().Name
                    },
                    exception: auditError);
            }
        }
    }

    public sealed class SqlAuditEventSink : IAuditEventSink
    {
        private readonly ModuleCapaAccesoDatos dataAccess;
        private readonly string username;

        public SqlAuditEventSink(
            ModuleCapaAccesoDatos dataAccess,
            string username)
        {
            this.dataAccess = dataAccess ??
                throw new ArgumentNullException(nameof(dataAccess));
            this.username = username ?? string.Empty;
        }

        public void Write(
            string eventName,
            IReadOnlyDictionary<string, string> fields)
        {
            IReadOnlyDictionary<string, string> safeFields =
                EventFieldPolicy.Filter(fields);
            string suffix = safeFields.Count == 0
                ? string.Empty
                : " " + string.Join(
                    " ",
                    safeFields.Select(item =>
                        item.Key + "=" + item.Value));
            bool written = dataAccess.TryRegistraLogConeccionyAccion(
                username,
                EventFieldPolicy.SanitizeValue(eventName) + suffix);
            if (!written)
            {
                throw new InvalidOperationException(
                    "El sink SQL de auditoría no estuvo disponible.");
            }
        }
    }
}
