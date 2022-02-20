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
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Forms;
using Microsoft.WindowsAPICodePack.Dialogs;
using detectionLibrary;

namespace detectionWPFApplication
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private CancellationTokenSource cts;
        //private CancellationToken token;
        public ImmutableList<BitmapImage> images;
        public string[] filenames;
        public string directoryPath = "";
        public MainWindow()
        {
            InitializeComponent();
            cts = new CancellationTokenSource();
            listBox_Images.ItemsSource = new List<BitmapImage>();
            images = ImmutableList.Create<BitmapImage>();
            Status.Text = "Detection didn't start";

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
                System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(outStream);

                return new Bitmap(bitmap);
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
                            if (index < 0)
                            {
                                continue;
                            }

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

                            this.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                listBox_Images.ItemsSource = images;
                                LastObject.Text = "Last detected object: " + infoString;
                                NumberOfObjects.Text = "Number of detected objects: " + numberOfObjects;
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
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            cts.Cancel();
        }
    }
}
