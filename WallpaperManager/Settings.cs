using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows.Media;
using System.Xml;
using Microsoft.Win32;

namespace WallpaperManager
{
    public class Settings
    {
        public static string AppFolder = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData) + "\\WallpaperManager\\";

        // Paths of the files related to the online database
        public static string OnlineDatabaseFolder = Settings.AppFolder + "onlineDatabase";
        public static string OnlineDatabaseZip = Settings.AppFolder + "lastDataFile.zip";
        public static string OnlineDatabaseLog = Settings.AppFolder + "lastDataFile.txt";

        // Cache folders
        public static string ThumbnailCacheFolder = Settings.AppFolder + "thumbnailCache";
        public static string WallpaperCacheFolder = Settings.AppFolder + "wallpaperCache";

        // App files
        public static string SettingsFile = Settings.AppFolder + "settings.xml";
        public static string FileList_Local = Settings.AppFolder + "localList.xml";
        public static string FileList_Online = Settings.AppFolder + "onlineList.xml";

        // static instance
        private static Settings instance;

        // Properties which are stored in file
        private string selectedImage;
        private string presentationImage;
        private bool inPresentationMode;
        private DisplayModeType displayMode;
        private string backgroundColor;
        private bool startAutomatically;
        private bool startMinimized;
        private bool changeAtStartup;
        private bool changeRandomly;
        private bool changeAutomatically;
        private int fileListIndex;
        private bool highResolutionThumbs;
        private uint timeInterval;

        // Properties which used in code
        private List<ColorValue> colorValues;

        /// <summary>
        /// Static instance
        /// </summary>
        public static Settings Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new Settings();
                }

                return instance;
            }
        }

        /// <summary>
        /// List of color values supported
        /// </summary>
        public List<ColorValue> ColorValues
        {
            get { return this.colorValues; }
        }

        /// <summary>
        /// Path of the last selected image
        /// </summary>
        public string SelectedImage
        {
            get { return this.selectedImage; }

            set
            {
                this.selectedImage = value;
                this.SaveToFile();
            }
        }

        /// <summary>
        /// Path of the last selected presentation image
        /// </summary>
        public string PresentationImage
        {
            get { return this.presentationImage; }

            set
            {
                this.presentationImage = value;
                this.SaveToFile();
            }
        }

        /// <summary>
        /// Whether the app is in presentation mode
        /// </summary>
        public bool InPresentationMode
        {
            get { return this.inPresentationMode; }

            set
            {
                this.inPresentationMode = value;
                this.SaveToFile();
            }
        }

        /// <summary>
        /// The display mode
        /// </summary>
        public DisplayModeType DisplayMode
        {
            get { return this.displayMode; }

            set
            {
                this.displayMode = value;
                this.SaveToFile();
            }
        }

        /// <summary>
        /// The background color
        /// </summary>
        public string BackgroundColor
        {
            get { return this.backgroundColor; }

            set
            {
                this.backgroundColor = value;
                this.SaveToFile();
            }
        }

        /// <summary>
        /// Whether to launch the app when Windows starts
        /// </summary>
        public bool StartWithWindows
        {
            get { return this.startAutomatically; }

            set
            {
                this.startAutomatically = value;
                this.SaveToFile();
            }
        }

        /// <summary>
        /// Whether to launch the app in minimized state
        /// </summary>
        public bool StartMinimized
        {
            get { return this.startMinimized; }

            set
            {
                this.startMinimized = value;
                this.SaveToFile();
            }
        }

        /// <summary>
        /// Whether to change the wallpaper automatically at app launch
        /// </summary>
        public bool ChangeAtStartup
        {
            get { return this.changeAtStartup; }

            set
            {
                this.changeAtStartup = value;
                this.SaveToFile();
            }
        }

        /// <summary>
        /// Whether to choose the wallpapers in sequence or at random
        /// </summary>
        public bool ChangeRandomly
        {
            get { return this.changeRandomly; }

            set
            {
                this.changeRandomly = value;
                this.SaveToFile();
            }
        }

        /// <summary>
        /// Index of the tab to display at startup
        /// </summary>
        public int FileListIndex
        {
            get { return this.fileListIndex; }

            set
            {
                this.fileListIndex = value;
                this.SaveToFile();
            }
        }

        /// <summary>
        /// Whether to use high resolution thumbnails for the online repository
        /// </summary>
        public bool HighResolutionThumbs
        {
            get { return this.highResolutionThumbs; }
            set
            {
                this.highResolutionThumbs = value;
                this.SaveToFile();
            }
        }

        /// <summary>
        /// Whether to change the wallpaper at regular intervals
        /// </summary>
        public bool ChangeAutomatically
        {
            get { return this.changeAutomatically; }

            set
            {
                this.changeAutomatically = value;
                this.SaveToFile();
            }
        }

        /// <summary>
        /// The time interval after which to change the wallpaper
        /// </summary>
        public uint TimeInterval
        {
            get { return this.timeInterval; }

            set
            {
                this.timeInterval = value;
                this.SaveToFile();
            }
        }

        /// <summary>
        /// Constructor for the Settings
        /// </summary>
        private Settings()
        {
            this.colorValues = new List<ColorValue>();
            Type colorsType = typeof(Colors);
            PropertyInfo[] pis = colorsType.GetProperties();
            foreach (PropertyInfo pi in pis)
            {
                ColorValue value = new ColorValue(pi.Name, (Color)pi.GetValue(null, null));
                this.colorValues.Add(value);
            }

            //load the defaults to begin with
            this.selectedImage = string.Empty;
            this.presentationImage = string.Empty;
            this.inPresentationMode = false;

            this.RestoreDefaults();

            try
            {
                this.LoadFromFile();
            }
            catch (Exception e)
            {
                ErrorLog.Log("Could not properly load the settings, using defaults.", e);
                this.RestoreDefaults();
            }
        }

        /// <summary>
        /// Gets the color value for the background color to use
        /// </summary>
        /// <returns></returns>
        public Color GetBackgroundColor()
        {
            string searchString = this.backgroundColor.ToLower();
            foreach (ColorValue color in this.colorValues)
            {
                if (color.Name.ToLower() == searchString)
                {
                    return color.Color;
                }
            }

            return Color.FromRgb(255, 255, 255);
        }

        /// <summary>
        /// Load the settings from the file
        /// </summary>
        /// <exception cref="Exception">Can throw exception</exception>
        private void LoadFromFile()
        {
            XmlTextReader reader = null;

            try
            {
                //the settings file doesn't exist, this might be the first time the application ran, so load the defaults
                if (!File.Exists(Settings.SettingsFile))
                {
                    this.RestoreDefaults();
                    return;
                }

                reader = new XmlTextReader(Settings.SettingsFile);
                reader.WhitespaceHandling = WhitespaceHandling.None;
                reader.MoveToContent();

                if ((reader.NodeType == XmlNodeType.Element) && (reader.Name == "Settings"))
                {
                    //read to the first element
                    if (!reader.Read())
                    {
                        throw (new Exception("Unable to read first token."));
                    }

                    while (true)
                    {
                        if ((reader.NodeType == XmlNodeType.EndElement) && (reader.Name == "Settings"))
                        {
                            break;
                        }

                        string tag = reader.Name;
                        string text = reader.ReadElementString();
                        if (tag == "DisplayMode")
                        {
                            this.displayMode = (DisplayModeType)Enum.Parse(typeof(DisplayModeType), text);
                        }
                        else if (tag == "BackgroundColor")
                        {
                            this.backgroundColor = text;
                        }
                        else if (tag == "ChangeAtStartup")
                        {
                            this.changeAtStartup = bool.Parse(text);
                        }
                        else if (tag == "ChangeRandomly")
                        {
                            this.changeRandomly = bool.Parse(text);
                        }
                        else if (tag == "FileListIndex")
                        {
                            this.fileListIndex = int.Parse(text);
                        }
                        else if (tag == "HighResolutionThumbs")
                        {
                            this.highResolutionThumbs = bool.Parse(text);
                        }
                        else if (tag == "ChangeAutomatically")
                        {
                            this.changeAutomatically = bool.Parse(text);
                        }
                        else if (tag == "TimeInterval")
                        {
                            this.timeInterval = uint.Parse(text);
                        }
                        else if (tag == "SelectedImage")
                        {
                            this.selectedImage = text;
                            if (this.selectedImage == "NULL")
                            {
                                this.selectedImage = string.Empty;
                            }
                        }
                        else if (tag == "PresentationImage")
                        {
                            this.presentationImage = text;
                            if (this.presentationImage == "NULL")
                            {
                                this.presentationImage = string.Empty;
                            }
                        }
                        else if (tag == "InPresentationMode")
                        {
                            this.inPresentationMode = bool.Parse(text);
                        }
                        else if (tag == "StartMinimized")
                        {
                            this.startMinimized = bool.Parse(text);
                        }
                        else if (tag == "StartWithWindows")
                        {
                            this.startAutomatically = bool.Parse(text);
                        }
                    }
                }
                else
                {
                    throw (new Exception("Invalid starting token."));
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
        /// Save the settings to the file
        /// </summary>
        /// <exception cref="Exception">Can throw exception</exception>
        private void SaveToFile()
        {
            XmlTextWriter writer = null;

            bool success = false;
            try
            {
                writer = new XmlTextWriter(Settings.SettingsFile, System.Text.Encoding.UTF8);
                writer.Formatting = System.Xml.Formatting.Indented;
                writer.WriteStartDocument();
                writer.WriteStartElement("Settings");
                writer.WriteElementString("DisplayMode", this.displayMode.ToString());
                writer.WriteElementString("BackgroundColor", this.backgroundColor);
                writer.WriteElementString("ChangeAtStartup", this.changeAtStartup.ToString());
                writer.WriteElementString("ChangeRandomly", this.changeRandomly.ToString());
                writer.WriteElementString("FileListIndex", this.fileListIndex.ToString());
                writer.WriteElementString("HighResolutionThumbs", this.highResolutionThumbs.ToString());
                writer.WriteElementString("ChangeAutomatically", this.changeAutomatically.ToString());
                writer.WriteElementString("TimeInterval", this.timeInterval.ToString());

                string img = this.selectedImage;
                if (string.IsNullOrEmpty(img))
                {
                    img = "NULL";
                }
                writer.WriteElementString("SelectedImage", img);

                img = this.presentationImage;
                if (string.IsNullOrEmpty(img))
                {
                    img = "NULL";
                }
                writer.WriteElementString("PresentationImage", img);
                writer.WriteElementString("InPresentationMode", this.inPresentationMode.ToString());
                writer.WriteElementString("StartMinimized", this.startMinimized.ToString());
                writer.WriteElementString("StartWithWindows", this.startAutomatically.ToString());
                writer.WriteEndElement();
                writer.WriteEndDocument();
                success = true;
            }
            catch (Exception e)
            {
                throw new Exception("Could not properly save the settings.", e);
            }

            if (success)
            {
                try
                {
                    RegistryKey rkApp = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                    if (this.startAutomatically)
                    {
                        rkApp.SetValue("Wallpaper Manager", System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);
                    }
                    else
                    {
                        rkApp.DeleteValue("Wallpaper Manager", false);
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception("Could not update application in startup list.", ex);
                }
            }

            if (writer != null)
            {
                writer.Close();
            }
        }

        /// <summary>
        /// Set the default values of the settings
        /// </summary>
        private void RestoreDefaults()
        {
            this.displayMode = DisplayModeType.Center;
            this.backgroundColor = "Black";
            this.startAutomatically = true;
            this.startMinimized = false;
            this.changeAtStartup = false;
            this.changeRandomly = true;
            this.fileListIndex = 0;
            this.highResolutionThumbs = false;
            this.changeAutomatically = false;
            this.timeInterval = 30;
        }
    }

    /// <summary>
    /// The display mode options
    /// </summary>
    public enum DisplayModeType
    {
        Stretch = 0,
        Fill = 1,
        Center = 2,
        Tile = 3
    }

    /// <summary>
    /// The color values
    /// </summary>
    public class ColorValue
    {
        public string Name { get; set; }
        public Color Color { get; set; }
        public Brush ColorBrush { get { return new SolidColorBrush(Color); } }

        public ColorValue(string name, Color color)
        {
            Name = name;
            Color = color;
        }
    }
}
