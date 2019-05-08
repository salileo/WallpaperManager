using System;
using System.Windows;

namespace WallpaperManager
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            try
            {
                System.IO.Directory.CreateDirectory(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData) + "\\WallpaperManager");
            }
            catch (Exception e)
            {
                System.Windows.MessageBox.Show("Settings folder creation failed.\n\nError:\n" + e.ToString());
            } 
        }
    }
}
