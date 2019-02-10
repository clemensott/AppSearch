using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AppSearch
{
    internal class SearchApp : INotifyPropertyChanged
    {
        private int width, height;
        private Array pixels;
        private WriteableBitmap thumbnail;
        private string fullPath, name;

        public WriteableBitmap Thumbnail
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

        private SearchApp(string path)
        {
            FullPath = path;
            Name = Path.GetFileNameWithoutExtension(path);
        }

        public void LoadRawData()
        {
            Bitmap bmp = Icon.ExtractAssociatedIcon(FullPath).ToBitmap();
            width = bmp.Width;
            height = bmp.Height;
            pixels = GetPixelsData(bmp).ToArray();
        }

        public void LoadThumbnail()
        {
            PixelFormat format = PixelFormats.Pbgra32;
            BitmapPalette palette = BitmapPalettes.BlackAndWhite;
            Int32Rect rect = new Int32Rect(0, 0, width, height);

            Thumbnail = new WriteableBitmap(width, height, 96, 96, format, palette);
            Thumbnail.WritePixels(rect, pixels, width * 4, 0);
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

        public static  SearchApp Simple(string path)
        {
            return new SearchApp(path);
        }

        public static SearchApp WithData(string path)
        {
            SearchApp app = new SearchApp(path);
            app.LoadRawData();

            return app;
        }

        public static SearchApp Complete(string path)
        {
            SearchApp app = new SearchApp(path);
            app.LoadRawData();
            app.LoadThumbnail();

            return app;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

    }
}