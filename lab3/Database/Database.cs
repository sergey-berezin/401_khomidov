using System;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DatabaseManager
{
    public class ProcessedImage
    {
        public int ProcessedImageId { get; set; }
        public byte[] ImageContent { get; set; }
        public int ImageHashCode { get; set; }
        virtual public ICollection<RecognizedObject> Objects { get; set; }
    }
    public class RecognizedObject
    {
        public int RecognizedObjectId { get; set; }
        public float X1 { get; set; }
        public float Y1 { get; set; }
        public float X2 { get; set; }
        public float Y2 { get; set; }
        public string ClassName { get; set; }

        public int ProcessedImageId { get; set; }
    }
    public class ImageStoreContext : DbContext
    {
        public DbSet<ProcessedImage> Images { get; set; }
        public string DbPath { get; private set; }
        public ImageStoreContext()
        {
            var folder = Environment.SpecialFolder.LocalApplicationData;
            var path = Environment.GetFolderPath(folder);
            DbPath = $"{path}{System.IO.Path.DirectorySeparatorChar}image_store.db";
        }
        protected override void OnConfiguring(DbContextOptionsBuilder o)
            => o.UseLazyLoadingProxies().UseSqlite($"Data Source={DbPath}");
        public int GetHashCode(ProcessedImage img)
        {
            int res = img.ImageContent[0];
            foreach (var b in img.ImageContent)
            {
                res ^= b;
            }
            return res;
        }
        public bool Equal(ProcessedImage img1, ProcessedImage img2)
        {
            if (img1.ImageHashCode != img2.ImageHashCode)
                return false;
            if (img1 == null || img2 == null)
                return false;
            if (img1.ImageContent == null || img2.ImageContent == null)
                return false;
            if (img1.ImageContent.Length != img2.ImageContent.Length)
                return false;
            for (int i = 0; i < img1.ImageContent.Length; ++i)
            {
                if (img1.ImageContent[i] != img2.ImageContent[i])
                    return false;
            }
            return true;
        }
    }
}
