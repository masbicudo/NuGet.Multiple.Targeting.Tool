using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Masb.NuGet.Multiple.Targeting.Tool.Helpers
{
    /// <summary>
    /// Locking mechanism that xpto.
    /// </summary>
    public sealed class AsyncLock
    {
        private readonly SemaphoreSlim syncLock = new SemaphoreSlim(1);

        /// <summary>
        /// Gets a <see cref="TaskAwaiter{TResult}"/> that allows this object to be awaited with the `await` keyword.
        /// </summary>
        /// <returns>
        /// A <see cref="TaskAwaiter{TResult}"/> that either acquires the lock or awaits the lock being freed.
        /// </returns>
        [UsedImplicitly]
        public TaskAwaiter<IDisposable> GetAwaiter()
        {
            return this.UseLock().GetAwaiter();
        }

        private async Task<IDisposable> UseLock()
        {
            await this.syncLock.WaitAsync();
            Thread.MemoryBarrier();
            return new Disposer(this.Release);
        }

        private void Release()
        {
            var release = this.syncLock.Release();
            Debug.Assert(release == 0, "release == 0");
            Thread.MemoryBarrier();
        }
    }
}