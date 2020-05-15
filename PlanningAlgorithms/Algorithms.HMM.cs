using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using PlanningDES;
using UltraDES;
using TimeContext = System.ValueTuple<System.Collections.Immutable.ImmutableList<UltraDES.AbstractEvent>, PlanningDES.Scheduler, PlanningDES.Restriction, float>;

namespace PlanningAlgorithms
{
    public static partial class Algorithms
    {
        public static AbstractEvent[] HMM(ISchedulingProblem problem, int products, int limiar = 100, bool controllableFirst = true)
        {
            var initial = problem.InitialState;

            var target = problem.TargetState;
            var schOrig = problem.InitialScheduler;
            var resOrig = problem.InitialRestrition(products);
            var depth = products * problem.Depth;
            var uncontrollables = problem.Events.Where(e => !e.IsControllable).ToSet();
            var transitions = problem.Transitions;

            var frontier = new Dictionary<AbstractState, List<TimeContext>>
            {
                {initial, new List<TimeContext>{(ImmutableList<AbstractEvent>.Empty, schOrig, resOrig, 0f)}}
            };

            for (var it = 0; it < depth; it++)
            {
                var newFrontier = new Dictionary<AbstractState, List<TimeContext>>(frontier.Count);

                void Loop(KeyValuePair<AbstractState, List<TimeContext>> kvp)
                {
                    var q1 = kvp.Key;
                    foreach (var (sequence1, sch1, res1, time1) in kvp.Value)
                    {
                        var events = res1.Enabled;
                        events.UnionWith(uncontrollables);
                        events.IntersectWith(sch1.Enabled);
                        events.IntersectWith(transitions[q1].Keys);

                        if(controllableFirst && events.Any(e => e.IsControllable)) events.ExceptWith(uncontrollables);
                        
                        foreach (var e in events)
                        {
                            var q2 = transitions[q1][e];

                            var sequence2 = sequence1.Add(e);
                            var time2 = time1 + sch1[e];
                            var res2 = e.IsControllable ? res1.Update(e) : res1;
                            var sch2 = sch1.Update(e);

                            var context2 = (sequence2, sch2, res2, time2);

                            var contextLst = new List<TimeContext>();

                            lock (newFrontier)
                            {
                                if (!newFrontier.ContainsKey(q2)) newFrontier.Add(q2, contextLst);
                                else contextLst = newFrontier[q2];
                            }

                            lock (contextLst)
                            {
                                var candidate = contextLst.Where(c =>
                                {
                                    var (_, sch, res, _) = c;
                                    return problem.Events.All(ev => sch2[ev] == sch[ev]); //&& problem.Events.All(ev => res2[ev] == res[ev]);
                                }).ToList();

                                if (candidate.Any() && candidate.Single().Item4 <= time2) continue;
                                if (candidate.Any() && candidate.Single().Item4 > time2) contextLst.Remove(candidate.Single());
                                contextLst.Add(context2);
                            }
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

            return frontier[target].OrderBy(c => c.Item4).First().Item1.ToArray();
        }
    }
}
