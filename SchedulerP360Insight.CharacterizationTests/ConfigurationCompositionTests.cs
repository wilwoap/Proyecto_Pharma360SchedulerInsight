using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quartz;
using Quartz.Spi;
using ReportGenerator;
using SchedulerP360Insight.Configuration;
using SchedulerP360Insight.Modulos;
using SchedulerP360Insight.Scheduling;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SchedulerP360Insight.CharacterizationTests
{
    [TestClass]
    public sealed class ConfigurationCompositionTests
    {
        [TestMethod]
        public void Options_LoadsBatchByDefaultAndRedactsSecrets()
        {
            const string connectionSecret = "synthetic-db-password";
            const string mapsSecret = "synthetic-maps-key";
            Dictionary<string, string> environment =
                new Dictionary<string, string>
                {
                    [SchedulerOptions.ConnectionStringEnvironmentVariable] =
                        "synthetic-connection-value::" + connectionSecret,
                    [SchedulerOptions.GoogleMapsApiKeyEnvironmentVariable] = mapsSecret
                };
            Dictionary<string, string> settings = CreateAppSettings();

            SchedulerOptions options = SchedulerOptions.Load(
                name => GetValue(environment, name),
                name => GetValue(settings, name));

            Assert.AreEqual(ParameterProviderMode.Batch, options.ParameterProviderMode);
            Assert.AreEqual(mapsSecret, options.GoogleMapsApiKey);
            Assert.AreEqual(settings[SchedulerOptions.ReportsQuerySetting], options.ReportsQuery);
            Assert.AreEqual(TimeZoneInfo.Local.Id, options.QuartzTimeZone.Id);
            Assert.AreEqual(
                QuartzMisfirePolicy.FireOnceNow,
                options.QuartzMisfirePolicy);
            Assert.IsTrue(options.QuartzDisallowConcurrentExecution);
            Assert.AreEqual(10, options.QuartzMaxConcurrency);
            Assert.IsFalse(options.ToString().Contains(connectionSecret));
            Assert.IsFalse(options.ToString().Contains(mapsSecret));
            StringAssert.Contains(options.ToString(), "[REDACTED]");
        }

        [TestMethod]
        public void Options_AllowsExplicitLegacyRollbackMode()
        {
            Dictionary<string, string> environment = CreateEnvironment();
            environment[SchedulerOptions.ParameterProviderModeEnvironmentVariable] =
                "legacy";

            SchedulerOptions options = SchedulerOptions.Load(
                name => GetValue(environment, name),
                name => GetValue(CreateAppSettings(), name));

            Assert.AreEqual(ParameterProviderMode.Legacy, options.ParameterProviderMode);
        }

        [TestMethod]
        public void Options_LoadsExplicitQuartzPolicy()
        {
            Dictionary<string, string> environment = CreateEnvironment();
            environment[SchedulerOptions.QuartzTimeZoneEnvironmentVariable] =
                TimeZoneInfo.Utc.Id;
            environment[
                SchedulerOptions.QuartzMisfirePolicyEnvironmentVariable] =
                "do_nothing";
            environment[
                SchedulerOptions.QuartzDisallowConcurrentExecutionEnvironmentVariable] =
                "false";
            environment[
                SchedulerOptions.QuartzMaxConcurrencyEnvironmentVariable] =
                "4";

            SchedulerOptions options = SchedulerOptions.Load(
                name => GetValue(environment, name),
                name => GetValue(CreateAppSettings(), name));

            Assert.AreEqual(TimeZoneInfo.Utc.Id, options.QuartzTimeZone.Id);
            Assert.AreEqual(
                QuartzMisfirePolicy.DoNothing,
                options.QuartzMisfirePolicy);
            Assert.IsFalse(options.QuartzDisallowConcurrentExecution);
            Assert.AreEqual(4, options.QuartzMaxConcurrency);
        }

        [TestMethod]
        [DataRow(
            SchedulerOptions.QuartzMisfirePolicyEnvironmentVariable,
            "unsupported")]
        [DataRow(
            SchedulerOptions.QuartzDisallowConcurrentExecutionEnvironmentVariable,
            "yes")]
        [DataRow(
            SchedulerOptions.QuartzMaxConcurrencyEnvironmentVariable,
            "65")]
        public void Options_RejectsInvalidQuartzPolicyWithoutEchoingItsValue(
            string variableName,
            string invalidValue)
        {
            Dictionary<string, string> environment = CreateEnvironment();
            environment[variableName] = invalidValue;

            InvalidOperationException error =
                TestSupport.Throws<InvalidOperationException>(
                    () => SchedulerOptions.Load(
                        name => GetValue(environment, name),
                        name => GetValue(CreateAppSettings(), name)));

            StringAssert.Contains(error.Message, variableName);
            Assert.IsFalse(error.Message.Contains(invalidValue));
        }

        [TestMethod]
        public void Options_RejectsUnknownQuartzTimeZoneWithoutEchoingItsValue()
        {
            const string invalidTimeZone = "Synthetic/Unknown/Secret";
            Dictionary<string, string> environment = CreateEnvironment();
            environment[SchedulerOptions.QuartzTimeZoneEnvironmentVariable] =
                invalidTimeZone;

            InvalidOperationException error =
                TestSupport.Throws<InvalidOperationException>(
                    () => SchedulerOptions.Load(
                        name => GetValue(environment, name),
                        name => GetValue(CreateAppSettings(), name)));

            StringAssert.Contains(
                error.Message,
                SchedulerOptions.QuartzTimeZoneEnvironmentVariable);
            Assert.IsFalse(error.Message.Contains(invalidTimeZone));
        }

        [TestMethod]
        public void Options_RejectsUnknownProviderModeWithoutEchoingItsValue()
        {
            Dictionary<string, string> environment = CreateEnvironment();
            environment[SchedulerOptions.ParameterProviderModeEnvironmentVariable] =
                "not-a-supported-mode";

            InvalidOperationException error =
                TestSupport.Throws<InvalidOperationException>(
                    () => SchedulerOptions.Load(
                        name => GetValue(environment, name),
                        name => GetValue(CreateAppSettings(), name)));

            StringAssert.Contains(
                error.Message,
                SchedulerOptions.ParameterProviderModeEnvironmentVariable);
            Assert.IsFalse(error.Message.Contains("not-a-supported-mode"));
        }

        [TestMethod]
        public async Task Snapshot_ConcurrentCallsPerformOneBatchLoad()
        {
            using (ManualResetEventSlim loadStarted = new ManualResetEventSlim())
            using (ManualResetEventSlim releaseLoad = new ManualResetEventSlim())
            {
                FakeParameterSource source = new FakeParameterSource(
                    _ =>
                    {
                        loadStarted.Set();
                        if (!releaseLoad.Wait(TimeSpan.FromSeconds(5)))
                        {
                            throw new TimeoutException("Synthetic coordination timeout.");
                        }

                        return CreateParameters();
                    });
                StartupParameterSnapshotProvider provider =
                    new StartupParameterSnapshotProvider(source, "map-secret");

                Assert.AreEqual(0, source.CallCount, "El constructor no debe hacer I/O.");

                Task<LaboratoryConstants>[] calls = Enumerable
                    .Range(0, 8)
                    .Select(_ => Task.Run(() => provider.GetSnapshot()))
                    .ToArray();

                Assert.IsTrue(loadStarted.Wait(TimeSpan.FromSeconds(5)));
                releaseLoad.Set();
                LaboratoryConstants[] snapshots = await Task.WhenAll(calls);

                Assert.AreEqual(1, source.CallCount);
                Assert.IsTrue(snapshots.All(item => ReferenceEquals(item, snapshots[0])));
            }
        }

        [TestMethod]
        public void Snapshot_FailedLoadIsNotCached()
        {
            int attempt = 0;
            FakeParameterSource source = new FakeParameterSource(
                _ =>
                {
                    attempt++;
                    if (attempt == 1)
                    {
                        throw new InvalidOperationException("Synthetic transient failure.");
                    }

                    return CreateParameters();
                });
            StartupParameterSnapshotProvider provider =
                new StartupParameterSnapshotProvider(source, null);

            TestSupport.Throws<InvalidOperationException>(
                () => provider.GetSnapshot());
            LaboratoryConstants snapshot = provider.GetSnapshot();

            Assert.IsNotNull(snapshot);
            Assert.AreEqual(2, source.CallCount);
        }

        [TestMethod]
        public void Snapshot_MissingRequiredParameterFailsByName()
        {
            Dictionary<string, string> values = CreateParameters();
            values.Remove("MAIL_SMTP");
            StartupParameterSnapshotProvider provider =
                CreateProvider(values);

            InvalidOperationException error =
                TestSupport.Throws<InvalidOperationException>(
                    () => provider.GetSnapshot());

            StringAssert.Contains(error.Message, "MAIL_SMTP");
        }

        [TestMethod]
        public void Snapshot_RejectsInvalidSmtpPort()
        {
            Dictionary<string, string> values = CreateParameters();
            values["MAIL_PORT"] = "70000";

            InvalidOperationException error =
                TestSupport.Throws<InvalidOperationException>(
                    () => CreateProvider(values).GetSnapshot());

            StringAssert.Contains(error.Message, "MAIL_PORT");
        }

        [TestMethod]
        public void Snapshot_RejectsInvalidSslFlag()
        {
            Dictionary<string, string> values = CreateParameters();
            values["MAIL_SSL"] = "true";

            InvalidOperationException error =
                TestSupport.Throws<InvalidOperationException>(
                    () => CreateProvider(values).GetSnapshot());

            StringAssert.Contains(error.Message, "MAIL_SSL");
        }

        [TestMethod]
        public void Snapshot_IsImmutableAndDoesNotPrintSecrets()
        {
            Dictionary<string, string> values = CreateParameters();
            values["MAIL_PASS"] = "synthetic-mail-secret";
            LaboratoryConstants snapshot =
                CreateProvider(values, "synthetic-map-secret").GetSnapshot();

            Assert.IsTrue(
                typeof(LaboratoryConstants)
                    .GetProperties()
                    .All(property => !property.CanWrite));
            Assert.IsFalse(snapshot.ToString().Contains("synthetic-mail-secret"));
            Assert.IsFalse(snapshot.ToString().Contains("synthetic-map-secret"));
            StringAssert.Contains(snapshot.ToString(), "[REDACTED]");
        }

        [TestMethod]
        public void LegacySourceReadsEachRequiredParameterForRollbackOnly()
        {
            Dictionary<string, string> values = CreateParameters();
            int readCount = 0;
            LegacySystemParameterSource source =
                new LegacySystemParameterSource(
                    name =>
                    {
                        readCount++;
                        return values[name];
                    });

            IReadOnlyDictionary<string, string> loaded =
                source.Load(LaboratoryParameterNames.All);

            Assert.AreEqual(LaboratoryParameterNames.All.Count, readCount);
            Assert.AreEqual(LaboratoryParameterNames.All.Count, loaded.Count);
        }

        [TestMethod]
        public void ComposedJobFactoryCreatesKnownJobsWithoutParameterIo()
        {
            FakeParameterSource source =
                new FakeParameterSource(_ => CreateParameters());
            LaboratoryConstants snapshot =
                new StartupParameterSnapshotProvider(source, null).GetSnapshot();
            ComposedJobFactory factory = new ComposedJobFactory(
                new SchedulerOptions(
                    "Server=sql.example.test;Database=p360;Integrated Security=true;",
                    null,
                    "SELECT * FROM synthetic_reports",
                    "SELECT * FROM synthetic_queue WHERE report_id = @ReportId",
                    ParameterProviderMode.Batch),
                snapshot,
                new ModuleCapaAccesoDatos(
                    "Server=sql.example.test;Database=p360;Integrated Security=true;"));

            Assert.IsInstanceOfType(
                factory.CreateJob(typeof(P360CrystalReportsReportJob)),
                typeof(P360CrystalReportsReportJob));
            Assert.IsInstanceOfType(
                factory.CreateJob(typeof(P360DevExpressReportsReportJob)),
                typeof(P360DevExpressReportsReportJob));
            Assert.IsInstanceOfType(
                factory.CreateJob(typeof(P360HtmlReportsReportJob)),
                typeof(P360HtmlReportsReportJob));

            ReportJobFactory reportJobFactory = new ReportJobFactory();
            ReportScheduleDefinition definition = TestSupport.CreateReport(
                reportType: "html");
            IJobDetail jobDetail = reportJobFactory.CreateJob(definition);
            IOperableTrigger trigger = (IOperableTrigger)
                reportJobFactory.CreateTrigger(definition);
            TriggerFiredBundle bundle = new TriggerFiredBundle(
                jobDetail,
                trigger,
                null,
                false,
                DateTimeOffset.UtcNow,
                null,
                null,
                null);
            Assert.IsInstanceOfType(
                factory.NewJob(bundle, null),
                typeof(ObservedJob));
            Assert.AreEqual(1, source.CallCount);
        }

        [TestMethod]
        public void BusinessExceptionCreationMapsMessageWithoutExternalIo()
        {
            BusinessP360Exception error = new BusinessP360Exception(
                "-2",
                "synthetic-original-message",
                "synthetic-procedure");

            StringAssert.Contains(error.Message, "demora inusual");
            Assert.AreEqual(error.Message, error.ErrorP360Mensaje);
            Assert.AreEqual("synthetic-procedure", error.ErrorP360Procedimiento);
        }

        private static StartupParameterSnapshotProvider CreateProvider(
            IReadOnlyDictionary<string, string> values,
            string googleMapsApiKey = null)
        {
            return new StartupParameterSnapshotProvider(
                new FakeParameterSource(_ => values),
                googleMapsApiKey);
        }

        private static Dictionary<string, string> CreateEnvironment()
        {
            return new Dictionary<string, string>
            {
                [SchedulerOptions.ConnectionStringEnvironmentVariable] =
                    "Server=sql.example.test;Database=p360;Integrated Security=true;"
            };
        }

        private static Dictionary<string, string> CreateAppSettings()
        {
            return new Dictionary<string, string>
            {
                [SchedulerOptions.ReportsQuerySetting] =
                    "SELECT * FROM synthetic_reports",
                [SchedulerOptions.NotificationQueueQuerySetting] =
                    "SELECT * FROM synthetic_queue WHERE report_id = @ReportId"
            };
        }

        private static Dictionary<string, string> CreateParameters()
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["MAIL_SSL"] = "1",
                ["LABORATORIO_URL_LOGO"] = "https://example.test/logo.png",
                ["LABORATORIO_IMPLEMENTACION"] = "Laboratorio sintético",
                ["MAIL_ADMINISTRADOR_LABORATORIO"] = "admin@example.test",
                ["MAIL_SMTP"] = "smtp.example.test",
                ["MAIL_USER"] = "sender@example.test",
                ["MAIL_PASS"] = "synthetic-value",
                ["MAIL_PORT"] = "2525",
                ["EMPRESA_PAIS"] = "EC",
                ["EMPRESA_CIUDAD"] = "Quito",
                ["EMPRESA_DIRECCION"] = "Dirección sintética",
                ["EMPRESA_SITIO_WEB"] = "https://example.test",
                ["EMPRESA_EMAIL_CONTACTO"] = "contact@example.test",
                ["EMPRESA_TELEFONO_CONTACTO"] = "+593000000000"
            };
        }

        private static string GetValue(
            IReadOnlyDictionary<string, string> values,
            string name)
        {
            string value;
            return values.TryGetValue(name, out value) ? value : null;
        }

        private sealed class FakeParameterSource : ISystemParameterSource
        {
            private readonly Func<
                IReadOnlyCollection<string>,
                IReadOnlyDictionary<string, string>> load;
            private int callCount;

            public FakeParameterSource(
                Func<
                    IReadOnlyCollection<string>,
                    IReadOnlyDictionary<string, string>> load)
            {
                this.load = load ?? throw new ArgumentNullException(nameof(load));
            }

            public int CallCount => Volatile.Read(ref callCount);

            public IReadOnlyDictionary<string, string> Load(
                IReadOnlyCollection<string> parameterNames)
            {
                Interlocked.Increment(ref callCount);
                return load(parameterNames);
            }
        }
    }
}
