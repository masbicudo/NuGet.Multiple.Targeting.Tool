using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Masb.NuGet.Multiple.Targeting.Tool
{
    public class HierarchyGraph
    {
        public static ImmutableArray<HierarchyGraph> Create(IEnumerable<FrameworkInfo> frameworkInfos)
        {
            var array = frameworkInfos.Select(x => new S { item = x }).ToArray();
            FrameworksGraphs(array, 0);
            return array.Select(x => x.hierarchyGraph).Where(x => x != null).ToImmutableArray();
        }

        private static void FrameworksGraphs(S[] sets, int level)
        {
            var subsets = new S[sets.Length];
            for (int it = 0; it < sets.Length; it++)
            {
                var master = sets[it];
                if (master.item != null)
                {
                    ConsoleHelper.Write("" + master.item + ": {", ConsoleColor.Green, level);

                    Array.Clear(subsets, 0, subsets.Length);

                    for (int it2 = 0; it2 < sets.Length; it2++)
                    {
                        if (it != it2)
                        {
                            var child = sets[it2];
                            var item = child.item ?? (child.hierarchyGraph == null ? null : child.hierarchyGraph.FrameworkInfo);
                            if (item != null && master.item.IsSupersetOf(item) == true)
                            {
                                ConsoleHelper.Write("*", ConsoleColor.Gray);

                                subsets[it2] = child;
                                sets[it2] = default(S);
                            }
                        }
                    }

                    ConsoleHelper.WriteLine("}", ConsoleColor.Green);

                    FrameworksGraphs(subsets, level + 1);
                    sets[it] = new S
                        {
                            hierarchyGraph = new HierarchyGraph(
                                master.item,
                                subsets.Select(x => x.hierarchyGraph).Where(x => x != null).ToImmutableArray())
                        };
                }
            }
        }

        internal HierarchyGraph([CanBeNull] FrameworkInfo frameworkInfo, ImmutableArray<HierarchyGraph> subsets)
        {
            this.FrameworkInfo = frameworkInfo;
            this.Subsets = subsets;
        }

        [CanBeNull]
        public FrameworkInfo FrameworkInfo { get; private set; }

        public ImmutableArray<HierarchyGraph> Subsets { get; private set; }

        public override string ToString()
        {
            return this.FrameworkInfo == null ? "Master" : this.FrameworkInfo.ToString();
        }

        struct S
        {
            public FrameworkInfo item;
            public HierarchyGraph hierarchyGraph;

            public static bool HasValue(S s)
            {
                return !(s.item == null && s.hierarchyGraph == null);
            }
        }

        public async Task<TNode> VisitAsync<TNode>(
            Func<ImmutableStack<HierarchyGraph>, IEnumerable<TNode>, Task<TNode>> func)
        {
            return await this.VisitAsync(this, ImmutableStack<HierarchyGraph>.Empty, func);
        }

        public TNode Visit<TNode>(
            Func<ImmutableStack<HierarchyGraph>, IEnumerable<TNode>, TNode> func)
        {
            return this.Visit(this, ImmutableStack<HierarchyGraph>.Empty, func);
        }

        public TNode Visit<TNode, TContext>(
            TContext context,
            Func<ImmutableStack<HierarchyGraph>, IEnumerable<TNode>, TContext, TNode> func)
        {
            return this.Visit(context, this, ImmutableStack<HierarchyGraph>.Empty, func);
        }

        public async Task<TNode> VisitAsync<TNode, TContext>(
            TContext context,
            Func<ImmutableStack<HierarchyGraph>, IEnumerable<TNode>, TContext, Task<TNode>> func)
        {
            return await this.VisitAsync(context, this, ImmutableStack<HierarchyGraph>.Empty, func);
        }

        public void Visit(Action<ImmutableStack<HierarchyGraph>> action)
        {
            this.Visit(this, ImmutableStack<HierarchyGraph>.Empty, action);
        }

        public async Task VisitAsync(Func<ImmutableStack<HierarchyGraph>, Task> action)
        {
            await this.VisitAsync(this, ImmutableStack<HierarchyGraph>.Empty, action);
        }

        private async Task<TNode> VisitAsync<TNode>(
            HierarchyGraph hierarchyGraph,
            ImmutableStack<HierarchyGraph> stack,
            Func<ImmutableStack<HierarchyGraph>, IEnumerable<TNode>, Task<TNode>> func)
        {
            stack = stack.Push(hierarchyGraph);
            var manyTasks = hierarchyGraph.Subsets.Select(x => this.VisitAsync(x, stack, func)).ToArray();
            await Task.WhenAll(manyTasks);
            var newChildren = manyTasks.Select(x => x.Result);
            var result = await func(stack, newChildren);
            return result;
        }

        private TNode Visit<TNode>(
            HierarchyGraph hierarchyGraph,
            ImmutableStack<HierarchyGraph> stack,
            Func<ImmutableStack<HierarchyGraph>, IEnumerable<TNode>, TNode> func)
        {
            stack = stack.Push(hierarchyGraph);
            var newChildren = hierarchyGraph.Subsets.Select(x => this.Visit(x, stack, func));
            var result = func(stack, newChildren);
            return result;
        }

        private TNode Visit<TNode, TContext>(
            TContext context,
            HierarchyGraph hierarchyGraph,
            ImmutableStack<HierarchyGraph> stack,
            Func<ImmutableStack<HierarchyGraph>, IEnumerable<TNode>, TContext, TNode> func)
        {
            stack = stack.Push(hierarchyGraph);
            var newChildren = hierarchyGraph.Subsets.Select(x => this.Visit(context, x, stack, func));
            var result = func(stack, newChildren, context);
            return result;
        }

        private async Task<TNode> VisitAsync<TNode, TContext>(
            TContext context,
            HierarchyGraph hierarchyGraph,
            ImmutableStack<HierarchyGraph> stack,
            Func<ImmutableStack<HierarchyGraph>, IEnumerable<TNode>, TContext, Task<TNode>> func)
        {
            stack = stack.Push(hierarchyGraph);
            var manyTasks = hierarchyGraph.Subsets.Select(x => this.VisitAsync(context, x, stack, func)).ToArray();
            await Task.WhenAll(manyTasks);
            var newChildren = manyTasks.Select(x => x.Result);
            var result = await func(stack, newChildren, context);
            return result;
        }

        private void Visit(
            HierarchyGraph hierarchyGraph,
            ImmutableStack<HierarchyGraph> stack,
            Action<ImmutableStack<HierarchyGraph>> action)
        {
            stack = stack.Push(hierarchyGraph);
            action(stack);
            foreach (var each in hierarchyGraph.Subsets)
                this.Visit(each, stack, action);
        }

        private async Task VisitAsync(
            HierarchyGraph hierarchyGraph,
            ImmutableStack<HierarchyGraph> stack,
            Func<ImmutableStack<HierarchyGraph>, Task> action)
        {
            stack = stack.Push(hierarchyGraph);
            await action(stack);
            foreach (var each in hierarchyGraph.Subsets)
                await this.VisitAsync(each, stack, action);
        }
    }
}