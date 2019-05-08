using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Xml;
using Microsoft.VisualBasic.FileIO;

namespace WallpaperManager
{
    public class WallpaperList
    {
        private string path;
        private ObservableCollection<WallpaperEntry> list;
        private Dictionary<string, WallpaperEntry> map;
        private int lastInsertIndex;
        private bool sorted;

        /// <summary>
        /// The file path corresponding to this list
        /// </summary>
        public string Path
        {
            get { return this.path; }
        }

        /// <summary>
        /// The track-able list
        /// </summary>
        public ObservableCollection<WallpaperEntry> List
        {
            get { return this.list; }
        }

        /// <summary>
        /// Number of elements in the list
        /// </summary>
        public int Count
        {
            get { return this.list.Count; }
        }

        /// <summary>
        /// Creates an instance of the WallpaperList
        /// </summary>
        /// <param name="filePath">the file path to read/write to</param>
        /// <param name="sorted">whether the list needs to be sorted</param>
        /// <exception cref="Exception">Can throw exception</exception>
        public WallpaperList(string filePath, bool sorted)
        {
            this.path = filePath;
            this.sorted = sorted;

            this.list = new ObservableCollection<WallpaperEntry>();
            this.map = new Dictionary<string, WallpaperEntry>();
            this.lastInsertIndex = -1;

            this.LoadFromFile();
        }

        /// <summary>
        /// Adds a new directory entry to the list
        /// </summary>
        /// <param name="path">path of the directory to traverse and add</param>
        public void AddDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                return;
            }

            DirectoryInfo dir = new DirectoryInfo(path);
            foreach (DirectoryInfo file in dir.GetDirectories())
            {
                this.AddDirectory(file.FullName);
            }

            foreach (FileInfo file in dir.GetFiles())
            {
                this.AddFile(file.FullName);
            }
        }

        /// <summary>
        /// Adds a new file entry to the list
        /// </summary>
        /// <param name="filePath">path of the file to add</param>
        public void AddFile(string filePath)
        {
            this.AddFile(filePath, filePath, null);
        }

        /// <summary>
        /// Adds a new file entry to the list
        /// </summary>
        /// <param name="filePath">path of the file to add</param>
        /// <param name="thumbnailPath">path of the thumbnail file</param>
        /// <param name="dimensions">dimensions of the file</param>
        public void AddFile(string filePath, string thumbnailPath, string dimensions)
        {
            string thumb = thumbnailPath.ToLower();
            if (!this.IsSupportedFileType(thumbnailPath))
            {
                thumbnailPath = filePath;
            }

            string file = filePath.ToLower();
            if (this.IsSupportedFileType(file))
            {
                WallpaperEntry entry = this.GetEntry(filePath);
                if (entry == null)
                {
                    entry = new WallpaperEntry(filePath, thumbnailPath, dimensions);
                    this.map[filePath] = entry;

                    if (this.sorted)
                    {
                        this.AddSortedToList(entry);
                    }
                    else
                    {
                        this.list.Add(entry);
                        this.lastInsertIndex = this.list.Count - 1;
                    }
                }
            }
        }

        /// <summary>
        /// Removes a file entry from the list
        /// </summary>
        /// <param name="filePath">path of the file to remove</param>
        /// <param name="delete">indicates whether the file should be deleted as well</param>
        public void RemoveFile(string filePath, bool delete)
        {
            WallpaperEntry entry = null;
            if (this.map.TryGetValue(filePath, out entry))
            {
                this.map.Remove(filePath);
                this.list.Remove(entry);
                this.lastInsertIndex = -1;

                if (delete && File.Exists(filePath))
                {
                    FileSystem.DeleteFile(filePath, UIOption.AllDialogs, RecycleOption.SendToRecycleBin, UICancelOption.DoNothing);
                }
            }
        }

        /// <summary>
        /// Remove all entries from the list
        /// </summary>
        public void RemoveAll()
        {
            this.map.Clear();
            this.list.Clear();
            this.lastInsertIndex = -1;
        }

        /// <summary>
        /// Look for an entry based on the file path
        /// </summary>
        /// <param name="file">path of the file to search</param>
        /// <returns></returns>
        public WallpaperEntry GetEntry(string file)
        {
            WallpaperEntry entry = null;
            if (this.map.TryGetValue(file, out entry))
            {
                return entry;
            }

            return null;
        }

        /// <summary>
        /// Load the list from file
        /// </summary>
        /// <exception cref="Exception">Can throw exception</exception>
        public void LoadFromFile()
        {
            XmlTextReader reader = null;
            this.RemoveAll();

            try
            {
                //the list file doesn't exist, this might be the first time the application ran
                if (!File.Exists(this.path))
                {
                    this.map.Clear();
                    this.list.Clear();
                    return;
                }

                reader = new XmlTextReader(this.path);
                reader.WhitespaceHandling = WhitespaceHandling.None;
                reader.MoveToContent();

                if ((reader.NodeType == XmlNodeType.Element) && (reader.Name == "List"))
                {
                    //read to the first element
                    if (reader.Read())
                    {
                        while (true)
                        {
                            if ((reader.NodeType == XmlNodeType.EndElement) && (reader.Name == "List"))
                            {
                                break;
                            }

                            string filePath = reader.ReadElementString("FilePath");
                            if (!string.IsNullOrEmpty(filePath))
                            {
                                string thumbnailPath = reader.ReadElementString("ThumbnailPath");
                                if (string.IsNullOrEmpty(thumbnailPath))
                                {
                                    thumbnailPath = filePath;
                                }

                                string dimensions = reader.ReadElementString("Dimensions");
                                WallpaperEntry entry = new WallpaperEntry(filePath, thumbnailPath, dimensions);
                                this.map[filePath] = entry;
                                this.list.Add(entry);
                            }
                        }
                    }
                    else
                    {
                        //list is empty. Nothing to do
                    }
                }
                else
                {
                    throw new Exception("Invalid starting token");
                }
            }
            finally
            {
                if (reader != null)
                {
                    reader.Close();
                }
            }
        }

        /// <summary>
        /// Save the list to file
        /// </summary>
        /// <exception cref="Exception">Can throw exception</exception>
        public void SaveToFile()
        {
            XmlTextWriter writer = null;

            try
            {
                writer = new XmlTextWriter(this.path, Encoding.UTF8);
                writer.Formatting = Formatting.Indented;
                writer.WriteStartDocument();
                writer.WriteStartElement("List");

                foreach (WallpaperEntry entry in this.list)
                {
                    writer.WriteElementString("FilePath", entry.FilePath);
                    writer.WriteElementString("ThumbnailPath", entry.ThumbnailPath);
                    writer.WriteElementString("Dimensions", entry.Dimensions);
                }

                writer.WriteEndElement();
                writer.WriteEndDocument();
            }
            finally
            {
                if (writer != null)
                {
                    writer.Close();
                }
            }
        }

        /// <summary>
        /// Check if a file is of a supported type or not
        /// </summary>
        /// <param name="file">file name</param>
        /// <returns></returns>
        private bool IsSupportedFileType(string file)
        {
            return (file.EndsWith(".bmp") || file.EndsWith(".jpg") || file.EndsWith(".gif") || file.EndsWith(".png"));
        }

        /// <summary>
        /// Add an entry to the list in a sorted manner
        /// </summary>
        /// <param name="newEntry">the entry to add</param>
        private void AddSortedToList(WallpaperEntry newEntry)
        {
            if (this.lastInsertIndex >= this.list.Count)
            {
                this.lastInsertIndex = -1;
            }

            if (this.lastInsertIndex == -1)
            {
                foreach (WallpaperEntry entry in this.list)
                {
                    if (entry.FilePath.CompareTo(newEntry.FilePath) > 0)
                    {
                        int index = this.list.IndexOf(entry);
                        this.list.Insert(index, newEntry);
                        this.lastInsertIndex = index;
                        return;
                    }
                }

                this.list.Add(newEntry);
                this.lastInsertIndex = this.list.Count - 1;
                return;
            }
            else
            {
                int currentIndex = this.lastInsertIndex;
                WallpaperEntry lastAddedItem = this.list[this.lastInsertIndex];
                if (lastAddedItem.FilePath.CompareTo(newEntry.FilePath) > 0)
                {
                    //go down in list
                    currentIndex--;

                    while (currentIndex > 0)
                    {
                        WallpaperEntry current = this.list[currentIndex];
                        if (current.FilePath.CompareTo(newEntry.FilePath) <= 0)
                        {
                            this.list.Insert(currentIndex + 1, newEntry);
                            this.lastInsertIndex = currentIndex + 1;
                            return;
                        }

                        currentIndex--;
                    }

                    this.list.Insert(0, newEntry);
                    this.lastInsertIndex = 0;
                    return;
                }
                else
                {
                    //go up in list
                    currentIndex++;

                    while (currentIndex < this.list.Count)
                    {
                        WallpaperEntry current = this.list[currentIndex];
                        if (current.FilePath.CompareTo(newEntry.FilePath) > 0)
                        {
                            this.list.Insert(currentIndex, newEntry);
                            this.lastInsertIndex = currentIndex;
                            return;
                        }

                        currentIndex++;
                    }

                    this.list.Add(newEntry);
                    this.lastInsertIndex = this.list.Count - 1;
                    return;
                }
            }
        }
    }
}
