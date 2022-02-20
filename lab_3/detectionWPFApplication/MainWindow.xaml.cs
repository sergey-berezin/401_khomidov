using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Windows;
using System.Windows.Media.Imaging;
using detectionLibrary;

namespace detectionWPFApplication
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private CancellationTokenSource cts;
        public ImmutableList<BitmapImage> images;
        public List<BitmapImage> clearImages;
        public string[] filenames;
        public List<DetectedObject> dbObjects;
        public string directoryPath = "";
        public DatabaseManager db;

        public MainWindow()
        {
            InitializeComponent();
            cts = new CancellationTokenSource();
            images = ImmutableList.Create<BitmapImage>();
            clearImages = new List<BitmapImage>();
            dbObjects = new List<DetectedObject>();

            Status.Text = "Detection didn't start";
            listBox_Images.ItemsSource = new List<BitmapImage>();


            db = new DatabaseManager();
            UpdateDbView();
        }
        private void UpdateDbView()
        {
            dbObjects = new List<DetectedObject>();
            foreach (Image img in db.Images)
            {
                dbObjects.AddRange(img.DetectedObjects);
            }
            listBox_DataBase.ItemsSource = dbObjects;
        }

        private void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new System.Windows.Forms.FolderBrowserDialog();
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    directoryPath = dlg.SelectedPath;

                    filenames = Directory.GetFiles(directoryPath);
                    images = ImmutableList.Create<BitmapImage>();
                    foreach (var filename in filenames)
                    {
                        images = images.Add(new BitmapImage(new Uri(filename)));
                    }
                    listBox_Images.ItemsSource = images;
                    clearImages = new List<BitmapImage>(images);
                }
            }
            catch (Exception ex)
            {
                LastObject.Text = ex.Message;
            }
        }

        private BitmapImage BitmapImageFromBitmap(Bitmap bitmap)
        {
            using (var memory = new MemoryStream())
            {
                bitmap.Save(memory, ImageFormat.Png);
                memory.Position = 0;

                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memory;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                bitmapImage.Freeze();

                return bitmapImage;
            }
        }
        private Bitmap BitmapFromBitmapImage(BitmapImage bitmapImage)
        {
            using (MemoryStream outStream = new MemoryStream())
            {
                BitmapEncoder enc = new BmpBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(bitmapImage));
                enc.Save(outStream);
                Bitmap bitmap = new Bitmap(outStream);

                return new Bitmap(bitmap);
            }
        }


        private byte[] BitmapToByteArray(Bitmap bitmap)
        {
            using (var memoryStream = new MemoryStream())
            {
                bitmap.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);
                return memoryStream.ToArray();
            }
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            var detectionResults = new ConcurrentQueue<Tuple<string, YoloV4Result>>();
            cts = new CancellationTokenSource();

            try
            {
                OpenButton.IsEnabled = false;
                StartButton.IsEnabled = false;
                ClearButton.IsEnabled = false;

                int numberOfObjects = 0;
                var detector = new Detector(directoryPath);
                var objects = new ConcurrentQueue<Tuple<string, YoloV4Result>>();
                cts = new CancellationTokenSource();

                var detectionTask = Task.Factory.StartNew(token =>
                {
                    detector.Detect(objects, (CancellationToken)token);
                }, cts.Token);

                Status.Text = "Detection started";

                var outputTask = Task.Factory.StartNew(tokenObject =>
                {

                    this.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        NumberOfObjects.Text = "Number of detected objects: 0";
                        LastObject.Text = "";
                    }));
                    var token = (CancellationToken)tokenObject;

                    while (detectionTask.Status == TaskStatus.Running)
                    {
                        while (objects.TryDequeue(out Tuple<string, YoloV4Result> result))
                        {
                            if (token.IsCancellationRequested)
                            {
                                this.Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    Status.Text = "Detection stopped";
                                }));
                                break;
                            }

                            numberOfObjects += 1;
                            var filePath = result.Item1;
                            int index = Array.FindIndex(filenames, val => val == filePath);

                            var dirsInPath = filePath.Split('\\');
                            var filename = dirsInPath[dirsInPath.Length - 1];

                            var detected = result.Item2;
                            var x1 = detected.BBox[0];
                            var y1 = detected.BBox[1];
                            var x2 = detected.BBox[2];
                            var y2 = detected.BBox[3];
                            var label = detected.Label;
                            var conf = detected.Confidence;

                            var bitmap = BitmapFromBitmapImage(images[index]);
                            var g = Graphics.FromImage(bitmap);
                            g.DrawRectangle(Pens.Red, x1, y1, x2 - x1, y2 - y1);
                            var brushes = new SolidBrush(Color.FromArgb(50, Color.DarkRed));
                            g.FillRectangle(brushes, x1, y1, x2 - x1, y2 - y1);
                            g.DrawString(label + ',' + conf.ToString(),
                                new Font("Times New Roman", 20), Brushes.DarkRed, new PointF(x1, y1));


                            images = images.RemoveAt(index);
                            images = images.Insert(index, BitmapImageFromBitmap(bitmap));

                            string infoString = $"{label} was detected in {filename} at " +
                                $"position ({x1:0.0}, {y1:0.0}) and ({x2:0.0}, {y2:0.0})" +
                                $"with confidence {conf:0.00})";


                            byte[] bytes = BitmapToByteArray(BitmapFromBitmapImage(clearImages[index]));
                            DetectedObject detectedForDb = new DetectedObject { ClassName = label,
                                Filename = filename, Up = x1, 
                                Down = x2, Left = y1, Right = y2 };
                            Image imgForDb = new Image
                            {
                                ImageContent = bytes,
                                ImageHash = DatabaseManager.Hash(bytes)
                            };

                            db.AddObject(imgForDb, detectedForDb);
                            db.SaveChanges();

                            this.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                listBox_Images.ItemsSource = images;
                                LastObject.Text = $"Last detected object: {infoString}";
                                NumberOfObjects.Text = $"Number of detected objects: {numberOfObjects}";
                            }));
                        }
                    }
                    this.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (token.IsCancellationRequested)
                        {
                            Status.Text = "Detection stopped";
                        }
                        else
                        {
                            Status.Text = "Detection ended";
                        }
                        UpdateDbView();
                    }));
                }, cts.Token, TaskCreationOptions.LongRunning);

                await Task.WhenAll(detectionTask, outputTask);
                
            }
            catch (Exception ex)
            {
                LastObject.Text = ex.Message;
            } finally
            {
                OpenButton.IsEnabled = true;
                StartButton.IsEnabled = true;
                ClearButton.IsEnabled = true;
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            cts.Cancel();
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {

            foreach (Image dbImage in db.Images)
            {
                var objectsQuery = dbImage.DetectedObjects;
                foreach (DetectedObject dbObject in objectsQuery)
                {
                    db.Remove(dbObject);
                }
                db.Remove(dbImage);
            }
            db.SaveChanges();
            UpdateDbView();
        }
    }
}
