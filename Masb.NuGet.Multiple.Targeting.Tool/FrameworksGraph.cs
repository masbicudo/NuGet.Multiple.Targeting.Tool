using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

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
                    ConsoleHelper.Write("Item " + it + ": ", ConsoleColor.Green, level);

                    Array.Clear(subsets, 0, subsets.Length);

                    for (int it2 = 0; it2 < sets.Length; it2++)
                    {
                        if (it != it2)
                        {
                            var child = sets[it2];
                            var item = child.item ?? (child.graph == null ? null : child.graph.Item);
                            if (item != null && master.item.IsSupersetOf(item) == true)
                            {
                                ConsoleHelper.Write("*", ConsoleColor.Gray);

                                subsets[it2] = child;
                                sets[it2] = default(S);
                            }
                        }
                    }

                    ConsoleHelper.WriteLine();

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

        private FrameworksGraph(FrameworkInfo item, ImmutableArray<FrameworksGraph> subsets)
        {
            this.Item = item;
            this.Subsets = subsets;
        }

        public FrameworkInfo Item { get; private set; }

        public ImmutableArray<FrameworksGraph> Subsets { get; private set; }

        public override string ToString()
        {
            return this.Item.ToString();
        }

        struct S
        {
            public FrameworkInfo item;
            public FrameworksGraph graph;
        }

        public void Visit(Action<Stack<FrameworksGraph>> action)
        {
            this.Visit(this, new Stack<FrameworksGraph>(), action);
        }

        private void Visit(FrameworksGraph graph, Stack<FrameworksGraph> stack, Action<Stack<FrameworksGraph>> action)
        {
            stack.Push(graph);
            try
            {
                action(stack);
                foreach (var each in graph.Subsets)
                    this.Visit(each, stack, action);
            }
            finally
            {
                stack.Pop();
            }
        }
    }
}