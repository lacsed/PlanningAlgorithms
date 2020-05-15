using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;
using PlanningDES;
using UltraDES;
using Context = System.ValueTuple<System.Collections.Immutable.ImmutableList<UltraDES.AbstractEvent>, PlanningDES.Restriction, float>;

namespace PlanningAlgorithms
{
    public partial class Algorithms
    {
        public static AbstractEvent[] PML(ISchedulingProblem problem, int products, int limiar = 0, bool controllableFirst = false)
        {
            var initial = problem.InitialState;
            var target = problem.TargetState;
            var resOrig = problem.InitialRestrition(products);
            var depth = products * problem.Depth;
            var uncontrollables = problem.Events.Where(e => !e.IsControllable).ToSet();
            var transitions = problem.Transitions;

            IDictionary<AbstractState, Context> frontier = new Dictionary<AbstractState, Context> { { initial, (ImmutableList<AbstractEvent>.Empty, resOrig, 0f) } };

            for (var i = 0; i < depth; i++)
            {
                var newFrontier = new ConcurrentDictionary<AbstractState, Context>();

                void Loop(KeyValuePair<AbstractState, Context> kvp)
                {
                    var (q1, (sequence1, res1, parallelism1)) = kvp;

                    var events = res1.Enabled;
                    events.UnionWith(uncontrollables);
                    events.IntersectWith(transitions[q1].Keys);

                    if (controllableFirst && events.Any(e => e.IsControllable)) events.ExceptWith(uncontrollables);

                    foreach (var e in events)
                    {
                        var q2 = transitions[q1][e];

                        var parallelism2 = parallelism1 + q2.ActiveTasks();
                        var sequence2 = sequence1.Add(e);
                        var res2 = e.IsControllable ? res1.Update(e) : res1;
                        var context2 = (sequence2, res2, parallelism2);

                        newFrontier.AddOrUpdate(q2, context2, (oldq, oldc) => oldc.Item3 < parallelism2 ? context2 : oldc);

                    }
                }

                if (frontier.Count > limiar) Partitioner.Create(frontier, EnumerablePartitionerOptions.NoBuffering).AsParallel().ForAll(Loop);
                else foreach (var kvp in frontier) Loop(kvp);

                frontier = newFrontier;

                Debug.WriteLine($"Frontier: {frontier.Count} elements");

            }

            if (!frontier.ContainsKey(target))
                throw new Exception($"The algorithm could not reach the targer ({target})");

            return frontier[target].Item1.ToArray();
        }
    }
}
