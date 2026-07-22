using SchedulerP360Insight.Modulos;
using System;

namespace SchedulerP360Insight.Hosting
{
    public sealed class LegacyApplicationEventSink : IApplicationEventSink
    {
        private readonly ModuleCapaAccesoDatos dataAccess;
        private readonly string username;

        public LegacyApplicationEventSink(
            ModuleCapaAccesoDatos dataAccess,
            string username)
        {
            this.dataAccess = dataAccess ??
                throw new ArgumentNullException(nameof(dataAccess));
            this.username = username ?? string.Empty;
        }

        public void Write(string message)
        {
            string safeMessage = message ?? string.Empty;
            Console.WriteLine(safeMessage);
            dataAccess.RegistraLogConeccionyAccion(username, safeMessage);
        }
    }
}
