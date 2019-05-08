using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WallpaperManager
{
    class BackgroundSetter
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int SystemParametersInfo(uint uiAction, uint uiParam, string pvParam, uint fWinIni);
        private static uint SPI_SETDESKWALLPAPER = 20;
        private static uint SPIF_UPDATEINIFILE = 0x1;

        /// <summary>
        /// Sets the background image
        /// </summary>
        /// <param name="filename">the file to use for setting the background image</param>
        /// <param name="displaymode">the display mode</param>
        /// <param name="backgroundcolor">the background color</param>
        /// <exception cref="Exception">Can throw exception</exception>
        public static void SetBackgroundImage(string filename, DisplayModeType displaymode, System.Windows.Media.Color backgroundcolor)
        {
            //get the screen dimensions and calculate the width of the task bar
            Rectangle totalScreenSize = Screen.PrimaryScreen.Bounds;
            Rectangle workingScreenSize = Screen.PrimaryScreen.WorkingArea;

            int lowerBorder = totalScreenSize.Height - workingScreenSize.Height;
            Int32Rect desiredImageSize = new Int32Rect(0, 0, totalScreenSize.Width, workingScreenSize.Height);
            Int32Rect actualImageSize = new Int32Rect(0, 0, 0, 0);
            Int32Rect finalImageSize = new Int32Rect(0, 0, 0, 0);
            PixelFormat imgFormat;
            BitmapPalette imgPalette;
            int imgScaleFactor = 4;

            //get the size and details of the actual image
            {
                BitmapImage tmp_img = new BitmapImage();
                tmp_img.BeginInit();
                tmp_img.CreateOptions = BitmapCreateOptions.None;
                tmp_img.CacheOption = BitmapCacheOption.OnLoad;
                tmp_img.UriSource = new Uri(filename);
                tmp_img.EndInit();

                actualImageSize.Width = tmp_img.PixelWidth;
                actualImageSize.Height = tmp_img.PixelHeight;
                imgFormat = tmp_img.Format;
                imgPalette = tmp_img.Palette;
                imgScaleFactor = imgFormat.BitsPerPixel / 8;
            }

            //create the final bitmap which we will write to the file
            WriteableBitmap bmp = new WriteableBitmap(totalScreenSize.Width, totalScreenSize.Height, 96, 96, imgFormat, imgPalette);
                
            //set the background of the final image
            {
                List<System.Windows.Media.Color> colors = new List<System.Windows.Media.Color>();
                colors.Add(backgroundcolor);
                BitmapPalette palette = new BitmapPalette(colors);

                // Creates a new empty image with the pre-defined palette
                byte[] pixels = new byte[totalScreenSize.Width * totalScreenSize.Height * imgScaleFactor];
                BitmapSource source = BitmapSource.Create(totalScreenSize.Width, totalScreenSize.Height, 96, 96, PixelFormats.Indexed1, palette, pixels, totalScreenSize.Width * imgScaleFactor);
                FormatConvertedBitmap converted = new FormatConvertedBitmap(source, imgFormat, imgPalette, 0);

                converted.CopyPixels(pixels, totalScreenSize.Width * imgScaleFactor, 0);
                Int32Rect background_srcRect = new Int32Rect(0, 0, totalScreenSize.Width, totalScreenSize.Height);
                bmp.WritePixels(background_srcRect, pixels, totalScreenSize.Width * imgScaleFactor, 0);
            }

            //get the image in the corrected dimension space
            BitmapImage img = new BitmapImage();
            img.BeginInit();
            img.CreateOptions = BitmapCreateOptions.None;
            img.CacheOption = BitmapCacheOption.OnLoad;

            switch (displaymode)
            {
                case DisplayModeType.Stretch:
                    if (desiredImageSize.Width > actualImageSize.Width)
                    {
                        if (desiredImageSize.Height >= actualImageSize.Height)
                        {
                            //image is smaller than the screen size
                            if (((double)desiredImageSize.Height / (double)actualImageSize.Height) < ((double)desiredImageSize.Width / (double)actualImageSize.Width))
                                img.DecodePixelHeight = desiredImageSize.Height;
                            else
                                img.DecodePixelWidth = desiredImageSize.Width;
                        }
                        else
                        {
                            //need to shrink the height of the image
                            img.DecodePixelHeight = desiredImageSize.Height;
                        }
                    }
                    else
                    {
                        if (desiredImageSize.Height >= actualImageSize.Height)
                        {
                            //need to shrink the width of the image
                            img.DecodePixelWidth = desiredImageSize.Width;
                        }
                        else
                        {
                            //image is totally larger than the screen size
                            if (((double)actualImageSize.Height / (double)desiredImageSize.Height) > ((double)actualImageSize.Width / (double)desiredImageSize.Width))
                                img.DecodePixelHeight = desiredImageSize.Height;
                            else
                                img.DecodePixelWidth = desiredImageSize.Width;
                        }
                    }
                    break;

                case DisplayModeType.Fill:
                    img.DecodePixelWidth = desiredImageSize.Width;
                    img.DecodePixelHeight = desiredImageSize.Height;
                    break;

                case DisplayModeType.Tile:
                    //tile would be the same as center as we just want the image to fit on the screen
                    //rest of the calculations would be done later

                default: //center
                    if (desiredImageSize.Width > actualImageSize.Width)
                    {
                        if (desiredImageSize.Height >= actualImageSize.Height)
                        {
                            //image is smaller than the screen size
                            //we don't need to do anything here
                        }
                        else
                        {
                            //need to shrink the height of the image
                            img.DecodePixelHeight = desiredImageSize.Height;
                        }
                    }
                    else
                    {
                        if (desiredImageSize.Height >= actualImageSize.Height)
                        {
                            //need to shrink the width of the image
                            img.DecodePixelWidth = desiredImageSize.Width;
                        }
                        else
                        {
                            //image is totally larger than the screen size
                            if (((double)actualImageSize.Height / (double)desiredImageSize.Height) > ((double)actualImageSize.Width / (double)desiredImageSize.Width))
                                img.DecodePixelHeight = desiredImageSize.Height;
                            else
                                img.DecodePixelWidth = desiredImageSize.Width;
                        }
                    }
                    break;
            }

            img.UriSource = new Uri(filename);
            img.EndInit();

            //recalculate the image offsets on the final bitmap
            finalImageSize.Width = img.PixelWidth;
            finalImageSize.Height = img.PixelHeight;
            finalImageSize.X = (desiredImageSize.Width - finalImageSize.Width) / 2;
            finalImageSize.Y = (desiredImageSize.Height - finalImageSize.Height) / 2;

            //copy the image to the final output
            byte[] img_bytes = new byte[finalImageSize.Width * finalImageSize.Height * imgScaleFactor];
            img.CopyPixels(img_bytes, finalImageSize.Width * imgScaleFactor, 0);

            if (displaymode == DisplayModeType.Tile)
            {
                int xoffset = 0;
                int yoffset = 0;

                Int32Rect srcRect = new Int32Rect();
                while (yoffset < desiredImageSize.Height)
                {
                    srcRect.Y = 0;
                    srcRect.Height = Math.Min((desiredImageSize.Height - yoffset), finalImageSize.Height);

                    while (xoffset < desiredImageSize.Width)
                    {
                        srcRect.X = 0;
                        srcRect.Width = Math.Min((desiredImageSize.Width - xoffset), finalImageSize.Width);
                        bmp.WritePixels(srcRect, img_bytes, finalImageSize.Width * imgScaleFactor, xoffset, yoffset);
                        xoffset += srcRect.Width;
                    }

                    yoffset += srcRect.Height;
                    xoffset = 0;
                }
            }
            else
            {
                Int32Rect srcRect = new Int32Rect(0, 0, finalImageSize.Width, finalImageSize.Height);
                bmp.WritePixels(srcRect, img_bytes, finalImageSize.Width * imgScaleFactor, finalImageSize.X, finalImageSize.Y);
            }

            //store the bitmap to a file
            string tempfilename = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\WallpaperManager\\wallpapermanager.bmp";
            FileStream stream = new FileStream(tempfilename, FileMode.Create);
            BmpBitmapEncoder encoder = new BmpBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bmp));
            encoder.Save(stream);
            stream.Close();

            SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, tempfilename, SPIF_UPDATEINIFILE);
        }
    }
}
