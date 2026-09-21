using System;
using System.Threading;
using System.Threading.Tasks;

namespace WpfApp1.Services.Downloads
{
    /// <summary>
    /// Cooperative pause controller for streaming downloads. Pause keeps the
    /// partial file intact; cancellation is still able to break the wait.
    /// </summary>
    public sealed class PauseController : IDisposable
    {
        private readonly object _sync = new object();
        private TaskCompletionSource<bool> _resumeSignal = CreateCompletedSignal();
        private bool _paused;
        private bool _disposed;

        public bool IsPaused
        {
            get
            {
                lock (_sync) return _paused;
            }
        }

        public void Pause()
        {
            lock (_sync)
            {
                if (_disposed || _paused) return;
                _paused = true;
                _resumeSignal = CreatePendingSignal();
            }
        }

        public void Resume()
        {
            TaskCompletionSource<bool> signal;
            lock (_sync)
            {
                if (_disposed || !_paused) return;
                _paused = false;
                signal = _resumeSignal;
                _resumeSignal = CreateCompletedSignal();
            }
            signal.TrySetResult(true);
        }

        public async Task WaitIfPausedAsync(CancellationToken token)
        {
            while (true)
            {
                Task waitTask;
                lock (_sync)
                {
                    if (_disposed || !_paused) return;
                    waitTask = _resumeSignal.Task;
                }
                await waitTask.WaitAsync(token);
            }
        }

        public void Dispose()
        {
            TaskCompletionSource<bool> signal;
            lock (_sync)
            {
                if (_disposed) return;
                _disposed = true;
                _paused = false;
                signal = _resumeSignal;
                _resumeSignal = CreateCompletedSignal();
            }
            signal.TrySetResult(true);
        }

        private static TaskCompletionSource<bool> CreateCompletedSignal()
        {
            var source = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            source.TrySetResult(true);
            return source;
        }

        private static TaskCompletionSource<bool> CreatePendingSignal()
        {
            return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }
}
