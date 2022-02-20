using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.Drawing;
using DetectionCore;
using YOLOv4MLNet.DataStructures;
using System.Collections.Generic;
using System.Windows.Media.Imaging;
using System;
using System.Threading;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Windows.Interop;
using System.Drawing.Imaging;
using System.Collections.ObjectModel;
using System.Collections.Immutable;
namespace WpfApp1
{ /// <summary>
  /// Interaction logic for MainWindow.xaml
  /// </summary>
    public partial class MainWindow : Window
    {
        static readonly CancellationTokenSource source = new CancellationTokenSource();
        static readonly CancellationToken token = source.Token;
        private ImmutableList<BitmapImage> im_items;
        private string imageFolder = "";
        private string[] filenames = new string[0];

        public MainWindow()
        {
            InitializeComponent();
        }

        private BitmapImage Bitmap2BitmapImage(Bitmap bitmap)
        {
            BitmapImage bitmapImage = new BitmapImage();
            using (MemoryStream memory = new MemoryStream())
            {
                bitmap.Save(memory, ImageFormat.Png);
                memory.Position = 0;
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memory;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                bitmapImage.Freeze();
            }
            return bitmapImage;
        }

        private void Button_Open(object sender, RoutedEventArgs e)
        {
            var dlg = new CommonOpenFileDialog
            {
                InitialDirectory = "C:\\Users\\murad\\Desktop",
                IsFolderPicker = true
            };
            if (dlg.ShowDialog() == CommonFileDialogResult.Ok)
            {
                im_items = ImmutableList.Create<BitmapImage>();
                var filepaths = Directory.GetFiles(dlg.FileName, "*", SearchOption.TopDirectoryOnly).ToArray();
                filenames = filepaths.Select(path => Path.GetFileName(path)).ToArray();
                int n = filepaths.Length;
                for (int i = 0; i < n; ++i)
                {
                    im_items = im_items.Add(new BitmapImage(new Uri(filepaths[i])));
                }
                listBox_Images.ItemsSource = im_items;

                imageFolder = dlg.FileName;
            }
        }

        private void Button_Stop(object sender, RoutedEventArgs e)
        {
            source.Cancel();
        }

        private async void Button_Start(object sender, RoutedEventArgs e)
        {
            var recognitionResult = new ConcurrentQueue<Tuple<string, IReadOnlyList<YoloV4Result>>>();

            var task1 = Task.Factory.StartNew(() => Detection.Detect(imageFolder, recognitionResult, token), TaskCreationOptions.LongRunning);
            var task2 = Task.Factory.StartNew(() =>
            {
                while (task1.Status == TaskStatus.Running)
                {
                    while (recognitionResult.TryDequeue(out Tuple<string, IReadOnlyList<YoloV4Result>> result))
                    {
                        string name = result.Item1;
                        var bitmap = new Bitmap(Image.FromFile(Path.Combine(imageFolder, name)));
                        using var g = Graphics.FromImage(bitmap);
                        foreach (var res in result.Item2)
                        {
                            var x1 = res.BBox[0];
                            var y1 = res.BBox[1];
                            var x2 = res.BBox[2];
                            var y2 = res.BBox[3];
                            g.DrawRectangle(Pens.Red, x1, y1, x2 - x1, y2 - y1);
                            using (var brushes = new SolidBrush(Color.FromArgb(50, Color.Red)))
                            {
                                g.FillRectangle(brushes, x1, y1, x2 - x1, y2 - y1);
                            }

                            g.DrawString(res.Label, new Font("Arial", 12),
                                         Brushes.Blue, new PointF(x1, y1));
                        }
                        int ind = Array.FindIndex(filenames, val => val.Equals(name));
                        im_items = im_items.RemoveAt(ind);
                        im_items = im_items.Insert(ind, Bitmap2BitmapImage(bitmap));
                        this.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            listBox_Images.ItemsSource = im_items;
                        }));
                    }
                }
            }, TaskCreationOptions.LongRunning);
            await Task.WhenAll(task1, task2);
        }
    }
}
