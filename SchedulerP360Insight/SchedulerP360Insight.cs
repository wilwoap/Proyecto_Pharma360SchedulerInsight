using SchedulerP360Insight.Hosting;
using System.Threading.Tasks;

namespace ReportGenerator
{
    internal static class SchedulerP360Insight
    {
        private static Task<int> Main(string[] args)
        {
            return SchedulerProcess.RunAsync(args);
        }

        public static string GetConnectionStringFromMachineEnvironment()
        {
            return AppConfig.GetRequiredEnvironmentVariable(
                AppConfig.ConnectionStringEnvironmentVariable);
        }
    }
}
