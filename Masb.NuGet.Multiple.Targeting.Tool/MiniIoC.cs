using System;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Masb.NuGet.Multiple.Targeting.Tool
{
    /// <summary>
    /// Mini Inversion of Control class that can be used internally by other libraries.
    /// </summary>
    public static class MiniIoC
    {
        private static readonly AsyncLock locker = new AsyncLock();

        #region GetAsync

        private static ImmutableDictionary<Type, object> funcs = ImmutableDictionary<Type, object>.Empty;

        public static Task<T> GetAsync<T>()
        {
            return GetAsync<T>(null);
        }

        public static async Task<T> GetAsync<T>(ExternalProvider defaultProvider)
        {
            // First we try to use external providers.
            // This allows the registration of external IoC containers.
            if (externalProviders != null)
                foreach (var externalProvider in externalProviders.Where(x => x.CanProvideDelegates))
                {
                    var result = await externalProvider.GetDelegateAsync<T>();
                    if (result != null)
                        return result;
                }

            if (defaultProvider != null && defaultProvider.CanProvideDelegates)
            {
                var result = await defaultProvider.GetDelegateAsync<T>();
                if (result != null)
                    return result;
            }

            object value;
            if (!funcs.TryGetValue(typeof(T), out value))
            {
                using (await locker)
                {
                    if (!funcs.TryGetValue(typeof(T), out value))
                    {
                        var func = Expression.Lambda<Func<T>>(await GetExpressionAsync(typeof(T), defaultProvider)).Compile();
                        funcs = funcs.Add(typeof(T), func);
                        return func();
                    }
                }
            }

            var func2 = (Func<T>)value;
            return func2();
        }

        public static Task<Expression> GetExpressionAsync(Type type)
        {
            return GetExpressionAsync(type, null);
        }

        public static async Task<Expression> GetExpressionAsync(Type type, ExternalProvider defaultProvider)
        {
            // First we try to use external providers.
            // This allows the registration of external IoC containers.
            if (externalProviders != null)
                foreach (var externalProvider in externalProviders.Where(x => x.CanProvideExpressions))
                {
                    var result = await externalProvider.GetExpressionAsync(type);
                    if (result != null)
                        return result;
                }

            if (defaultProvider != null && defaultProvider.CanProvideExpressions)
            {
                var result = await defaultProvider.GetExpressionAsync(type);
                if (result != null)
                    return result;
            }

            Expression expr;
            if (!exprs.TryGetValue(type, out expr))
            {
                var ctors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);

                // creating a default constructor
                var defCtor = ctors.FirstOrDefault(x => x.GetParameters().Length == 0);
                var sngCtor = ctors.Length == 1 ? ctors[0] : null;
                var ctor = defCtor ?? sngCtor;

                if (ctor == null)
                    throw new Exception(
                        string.Format(
                            "Cannot create type {0} because it neither:\n - {1}\n - {2}",
                            type.Name,
                            "has a parameterless constructor",
                            "nor - has a single constructor"));

                var pars = await Task.WhenAll(ctor.GetParameters().Select(p => GetExpressionAsync(p.ParameterType)));
                return Expression.ConvertChecked(Expression.New(ctor, pars), type);
            }

            return expr;
        }

        #endregion

        #region RegisterAsync

        private static ImmutableDictionary<Type, Expression> exprs = ImmutableDictionary<Type, Expression>.Empty;

        /// <summary>
        /// Registers a new expression in this IoC bridge.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="creatorExpression"></param>
        /// <returns></returns>
        public static async Task RegisterAsync<T>([NotNull] Expression<Func<T>> creatorExpression)
            where T : class
        {
            if (creatorExpression == null)
                throw new ArgumentNullException("creatorExpression");

            using (await locker)
                exprs = exprs.Add(typeof(T), creatorExpression.Body);
        }

        #endregion

        #region ExternalProvider

        private static ImmutableList<ExternalProvider> externalProviders;

        public static async Task AddExternalProviders(ExternalProvider externalProvider)
        {
            using (await locker)
                externalProviders = externalProviders.Add(externalProvider);
        }

        public static async Task RemoveExternalProviders(ExternalProvider externalProvider)
        {
            using (await locker)
                externalProviders = externalProviders.Remove(externalProvider);
        }

        public abstract class ExternalProvider
        {
            public ExternalProvider(bool canProvideExpressions, bool canProvideDelegates)
            {
                this.CanProvideExpressions = canProvideExpressions;
                this.CanProvideDelegates = canProvideDelegates;
            }

            public bool CanProvideExpressions { get; private set; }

            public bool CanProvideDelegates { get; private set; }

            public abstract Task<Expression> GetExpressionAsync(Type type);

            public abstract Task<T> GetDelegateAsync<T>();
        }

        #endregion
    }
}
