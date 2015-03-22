using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Masb.NuGet.Multiple.Targeting.Tool.InfoModel;

namespace Masb.NuGet.Multiple.Targeting.Tool.IoC
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
                { typeof(IFrameworkInfoCache), typeof(InMemoryFrameworkInfoCache) }
            };

        public override async Task<MiniIoC.Result<Expression>> GetExpressionAsync(Type type)
        {
            if (typeMap.TryGetValue(type, out type))
            {
                return new MiniIoC.Result<Expression>(
                    await MiniIoC.GetExpressionAsync(type, this));
            }

            return new MiniIoC.Result<Expression>();
        }

        public override Task<MiniIoC.Result<T>> GetValueAsync<T>()
        {
            throw new NotSupportedException();
        }
    }
}