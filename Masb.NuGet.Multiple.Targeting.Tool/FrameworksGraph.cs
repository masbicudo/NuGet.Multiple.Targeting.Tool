using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using JetBrains.Annotations;

namespace Masb.NuGet.Multiple.Targeting.Tool
{
    public class FrameworksGraph
    {
        public static ImmutableArray<FrameworksGraph> Create(IEnumerable<FrameworkInfo> frameworkInfos)
        {
            var array = frameworkInfos.Select(x => new S { item = x }).ToArray();
            FrameworksGraphs(array, 0);
            return array.Select(x => x.graph).Where(x => x != null).ToImmutableArray();
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
                            var item = child.item ?? (child.graph == null ? null : child.graph.FrameworkInfo);
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
                            graph = new FrameworksGraph(
                                master.item,
                                subsets.Select(x => x.graph).Where(x => x != null).ToImmutableArray())
                        };
                }
            }
        }

        internal FrameworksGraph([CanBeNull] FrameworkInfo frameworkInfo, ImmutableArray<FrameworksGraph> subsets)
        {
            this.FrameworkInfo = frameworkInfo;
            this.Subsets = subsets;
        }

        [CanBeNull]
        public FrameworkInfo FrameworkInfo { get; private set; }

        public ImmutableArray<FrameworksGraph> Subsets { get; private set; }

        public override string ToString()
        {
            return this.FrameworkInfo == null ? "Master" : this.FrameworkInfo.ToString();
        }

        struct S
        {
            public FrameworkInfo item;
            public FrameworksGraph graph;

            public static bool HasValue(S s)
            {
                return !(s.item == null && s.graph == null);
            }
        }

        public TNode Visit<TNode>(
            Func<ImmutableStack<FrameworksGraph>, IEnumerable<TNode>, TNode> func)
        {
            return this.Visit(this, ImmutableStack<FrameworksGraph>.Empty, func);
        }

        public TNode Visit<TNode, TContext>(
            TContext context,
            Func<ImmutableStack<FrameworksGraph>, IEnumerable<TNode>, TContext, TNode> func)
        {
            return this.Visit(context, this, ImmutableStack<FrameworksGraph>.Empty, func);
        }

        public void Visit(Action<ImmutableStack<FrameworksGraph>> action)
        {
            this.Visit(this, ImmutableStack<FrameworksGraph>.Empty, action);
        }

        private TNode Visit<TNode>(
            FrameworksGraph graph,
            ImmutableStack<FrameworksGraph> stack,
            Func<ImmutableStack<FrameworksGraph>, IEnumerable<TNode>, TNode> func)
        {
            stack = stack.Push(graph);
            var newChildren = graph.Subsets.Select(x => this.Visit(x, stack, func));
            var result = func(stack, newChildren);
            return result;
        }

        private TNode Visit<TNode, TContext>(
            TContext context,
            FrameworksGraph graph,
            ImmutableStack<FrameworksGraph> stack,
            Func<ImmutableStack<FrameworksGraph>, IEnumerable<TNode>, TContext, TNode> func)
        {
            stack = stack.Push(graph);
            var newChildren = graph.Subsets.Select(x => this.Visit(context, x, stack, func));
            var result = func(stack, newChildren, context);
            return result;
        }

        private void Visit(FrameworksGraph graph, ImmutableStack<FrameworksGraph> stack, Action<ImmutableStack<FrameworksGraph>> action)
        {
            stack = stack.Push(graph);
            action(stack);
            foreach (var each in graph.Subsets)
                this.Visit(each, stack, action);
        }
    }
}