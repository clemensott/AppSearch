using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows.Media.Imaging;

namespace AppSearch
{
    internal class SearchApp : INotifyPropertyChanged
    {
        private BitmapSource thumbnail;
        private string fullPath, name;

        public BitmapSource Thumbnail
        {
            get { return thumbnail; }
            set
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
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}