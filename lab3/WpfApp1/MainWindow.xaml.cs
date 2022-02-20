using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.Drawing;
using DetectionCore;
using DatabaseManager;
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
using Microsoft.EntityFrameworkCore;

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
        ImageStoreContext db;

        private void ShowDBContent()
        {
            listView_Images.ItemsSource = db.Images.ToList();

            var objectList = new List<RecognizedObject>();
            foreach (var img in db.Images)
            {
                objectList.AddRange(img.Objects);
            }
            listView_Objects.ItemsSource = objectList;
        }

        public MainWindow()
        {
            InitializeComponent();
            db = new ImageStoreContext();

            ShowDBContent();
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

        private byte[] ImageToByteArray(Image img)
        {
            using (var stream = new MemoryStream())
            {
                img.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                return stream.ToArray();
            }
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
            Open_Button.IsEnabled = false;
            Start_Button.IsEnabled = false;
            Clear_Button.IsEnabled = false;
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
                        // Create ProcessedImage object
                        var currImage = new ProcessedImage
                        {
                            Objects = new List<RecognizedObject>()
                        };
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
                            // Add recognized object to currImage
                            currImage.Objects.Add(new RecognizedObject() { ClassName = res.Label, X1 = x1, Y1 = y1, X2 = x2, Y2 = y2 });
                        }
                        int ind = Array.FindIndex(filenames, val => val.Equals(name));
                        im_items = im_items.RemoveAt(ind);
                        im_items = im_items.Insert(ind, Bitmap2BitmapImage(bitmap));
                        this.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            listBox_Images.ItemsSource = im_items;
                        }));
                        // Fill BLOB and hash code in currImage
                        currImage.ImageContent = ImageToByteArray(bitmap);
                        currImage.ImageHashCode = db.GetHashCode(currImage);
                        // Add currImage to DB
                        bool inDB = false;
                        foreach (var img in db.Images)
                        {
                            if (db.Equal(currImage, img))
                            {
                                inDB = true;
                                break;
                            }
                        }
                        if (!inDB)
                        {
                            db.Add(currImage);
                            db.SaveChanges();
                        }
                    }
                }
            }, TaskCreationOptions.LongRunning);
            await Task.WhenAll(task1, task2);

            ShowDBContent();

            Open_Button.IsEnabled = true;
            Start_Button.IsEnabled = true;
            Clear_Button.IsEnabled = true;
        }

        private void Button_Clear(object sender, RoutedEventArgs e)
        {
            Open_Button.IsEnabled = false;
            Start_Button.IsEnabled = false;
            Clear_Button.IsEnabled = false;

            var images = db.Images.Include(e => e.Objects);
            db.RemoveRange(images);
            db.SaveChanges();

            ShowDBContent();

            Open_Button.IsEnabled = true;
            Start_Button.IsEnabled = true;
            Clear_Button.IsEnabled = true;
        }
    }
}
