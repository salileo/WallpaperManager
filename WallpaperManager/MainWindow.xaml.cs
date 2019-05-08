using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Collections.Generic;
using System.Windows.Data;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Threading.Tasks;
using System.ComponentModel;

namespace WallpaperManager
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // the current product version
        public static string Version = ProductVersion.VersionInfo.Version;

        private WallpaperList activeList;
        private Stack<WallpaperEntry> imageStack;
        private TrayIcon trayIcon;
        private Timer autoChangeTimer;
        private bool shouldExit;
        private bool inUpdatingMode;

        public event EventHandler PresentationModeChanged;

        /// <summary>
        /// Constructor for the main window
        /// </summary>
        public MainWindow()
        {
            // check for an update
            GenericUpdater.Updater.DoUpdate("WallpaperManager", MainWindow.Version, "WallpaperManager_Version.xml", "WallpaperManager.msi", new GenericUpdater.Updater.UpdateFiredDelegate(UpdateFired));

            // load the settings
            Settings settings = Settings.Instance;

            InitializeComponent();

            // hide the settings panel
            c_settings.ShowPanel(false);

            // setup the tray icon
            this.trayIcon = new TrayIcon(this);

            this.imageStack = new Stack<WallpaperEntry>();
            c_imagenext.IsEnabled = false;
            c_imageprevious.IsEnabled = false;
            c_imageset.IsEnabled = false;

            // setup the file list
            c_tabs.SelectedIndex = Settings.Instance.FileListIndex;

            // start the auto change time if required
            this.autoChangeTimer = new Timer();
            SetupAutoChangeTimer();

            // show the tray icon
            this.trayIcon.UpdateTrayIcon(true);

            // hide the main window if required
            this.trayIcon.UpdateMainWindow(!Settings.Instance.StartMinimized);

            // setup the presentation mode
            this.UpdatePresentationMode(Settings.Instance.InPresentationMode);

            // select the appropriate image
            if (Settings.Instance.InPresentationMode)
            {
                this.trayIcon.SetToolText(Settings.Instance.PresentationImage);
                WallpaperEntry entry = this.activeList.GetEntry(Settings.Instance.SelectedImage);
                if (entry != null)
                {
                    c_filelist.SelectedItem = entry;
                }
            }
            else
            {
                if (Settings.Instance.ChangeAtStartup)
                {
                    this.GotoNextImage(true);
                }
                else
                {
                    this.trayIcon.SetToolText(Settings.Instance.SelectedImage);
                    WallpaperEntry entry = this.activeList.GetEntry(Settings.Instance.SelectedImage);
                    if (entry != null)
                    {
                        c_filelist.SelectedItem = entry;
                    }
                }
            }
        }

        /// <summary>
        /// Create and load the file list based on the settings
        /// </summary>
        public void UpdateFileList()
        {
            if (Settings.Instance.FileListIndex == 0)
            {
                // if none loaded or different from active one, reload
                if (this.activeList == null ||
                    this.activeList.Path != Settings.FileList_Online)
                {
                    try
                    {
                        OnlineRepository online = new OnlineRepository();
                        if (online.SyncToLocal())
                        {
                            if (File.Exists(Settings.FileList_Online))
                            {
                                File.Delete(Settings.FileList_Online);
                            }
                        }

                        this.BuildOnlineFile();
                    }
                    catch (Exception ex)
                    {
                        ErrorLog.Log("Unable to sync with online repository, the cached list will be used.", ex);
                    }

                    WallpaperList list = null;
                    try
                    {
                        list = new WallpaperList(Settings.FileList_Online, false);
                    }
                    catch (Exception e)
                    {
                        ErrorLog.Log("There was an error loading the online file list, loading an empty list.", e);

                        if (File.Exists(Settings.FileList_Online))
                        {
                            File.Delete(Settings.FileList_Online);
                        }

                        list = new WallpaperList(Settings.FileList_Online, false);
                    }

                    this.activeList = list;
                }

                c_copyPath.Visibility = Visibility.Collapsed;
                c_openFolder.Visibility = Visibility.Collapsed;
                c_deleteFile.Visibility = Visibility.Collapsed;
            }
            else
            {
                // if none loaded or different from active one, reload
                if (this.activeList == null ||
                    this.activeList.Path != Settings.FileList_Local)
                {
                    WallpaperList list = null;
                    try
                    {
                        list = new WallpaperList(Settings.FileList_Local, true);
                    }
                    catch (Exception e)
                    {
                        ErrorLog.Log("There was an error loading the local file list, loading an empty list.", e);

                        if (File.Exists(Settings.FileList_Local))
                        {
                            File.Delete(Settings.FileList_Local);
                        }

                        list = new WallpaperList(Settings.FileList_Local, true);
                    }

                    this.activeList = list;
                }

                c_copyPath.Visibility = Visibility.Visible;
                c_openFolder.Visibility = Visibility.Visible;
                c_deleteFile.Visibility = Visibility.Visible;
            }

            c_filelist.ItemsSource = this.activeList.List;
            if (this.activeList.Count > 0)
            {
                c_imagenext.IsEnabled = true;
            }
            else
            {
                c_imagenext.IsEnabled = false;
            }
        }

        /// <summary>
        /// Set the UI properties based on the current image
        /// </summary>
        public async void GotoCurrentImage()
        {
            WallpaperEntry currentItem = c_filelist.SelectedItem as WallpaperEntry;
            if (currentItem == null)
            {
                // if no current entry found, clear out the UI

                c_imageset.IsEnabled = false;
                c_previewimage.Source = null;
                c_imageresolution.Text = string.Empty;
                c_imageformat.Text = string.Empty;
                c_imagefilesize.Text = string.Empty;

                if (this.imageStack.Count > 1)
                {
                    c_imageprevious.IsEnabled = true;
                }
                else
                {
                    c_imageprevious.IsEnabled = false;
                }

                c_filelist.ScrollIntoView(c_filelist.SelectedItem);
            }
            else
            {
                // try setting the UI properties based on current selection 
                if (this.BeginTransaction())
                {
                    return;
                }

                c_imageset.IsEnabled = true;

                try
                {
                    // download the file to the cache if required
                    string filePath = this.GetLocalCopyOfImage(currentItem, true);

                    // load the image in memory
                    BitmapDecoder uriBitmap = BitmapDecoder.Create(new Uri(filePath), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);

                    // set the image in UI
                    c_previewimage.Source = uriBitmap.Frames[0];

                    // update the image info
                    c_imageformat.Text = uriBitmap.CodecInfo.FriendlyName;

                    if (String.IsNullOrEmpty(currentItem.Dimensions))
                    {
                        currentItem.Dimensions = uriBitmap.Frames[0].PixelWidth.ToString() + "x" + uriBitmap.Frames[0].PixelHeight.ToString();
                    }

                    c_imageresolution.Text = currentItem.Dimensions;

                    FileInfo info = new FileInfo(filePath);
                    c_imagefilesize.Text = info.Length.ToString() + " bytes";

                    // push the current image in the stack
                    if ((this.imageStack.Count == 0) || (currentItem != this.imageStack.First()))
                    {
                        this.imageStack.Push(currentItem);
                    }

                    // enable the 'previous' button if there is something in the stack
                    if (this.imageStack.Count > 1)
                    {
                        c_imageprevious.IsEnabled = true;
                    }
                    else
                    {
                        c_imageprevious.IsEnabled = false;
                    }

                    // scroll the selection into view
                    c_filelist.ScrollIntoView(c_filelist.SelectedItem);

                    this.EndTransaction();

                    // completed processed current selection
                    return;
                }
                catch (Exception)
                {
                    // nothing to do here
                }

                // we should come here only if something went wrong in processing current selection
                // in that case store the current index and eset the position
                int currentIndex = c_filelist.SelectedIndex;
                c_filelist.SelectedIndex = -1;

                // remove the bad entry from the list
                this.activeList.RemoveFile(currentItem.FilePath, true);

                if (this.activeList.Count > 0)
                {
                    c_imagenext.IsEnabled = true;
                }
                else
                {
                    c_imagenext.IsEnabled = false;
                }

                try
                {
                    this.activeList.SaveToFile();
                }
                catch (Exception)
                {
                    // nothing to do here
                }

                this.EndTransaction();

                // now look for the next best alternative
                await Task.Delay(1);
                this.GotoNextImage(false, currentIndex - 1);
            }
        }

        /// <summary>
        /// Look for the next valid image
        /// </summary>
        /// <param name="setImage">whether to set the next valid image as the background</param>
        /// <param name="startIndex">the index to start the scan from, if this is null use the current index</param>
        public async void GotoNextImage(bool setImage, int? startIndex = null)
        {
            // re-entrant protection
            if (this.BeginTransaction())
            {
                return;
            }

            // if the image also needs to be set then show balloon tooltip as it can be a lengthy process
            if (setImage)
            {
                this.trayIcon.SetLoadingText();
            }

            bool listUpdated = false;
            int currentIndex = (startIndex.HasValue) ? startIndex.Value : c_filelist.SelectedIndex;
            c_filelist.SelectedIndex = -1;
            int newIndex = -1;

            // we will keep looping till we find a valid image
            while (true)
            {
                await Task.Delay(1);

                // get the index of the next image
                if (this.activeList.Count == 0)
                {
                    newIndex = -1;
                    break;
                }
                else if (Settings.Instance.ChangeRandomly)
                {
                    Random rand = new Random();
                    while (true)
                    {
                        int index = rand.Next(this.activeList.Count);
                        if (index != currentIndex)
                        {
                            newIndex = index;
                            break;
                        }
                    }
                }
                else
                {
                    if (currentIndex == -1)
                    {
                        newIndex = 0;
                    }
                    else if (currentIndex >= (this.activeList.Count - 1))
                    {
                        newIndex = 0;
                    }
                    else
                    {
                        newIndex = currentIndex + 1;
                    }
                }

                // get the entry corresponding to the index
                WallpaperEntry newEntry = this.activeList.List[newIndex];

                // try to load the thumbnail
                try
                {
                    string filePath = this.GetLocalCopyOfImage(newEntry, true);
                    BitmapDecoder uriBitmap = BitmapDecoder.Create(new Uri(filePath), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                }
                catch (Exception)
                {
                    // in case of an exception remove the entry from the list
                    this.activeList.RemoveFile(newEntry.FilePath, true);
                    listUpdated = true;

                    // try a different image
                    continue;
                }

                if (setImage)
                {
                    // try to set the background
                    try
                    {
                        this.SetBackgroundImage(newEntry);
                    }
                    catch (Exception)
                    {
                        // in case of an exception remove the entry from the list
                        this.activeList.RemoveFile(newEntry.FilePath, true);
                        listUpdated = true;

                        // try a different image
                        continue;
                    }
                }

                // if the current selection succeeded to load, end the loop
                break;
            }

            if (listUpdated)
            {
                if (this.activeList.Count > 0)
                {
                    c_imagenext.IsEnabled = true;
                }
                else
                {
                    c_imagenext.IsEnabled = false;
                }

                try
                {
                    this.activeList.SaveToFile();
                }
                catch (Exception)
                {
                    // nothing to do here
                }
            }

            this.EndTransaction();

            // set the appropriate image as current
            c_filelist.SelectedIndex = newIndex;
        }

        /// <summary>
        /// Look for the previous valid image
        /// </summary>
        /// <param name="setImage">whether to set the previous valid image as the background</param>
        public async void GotoPreviousImage(bool setImage)
        {
            // re-entrant protection
            if (this.BeginTransaction())
            {
                return;
            }

            // if the image also needs to be set then show balloon tooltip as it can be a lengthy process
            if (setImage)
            {
                this.trayIcon.SetLoadingText();
            }

            bool listUpdated = false;
            WallpaperEntry newEntry = null;
            c_filelist.SelectedIndex = -1;

            // we will keep looping till we find a valid image
            while (true)
            {
                await Task.Delay(1);

                // get the index of the previous image
                if (this.imageStack.Count == 0)
                {
                    newEntry = null;
                }
                else
                {
                    this.imageStack.Pop();
                    if (this.imageStack.Count > 0)
                    {
                        newEntry = this.imageStack.First();
                    }
                }

                if (this.imageStack.Count > 1)
                {
                    c_imageprevious.IsEnabled = true;
                }
                else
                {
                    c_imageprevious.IsEnabled = false;
                }

                // if no previous entry, break the loop
                if (newEntry == null)
                {
                    break;
                }

                // try to load the thumbnail
                try
                {
                    string filePath = this.GetLocalCopyOfImage(newEntry, true);
                    BitmapDecoder uriBitmap = BitmapDecoder.Create(new Uri(filePath), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                }
                catch (Exception)
                {
                    // in case of an exception remove the entry from the list
                    this.activeList.RemoveFile(newEntry.FilePath, true);
                    listUpdated = true;

                    // try a different image
                    continue;
                }

                if (setImage)
                {
                    // try to set the background
                    try
                    {
                        this.SetBackgroundImage(newEntry);
                    }
                    catch (Exception)
                    {
                        // in case of an exception remove the entry from the list
                        this.activeList.RemoveFile(newEntry.FilePath, true);
                        listUpdated = true;

                        // try a different image
                        continue;
                    }
                }

                // if the current selection succeeded to load, end the loop
                break;
            }

            if (listUpdated)
            {
                if (this.activeList.Count > 0)
                {
                    c_imagenext.IsEnabled = true;
                }
                else
                {
                    c_imagenext.IsEnabled = false;
                }

                try
                {
                    this.activeList.SaveToFile();
                }
                catch (Exception)
                {
                    // nothing to do here
                }
            }

            this.EndTransaction();

            // set the appropriate image as current
            if (newEntry == null)
            {
                c_filelist.SelectedIndex = -1;
            }
            else
            {
                c_filelist.SelectedItem = newEntry;
            }
        }

        /// <summary>
        /// Set the current selection as the background image
        /// </summary>
        public void SetCurrentImageAsBackground()
        {
            // re-entrant protection
            if (this.BeginTransaction())
            {
                return;
            }

            WallpaperEntry entry = c_filelist.SelectedItem as WallpaperEntry;

            // try to set the background
            try
            {
                this.SetBackgroundImage(entry);
            }
            catch (Exception e)
            {
                ErrorLog.Log("Could not set the current image as the background, please choose a different image.", e);
            }

            this.EndTransaction();
        }

        /// <summary>
        /// Set the provided image as the background
        /// </summary>
        /// <param name="entry">the image entry</param>
        private void SetBackgroundImage(WallpaperEntry entry)
        {
            try
            {
                // try to download image to cache
                string localFile = this.GetLocalCopyOfImage(entry, false);
                if (localFile == null)
                {
                    throw new Exception("Could not get local copy of the image");
                }

                // set the new file as the desktop wallpaper
                BackgroundSetter.SetBackgroundImage(localFile, Settings.Instance.DisplayMode, Settings.Instance.GetBackgroundColor());
            }
            catch (Exception e)
            {
                throw new Exception("Setting image did not complete fully", e);
            }

            Settings.Instance.SelectedImage = entry.FilePath;
            this.trayIcon.SetToolText(entry.FilePath);
        }

        /// <summary>
        /// Switch to/from the presentation mode
        /// </summary>
        /// <param name="enable">whether to enable or disable the presentation mode</param>
        /// <exception cref="Exception">Can throw exception</exception>
        public void SwitchPresentationMode(bool enable)
        {
            if (string.IsNullOrEmpty(Settings.Instance.PresentationImage) ||
                Settings.Instance.InPresentationMode == enable)
            {
                // nothing to do if it is already set appropriately
                return;
            }

            try
            {
                string imageFile = enable ? Settings.Instance.PresentationImage : Settings.Instance.SelectedImage;
                if (string.IsNullOrEmpty(imageFile))
                {
                    throw new Exception("No image file found");
                }

                // download image to cache
                string localFile = this.GetLocalCopyOfImage(imageFile, false);
                if (localFile == null)
                {
                    throw new Exception("Could not get local copy of the image");
                }

                //set the new file as the desktop wallpaper
                BackgroundSetter.SetBackgroundImage(localFile, Settings.Instance.DisplayMode, Settings.Instance.GetBackgroundColor());
                this.trayIcon.SetToolText(imageFile);

                // update the UI elements
                this.UpdatePresentationMode(enable);
            }
            catch (Exception e)
            {
                ErrorLog.Log("Failed to switch presentation mode.", e);
            }
        }

        /// <summary>
        /// Start the auto change timer
        /// </summary>
        public void SetupAutoChangeTimer()
        {
            if (this.autoChangeTimer == null)
            {
                return;
            }

            try
            {
                this.autoChangeTimer.Stop();

                if (Settings.Instance.ChangeAutomatically)
                {
                    this.autoChangeTimer.Interval = (int)(Settings.Instance.TimeInterval * 60 * 1000);
                    this.autoChangeTimer.Tick += new EventHandler(this.DoAutoChange);
                    this.autoChangeTimer.Start();
                }
            }
            catch (Exception e)
            {
                throw new Exception("Timer setup failed", e);
            }
        }

        /// <summary>
        /// Quit the application
        /// </summary>
        public void ForceExitApp()
        {
            this.shouldExit = true;
            this.ExitOrHideApp(false);
        }

        /// <summary>
        /// Update the file list based on user selection
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Settings.Instance.FileListIndex = c_tabs.SelectedIndex;
            this.UpdateFileList();
        }

        /// <summary>
        /// Updates the UI based of the presentation mode option
        /// </summary>
        /// <param name="enable">whether presentation mode is being enabled or disabled</param>
        private void UpdatePresentationMode(bool enable)
        {
            // fix the enable state based on whether a presentation image is present or not
            bool imagePresent = !string.IsNullOrEmpty(Settings.Instance.PresentationImage);
            enable = enable && imagePresent;

            // store the state
            Settings.Instance.InPresentationMode = enable;

            // update the UI elements
            c_presentationimage.Text = Settings.Instance.PresentationImage;
            c_disablepresentation.IsEnabled = enable && imagePresent;
            c_enablepresentation.IsEnabled = !enable && imagePresent;
            c_clearpresentation.IsEnabled = !enable && imagePresent;

            c_setPresentation.IsEnabled = !enable;
            c_filelist.IsEnabled = !enable;

            c_imagenext.IsEnabled = !enable && (this.activeList.Count > 0);
            c_imageprevious.IsEnabled = !enable && (this.imageStack.Count > 1);
            c_imageset.IsEnabled = !enable && (c_filelist.SelectedItem as WallpaperEntry != null);

            // raise mode changed event
            if (this.PresentationModeChanged != null)
            {
                this.PresentationModeChanged(null, null);
            }
        }

        /// <summary>
        /// Close or hide the main window
        /// </summary>
        /// <param name="isClosing">whether this was called from the window closing event</param>
        /// <returns></returns>
        private bool ExitOrHideApp(bool isClosing)
        {
            bool exited = false;
            if (this.shouldExit)
            {
                exited = true;
                if (!isClosing)
                {
                    // if not from the closing event, hide the tray icon and close this window
                    this.trayIcon.UpdateTrayIcon(false);
                    this.Close();
                }
            }
            else
            {
                // simply minimize the main window and show the tray icon
                exited = false;
                this.trayIcon.UpdateTrayIcon(true);
                this.trayIcon.UpdateMainWindow(false);
            }

            return exited;
        }

        /// <summary>
        /// Called after the update is triggered by the user
        /// </summary>
        private void UpdateFired()
        {
            ForceExitApp();
        }

        /// <summary>
        /// windows key handler
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs args)
        {
            if (c_settings.IsVisible)
            {
                c_settings.ShowPanel(false);
                args.Handled = true;
            }
            else if (args.Key == System.Windows.Input.Key.Escape)
            {
                this.ExitOrHideApp(false);
                args.Handled = true;
            }
            else if (args.Key == System.Windows.Input.Key.N)
            {
                this.GotoNextImage(false);
                args.Handled = true;
            }
            else if (args.Key == System.Windows.Input.Key.P)
            {
                this.GotoPreviousImage(false);
                args.Handled = true;
            }
            else if (args.Key == System.Windows.Input.Key.S)
            {
                this.SetCurrentImageAsBackground();
                args.Handled = true;
            }
        }

        /// <summary>
        /// windows state change handler
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void Window_StateChanged(object sender, EventArgs args)
        {
            if (this.WindowState == WindowState.Minimized)
            {
                this.ExitOrHideApp(false);
            }
        }

        /// <summary>
        /// windows closing handler
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void Window_Closing(object sender, CancelEventArgs args)
        {
            if (args != null)
            {
                bool exited = this.ExitOrHideApp(true);
                args.Cancel = !exited;
            }
        }

        /// <summary>
        /// Handler for exit button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void Exit_Click(object sender, RoutedEventArgs args)
        {
            this.ForceExitApp();
        }

        /// <summary>
        /// Handler for settings button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void Settings_Click(object sender, RoutedEventArgs args)
        {
            c_settings.ShowPanel(true);
        }

        /// <summary>
        /// Handler for add directory button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void AddDirectory_Click(object sender, RoutedEventArgs args)
        {
            if (this.BeginTransaction())
            {
                return;
            }

            FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
            folderBrowserDialog.ShowNewFolderButton = true;
            folderBrowserDialog.Description = "Select the directory that you want to add.";

            System.Windows.Forms.DialogResult result = folderBrowserDialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                this.activeList.AddDirectory(folderBrowserDialog.SelectedPath);
                if (this.activeList.Count > 0)
                {
                    c_imagenext.IsEnabled = true;
                }
                else
                {
                    c_imagenext.IsEnabled = false;
                }

                try
                {
                    this.activeList.SaveToFile();
                }
                catch (Exception ex)
                {
                    ErrorLog.Log("Failed to the save the list file, it will continue to be updated in memory", ex);
                }
            }

            this.EndTransaction();
        }

        /// <summary>
        /// Handler for add file button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void AddFile_Click(object sender, RoutedEventArgs args)
        {
            if (this.BeginTransaction())
            {
                return;
            }

            OpenFileDialog fileDialog = new OpenFileDialog();
            fileDialog.CheckFileExists = true;
            fileDialog.CheckPathExists = true;
            fileDialog.Multiselect = true;
            fileDialog.Title = "Select image files";
            fileDialog.Filter = "Image Files(*.BMP;*.JPG;*.GIF;*.PNG)|*.BMP;*.JPG;*.GIF;*.PNG";
            fileDialog.FilterIndex = 1;
            fileDialog.RestoreDirectory = true;

            System.Windows.Forms.DialogResult result = fileDialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                foreach (string file in fileDialog.FileNames)
                {
                    this.activeList.AddFile(file);
                }

                if (this.activeList.Count > 0)
                {
                    c_imagenext.IsEnabled = true;
                }
                else
                {
                    c_imagenext.IsEnabled = false;
                }

                try
                {
                    this.activeList.SaveToFile();
                }
                catch (Exception ex)
                {
                    ErrorLog.Log("Failed to the save the list file, it will continue to be updated in memory", ex);
                }
            }

            this.EndTransaction();
        }

        /// <summary>
        /// Handler for remove file button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void RemoveFile_Click(object sender, RoutedEventArgs args)
        {
            if (this.BeginTransaction())
            {
                return;
            }

            foreach (Object item in c_filelist.SelectedItems)
            {
                this.activeList.RemoveFile(item.ToString(), false);
            }

            if (this.activeList.Count > 0)
            {
                c_imagenext.IsEnabled = true;
            }
            else
            {
                c_imagenext.IsEnabled = false;
            }

            try
            {
                this.activeList.SaveToFile();
            }
            catch (Exception ex)
            {
                ErrorLog.Log("Failed to the save the list file, it will continue to be updated in memory", ex);
            }

            this.EndTransaction();
        }

        /// <summary>
        /// Handler for remove all button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void RemoveAll_Click(object sender, RoutedEventArgs args)
        {
            if (this.BeginTransaction())
            {
                return;
            }

            this.activeList.RemoveAll();

            if (this.activeList.Count > 0)
            {
                c_imagenext.IsEnabled = true;
            }
            else
            {
                c_imagenext.IsEnabled = false;
            }

            try
            {
                this.activeList.SaveToFile();
            }
            catch (Exception ex)
            {
                ErrorLog.Log("Failed to the save the list file, it will continue to be updated in memory", ex);
            }

            this.EndTransaction();
        }

        /// <summary>
        /// Handler for refresh button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void Refresh_Click(object sender, RoutedEventArgs args)
        {
            if (this.BeginTransaction())
            {
                return;
            }

            try
            {
                if (Directory.Exists(Settings.OnlineDatabaseFolder))
                {
                    string[] files = Directory.GetFiles(Settings.OnlineDatabaseFolder);
                    foreach (string file in files)
                    {
                        File.Delete(file);
                    }

                    Directory.Delete(Settings.OnlineDatabaseFolder);
                }

                if (File.Exists(Settings.OnlineDatabaseLog))
                {
                    File.Delete(Settings.OnlineDatabaseLog);
                }

                if (File.Exists(Settings.OnlineDatabaseZip))
                {
                    File.Delete(Settings.OnlineDatabaseZip);
                }

                if (File.Exists(Settings.FileList_Online))
                {
                    File.Delete(Settings.FileList_Online);
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Log("Could not cleanup old stuff, will try to rebuild anyway.", ex);
            }

            this.activeList = null;
            this.UpdateFileList();

            this.EndTransaction();
        }

        /// <summary>
        /// Event handler for selection change in list
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void FileList_Selected(object sender, SelectionChangedEventArgs args)
        {
            // re-entrant check
            if (this.inUpdatingMode)
            {
                return;
            }

            this.GotoCurrentImage();
        }

        /// <summary>
        /// Parse the online repository and create athe online file list
        /// </summary>
        private void BuildOnlineFile()
        {
            if (File.Exists(Settings.FileList_Online))
            {
                return;
            }

            WallpaperList tmpList = null;

            try
            {
                tmpList = new WallpaperList(Settings.FileList_Online, false);

                string[] files = Directory.GetFiles(Settings.OnlineDatabaseFolder);
                foreach (string file in files)
                {
                    using (Stream fileStream = File.Open(file, FileMode.Open))
                    {
                        using (StreamReader fileReader = new StreamReader(fileStream))
                        {
                            while (true)
                            {
                                string line = fileReader.ReadLine();
                                if (line == null)
                                {
                                    break;
                                }

                                if (line.StartsWith("#"))
                                {
                                    continue;
                                }

                                string filePath = null;
                                int firstDelimiterIndex = line.IndexOf('|');
                                if (firstDelimiterIndex != -1)
                                {
                                    filePath = line.Substring(0, firstDelimiterIndex);
                                }

                                if (string.IsNullOrEmpty(filePath))
                                {
                                    continue;
                                }

                                string dimensions = null;
                                int secondDelimiterIndex = line.IndexOf('^', firstDelimiterIndex);
                                if (secondDelimiterIndex != -1)
                                {
                                    dimensions = line.Substring(firstDelimiterIndex + 1, secondDelimiterIndex - firstDelimiterIndex - 1);
                                }

                                string thumbnail = null;
                                if ((secondDelimiterIndex < line.Length))
                                {
                                    thumbnail = line.Substring(secondDelimiterIndex + 1);
                                }

                                if (string.IsNullOrEmpty(thumbnail))
                                {
                                    thumbnail = filePath;
                                }

                                tmpList.AddFile(filePath, thumbnail, dimensions);
                            }
                        }
                    }
                }
            }
            finally
            {
                if (tmpList != null)
                {
                    tmpList.SaveToFile();
                }
            }
        }

        /// <summary>
        /// Gets the path of a local copy of the file.
        /// If the file is online, it downloads to a cache and returns path of the local copy
        /// </summary>
        /// <param name="entry">entry of the file to lookup</param>
        /// <param name="isThumbnail">whether this is a thumbnail image</param>
        /// <returns>Path of the local copy</returns>
        /// <exception cref="Exception">Can throw exception</exception>
        private string GetLocalCopyOfImage(WallpaperEntry entry, bool isThumbnail)
        {
            if (isThumbnail && Settings.Instance.HighResolutionThumbs)
            {
                isThumbnail = false;
            }

            return GetLocalCopyOfImage(isThumbnail ? entry.ThumbnailPath : entry.FilePath, isThumbnail);
        }

        /// <summary>
        /// Gets the path of a local copy of the file.
        /// If the file is online, it downloads to a cache and returns path of the local copy
        /// </summary>
        /// <param name="filePath">path of the file to lookup</param>
        /// <param name="isThumbnail">whether this is a thumbnail image</param>
        /// <returns>Path of the local copy</returns>
        /// <exception cref="Exception">Can throw exception</exception>
        private string GetLocalCopyOfImage(string filePath, bool isThumbnail)
        {
            Uri fileUri = new Uri(filePath);
            if (fileUri.IsFile)
            {
                return filePath;
            }

            string dstFolder = isThumbnail ? Settings.ThumbnailCacheFolder : Settings.WallpaperCacheFolder;
            int cacheSize = isThumbnail ? 1000 : 100;
            string fileName = dstFolder + "\\" + Path.GetFileName(filePath);

            if (File.Exists(fileName))
            {
                return fileName;
            }

            // cleanup cache
            try
            {
                if (Directory.Exists(dstFolder))
                {
                    DirectoryInfo dir = new DirectoryInfo(dstFolder);
                    foreach (FileInfo file in dir.GetFiles().OrderByDescending(x => x.LastWriteTime).Skip(cacheSize))
                    {
                        file.Delete();
                    }
                }
            }
            catch (Exception)
            {
                //nothing to do here
            }

            try
            {
                if (!Directory.Exists(dstFolder))
                {
                    Directory.CreateDirectory(dstFolder);
                }

                using (HttpClientHandler handler = new HttpClientHandler())
                {
                    using (HttpClient client = new HttpClient(handler))
                    {
                        client.Timeout = TimeSpan.FromSeconds(3);
                        using (HttpResponseMessage response = client.GetAsync(filePath).Result)
                        {
                            if (response.IsSuccessStatusCode)
                            {
                                using (Stream contentStream = response.Content.ReadAsStreamAsync().Result)
                                {
                                    using (Stream fileStream = File.Open(fileName, FileMode.OpenOrCreate))
                                    {
                                        contentStream.CopyTo(fileStream);
                                        return fileName;
                                    }
                                }
                            }
                            else
                            {
                                throw new Exception("Failure status code - " + response.StatusCode.ToString());
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to download image file from " + filePath, ex);
            }
        }

        /// <summary>
        /// Handler for copy path context menu item
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void FileList_CopyPath(object sender, RoutedEventArgs args)
        {
            if (c_filelist.SelectedItem == null)
            {
                return;
            }

            try
            {
                WallpaperEntry item = c_filelist.SelectedItem as WallpaperEntry;
                System.Windows.Clipboard.SetText(item.FilePath);
            }
            catch (Exception e)
            {
                ErrorLog.Log("Copy to clipboard failed.", e);
            }
        }

        /// <summary>
        /// Handler for open path context menu item
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void FileList_OpenPath(object sender, RoutedEventArgs args)
        {
            if (c_filelist.SelectedItem == null)
            {
                return;
            }

            try
            {
                WallpaperEntry item = c_filelist.SelectedItem as WallpaperEntry;
                Process.Start("explorer.exe", "/select, " + item.FilePath);
            }
            catch (Exception e)
            {
                ErrorLog.Log("Open folder failed.", e);
            }
        }

        /// <summary>
        /// Handler for open context menu item
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void FileList_OpenFile(object sender, RoutedEventArgs args)
        {
            if (c_filelist.SelectedItem == null)
            {
                return;
            }

            try
            {
                WallpaperEntry item = c_filelist.SelectedItem as WallpaperEntry;
                Process.Start("explorer.exe", item.FilePath);
            }
            catch (Exception e)
            {
                ErrorLog.Log("Open file failed.", e);
            }
        }

        /// <summary>
        /// Handler for delete context menu item
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void FileList_DeleteFile(object sender, RoutedEventArgs args)
        {
            WallpaperEntry item = c_filelist.SelectedItem as WallpaperEntry;
            if (c_filelist.SelectedItem == null)
            {
                return;
            }

            this.activeList.RemoveFile(item.FilePath, true);

            if (this.activeList.Count > 0)
            {
                c_imagenext.IsEnabled = true;
            }
            else
            {
                c_imagenext.IsEnabled = false;
            }

            try
            {
                this.activeList.SaveToFile();
            }
            catch (Exception ex)
            {
                ErrorLog.Log("Failed to the save the list file, it will continue to be updated in memory", ex);
            }
        }

        /// <summary>
        /// Handler for next image button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void ImageNext_Click(object sender, RoutedEventArgs args)
        {
            this.GotoNextImage(false);
        }

        /// <summary>
        /// Handler for previous image button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void ImagePrevious_Click(object sender, RoutedEventArgs args)
        {
            this.GotoPreviousImage(false);
        }

        /// <summary>
        /// Handler for set image button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void ImageSet_Click(object sender, RoutedEventArgs args)
        {
            this.SetCurrentImageAsBackground();
        }

        /// <summary>
        /// Handler for set presentation button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void PresentationSet_Click(object sender, RoutedEventArgs args)
        {
            if (Settings.Instance.InPresentationMode)
            {
                return;
            }

            WallpaperEntry item = c_filelist.SelectedItem as WallpaperEntry;
            if (item != null)
            {
                Settings.Instance.PresentationImage = item.FilePath;
                this.UpdatePresentationMode(false);
            }
        }

        /// <summary>
        /// Handler for enable presentation button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void EnablePresentation_Click(object sender, RoutedEventArgs args)
        {
            this.SwitchPresentationMode(true);
        }

        /// <summary>
        /// Handler for disable presentation button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void DisablePresentation_Click(object sender, RoutedEventArgs args)
        {
            this.SwitchPresentationMode(false);
        }

        /// <summary>
        /// Handler for clear presentation button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void ClearPresentation_Click(object sender, RoutedEventArgs args)
        {
            if (Settings.Instance.InPresentationMode)
            {
                return;
            }

            Settings.Instance.PresentationImage = string.Empty;
            this.UpdatePresentationMode(false);
        }

        /// <summary>
        /// Handler for the auto change timeout
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DoAutoChange(object sender, EventArgs e)
        {
            this.GotoNextImage(true);
        }

        /// <summary>
        /// Method to start a lengthy process
        /// </summary>
        /// <returns>true if not to enter following code block, false otherwise</returns>
        private bool BeginTransaction()
        {
            // re-entrant protection
            if (this.inUpdatingMode)
            {
                return true;
            }

            this.inUpdatingMode = true;
            this.Cursor = System.Windows.Input.Cursors.Wait;
            return false;
        }

        /// <summary>
        /// Method to end a lengthy process
        /// </summary>
        private void EndTransaction()
        {
            this.Cursor = System.Windows.Input.Cursors.Arrow;
            this.inUpdatingMode = false;
        }
    }
}
