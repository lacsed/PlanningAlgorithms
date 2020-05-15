using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using PlanningDES;
using UltraDES;
using TimeContext = System.ValueTuple<System.Collections.Immutable.ImmutableList<UltraDES.AbstractEvent>, PlanningDES.Scheduler, PlanningDES.Restriction, float, float>;

namespace PlanningAlgorithms
{
    public static partial class Algorithms
    {
        public static AbstractEvent[] PMT(ISchedulingProblem problem, int products, int limiar = 0, bool controllableFirst = false)
        {
            var initial = problem.InitialState;
            var target = problem.TargetState;
            var schOrig = problem.InitialScheduler;
            var resOrig = problem.InitialRestrition(products);
            var depth = products * problem.Depth;
            var uncontrollables = problem.Events.Where(e => !e.IsControllable).ToSet();
            var transitions = problem.Transitions;

            IDictionary<AbstractState, TimeContext> frontier = new Dictionary<AbstractState, TimeContext> { {initial, (ImmutableList<AbstractEvent>.Empty, schOrig, resOrig, 0f, 0f)} };

            for (var i = 0; i < depth; i++)
            {
                var newFrontier = new Dictionary<AbstractState, TimeContext>(frontier.Count);

                void Loop(KeyValuePair<AbstractState, TimeContext> kvp)
                {
                    var (q1, (sequence1, sch1, res1, time1, parallelism1)) = kvp;

                    var events = res1.Enabled;
                    events.UnionWith(uncontrollables);
                    events.IntersectWith(sch1.Enabled);
                    events.IntersectWith(transitions[q1].Keys);

                    if (controllableFirst && events.Any(e => e.IsControllable)) events.ExceptWith(uncontrollables);

                    foreach (var e in events)
                    {
                        var q2 = transitions[q1][e];

                        var parallelism2 = parallelism1 + q2.ActiveTasks();
                        var time2 = time1 + sch1[e];
                        var sequence2 = sequence1.Add(e);
                        var res2 = e.IsControllable ? res1.Update(e) : res1;
                        var sch2 = sch1.Update(e);
                        var context2 = (sequence2, sch2, res2, time2, parallelism2);


                        lock (newFrontier)
                        {
                            if (!newFrontier.ContainsKey(q2)) newFrontier.Add(q2, context2);
                            else if (newFrontier[q2].Item5 < parallelism2) newFrontier[q2] = context2;
                        }
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
