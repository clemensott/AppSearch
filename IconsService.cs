using StdOttFramework;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace AppSearch
{
    internal class IconsService
    {
        public static BitmapSource GenericFileIcon { get; private set; }

        public static BitmapSource GenericFolderIcon { get; private set; }

        private static void SetGenericFileIcon()
        {
            GenericFileIcon = LoadPicture("genericFileThumbnail.png");
        }

        private static void SetGenericFolderIcon()
        {
            GenericFolderIcon = LoadPicture("genericFolderThumbnail.png");
        }

        private static BitmapImage LoadPicture(string fileName)
        {
            try
            {
                string path = FrameworkUtils.GetFullPathToExe(fileName);
                return new BitmapImage(new Uri(path));
            }
            catch
            {
                return new BitmapImage();
            }
        }

        public static BitmapSource GetGenericIcon(string path)
        {
            return File.Exists(path) ? GenericFileIcon : GenericFolderIcon;
        }

        private readonly Dictionary<string, BitmapSource> icons;

        public IconsService(IEnumerable<string> dontShare)
        {
            SetGenericFileIcon();
            SetGenericFolderIcon();

            icons = new Dictionary<string, BitmapSource>();

            try
            {
                foreach (string extension in dontShare)
                {
                    icons.Add(extension.ToLower(), null);
                }
            }
            catch { }
        }

        public async Task<BitmapSource> GetIcon(string path, int delay)
        {
            BitmapSource bmp;
            string extension = Path.GetExtension(path)?.ToLower() ?? string.Empty;

            if (!icons.TryGetValue(extension, out bmp))
            {
                bmp = LoadIcon(path);
                icons.Add(extension, bmp);
                await Task.Delay(delay);
            }
            else if (bmp == null)
            {
                bmp = LoadIcon(path);
                await Task.Delay(delay);
            }

            return bmp;
        }

        public static BitmapSource LoadIcon(string path)
        {
            using (Icon icon = Icon.ExtractAssociatedIcon(path))
            {
                return Imaging.CreateBitmapSourceFromHIcon(icon.Handle,
                    Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            }
        }
    }
}