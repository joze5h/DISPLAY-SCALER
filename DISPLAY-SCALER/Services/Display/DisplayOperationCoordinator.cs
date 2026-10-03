using System;
using System.Threading;
using System.Threading.Tasks;

namespace DISPLAY_SCALER.Services.Display
{
    public sealed class DisplayOperationCoordinator
    {
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);

        public async Task RunExclusiveAsync(Func<Task> action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                await action().ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<T> RunExclusiveAsync<T>(Func<Task<T>> action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                return await action().ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }
        }
    }
}