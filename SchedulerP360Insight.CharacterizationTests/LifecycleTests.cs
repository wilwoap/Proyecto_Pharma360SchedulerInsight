using Microsoft.VisualStudio.TestTools.UnitTesting;
using SchedulerP360Insight.Configuration;
using SchedulerP360Insight.Hosting;
using SchedulerP360Insight.Observability;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SchedulerP360Insight.CharacterizationTests
{
    [TestClass]
    [DoNotParallelize]
    public sealed class LifecycleTests
    {
        [TestMethod]
        public async Task Run_StartsWaitsAndStopsInOrder()
        {
            List<string> sequence = new List<string>();
            FakeLifetime lifetime = new FakeLifetime();
            FakeSchedulerLifecycle scheduler =
                new FakeSchedulerLifecycle(sequence);
            using (OperationalTelemetry telemetry = new OperationalTelemetry(
                new JsonLineStructuredEventSink(new StringWriter()),
                NullHealthPublisher.Instance,
                TimeSpan.FromHours(1)))
            {
                SchedulerApplication application = CreateApplication(
                    new FakeRegistrar(sequence),
                    scheduler,
                    lifetime,
                    TimeSpan.FromSeconds(1),
                    telemetry);

                Task<SchedulerRunResult> run = application.RunAsync(
                    CancellationToken.None);
                await scheduler.Started.Task;

                Assert.IsFalse(run.IsCompleted);
                lifetime.StopApplication();
                SchedulerRunResult result = await run;

                Assert.AreEqual(SchedulerRunResult.Completed, result);
                CollectionAssert.AreEqual(
                    new[] { "Register", "Start", "Standby", "Shutdown:True" },
                    sequence);
                Assert.AreEqual("stopped", telemetry.GetHealthSnapshot().State);
                Assert.AreEqual(
                    3,
                    telemetry.GetMetricSnapshot().Count(item =>
                        item.Outcome == TelemetryOutcomes.Success &&
                        item.Operation.StartsWith(
                            "scheduler.",
                            StringComparison.Ordinal)));
            }

            lifetime.Dispose();
        }

        [TestMethod]
        public async Task ShutdownTimeout_RequestsNonWaitingShutdownAndReportsIt()
        {
            List<string> sequence = new List<string>();
            FakeLifetime lifetime = new FakeLifetime();
            lifetime.StopApplication();
            FakeSchedulerLifecycle scheduler =
                new FakeSchedulerLifecycle(sequence)
                {
                    BlockGracefulShutdown = true
                };
            SchedulerApplication application = CreateApplication(
                new FakeRegistrar(sequence),
                scheduler,
                lifetime,
                TimeSpan.FromMilliseconds(40));

            SchedulerRunResult result = await application.RunAsync(
                CancellationToken.None);

            Assert.AreEqual(SchedulerRunResult.ShutdownTimedOut, result);
            CollectionAssert.AreEqual(
                new[]
                {
                    "Register",
                    "Start",
                    "Standby",
                    "Shutdown:True",
                    "Shutdown:False"
                },
                sequence);
            lifetime.Dispose();
        }

        [TestMethod]
        public async Task RegistrationFailure_ForcesShutdownWithoutStarting()
        {
            List<string> sequence = new List<string>();
            FakeLifetime lifetime = new FakeLifetime();
            FakeSchedulerLifecycle scheduler =
                new FakeSchedulerLifecycle(sequence);
            FakeRegistrar registrar = new FakeRegistrar(sequence)
            {
                Failure = new InvalidOperationException(
                    "Synthetic registration failure.")
            };
            SchedulerApplication application = CreateApplication(
                registrar,
                scheduler,
                lifetime,
                TimeSpan.FromSeconds(1));

            InvalidOperationException observed = null;
            try
            {
                await application.RunAsync(CancellationToken.None);
            }
            catch (InvalidOperationException error)
            {
                observed = error;
            }

            Assert.IsNotNull(observed);
            CollectionAssert.AreEqual(
                new[] { "Register", "Shutdown:False" },
                sequence);
            lifetime.Dispose();
        }

        [TestMethod]
        public async Task CallerCancellation_ForcesShutdownAndPropagatesCancellation()
        {
            List<string> sequence = new List<string>();
            FakeLifetime lifetime = new FakeLifetime();
            FakeSchedulerLifecycle scheduler =
                new FakeSchedulerLifecycle(sequence);
            SchedulerApplication application = CreateApplication(
                new FakeRegistrar(sequence),
                scheduler,
                lifetime,
                TimeSpan.FromSeconds(1));
            using (CancellationTokenSource cancellation =
                new CancellationTokenSource())
            {
                Task<SchedulerRunResult> run = application.RunAsync(
                    cancellation.Token);
                await scheduler.Started.Task;
                cancellation.Cancel();

                OperationCanceledException observed = null;
                try
                {
                    await run;
                }
                catch (OperationCanceledException error)
                {
                    observed = error;
                }

                Assert.IsNotNull(observed);
                CollectionAssert.AreEqual(
                    new[] { "Register", "Start", "Shutdown:False" },
                    sequence);
            }

            lifetime.Dispose();
        }

        [TestMethod]
        public async Task ConsoleLifetime_StopIsIdempotentAndUnblocksWaiter()
        {
            using (ConsoleApplicationLifetime lifetime =
                new ConsoleApplicationLifetime())
            {
                Task wait = lifetime.WaitForStopAsync(CancellationToken.None);

                lifetime.StopApplication();
                lifetime.StopApplication();
                await wait;

                Assert.IsTrue(lifetime.Stopping.IsCancellationRequested);
            }
        }

        [TestMethod]
        public async Task ProcessHelpAndInvalidArgumentsDoNotRequireConfiguration()
        {
            int help = await SchedulerProcess.RunAsync(new[] { "--help" });
            int invalid = await SchedulerProcess.RunAsync(
                new[] { "--unknown" });

            Assert.AreEqual((int)ApplicationExitCode.Success, help);
            Assert.AreEqual(
                (int)ApplicationExitCode.InvalidConfiguration,
                invalid);
        }

        [TestMethod]
        public void ShutdownTimeout_DefaultsToThirtySecondsAndValidatesRange()
        {
            Dictionary<string, string> environment =
                new Dictionary<string, string>
                {
                    [SchedulerOptions.ConnectionStringEnvironmentVariable] =
                        "synthetic-connection"
                };
            Dictionary<string, string> settings =
                new Dictionary<string, string>
                {
                    [SchedulerOptions.ReportsQuerySetting] = "SELECT reports",
                    [SchedulerOptions.NotificationQueueQuerySetting] =
                        "SELECT notifications"
                };

            SchedulerOptions defaults = SchedulerOptions.Load(
                name => GetValue(environment, name),
                name => GetValue(settings, name));
            Assert.AreEqual(TimeSpan.FromSeconds(30), defaults.ShutdownTimeout);

            environment[SchedulerOptions.ShutdownTimeoutSecondsEnvironmentVariable] =
                "75";
            SchedulerOptions configured = SchedulerOptions.Load(
                name => GetValue(environment, name),
                name => GetValue(settings, name));
            Assert.AreEqual(TimeSpan.FromSeconds(75), configured.ShutdownTimeout);

            environment[SchedulerOptions.ShutdownTimeoutSecondsEnvironmentVariable] =
                "901";
            InvalidOperationException error =
                TestSupport.Throws<InvalidOperationException>(
                    () => SchedulerOptions.Load(
                        name => GetValue(environment, name),
                        name => GetValue(settings, name)));
            StringAssert.Contains(
                error.Message,
                SchedulerOptions.ShutdownTimeoutSecondsEnvironmentVariable);
            Assert.IsFalse(error.Message.Contains("901"));
        }

        [TestMethod]
        public void ProductionSourceContainsNoInteractivePromptOrMessageBox()
        {
            string root = TestSupport.FindRepositoryRoot();
            string sourceRoot = Path.Combine(root, "SchedulerP360Insight");
            string[] forbidden =
            {
                "Console.ReadKey(",
                "Console.ReadLine(",
                "MessageBox.Show(",
                "Environment.Exit("
            };

            List<string> findings = Directory
                .EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => forbidden.Any(item =>
                    File.ReadAllText(path).Contains(item)))
                .Select(path => path.Substring(root.Length + 1))
                .ToList();

            Assert.AreEqual(
                0,
                findings.Count,
                "Se encontraron caminos interactivos: " +
                string.Join(", ", findings));
        }

        private static SchedulerApplication CreateApplication(
            IReportScheduleRegistrar registrar,
            ISchedulerLifecycle scheduler,
            IApplicationLifetime lifetime,
            TimeSpan shutdownTimeout,
            IOperationalTelemetry telemetry = null)
        {
            return new SchedulerApplication(
                registrar,
                scheduler,
                lifetime,
                new FakeEventSink(),
                shutdownTimeout,
                telemetry);
        }

        private static string GetValue(
            IReadOnlyDictionary<string, string> values,
            string name)
        {
            string value;
            return values.TryGetValue(name, out value) ? value : null;
        }

        private sealed class FakeRegistrar : IReportScheduleRegistrar
        {
            private readonly List<string> sequence;

            public FakeRegistrar(List<string> sequence)
            {
                this.sequence = sequence;
            }

            public Exception Failure { get; set; }

            public Task<int> RegisterAsync(CancellationToken cancellationToken)
            {
                sequence.Add("Register");
                cancellationToken.ThrowIfCancellationRequested();
                if (Failure != null)
                {
                    return Task.FromException<int>(Failure);
                }

                return Task.FromResult(3);
            }
        }

        private sealed class FakeSchedulerLifecycle : ISchedulerLifecycle
        {
            private readonly List<string> sequence;

            public FakeSchedulerLifecycle(List<string> sequence)
            {
                this.sequence = sequence;
            }

            public bool BlockGracefulShutdown { get; set; }

            public TaskCompletionSource<bool> Started { get; } =
                new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

            public Task StartAsync(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                sequence.Add("Start");
                Started.TrySetResult(true);
                return Task.CompletedTask;
            }

            public Task StandbyAsync(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                sequence.Add("Standby");
                return Task.CompletedTask;
            }

            public async Task ShutdownAsync(
                bool waitForJobsToComplete,
                CancellationToken cancellationToken)
            {
                sequence.Add("Shutdown:" + waitForJobsToComplete);
                if (waitForJobsToComplete && BlockGracefulShutdown)
                {
                    await Task.Delay(Timeout.Infinite, cancellationToken);
                }
            }
        }

        private sealed class FakeLifetime : IApplicationLifetime
        {
            private readonly CancellationTokenSource stopping =
                new CancellationTokenSource();
            private readonly TaskCompletionSource<bool> stopped =
                new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

            public CancellationToken Stopping => stopping.Token;

            public void StopApplication()
            {
                if (stopped.TrySetResult(true))
                {
                    stopping.Cancel();
                }
            }

            public Task WaitForStopAsync(CancellationToken cancellationToken)
            {
                if (!cancellationToken.CanBeCanceled)
                {
                    return stopped.Task;
                }

                return WaitWithCancellationAsync(cancellationToken);
            }

            public void Dispose()
            {
                stopping.Dispose();
            }

            private async Task WaitWithCancellationAsync(
                CancellationToken cancellationToken)
            {
                Task cancellation = Task.Delay(
                    Timeout.Infinite,
                    cancellationToken);
                Task completed = await Task.WhenAny(stopped.Task, cancellation);
                await completed;
            }
        }

        private sealed class FakeEventSink : IApplicationEventSink
        {
            public void Write(
                string eventName,
                IReadOnlyDictionary<string, string> fields = null)
            {
            }
        }
    }
}
