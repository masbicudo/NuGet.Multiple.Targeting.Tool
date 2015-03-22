using System;
using JetBrains.Annotations;

namespace Masb.NuGet.Multiple.Targeting.Tool.Helpers
{
    public class Disposer : IDisposable
    {
        private readonly Action disposer;

        public Disposer([NotNull] Action disposer)
        {
            if (disposer == null)
                throw new ArgumentNullException("disposer");

            this.disposer = disposer;
        }

        public void Dispose()
        {
            this.disposer();
        }
    }
}