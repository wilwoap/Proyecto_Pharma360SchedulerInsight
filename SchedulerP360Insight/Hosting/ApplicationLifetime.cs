using System;
using System.Threading;
using System.Threading.Tasks;

namespace SchedulerP360Insight.Hosting
{
    public interface IApplicationLifetime : IDisposable
    {
        CancellationToken Stopping { get; }

        void StopApplication();

        Task WaitForStopAsync(CancellationToken cancellationToken);
    }

    public sealed class ConsoleApplicationLifetime : IApplicationLifetime
    {
        private readonly CancellationTokenSource stopping =
            new CancellationTokenSource();
        private readonly TaskCompletionSource<bool> stopCompletion =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        private int disposed;

        public ConsoleApplicationLifetime()
        {
            Console.CancelKeyPress += OnCancelKeyPress;
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        }

        public CancellationToken Stopping => stopping.Token;

        public void StopApplication()
        {
            if (!stopCompletion.TrySetResult(true))
            {
                return;
            }

            try
            {
                stopping.Cancel(throwOnFirstException: false);
            }
            catch (AggregateException)
            {
                // La señal de parada no debe ser anulada por un callback defectuoso.
            }
        }

        public Task WaitForStopAsync(CancellationToken cancellationToken)
        {
            if (!cancellationToken.CanBeCanceled)
            {
                return stopCompletion.Task;
            }

            return WaitWithCancellationAsync(cancellationToken);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
            {
                return;
            }

            Console.CancelKeyPress -= OnCancelKeyPress;
            AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
            stopping.Dispose();
        }

        private async Task WaitWithCancellationAsync(
            CancellationToken cancellationToken)
        {
            TaskCompletionSource<bool> cancellation =
                new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(
                () => cancellation.TrySetCanceled()))
            {
                Task completed = await Task.WhenAny(
                    stopCompletion.Task,
                    cancellation.Task);
                await completed;
            }
        }

        private void OnCancelKeyPress(
            object sender,
            ConsoleCancelEventArgs eventArgs)
        {
            eventArgs.Cancel = true;
            StopApplication();
        }

        private void OnProcessExit(object sender, EventArgs eventArgs)
        {
            StopApplication();
        }
    }
}
