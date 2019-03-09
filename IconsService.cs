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
    class IconsService
    {
        private static readonly object fileThumbnailLockObj = new object(), folderThumbnailLockObj = new object();
        private static BitmapSource fileIco, folderIco;

        public static BitmapSource GenericFileIcon
        {
            get
            {
                if (fileIco != null) return fileIco;

                lock (fileThumbnailLockObj)
                {
                    if (fileIco != null) return fileIco;

                    fileIco = LoadPicture("genericFileThumbnail.png");

                    return fileIco;
                }
            }
        }

        public static BitmapSource GenericFolderIcon
        {
            get
            {
                if (folderIco != null) return folderIco;

                lock (folderThumbnailLockObj)
                {
                    if (folderIco != null) return folderIco;

                    return folderIco = LoadPicture("genericFolderThumbnail.png");
                }
            }
        }

        private static BitmapImage LoadPicture(string fileName)
        {
            try
            {
                string path = Utils.GetFullPath(fileName);
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

        private Dictionary<string, BitmapSource> icons;

        public IconsService(IEnumerable<string> dontShare)
        {
            BitmapSource file = GenericFileIcon;
            BitmapSource folder = GenericFolderIcon;

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
            string extension = Path.GetExtension(path).ToLower();
            
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
            using (Icon sysicon = Icon.ExtractAssociatedIcon(path))
            {
                return Imaging.CreateBitmapSourceFromHIcon(sysicon.Handle,
                     Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            }
        }
    }
}
