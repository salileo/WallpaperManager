using System;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Resources;

namespace WallpaperManager
{
    class TrayIcon
    {
        private MainWindow parentWindow;
        private NotifyIcon trayIcon;

        private MenuItem openWindowMenuItem;
        private MenuItem changeWallpaperMenuItem;
        private MenuItem enablePPTMenuItem;
        private MenuItem disablePPTMenuItem;
        private MenuItem exitApplicationMenuItem;

        public TrayIcon(MainWindow parentWindow)
        {
            //create the tray icon and set the icon file
            this.trayIcon = new NotifyIcon();

            StreamResourceInfo info = App.GetResourceStream(new Uri("/Icons/main.ico", UriKind.Relative));
            this.trayIcon.Icon = new Icon(info.Stream);
            this.trayIcon.Visible = false;
            this.trayIcon.Text = "Wallpaper Manager";
            this.trayIcon.BalloonTipTitle = "Wallpaper Manager";
            this.trayIcon.BalloonTipText = "Wallpaper Manager";

            //set the mouse handlers
            this.trayIcon.DoubleClick += new System.EventHandler(this.ChangeWallpaper);

            //setup the context menu
            ContextMenu menu = new ContextMenu();
            this.trayIcon.ContextMenu = menu;

            this.openWindowMenuItem = new MenuItem("Open main window");
            this.openWindowMenuItem.Click += new EventHandler(this.OpenMainWindow);
            menu.MenuItems.Add(this.openWindowMenuItem);

            this.changeWallpaperMenuItem = new MenuItem("Change wallpaper");
            this.changeWallpaperMenuItem.Click += new EventHandler(this.ChangeWallpaper);
            menu.MenuItems.Add(this.changeWallpaperMenuItem);

            this.enablePPTMenuItem = new MenuItem("Enable presentation");
            this.enablePPTMenuItem.Click += new EventHandler(this.EnablePresentation);
            menu.MenuItems.Add(this.enablePPTMenuItem);

            this.disablePPTMenuItem = new MenuItem("Disable presentation");
            this.disablePPTMenuItem.Click += new EventHandler(this.DisablePresentation);
            menu.MenuItems.Add(this.disablePPTMenuItem);

            this.exitApplicationMenuItem = new MenuItem("Exit Application");
            this.exitApplicationMenuItem.Click += new EventHandler(this.ExitApplication);
            menu.MenuItems.Add(this.exitApplicationMenuItem);

            //set the parent in the end after all initialization has succeeded
            this.parentWindow = parentWindow;
            this.parentWindow.PresentationModeChanged += ParentWindow_ModeChanged;

            this.UpdateBasedOnPresentationMode();
        }

        /// <summary>
        /// Show/Hide the main app window
        /// </summary>
        public void UpdateMainWindow(bool show)
        {
            if (show)
            {
                this.parentWindow.Show();
                this.parentWindow.WindowState = WindowState.Normal;
                this.openWindowMenuItem.Visible = false;

                //bug: item not selected if application starts minimized
                //m_parent.c_filelist.ScrollIntoView(m_parent.c_filelist.SelectedItem);
            }
            else
            {
                this.parentWindow.Hide();
                this.openWindowMenuItem.Visible = true;
            }
        }

        /// <summary>
        /// Show/Hide the icon in tray
        /// </summary>
        public void UpdateTrayIcon(bool show)
        {
            this.trayIcon.Visible = show;
        }

        /// <summary>
        /// Update the context menu based on the presentation mode
        /// </summary>
        private void UpdateBasedOnPresentationMode()
        {
            bool enabled = Settings.Instance.InPresentationMode;
            bool imagePresent = !string.IsNullOrEmpty(Settings.Instance.PresentationImage);

            this.enablePPTMenuItem.Visible = !enabled && imagePresent;
            this.disablePPTMenuItem.Visible = enabled && imagePresent;
            
            this.changeWallpaperMenuItem.Visible = !enabled;
        }

        /// <summary>
        /// Sets the tool tip text
        /// </summary>
        public void SetToolText(string text)
        {
            string tooltip_text;

            text = Path.GetFileName(text);
            if (text.Length > 0)
            {
                tooltip_text = "Wallpaper: " + text;

                // if the length is too long, try to just use the file name
                if (tooltip_text.Length >= 64)
                {
                    tooltip_text = text;
                }

                // if the length is still too long, truncate the file name
                if (tooltip_text.Length >= 64)
                {
                    tooltip_text = text.Substring(0, 60) + "...";
                }
            }
            else
            {
                tooltip_text = "Wallpaper Manager";
            }

            this.trayIcon.Text = tooltip_text;
            this.trayIcon.BalloonTipText = text;

            if (this.trayIcon.Visible && !string.IsNullOrEmpty(text))
            {
                this.trayIcon.ShowBalloonTip(10);
            }
        }

        /// <summary>
        /// Set the tool tip text to the loading state
        /// </summary>
        public void SetLoadingText()
        {
            string text = "Loading next image ...";
            this.trayIcon.Text = text;
            this.trayIcon.BalloonTipText = text;

            if (this.trayIcon.Visible)
            {
                this.trayIcon.ShowBalloonTip(60);
            }
        }

        /// <summary>
        /// Handler for "enable presentation" menu item
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void EnablePresentation(object sender, EventArgs args)
        {
            this.parentWindow.SwitchPresentationMode(true);
        }

        /// <summary>
        /// Handler for "disable presentation" menu item
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void DisablePresentation(object sender, EventArgs args)
        {
            this.parentWindow.SwitchPresentationMode(false);
        }

        /// <summary>
        /// Handler for "change wallpaper" menu item
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void ChangeWallpaper(object sender, EventArgs args)
        {
            this.parentWindow.GotoNextImage(true);
        }

        /// <summary>
        /// Handler for "open main window" menu item
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void OpenMainWindow(object sender, EventArgs args)
        {
            this.UpdateMainWindow(true);
        }

        /// <summary>
        /// Handler for "exit application" menu item
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void ExitApplication(object sender, EventArgs args)
        {
            this.parentWindow.ForceExitApp();
        }

        /// <summary>
        /// Handler for action to take when presentation mode is changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ParentWindow_ModeChanged(object sender, EventArgs e)
        {
            this.UpdateBasedOnPresentationMode();
        }
    }
}
