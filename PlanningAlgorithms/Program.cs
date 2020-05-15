using System;
using PlanningDES;
using PlanningDES.Problems;
using UltraDES;
using ConsoleTables;
using static PlanningAlgorithms.Algorithms;

namespace PlanningAlgorithms
{
    class Program
    {
        static void Main()
        {
            System.Threading.Thread.CurrentThread.Priority = System.Threading.ThreadPriority.AboveNormal;
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-US");

            var problem = new FlexibleManufacturingSystem();
            var algorithms = new (string, Func<int, AbstractEvent[]>)[2];
            algorithms[0] = ("Parallelism Maximization with Time Restrictions", (p) => PMT(problem, p, controllableFirst: true));
            algorithms[1] = ("Heuristic Makespan Minimization", (p) => HMM(problem, p, controllableFirst: true));
            //algorithms[2] = ("Parallelism Maximization (Logic)", (p) => PML(problem, p, controllableFirst: false));

            foreach (var (name, algorithm) in algorithms)
            {
                var table = new ConsoleTable("Batch", "Time", "Makespan", "Parallelism");
                foreach (var products in new[] { 1, 10, 100, 1000 })
                {
                    Func<AbstractEvent[]> test = () => algorithm(products);

                    var (time, sequence) = test.Timming();
                    var makespan = problem.TimeEvaluation(sequence);
                    var parallelism = problem.MetricEvaluation(sequence, (t) => t.destination.ActiveTasks());

                    table.AddRow(products, time, makespan, parallelism);
                }

                Console.WriteLine($"{name}:");
                table.Write(Format.Alternative);
                Console.WriteLine();
            }


            
        }
    }
}
