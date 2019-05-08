using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text;

namespace WallpaperManager
{
    class OnlineRepository
    {
        private Random rand = new Random();

        /// <summary>
        /// Sync the online repository to the local file system
        /// </summary>
        /// <returns>true if the local copies were updated, false otherwise</returns>
        /// <exception cref="Exception">Can throw exception</exception>
        public bool SyncToLocal()
        {
            try
            {
                using (HttpClientHandler handler = new HttpClientHandler())
                {
                    using (HttpClient client = new HttpClient(handler))
                    {
                        string url = string.Format("http://www.myportablesoftware.com/data/mdwallpaper-data.txt?SID={0:D6}", this.rand.Next(100000, 1000000));

                        using (HttpResponseMessage response = client.GetAsync(url).Result)
                        {
                            if (response.IsSuccessStatusCode)
                            {
                                using (Stream contentStream = response.Content.ReadAsStreamAsync().Result)
                                {
                                    using (StreamReader streamReader = new StreamReader(contentStream))
                                    {
                                        string content = streamReader.ReadToEnd();
                                        using (MemoryStream memStream = new MemoryStream(Encoding.UTF8.GetBytes(content)))
                                        {
                                            using (StreamReader memReader = new StreamReader(memStream))
                                            {
                                                DateTime lastDatabaseUpdate;
                                                DateTime lastCategoriesUpdate;
                                                string[] dataDownloadURLs;
                                                bool success = this.ProcessDataResponse(memReader, out lastDatabaseUpdate, out lastCategoriesUpdate, out dataDownloadURLs);
                                                if (success)
                                                {
                                                    this.DownloadCategoriesIfRequired(client, lastCategoriesUpdate);
                                                    if (this.DownloadDataIfRequired(client, lastDatabaseUpdate, dataDownloadURLs))
                                                    {
                                                        return true;
                                                    }
                                                }
                                                else
                                                {
                                                    throw new HttpRequestException("Could not extract data info", response.StatusCode, content);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                throw new HttpRequestException("Data request failed", response.StatusCode);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                throw new Exception("Failed to sync online database", e);
            }

            return false;
        }

        /// <summary>
        /// Parses the initial database status response
        /// </summary>
        /// <param name="reader">the stream reader which is serving the data</param>
        /// <param name="lastDatabaseUpdate">returns the last online database update time</param>
        /// <param name="lastCategoriesUpdate">return the last online categories update time</param>
        /// <param name="dataDownloadURLs">returns the download URLs</param>
        /// <returns>true if the processing was successful, false otherwise</returns>
        private bool ProcessDataResponse(StreamReader reader, out DateTime lastDatabaseUpdate, out DateTime lastCategoriesUpdate, out string[] dataDownloadURLs)
        {
            lastDatabaseUpdate = DateTime.MinValue;
            lastCategoriesUpdate = DateTime.MinValue;
            List<string> downloadURLs = new List<string>();

            while (true)
            {
                string line = reader.ReadLine();
                if (line == null)
                {
                    break;
                }

                string[] properties = line.Split(new char[] {'#'}, StringSplitOptions.RemoveEmptyEntries);
                foreach (string property in properties)
                {
                    string prop = property.Trim();
                    if (string.IsNullOrEmpty(prop))
                    {
                        continue;
                    }

                    int index = prop.IndexOf(':');
                    if ((index != -1) && ((index + 1) < prop.Length))
                    {
                        string name = prop.Substring(0, index).ToLower();
                        string value = prop.Substring(index + 1).ToLower();
                        
                        switch(name)
                        {
                            case "last-upd":
                                DateTime tmpDatabaseUpdate;
                                if (DateTime.TryParse(value, out tmpDatabaseUpdate))
                                {
                                    lastDatabaseUpdate = tmpDatabaseUpdate;
                                }
                                break;

                            case "categories.last-upd":
                                DateTime tmpCategoriesUpdate;
                                if (DateTime.TryParse(value, out tmpCategoriesUpdate))
                                {
                                    lastCategoriesUpdate = tmpCategoriesUpdate;
                                }
                                break;

                            case "mirror.url":
                                downloadURLs.Add(value);
                                break;

                            default:
                                break;
                        }
                    }
                }
            }

            dataDownloadURLs = downloadURLs.ToArray();

            bool success =
                lastDatabaseUpdate != DateTime.MinValue &&
                lastCategoriesUpdate != DateTime.MinValue &&
                dataDownloadURLs.Length > 0;

            return success;
        }

        /// <summary>
        /// downloads the categories if need to update
        /// </summary>
        /// <param name="client">the HTTP client to use</param>
        /// <param name="lastUpdate">the last online update time</param>
        /// <exception cref="Exception">Can throw exception</exception>
        private void DownloadCategoriesIfRequired(HttpClient client, DateTime lastUpdate)
        {
            string url = string.Format("http://www.myportablesoftware.com/data/mdwallpaper-categories.txt?SID={0:D6}", this.rand.Next(100000, 1000000));

            using (HttpResponseMessage response = client.GetAsync(url).Result)
            {
                if (response.IsSuccessStatusCode)
                {
                    using (Stream contentStream = response.Content.ReadAsStreamAsync().Result)
                    {
                    }
                }
                else
                {
                    throw new HttpRequestException("Category request failed", response.StatusCode);
                }
            }
        }

        /// <summary>
        /// downloads the wallpaper list if need to update
        /// </summary>
        /// <param name="client">the HTTP client to use</param>
        /// <param name="lastUpdate">the last online update time</param>
        /// <param name="urls">the source URLs</param>
        /// <returns>true if fresh data was downloaded, false otherwise</returns>
        /// <exception cref="Exception">Can throw exception</exception>
        private bool DownloadDataIfRequired(HttpClient client, DateTime lastUpdate, string[] urls)
        {
            if (Directory.Exists(Settings.OnlineDatabaseFolder) &&
                File.Exists(Settings.OnlineDatabaseLog))
            {
                string str = File.ReadAllText(Settings.OnlineDatabaseLog);
                DateTime lastDownloaded;
                if (DateTime.TryParse(str, out lastDownloaded))
                {
                    if (lastDownloaded >= lastUpdate)
                    {
                        return false;
                    }
                }
            }

            Dictionary<string, Exception> errors = new Dictionary<string, Exception>();
            bool success = false;
            int index = 0;
            foreach (string url in urls)
            {
                index++;
                try
                {
                    using (HttpResponseMessage response = client.GetAsync(url).Result)
                    {
                        if (response.IsSuccessStatusCode)
                        {
                            using (Stream contentStream = response.Content.ReadAsStreamAsync().Result)
                            {
                                using (Stream fileStream = File.Open(Settings.OnlineDatabaseZip, FileMode.OpenOrCreate))
                                {
                                    contentStream.CopyTo(fileStream);
                                }
                            }

                            if (Directory.Exists(Settings.OnlineDatabaseFolder))
                            {
                                string[] files = Directory.GetFiles(Settings.OnlineDatabaseFolder);
                                foreach (string file in files)
                                {
                                    File.Delete(file);
                                }

                                Directory.Delete(Settings.OnlineDatabaseFolder);
                            }

                            Directory.CreateDirectory(Settings.OnlineDatabaseFolder);
                            ZipArchive archive = ZipFile.OpenRead(Settings.OnlineDatabaseZip);
                            foreach (ZipArchiveEntry entry in archive.Entries)
                            {
                                try
                                {
                                    string filename = Settings.OnlineDatabaseFolder + "\\" + entry.Name;
                                    entry.ExtractToFile(filename, true);
                                }
                                catch(Exception)
                                {
                                    // nothing to do
                                }
                            }

                            // ZipFile.ExtractToDirectory(Settings.OnlineDatabaseZip, Settings.OnlineDatabaseFolder);
                            File.WriteAllText(Settings.OnlineDatabaseLog, lastUpdate.ToString());
                        }
                        else
                        {
                            throw new HttpRequestException("Database request failed", response.StatusCode);
                        }
                    }

                    success = true;
                    break;
                }
                catch (Exception ex)
                {
                    errors[index.ToString() + url] = ex;
                    continue;
                }
            }

            if (!success)
            {
                throw new DownloadException("Database download failed", errors);
            }

            return true;
        }
    }

    class HttpRequestException : Exception
    {
        public HttpStatusCode StatusCode { get; set; }
        public string Content { get; set; }

        public HttpRequestException(string message, HttpStatusCode statusCode, string content = null)
            : base(message)
        {
            this.StatusCode = statusCode;
            this.Content = content;
        }
    }

    class DownloadException : Exception
    {
        Dictionary<string, Exception> InternalExceptions { get; set; }

        public DownloadException(string message, Dictionary<string, Exception> exceptions)
            : base(message)
        {
            this.InternalExceptions = exceptions;
        }
    }
}
