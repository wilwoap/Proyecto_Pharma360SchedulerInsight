using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quartz;
using Quartz.Impl;
using System;
using System.Collections.Specialized;
using System.Threading.Tasks;

namespace SchedulerP360Insight.CharacterizationTests
{
    [TestClass]
    [DoNotParallelize]
    public sealed class QuartzRuntimeCompatibilityTests
    {
        [TestMethod]
        [Timeout(10000)]
        public async Task RamScheduler_StartsExecutesJobAndShutsDown()
        {
            NameValueCollection properties = new NameValueCollection
            {
                ["quartz.scheduler.instanceName"] = "P360Characterization",
                ["quartz.scheduler.instanceId"] = "AUTO",
                ["quartz.threadPool.threadCount"] = "1",
                ["quartz.jobStore.type"] = "Quartz.Simpl.RAMJobStore, Quartz"
            };

            TaskCompletionSource<bool> execution =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            ProbeJob.Execution = execution;

            IScheduler scheduler = await new StdSchedulerFactory(properties).GetScheduler();
            try
            {
                await scheduler.Start();

                IJobDetail job = JobBuilder.Create<ProbeJob>()
                    .WithIdentity("compatibility-job", "characterization")
                    .Build();
                ITrigger trigger = TriggerBuilder.Create()
                    .WithIdentity("compatibility-trigger", "characterization")
                    .StartNow()
                    .Build();

                await scheduler.ScheduleJob(job, trigger);

                Task completed = await Task.WhenAny(
                    execution.Task,
                    Task.Delay(TimeSpan.FromSeconds(5)));
                Assert.AreSame(execution.Task, completed, "Quartz no ejecuto el job en el tiempo esperado.");
                Assert.IsTrue(await execution.Task);
            }
            finally
            {
                await scheduler.Shutdown(waitForJobsToComplete: true);
                ProbeJob.Execution = null;
            }

            Assert.IsTrue(scheduler.IsShutdown);
        }

        public sealed class ProbeJob : IJob
        {
            public static TaskCompletionSource<bool> Execution { get; set; }

            public Task Execute(IJobExecutionContext context)
            {
                TaskCompletionSource<bool> execution = Execution;
                if (execution == null)
                {
                    throw new InvalidOperationException("La sonda Quartz no fue inicializada.");
                }

                execution.TrySetResult(true);
                return Task.CompletedTask;
            }
        }
    }
}
