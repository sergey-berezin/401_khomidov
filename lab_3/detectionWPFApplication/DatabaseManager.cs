using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.IO;
using System.Linq;

namespace detectionWPFApplication
{
    public class DatabaseManager : DbContext
    {
        public DbSet<Image> Images { get; set; }

        public string DatabasePath { get; set; }
        public DatabaseManager()
        {
            var folder = Environment.SpecialFolder.LocalApplicationData;
            var path = Environment.GetFolderPath(folder);
            DatabasePath = Path.Join(path, "images.db");
        }
        protected override void OnConfiguring(DbContextOptionsBuilder o)
            => o.UseLazyLoadingProxies().UseSqlite($"Data Source={DatabasePath}");

        public static int Hash(byte[] imageContent)
        {
            const int prime = 30011;
            int hash = 0;

            for (int i = 0; i < imageContent.Length; i++)
            {
                hash = (imageContent[i] + hash << 1) % prime;
            }

            const int a = 18239;
            const int b = 83294;

            return (hash * a + b) % prime;
        }

        public bool Contains(Image image)
        {
            foreach (var dbImage in Images)
            {
                if (image.Equals(dbImage))
                    return true;
            }
            return false;
        }

        public void AddObject(Image image, DetectedObject newObject)
        {
            if (Contains(image))
            {
                image.DetectedObjects.Add(newObject);
                var dbImage = Images.Where(dbImage => dbImage.ImageHash == image.ImageHash &&
                dbImage.ImageContent.SequenceEqual(image.ImageContent)).First();

                if (dbImage.DetectedObjects.Where(earlyDetected => newObject.Equals(earlyDetected)).Count() == 0)
                {
                    dbImage.DetectedObjects.Add(newObject);
                }
            }
            else
            {
                image.DetectedObjects.Add(newObject);
                Add(image);
            }

        }

    }

    public class Image
    {
        public int ImageId { get; set; }
        public int ImageHash { get; set; }
        public byte[] ImageContent { get; set; }

        virtual public List<DetectedObject> DetectedObjects { get; set; } = new List<DetectedObject>();

        public bool Equals(Image other)
        {
            return ImageHash == other.ImageHash && ImageContent.SequenceEqual(other.ImageContent);
        }
    }

    public class DetectedObject
    {
        public int DetectedObjectId { get; set; }
        public string Filename { get; set; }
        public string ClassName { get; set; }
        public double Left { get; set; }
        public double Right { get; set; }
        public double Up { get; set; }
        public double Down { get; set; }

        public bool Equals(DetectedObject other)
        {
            return (ClassName == other.ClassName &&
                Filename == other.Filename &&
                Up == other.Up && Down == other.Down && 
                Left == other.Left && Right == other.Right);
        }
        public override string ToString()
        {
            return $"{ClassName} detected in {Filename} at [{Up}:{Down}, {Left}:{Right}]";
        }
    }
}