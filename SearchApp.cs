using StdOttFramework;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace AppSearch
{
    internal class SearchApp : INotifyPropertyChanged
    {
        private static readonly object fileThumbnailLockObj = new object(), folderThumbnailLockObj = new object();
        private static BitmapSource fileIco, folderIco;

        public static BitmapSource GenericFileThumbnail
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

        public static BitmapSource GenericFolderThumbnail
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

        private BitmapSource thumbnail;
        private string fullPath, name;

        public bool IsThumbnailLoaded => Thumbnail != GenericFileThumbnail;

        public BitmapSource Thumbnail
        {
            get { return thumbnail; }
            private set
            {
                if (value == thumbnail) return;

                thumbnail = value;
                OnPropertyChanged(nameof(Thumbnail));
            }
        }

        public string FullPath
        {
            get { return fullPath; }
            private set
            {
                if (value == fullPath) return;

                fullPath = value;
                OnPropertyChanged(nameof(FullPath));
            }
        }

        public string Name
        {
            get { return name; }
            private set
            {
                if (value == name) return;

                name = value;
                OnPropertyChanged(nameof(Name));
            }
        }

        public SearchApp(string path)
        {
            FullPath = path;
            Name = Path.GetFileName(path);
            Thumbnail = GenericFileThumbnail;
        }

        public static IEnumerable<byte> GetPixelsData(Bitmap bmp)
        {
            foreach (var color in GetPixels(bmp))
            {
                yield return color.B;
                yield return color.G;
                yield return color.R;
                yield return color.A;
            }
        }

        private static IEnumerable<System.Drawing.Color> GetPixels(Bitmap bmp)
        {
            for (int j = 0; j < bmp.Height; j++)
            {
                for (int i = 0; i < bmp.Width; i++)
                {
                    yield return bmp.GetPixel(i, j);
                }
            }
        }

        public void LoadThumbnail()
        {
            if (IsThumbnailLoaded) return;

            if (File.Exists(FullPath))
            {
                using (Icon sysicon = Icon.ExtractAssociatedIcon(FullPath))
                {
                    Thumbnail = Imaging.CreateBitmapSourceFromHIcon(sysicon.Handle,
                         Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                }
            }
            else Thumbnail = GenericFolderThumbnail;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}