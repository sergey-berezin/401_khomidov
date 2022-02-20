using System;
using System.IO;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Microsoft.ML;
using Microsoft.ML.OnnxRuntime;
using static Microsoft.ML.Transforms.Image.ImageResizingEstimator;


namespace detectionLibrary
{
    public class Detector
    {

        const string modelPath = @"C:\Users\User\Desktop\yolov4.onnx";
        static readonly string[] classesNames = new string[] { "person", "bicycle", "car", "motorbike", "aeroplane", "bus", "train", "truck", "boat", "traffic light", "fire hydrant", "stop sign", "parking meter", "bench", "bird", "cat", "dog", "horse", "sheep", "cow", "elephant", "bear", "zebra", "giraffe", "backpack", "umbrella", "handbag", "tie", "suitcase", "frisbee", "skis", "snowboard", "sports ball", "kite", "baseball bat", "baseball glove", "skateboard", "surfboard", "tennis racket", "bottle", "wine glass", "cup", "fork", "knife", "spoon", "bowl", "banana", "apple", "sandwich", "orange", "broccoli", "carrot", "hot dog", "pizza", "donut", "cake", "chair", "sofa", "pottedplant", "bed", "diningtable", "toilet", "tvmonitor", "laptop", "mouse", "remote", "keyboard", "cell phone", "microwave", "oven", "toaster", "sink", "refrigerator", "book", "clock", "vase", "scissors", "teddy bear", "hair drier", "toothbrush" };

        public string Path { get; set; }
        public Detector(string pathArg)
        {
            Path = pathArg;
        }
        public void Detect(
            ConcurrentQueue<Tuple<string, YoloV4Result>> resultsQueue,
            CancellationToken cancToken,
            string path = null)
        {
            if (path == null)
            {
                path = Path;
            }
            var filenames = Directory.GetFiles(path);
            foreach(var filename in filenames)
            {
                Console.WriteLine(filename);
            }

            // code from example
            var modelResults = new ConcurrentStack<YoloV4Result>();
            MLContext mlContext = new MLContext();

            var pipeline = mlContext.Transforms.ResizeImages(
                inputColumnName: "bitmap",
                outputColumnName: "input_1:0",
                imageWidth: 416, imageHeight: 416,
                resizing: ResizingKind.IsoPad
                ).Append(mlContext.Transforms.ExtractPixels(outputColumnName: "input_1:0",
                scaleImage: 1f / 255f, interleavePixelColors: true))
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

            // my code
            var tasks = new Task[filenames.Length];
            for (int i = 0; i < filenames.Length; ++i)
            {
                tasks[i] = Task.Factory.StartNew(index =>
                {
                    int file_index = (int) index;
                    var path = filenames[file_index];
                    var bitmap = new Bitmap(Image.FromFile(path));
                    var predictionEngine = mlContext.Model
                    .CreatePredictionEngine<YoloV4BitmapData, YoloV4Prediction>(model);
                    if (cancToken.IsCancellationRequested)
                    {
                        return;
                    }
                    var predict = predictionEngine.Predict(new YoloV4BitmapData() { Image = bitmap });
                    var results = predict.GetResults(classesNames, 0.3f, 0.7f);

                    foreach (var detected in results)
                    {
                        if (cancToken.IsCancellationRequested)
                        {
                            return;
                        }
                        var resTuple = new Tuple<string, YoloV4Result>(path, detected);
                        resultsQueue.Enqueue(resTuple);
                    }
                }, i);
            }

            Task.WaitAll(tasks);
        }
    }
}
