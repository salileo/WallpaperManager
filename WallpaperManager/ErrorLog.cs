using System;
using System.Windows;

namespace WallpaperManager
{
    class ErrorLog
    {
        public static void Log(string message, Exception exp)
        {
#if DEBUG
            MessageBox.Show(message + "\n\nIssue :\n" + ((exp == null) ? "" : exp.ToString()));
#else
            MessageBox.Show(message);
#endif
        }
    }
}
