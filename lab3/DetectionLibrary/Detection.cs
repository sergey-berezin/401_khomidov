using Microsoft.ML;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Drawing;
using System.IO;
using YOLOv4MLNet.DataStructures;
using static Microsoft.ML.Transforms.Image.ImageResizingEstimator;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Concurrent;
using System.Threading;

namespace DetectionCore
{
    public class Detection
    {

        const string modelPath = @"C:\Users\79237\Desktop\lab1\lab1\YOLOv4MLNet\yolov4.onnx";

        static readonly string[] classesNames = new string[] { 
            "person", "bicycle", "car", "motorbike", "aeroplane", "bus", "train", "truck", 
            "boat", "traffic light", "fire hydrant", "stop sign", "parking meter", "bench",
            "bird", "cat", "dog", "horse", "sheep", "cow", "elephant", "bear", "zebra", "giraffe",
            "backpack", "umbrella",  "handbag", "tie", "suitcase", "frisbee", "skis", "snowboard",
            "sports ball", "kite", "baseball bat", "baseball glove", "skateboard",
            "surfboard", "tennis racket", "bottle", "wine glass", "cup", "fork", "knife",
            "spoon", "bowl", "banana", "apple", "sandwich", "orange", "broccoli", "carrot",
            "hot dog", "pizza", "donut", "cake", "chair", "sofa", "pottedplant", "bed", "diningtable",
            "toilet", "tvmonitor", "laptop", "mouse", "remote", "keyboard", "cell phone", "microwave", 
            "oven", "toaster", "sink", "refrigerator", "book", "clock", "vase", "scissors", "teddy bear",
            "hair drier", "toothbrush" };

        public static void Detect(string imageDir, ConcurrentQueue<Tuple<string, IReadOnlyList<YoloV4Result>>> DetectionResult, CancellationToken token)
        {
            MLContext mlContext = new MLContext();

            var pipeline = mlContext.Transforms.ResizeImages(inputColumnName: "bitmap", outputColumnName: "input_1:0", imageWidth: 416, imageHeight: 416, resizing: ResizingKind.IsoPad)
                .Append(mlContext.Transforms.ExtractPixels(outputColumnName: "input_1:0", scaleImage: 1f / 255f, interleavePixelColors: true))
                .Append(mlContext.Transforms.ApplyOnnxModel(
                    shapeDictionary: new Dictionary<string, int[]>()
                    {
                        { "input_1:0", new[] { 1, 416, 416, 3 } },
                        { "Identity:0", new[] { 1, 52, 52, 3, 85 } },
                        { "Identity_1:0", new[] { 1, 26, 26, 3, 85 } },
                        { "Identity_2:0", new[] { 1, 13, 13, 3, 85 } },
                    },
                    inputColumnNames: new[]
                    {
                        "input_1:0"
                    },
                    outputColumnNames: new[]
                    {
                        "Identity:0",
                        "Identity_1:0",
                        "Identity_2:0"
                    },
                    modelFile: modelPath, recursionLimit: 100));

            var model = pipeline.Fit(mlContext.Data.LoadFromEnumerable(new List<YoloV4BitmapData>()));

            var filenames = Directory.GetFiles(imageDir, "*", SearchOption.TopDirectoryOnly).Select(path => Path.GetFileName(path)).ToArray();
            int n = filenames.Length;

            var predictionEngines = ImmutableList.Create<PredictionEngine<YoloV4BitmapData, YoloV4Prediction>>();
            for (int i = 0; i < n; ++i)
            {
                predictionEngines = predictionEngines.Add(mlContext.Model.CreatePredictionEngine<YoloV4BitmapData, YoloV4Prediction>(model));
            }

            var tasks = new Task[n];
            for (int i = 0; i < n; ++i)
            {
                tasks[i] = Task<bool>.Factory.StartNew(pi =>
                {
                    int i = (int)pi;
                    if (token.IsCancellationRequested)
                    {
                        Console.WriteLine($"Task {i} was cancelled!");
                        return true;
                    }
                    var bitmap = new Bitmap(Image.FromFile(Path.Combine(imageDir, filenames[i])));
                    var predict = predictionEngines[i].Predict(new YoloV4BitmapData() { Image = bitmap });
                    var results = predict.GetResults(classesNames, 0.3f, 0.7f);
                    var tuple = Tuple.Create(filenames[i], results);
                    DetectionResult.Enqueue(tuple);
                    return true;
                }, i);
            }
            Task.WaitAll(tasks);
        }
    }
}
