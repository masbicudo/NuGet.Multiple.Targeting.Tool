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
    /// Mini dependency injection container that can be used internally by libraries that wish to support Inversion of Control.
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
            if (externalProvider != null && externalProvider.CanProvideDelegates)
            {
                var result = await externalProvider.GetValueAsync<T>();
                if (result.IsValid)
                    return result.Value;
            }

            object value;
            if (!funcs.TryGetValue(typeof(T), out value))
            {
                if (defaultProvider != null && defaultProvider.CanProvideDelegates)
                {
                    var result = await defaultProvider.GetValueAsync<T>();
                    if (result.IsValid)
                        return result.Value;
                }

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

        public static async Task<Expression> GetExpressionAsync(Type type)
        {
            return Transform(await GetExpressionCoreAsync(type, null));
        }

        public static async Task<Expression> GetExpressionAsync(Type type, ExternalProvider defaultProvider)
        {
            return Transform(await GetExpressionCoreAsync(type, defaultProvider));
        }

        private static async Task<Expression> GetExpressionCoreAsync(Type type, ExternalProvider defaultProvider)
        {
            // First we try to use external providers.
            // This allows the registration of external IoC containers.
            if (externalProvider != null && externalProvider.CanProvideExpressions)
            {
                var result = await externalProvider.GetExpressionAsync(type);
                if (result.IsValid)
                    return result.Value;
            }

            Expression expr;
            if (!exprs.TryGetValue(type, out expr))
            {
                if (defaultProvider != null && defaultProvider.CanProvideExpressions)
                {
                    var result = await defaultProvider.GetExpressionAsync(type);
                    if (result.IsValid)
                        return result.Value;
                }

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

                var pars = await Task.WhenAll(ctor.GetParameters().Select(p => GetExpressionCoreAsync(p.ParameterType, defaultProvider)));
                return Expression.ConvertChecked(Expression.New(ctor, pars), type);
            }

            return expr;
        }

        private static Expression Transform(Expression expr)
        {
            return (new ReplacerVisitor()).Visit(expr);
        }

        private class ReplacerVisitor : ExpressionVisitor
        {
            protected override Expression VisitMethodCall(MethodCallExpression node)
            {
                if (node.Method.GetCustomAttributes(typeof(GetMethodMarkerAttribute)).Any())
                    return GetExpressionAsync(node.Method.GetGenericArguments()[0]).Result;

                return base.VisitMethodCall(node);
            }
        }

        #endregion

        #region RegisterAsync

        private static ImmutableDictionary<Type, Expression> exprs = ImmutableDictionary<Type, Expression>.Empty;

        /// <summary>
        /// Registers a new expression in this IoC bridge.
        /// </summary>
        /// <typeparam name="T">Type of the dependency being registered.</typeparam>
        /// <param name="creatorExpression">Expression used to get or create the dependency object.</param>
        /// <returns>Task representing the registration action.</returns>
        public static async Task RegisterAsync<T>([NotNull] Expression<Func<Context, T>> creatorExpression)
            where T : class
        {
            if (creatorExpression == null)
                throw new ArgumentNullException("creatorExpression");

            using (await locker)
                exprs = exprs.Add(typeof(T), creatorExpression.Body);
        }

        [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
        private sealed class GetMethodMarkerAttribute : Attribute
        {
        }

        public class Context
        {
            [GetMethodMarkerAttribute]
            public T Get<T>()
            {
                throw new NotImplementedException("This function is only usable in expression trees.");
            }
        }

        #endregion

        #region ExternalProvider

        private static ExternalProvider externalProvider;

        public static async Task SetExternalProvider(ExternalProvider provider)
        {
            using (await locker)
                externalProvider = provider;
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

            public abstract Task<Result<Expression>> GetExpressionAsync(Type type);

            public abstract Task<Result<T>> GetValueAsync<T>();
        }

        public struct Result<T>
        {
            public static readonly Result<T> Empty = new Result<T>();

            public Result(T value)
            {
                this.Value = value;
                this.IsValid = true;
            }

            public readonly T Value;
            public readonly bool IsValid;
        }

        #endregion
    }
}
