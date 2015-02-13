using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Masb.NuGet.Multiple.Targeting.Tool
{
    public class MiniIoCDefaults : MiniIoC.ExternalProvider
    {
        public static readonly MiniIoCDefaults Instance = new MiniIoCDefaults();

        private MiniIoCDefaults()
            : base(true, false)
        {
        }

        private static readonly Dictionary<Type, Type> typeMap = new Dictionary<Type, Type>
            {
                { typeof(IFrameworkInfoCache), typeof(InMemoryCache) }
            };

        public override async Task<Expression> GetExpressionAsync(Type type)
        {
            if (typeMap.TryGetValue(type, out type))
                return await MiniIoC.GetExpressionAsync(type, this);

            return null;
        }

        public override Task<T> GetDelegateAsync<T>()
        {
            throw new NotSupportedException();
        }
    }
}