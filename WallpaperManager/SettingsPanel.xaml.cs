using System;
using System.Windows;
using System.Windows.Controls;

namespace WallpaperManager
{
    public partial class SettingsPanel : UserControl
    {
        /// <summary>
        /// Parent window
        /// </summary>
        private MainWindow parentWindow;

        /// <summary>
        /// Indicates if the code is already processing settings
        /// </summary>
        private bool processingSettings;

        /// <summary>
        /// Constructor for the settings panel
        /// </summary>
        public SettingsPanel()
        {
            InitializeComponent();

            // set the display mode options
            foreach (DisplayModeType types in (DisplayModeType[])Enum.GetValues(typeof(DisplayModeType)))
            {
                ComboBoxItem item = new ComboBoxItem();
                item.Content = types.ToString();
                c_displayMode.Items.Add(item);
            }

            //set the color options
            c_backgroundcolor.ItemsSource = Settings.Instance.ColorValues;

            //set title
            c_productversion.Text = "Wallpaper Manager " + MainWindow.Version;

            this.parentWindow = Application.Current.MainWindow as MainWindow;
            this.processingSettings = false;

            //sync the settings with the UI
            DataToUI();
        }

        /// <summary>
        /// Display the settings panel
        /// </summary>
        /// <param name="show"></param>
        public void ShowPanel(bool show)
        {
            if (show)
            {
                this.parentWindow.c_settings.Visibility = System.Windows.Visibility.Visible;
                this.parentWindow.c_settingsBackground.Visibility = System.Windows.Visibility.Visible;
                this.parentWindow.c_maincontents.IsEnabled = false;

                //Storyboard story = this.TryFindResource("SettingsPanel") as Storyboard;
                //story.AutoReverse = false;
                //story.Begin();

                c_displayMode.Focus();
            }
            else
            {
                this.parentWindow.c_settings.Visibility = System.Windows.Visibility.Collapsed;
                this.parentWindow.c_settingsBackground.Visibility = System.Windows.Visibility.Collapsed;
                this.parentWindow.c_maincontents.IsEnabled = true;

                //Storyboard story = this.TryFindResource("SettingsPanel") as Storyboard;
                //story.AutoReverse = true;
                //story.Begin();
                //story.Seek(new TimeSpan(0, 0, 0), TimeSeekOrigin.Duration);

                this.parentWindow.c_filelist.Focus();
            }
        }

        /// <summary>
        /// Handle close button click
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void Close_Click(object sender, RoutedEventArgs args)
        {
            //hide the settings panel
            ShowPanel(false);
        }

        /// <summary>
        /// Handle key board events
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void UserControl_KeyDown(object sender, System.Windows.Input.KeyEventArgs args)
        {
            if (args.Key == System.Windows.Input.Key.Escape)
            {
                Close_Click(null, null);
                args.Handled = true;
            }
        }

        /// <summary>
        /// Get index of the chosen color value
        /// </summary>
        /// <param name="searchString">the color value</param>
        /// <returns>index of the color, else -1 if not found</returns>
        private int GetIndexOfColor(string searchString)
        {
            searchString = searchString.ToLower();

            foreach (ColorValue color in Settings.Instance.ColorValues)
            {
                if (color.Name.ToLower() == searchString)
                {
                    return Settings.Instance.ColorValues.IndexOf(color);
                }
            }

            return -1;
        }

        /// <summary>
        /// Map the properties to the UI element values
        /// </summary>
        private void DataToUI()
        {
            if (this.processingSettings)
            {
                //if we are already processing, bail out to avoid re-entrance issues.
                return;
            }

            this.processingSettings = true;

            c_displayMode.SelectedIndex = (int)Settings.Instance.DisplayMode;
            c_backgroundcolor.SelectedIndex = this.GetIndexOfColor(Settings.Instance.BackgroundColor);
            c_startwithwindows.IsChecked = Settings.Instance.StartWithWindows;
            c_startminimized.IsChecked = Settings.Instance.StartMinimized;
            c_changeatstartup.IsChecked = Settings.Instance.ChangeAtStartup;
            c_changerandomly.IsChecked = Settings.Instance.ChangeRandomly;
            c_highresolutionthumbs.IsChecked = Settings.Instance.HighResolutionThumbs;
            c_changeautomatically.IsChecked = Settings.Instance.ChangeAutomatically;
            c_timeinterval.Text = Settings.Instance.TimeInterval.ToString();
            c_timeinterval.IsEnabled = Settings.Instance.ChangeAutomatically;

            // update the auto change timer 
            this.parentWindow.SetupAutoChangeTimer();

            /*
            if (m_backgroundColor.ToLower() == "white")
                parent.c_previewimagebackground.Background = System.Windows.Media.Brushes.White;
            else
                parent.c_previewimagebackground.Background = System.Windows.Media.Brushes.Black;

            if (m_displayMode.ToLower() == "fill")
                parent.c_previewimage.Stretch = Stretch.Fill;
            else
                parent.c_previewimage.Stretch = Stretch.Uniform;
            */

            this.processingSettings = false;
        }

        /// <summary>
        /// Map the values in the UI elements to the actual properties
        /// </summary>
        private void UIToData()
        {
            if (this.processingSettings)
            {
                //if we are already processing, bail out to avoid re-entrance issues.
                return;
            }

            this.processingSettings = true;

            try
            {
                ComboBoxItem item = c_displayMode.SelectedItem as ComboBoxItem;
                Settings.Instance.DisplayMode = (DisplayModeType)Enum.Parse(typeof(DisplayModeType), item.Content.ToString());

                ColorValue color = c_backgroundcolor.SelectedItem as ColorValue;
                Settings.Instance.BackgroundColor = color.Name;

                Settings.Instance.StartWithWindows = (c_startwithwindows.IsChecked == true);
                Settings.Instance.StartMinimized = (c_startminimized.IsChecked == true);
                Settings.Instance.ChangeAtStartup = (c_changeatstartup.IsChecked == true);
                Settings.Instance.ChangeRandomly = (c_changerandomly.IsChecked == true);
                Settings.Instance.HighResolutionThumbs = (c_highresolutionthumbs.IsChecked == true);
                Settings.Instance.ChangeAutomatically = (c_changeautomatically.IsChecked == true);

                if (string.IsNullOrEmpty(c_timeinterval.Text))
                {
                    Settings.Instance.TimeInterval = 1;
                }
                else
                {
                    try
                    {
                        uint value = uint.Parse(c_timeinterval.Text);
                        if (Settings.Instance.TimeInterval == 0)
                        {
                            Settings.Instance.TimeInterval = 1;
                        }
                        else
                        {
                            uint max = (int.MaxValue / 60) / 1000;
                            Settings.Instance.TimeInterval = Math.Min(value, max);
                        }
                    }
                    catch (SystemException)
                    {
                        Settings.Instance.TimeInterval = 1;
                    }
                }
            }
            catch (Exception e)
            {
                ErrorLog.Log("Failed to sync settings with file.", e);
            }
            finally
            {
                this.processingSettings = false;
                DataToUI();
            }
        }

        /// <summary>
        /// Event raised whenever there is a change in the settings in the UI
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void Settings_Changed(object sender, RoutedEventArgs args)
        {
            UIToData();
        }
    }
}
