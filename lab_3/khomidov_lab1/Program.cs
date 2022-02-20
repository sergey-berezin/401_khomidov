using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using detectionLibrary;

namespace khomidov_lab1
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Bad input");
                return;
            }
            string path = args[0];
            var detector = new Detector(args[0]);

            var objects = new ConcurrentQueue<Tuple<string, YoloV4Result>>();
            var cts = new CancellationTokenSource();

            var cancellationTask = Task.Factory.StartNew(() =>
            {
                var stop = Console.ReadLine();
                if (stop == "Stop")
                {
                    cts.Cancel();
                }
            });

            var detectionTask = Task.Factory.StartNew(token =>
            {
                detector.Detect(objects, (CancellationToken)token);
            }, cts.Token);

            var outputTask = Task.Factory.StartNew(tokenObject =>
            {
                var token = (CancellationToken)tokenObject;
                while (detectionTask.Status == TaskStatus.Running)
                {
                    while (objects.TryDequeue(out Tuple<string, YoloV4Result> result))
                    {
                        if (token.IsCancellationRequested)
                        {
                            break;
                        }

                        var filePath = result.Item1;
                        var dirsInPath = filePath.Split('\\');
                        var filename = dirsInPath[dirsInPath.Length - 1];

                        var detected = result.Item2;
                        var x1 = detected.BBox[0];
                        var y1 = detected.BBox[1];
                        var x2 = detected.BBox[2];
                        var y2 = detected.BBox[3];
                        var label = detected.Label;
                        var conf = detected.Confidence;

                        Console.WriteLine($"{label} was detected in {filename} at " +
                            $"position ({x1:0.0}, {y1:0.0}) and ({x2:0.0}, {y2:0.0})" +
                            $"with confidence {conf:0.00})");
                    }
                }
                if (token.IsCancellationRequested)
                {
                    Console.WriteLine("Stop requested");
                }
            }, cts.Token);

            Task.WaitAll(outputTask);

        }
    }
}
