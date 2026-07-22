using Microsoft.VisualStudio.TestTools.UnitTesting;
using SchedulerP360Insight.Scheduling;
using System;
using System.IO;

namespace SchedulerP360Insight.CharacterizationTests
{
    internal static class TestSupport
    {
        public static ReportScheduleDefinition CreateReport(
            string reportType = "html",
            string reportUid = "RVIS",
            string cron = "0 0/5 * * * ?")
        {
            return new ReportScheduleDefinition
            {
                ReportId = 42,
                ReportUID = reportUid,
                ReportName = "Reporte sintético",
                ReportInsight = "Fixture sin datos personales",
                ReportFileName = "reporte-fixture",
                ReportType = reportType,
                ReportPathSource = @"C:\P360\Tests\Source",
                ReportPathOutput = @"C:\P360\Tests\Output",
                ReportSchedule = cron,
                ReportSubjectText = "Reporte [REPORT_NAME]",
                ReportBodyResourceKey = "HTMLBody_Plantilla_VM_01",
                ReportSendMail = true,
                ReportSendMailCopySupervisor = false
            };
        }

        public static TException Throws<TException>(Action action)
            where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException exception)
            {
                return exception;
            }
            catch (Exception exception)
            {
                Assert.Fail(string.Format(
                    "Se esperaba {0}, pero se recibió {1}: {2}",
                    typeof(TException).Name,
                    exception.GetType().Name,
                    exception.Message));
            }

            Assert.Fail(string.Format(
                "Se esperaba una excepción de tipo {0}.",
                typeof(TException).Name));
            return null;
        }

        public static string FindRepositoryRoot()
        {
            DirectoryInfo directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "SchedulerP360Insight.sln")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException(
                "No se encontró la raíz del repositorio desde el directorio de pruebas.");
        }
    }
}
