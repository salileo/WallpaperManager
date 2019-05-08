using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Windows;
using System.Diagnostics;
using System.ComponentModel;

namespace GenericUpdater
{
    public class Updater
    {
        public delegate void UpdateFiredDelegate();
        private class UpdaterInfo
        {
            public string productName;
            public string currentVersion;
            public string versionFile;
            public string installerFile;
            public UpdateFiredDelegate callback;
            public string result;
        }

        private static string baseSiteAddress = "http://sites.google.com/site/salilsoftware/";

        public static void DoUpdate(string productName, string currentVersion, string versionFile, string installerFile, UpdateFiredDelegate callback)
        {
            BackgroundWorker worker = new BackgroundWorker();
            worker.WorkerReportsProgress = false;
            worker.WorkerSupportsCancellation = false;
            worker.DoWork += new DoWorkEventHandler(worker_DoWork);
            worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(worker_RunWorkerCompleted);

            UpdaterInfo info = new UpdaterInfo();
            info.productName = productName;
            info.currentVersion = currentVersion;
            info.versionFile = versionFile;
            info.installerFile = installerFile;
            info.callback = callback;

            worker.RunWorkerAsync(info);
        }

        static void worker_DoWork(object sender, DoWorkEventArgs e)
        {
            UpdaterInfo info = e.Argument as UpdaterInfo;

            string url = baseSiteAddress + info.versionFile;
            string availableVersion = string.Empty;

            using (XmlReader reader = XmlReader.Create(url))
            {
                reader.ReadToFollowing("Version");
                availableVersion = reader.GetAttribute("Available");
                reader.Close();
            }

            info.result = availableVersion;
            e.Result = info;
        }

        static void worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                //an exception happened. ignore the update
            }
            else
            {
                UpdaterInfo info = e.Result as UpdaterInfo;
                if (info.result != info.currentVersion)
                {
                    string msg = "An update of " + info.productName + " is available online. Do you want to install the update?";
                    MessageBoxResult result = MessageBox.Show(msg, "Updater", MessageBoxButton.YesNo, MessageBoxImage.Information);
                    if (result == MessageBoxResult.Yes)
                    {
                        string installer = baseSiteAddress + info.installerFile;
                        try
                        {
                            Process.Start("explorer.exe", installer);
                            info.callback();
                        }
                        catch (Exception)
                        {
                        }
                    }
                }
            }
        }
    }
}
