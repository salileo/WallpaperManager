using System;

namespace WallpaperManager
{
    public class WallpaperEntry
    {
        public string FilePath { get; set; }
        public string ThumbnailPath { get; set; }
        public string Dimensions { get; set; }

        public WallpaperEntry(string filePath, string thumbnailPath, string dimensions)
        {
            this.FilePath = filePath;
            this.ThumbnailPath = thumbnailPath;
            this.Dimensions = dimensions;
        }
    }
}
