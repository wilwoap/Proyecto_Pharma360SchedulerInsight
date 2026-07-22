using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quartz;
using Quartz.Impl;
using Quartz.Impl.Matchers;
using SchedulerP360Insight.Configuration;
using SchedulerP360Insight.Hosting;
using SchedulerP360Insight.Observability;
using SchedulerP360Insight.Scheduling;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SchedulerP360Insight.CharacterizationTests
{
    [TestClass]
    [DoNotParallelize]
    public sealed class QuartzHardeningTests
    {
        [TestMethod]
        [Timeout(15000)]
        public async Task Registrar_IsolatesBadRowsAndReconcilesChangesIdempotently()
        {
            IScheduler scheduler = await CreateSchedulerAsync(2);
            MutableScheduleSource source = new MutableScheduleSource();
            RecordingApplicationEventSink events =
                new RecordingApplicationEventSink();
            ReportJobFactory factory = new ReportJobFactory(
                new QuartzSchedulingPolicy(
                    TimeZoneInfo.Utc,
                    QuartzMisfirePolicy.FireOnceNow,
                    disallowConcurrentExecution: true));
            QuartzReportScheduleRegistrar registrar =
                new QuartzReportScheduleRegistrar(
                    scheduler,
                    source,
                    factory,
                    events);
            try
            {
                ReportScheduleDefinition valid =
                    TestSupport.CreateReportWithId(10);
                source.Definitions = new[]
                {
                    valid,
                    TestSupport.CreateReportWithId(
                        11,
                        cron: "invalid-cron")
                };

                Assert.AreEqual(
                    1,
                    await registrar.RegisterAsync(CancellationToken.None));
                Assert.IsTrue(await scheduler.CheckExists(
                    factory.GetJobKey(10)));
                Assert.IsFalse(await scheduler.CheckExists(
                    factory.GetJobKey(11)));
                Assert.IsTrue(events.Names.Contains(
                    "scheduler.definition.rejected"));

                string firstFingerprint = (await scheduler.GetJobDetail(
                    factory.GetJobKey(10))).JobDataMap.GetString(
                        ReportJobFactory.ScheduleFingerprintKey);
                Assert.AreEqual(
                    1,
                    await registrar.RegisterAsync(CancellationToken.None));
                Assert.IsTrue(events.Names.Contains(
                    "scheduler.definition.unchanged"));
                Assert.AreEqual(
                    1,
                    (await scheduler.GetJobKeys(
                        GroupMatcher<JobKey>.GroupEquals(
                            ReportJobFactory.SchedulerGroup))).Count);

                source.Definitions = new[]
                {
                    TestSupport.CreateReportWithId(
                        10,
                        cron: "0 0/10 * * * ?"),
                    TestSupport.CreateReportWithId(
                        12,
                        reportName: valid.ReportName)
                };
                Assert.AreEqual(
                    2,
                    await registrar.RegisterAsync(CancellationToken.None));
                ICronTrigger changedTrigger = (ICronTrigger)
                    await scheduler.GetTrigger(factory.GetTriggerKey(10));
                Assert.AreEqual(
                    "0 0/10 * * * ?",
                    changedTrigger.CronExpressionString);
                Assert.AreNotEqual(
                    firstFingerprint,
                    (await scheduler.GetJobDetail(
                        factory.GetJobKey(10))).JobDataMap.GetString(
                            ReportJobFactory.ScheduleFingerprintKey));
                Assert.IsTrue(await scheduler.CheckExists(
                    factory.GetJobKey(12)));

                source.Definitions = Array.Empty<ReportScheduleDefinition>();
                Assert.AreEqual(
                    0,
                    await registrar.RegisterAsync(CancellationToken.None));
                Assert.AreEqual(
                    0,
                    (await scheduler.GetJobKeys(
                        GroupMatcher<JobKey>.GroupEquals(
                            ReportJobFactory.SchedulerGroup))).Count);
                Assert.IsTrue(events.Names.Contains(
                    "scheduler.definition.removed"));
            }
            finally
            {
                await ShutdownAsync(scheduler);
            }
        }

        [TestMethod]
        public void Cron_UsesAnExplicitSyntheticDstZone()
        {
            TimeZoneInfo zone = CreateSyntheticDstTimeZone();
            CronExpression expression = new CronExpression("0 30 1 * * ?")
            {
                TimeZone = zone
            };

            DateTimeOffset? beforeTransition = expression.GetTimeAfter(
                new DateTimeOffset(2026, 3, 7, 7, 0, 0, TimeSpan.Zero));
            DateTimeOffset? afterTransition = expression.GetTimeAfter(
                beforeTransition.Value);

            Assert.IsTrue(beforeTransition.HasValue);
            Assert.IsTrue(afterTransition.HasValue);
            Assert.AreEqual(
                TimeSpan.FromHours(23),
                afterTransition.Value - beforeTransition.Value);
        }

        [TestMethod]
        [Timeout(15000)]
        public async Task MisfirePolicies_FireOnceOrSkipAfterStandby()
        {
            IScheduler scheduler = await CreateSchedulerAsync(
                2,
                misfireThresholdMilliseconds: 100);
            FireOnceMisfireProbe.Execution = NewCompletionSource();
            SkipMisfireProbe.Execution = NewCompletionSource();
            try
            {
                DateTimeOffset historicalStart =
                    DateTimeOffset.UtcNow.AddDays(-2);
                IJobDetail fireJob = JobBuilder
                    .Create<FireOnceMisfireProbe>()
                    .WithIdentity("fire-once-job", "misfire-tests")
                    .Build();
                ITrigger fireTrigger = TriggerBuilder.Create()
                    .WithIdentity("fire-once-trigger", "misfire-tests")
                    .ForJob(fireJob.Key)
                    .StartAt(historicalStart)
                    .WithSchedule(CronScheduleBuilder
                        .CronSchedule("0 0 0 * * ?")
                        .InTimeZone(TimeZoneInfo.Utc)
                        .WithMisfireHandlingInstructionFireAndProceed())
                    .Build();
                IJobDetail skipJob = JobBuilder
                    .Create<SkipMisfireProbe>()
                    .WithIdentity("skip-job", "misfire-tests")
                    .Build();
                ITrigger skipTrigger = TriggerBuilder.Create()
                    .WithIdentity("skip-trigger", "misfire-tests")
                    .ForJob(skipJob.Key)
                    .StartAt(historicalStart)
                    .WithSchedule(CronScheduleBuilder
                        .CronSchedule("0 0 0 * * ?")
                        .InTimeZone(TimeZoneInfo.Utc)
                        .WithMisfireHandlingInstructionDoNothing())
                    .Build();

                await scheduler.ScheduleJob(fireJob, fireTrigger);
                await scheduler.ScheduleJob(skipJob, skipTrigger);
                await Task.Delay(250);
                await scheduler.Start();

                await AssertCompletesAsync(
                    FireOnceMisfireProbe.Execution.Task,
                    TimeSpan.FromSeconds(5),
                    "La política fire_once_now no ejecutó el misfire.");
                Task skipRace = await Task.WhenAny(
                    SkipMisfireProbe.Execution.Task,
                    Task.Delay(500));
                Assert.AreNotSame(
                    SkipMisfireProbe.Execution.Task,
                    skipRace,
                    "La política do_nothing no debe ejecutar el evento perdido.");
            }
            finally
            {
                await ShutdownAsync(scheduler);
                FireOnceMisfireProbe.Execution = null;
                SkipMisfireProbe.Execution = null;
            }
        }

        [TestMethod]
        [Timeout(15000)]
        public async Task DisallowConcurrentExecution_PreventsSameReportOverlap()
        {
            IScheduler scheduler = await CreateSchedulerAsync(2);
            DelayedConcurrencyProbe.Reset(expectedExecutions: 2);
            try
            {
                IJobDetail job = JobBuilder.Create<DelayedConcurrencyProbe>()
                    .WithIdentity("same-report", "overlap-tests")
                    .DisallowConcurrentExecution(true)
                    .Build();
                IReadOnlyCollection<ITrigger> triggers = new[]
                {
                    TriggerBuilder.Create()
                        .WithIdentity("simultaneous-a", "overlap-tests")
                        .ForJob(job.Key)
                        .StartNow()
                        .Build(),
                    TriggerBuilder.Create()
                        .WithIdentity("simultaneous-b", "overlap-tests")
                        .ForJob(job.Key)
                        .StartNow()
                        .Build()
                };

                await scheduler.ScheduleJob(job, triggers, replace: false);
                await scheduler.Start();
                await AssertCompletesAsync(
                    DelayedConcurrencyProbe.Completed.Task,
                    TimeSpan.FromSeconds(5),
                    "Los dos triggers no finalizaron.");

                Assert.AreEqual(2, DelayedConcurrencyProbe.Executions);
                Assert.AreEqual(1, DelayedConcurrencyProbe.MaximumConcurrent);
            }
            finally
            {
                await ShutdownAsync(scheduler);
                DelayedConcurrencyProbe.Release();
            }
        }

        [TestMethod]
        [Timeout(15000)]
        public async Task ThreadPoolLimit_BoundsActiveJobsAndLeavesBacklog()
        {
            IScheduler scheduler = await CreateSchedulerAsync(2);
            BlockingConcurrencyProbe.Reset(expectedExecutions: 4);
            try
            {
                for (int index = 0; index < 4; index++)
                {
                    IJobDetail job = JobBuilder
                        .Create<BlockingConcurrencyProbe>()
                        .WithIdentity("bounded-job-" + index, "limit-tests")
                        .Build();
                    ITrigger trigger = TriggerBuilder.Create()
                        .WithIdentity(
                            "bounded-trigger-" + index,
                            "limit-tests")
                        .ForJob(job.Key)
                        .StartNow()
                        .Build();
                    await scheduler.ScheduleJob(job, trigger);
                }

                await scheduler.Start();
                await AssertCompletesAsync(
                    BlockingConcurrencyProbe.CapacityReached.Task,
                    TimeSpan.FromSeconds(5),
                    "Quartz no ocupó el límite configurado.");
                await Task.Delay(200);

                Assert.AreEqual(2, BlockingConcurrencyProbe.Started);
                Assert.AreEqual(2, BlockingConcurrencyProbe.MaximumConcurrent);
                BlockingConcurrencyProbe.Release();
                await AssertCompletesAsync(
                    BlockingConcurrencyProbe.Completed.Task,
                    TimeSpan.FromSeconds(5),
                    "El backlog no terminó después de liberar capacidad.");
                Assert.AreEqual(4, BlockingConcurrencyProbe.Started);
                Assert.AreEqual(2, BlockingConcurrencyProbe.MaximumConcurrent);
            }
            finally
            {
                BlockingConcurrencyProbe.Release();
                await ShutdownAsync(scheduler);
            }
        }

        [TestMethod]
        public void TriggerListener_RecordsMisfiresWithBoundedMetrics()
        {
            RecordingStructuredEventSink sink =
                new RecordingStructuredEventSink();
            using (OperationalTelemetry telemetry = new OperationalTelemetry(
                sink,
                new DisabledHealthPublisher()))
            {
                ReportJobFactory factory = new ReportJobFactory(
                    new QuartzSchedulingPolicy(
                        TimeZoneInfo.Utc,
                        QuartzMisfirePolicy.FireOnceNow,
                        disallowConcurrentExecution: true));
                ITrigger trigger = factory.CreateTrigger(
                    TestSupport.CreateReport());
                QuartzOperationalTriggerListener listener =
                    new QuartzOperationalTriggerListener(telemetry);

                listener.TriggerMisfired(
                    trigger,
                    CancellationToken.None).GetAwaiter().GetResult();

                OperationMetricSnapshot metric = telemetry
                    .GetMetricSnapshot()
                    .Single(item =>
                        item.Operation == TelemetryOperations.SchedulerMisfire &&
                        item.Outcome == TelemetryOutcomes.Skipped);
                Assert.AreEqual(1L, metric.Count);
                Assert.AreEqual(
                    1L,
                    telemetry.GetHealthSnapshot().Gauges.Single(item =>
                        item.Name == "scheduler_misfires_total").Value);
                Assert.IsTrue(sink.Records.Any(item =>
                    item.EventName == "scheduler.trigger.misfired"));
            }
        }

        [TestMethod]
        public void TriggerListener_NeverPropagatesTelemetryFailures()
        {
            QuartzOperationalTriggerListener listener =
                new QuartzOperationalTriggerListener(
                    new ThrowingOperationalTelemetry());
            ITrigger trigger = new ReportJobFactory().CreateTrigger(
                TestSupport.CreateReport());

            listener.TriggerMisfired(
                trigger,
                CancellationToken.None).GetAwaiter().GetResult();
        }

        private static async Task<IScheduler> CreateSchedulerAsync(
            int maxConcurrency,
            int misfireThresholdMilliseconds =
                QuartzSchedulerSettings.MisfireThresholdMilliseconds)
        {
            SchedulerOptions options = new SchedulerOptions(
                "Server=sql.example.test;Database=p360;Integrated Security=true;",
                null,
                "SELECT synthetic_reports",
                "SELECT synthetic_queue WHERE report_id = @ReportId",
                ParameterProviderMode.Batch,
                quartzTimeZone: TimeZoneInfo.Utc,
                quartzMaxConcurrency: maxConcurrency);
            NameValueCollection properties =
                QuartzSchedulerSettings.CreateProperties(
                    options,
                    "P360Test-" + Guid.NewGuid().ToString("N"));
            properties["quartz.jobStore.misfireThreshold"] =
                misfireThresholdMilliseconds.ToString();
            IScheduler scheduler =
                await new StdSchedulerFactory(properties).GetScheduler();
            SchedulerMetaData metadata = await scheduler.GetMetaData();
            Assert.AreEqual(maxConcurrency, metadata.ThreadPoolSize);
            Assert.IsFalse(metadata.JobStoreSupportsPersistence);
            return scheduler;
        }

        private static async Task ShutdownAsync(IScheduler scheduler)
        {
            if (scheduler != null && !scheduler.IsShutdown)
            {
                await scheduler.Shutdown(waitForJobsToComplete: false);
            }
        }

        private static async Task AssertCompletesAsync(
            Task task,
            TimeSpan timeout,
            string message)
        {
            Task completed = await Task.WhenAny(task, Task.Delay(timeout));
            Assert.AreSame(task, completed, message);
            await task;
        }

        private static TaskCompletionSource<bool> NewCompletionSource()
        {
            return new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        }

        private static TimeZoneInfo CreateSyntheticDstTimeZone()
        {
            TimeZoneInfo.TransitionTime start =
                TimeZoneInfo.TransitionTime.CreateFloatingDateRule(
                    new DateTime(1, 1, 1, 2, 0, 0),
                    3,
                    2,
                    DayOfWeek.Sunday);
            TimeZoneInfo.TransitionTime end =
                TimeZoneInfo.TransitionTime.CreateFloatingDateRule(
                    new DateTime(1, 1, 1, 2, 0, 0),
                    11,
                    1,
                    DayOfWeek.Sunday);
            TimeZoneInfo.AdjustmentRule rule =
                TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
                    new DateTime(2020, 1, 1),
                    new DateTime(2030, 12, 31),
                    TimeSpan.FromHours(1),
                    start,
                    end);
            return TimeZoneInfo.CreateCustomTimeZone(
                "P360-Synthetic-DST",
                TimeSpan.FromHours(-5),
                "Synthetic DST",
                "Synthetic Standard",
                "Synthetic Daylight",
                new[] { rule });
        }

        public sealed class FireOnceMisfireProbe : IJob
        {
            public static TaskCompletionSource<bool> Execution { get; set; }

            public Task Execute(IJobExecutionContext context)
            {
                Execution?.TrySetResult(true);
                return Task.CompletedTask;
            }
        }

        public sealed class SkipMisfireProbe : IJob
        {
            public static TaskCompletionSource<bool> Execution { get; set; }

            public Task Execute(IJobExecutionContext context)
            {
                Execution?.TrySetResult(true);
                return Task.CompletedTask;
            }
        }

        public sealed class DelayedConcurrencyProbe : IJob
        {
            private static int current;
            private static int executions;
            private static int maximumConcurrent;
            private static int expected;

            public static TaskCompletionSource<bool> Completed { get; private set; }

            public static int Executions => Volatile.Read(ref executions);

            public static int MaximumConcurrent =>
                Volatile.Read(ref maximumConcurrent);

            public static void Reset(int expectedExecutions)
            {
                current = 0;
                executions = 0;
                maximumConcurrent = 0;
                expected = expectedExecutions;
                Completed = NewCompletionSource();
            }

            public static void Release()
            {
                Completed?.TrySetResult(true);
            }

            public async Task Execute(IJobExecutionContext context)
            {
                int active = Interlocked.Increment(ref current);
                UpdateMaximum(ref maximumConcurrent, active);
                try
                {
                    await Task.Delay(200);
                    if (Interlocked.Increment(ref executions) == expected)
                    {
                        Completed.TrySetResult(true);
                    }
                }
                finally
                {
                    Interlocked.Decrement(ref current);
                }
            }
        }

        public sealed class BlockingConcurrencyProbe : IJob
        {
            private static int current;
            private static int started;
            private static int completedCount;
            private static int maximumConcurrent;
            private static int expected;

            public static TaskCompletionSource<bool> CapacityReached { get; private set; }

            public static TaskCompletionSource<bool> Completed { get; private set; }

            private static TaskCompletionSource<bool> ReleaseSignal { get; set; }

            public static int Started => Volatile.Read(ref started);

            public static int MaximumConcurrent =>
                Volatile.Read(ref maximumConcurrent);

            public static void Reset(int expectedExecutions)
            {
                current = 0;
                started = 0;
                completedCount = 0;
                maximumConcurrent = 0;
                expected = expectedExecutions;
                CapacityReached = NewCompletionSource();
                Completed = NewCompletionSource();
                ReleaseSignal = NewCompletionSource();
            }

            public static void Release()
            {
                ReleaseSignal?.TrySetResult(true);
            }

            public async Task Execute(IJobExecutionContext context)
            {
                int active = Interlocked.Increment(ref current);
                UpdateMaximum(ref maximumConcurrent, active);
                int observedStarted = Interlocked.Increment(ref started);
                if (observedStarted == 2)
                {
                    CapacityReached.TrySetResult(true);
                }

                try
                {
                    await ReleaseSignal.Task;
                }
                finally
                {
                    Interlocked.Decrement(ref current);
                    if (Interlocked.Increment(ref completedCount) == expected)
                    {
                        Completed.TrySetResult(true);
                    }
                }
            }
        }

        private static void UpdateMaximum(ref int location, int candidate)
        {
            int observed;
            do
            {
                observed = Volatile.Read(ref location);
                if (candidate <= observed)
                {
                    return;
                }
            }
            while (Interlocked.CompareExchange(
                ref location,
                candidate,
                observed) != observed);
        }

        private sealed class MutableScheduleSource : IReportScheduleSource
        {
            public IReadOnlyList<ReportScheduleDefinition> Definitions { get; set; } =
                Array.Empty<ReportScheduleDefinition>();

            public Task<IReadOnlyList<ReportScheduleDefinition>> LoadAsync(
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(Definitions);
            }
        }

        private sealed class RecordingApplicationEventSink :
            IApplicationEventSink
        {
            public List<string> Names { get; } = new List<string>();

            public void Write(
                string eventName,
                IReadOnlyDictionary<string, string> fields = null)
            {
                Names.Add(eventName);
            }
        }

        private sealed class RecordingStructuredEventSink :
            IStructuredEventSink
        {
            public ConcurrentQueue<StructuredEventRecord> Records { get; } =
                new ConcurrentQueue<StructuredEventRecord>();

            public void Write(StructuredEventRecord record)
            {
                Records.Enqueue(record);
            }
        }

        private sealed class DisabledHealthPublisher : IHealthPublisher
        {
            public bool Enabled => false;

            public void Publish(HealthSnapshot snapshot)
            {
            }
        }

        private sealed class ThrowingOperationalTelemetry :
            IOperationalTelemetry
        {
            public string ProcessCorrelationId => "synthetic";

            public string CreateCorrelationId()
            {
                return "synthetic";
            }

            public IOperationScope BeginOperation(
                string operation,
                string correlationId,
                IReadOnlyDictionary<string, string> fields = null)
            {
                throw new InvalidOperationException(
                    "Synthetic telemetry failure.");
            }

            public void Write(
                string level,
                string eventName,
                string correlationId = null,
                IReadOnlyDictionary<string, string> fields = null,
                Exception exception = null)
            {
                throw new InvalidOperationException(
                    "Synthetic telemetry failure.");
            }

            public void ObserveGauge(string metricName, long value)
            {
                throw new InvalidOperationException(
                    "Synthetic telemetry failure.");
            }

            public HealthSnapshot GetHealthSnapshot()
            {
                return new HealthSnapshot();
            }

            public IReadOnlyList<OperationMetricSnapshot> GetMetricSnapshot()
            {
                return Array.Empty<OperationMetricSnapshot>();
            }

            public void MarkStarting()
            {
            }

            public void MarkReady(int registeredDefinitions)
            {
            }

            public void MarkStopping()
            {
            }

            public void MarkStopped()
            {
            }

            public void MarkFaulted(string failureCategory)
            {
            }

            public void Dispose()
            {
            }
        }
    }
}
