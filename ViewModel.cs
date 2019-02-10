using StdOttFramework;
using System.ComponentModel;
using System.IO;
using System.Linq;

namespace AppSearch
{
    class ViewModel : INotifyPropertyChanged
    {
        private const string positionFileName = "Position.txt";
        private static readonly string positionPath = Utils.GetFullPath(positionFileName);

        private string searchKey;
        private SearchApp[] allApps, searchApps;
        private int selectedAppIndex;
        private double windowLeft, windowTop;

        public string SearchKey
        {
            get { return searchKey; }
            set
            {
                if (value == searchKey) return;

                searchKey = value;
                OnPropertyChanged(nameof(SearchKey));

                UpdateSearchApps();
            }
        }

        public SearchApp[] AllApps
        {
            get { return allApps; }
            set
            {
                if (value == allApps) return;

                allApps = value;
                OnPropertyChanged(nameof(AllApps));

                int index = SelectedAppIndex;
                UpdateSearchApps();

                if (index > 0) SelectedAppIndex = index;
            }
        }

        public SearchApp[] SearchApps
        {
            get { return searchApps; }
            private set
            {
                if (value == searchApps) return;

                searchApps = value;
                OnPropertyChanged(nameof(SearchApps));
            }
        }

        public int SelectedAppIndex
        {
            get { return selectedAppIndex; }
            set
            {
                selectedAppIndex = value;
                OnPropertyChanged(nameof(SelectedAppIndex));
            }
        }

        public double WindowLeft
        {
            get { return windowLeft; }
            set
            {
                if (value != windowLeft)
                {
                    windowLeft = value;
                    OnPropertyChanged(nameof(WindowLeft));
                }

                SavePosition();
            }
        }

        public double WindowTop
        {
            get { return windowTop; }
            set
            {
                if (value != windowTop)
                {
                    windowTop = value;
                    OnPropertyChanged(nameof(WindowTop));
                }

                SavePosition();
            }
        }

        public ViewModel()
        {
            try
            {
                string[] pos = File.ReadAllLines(positionPath);

                double.TryParse(pos.ElementAtOrDefault(0), out windowLeft);
                double.TryParse(pos.ElementAtOrDefault(1), out windowTop);
            }
            catch { }
        }

        private void UpdateSearchApps()
        {
            if (!string.IsNullOrEmpty(searchKey) && AllApps != null && AllApps.Length > 0)
            {
                SearchApps = AllApps?.Where(a => a.Name.ToLower().Contains(searchKey.ToLower()))
                    .OrderBy(a => a.Name.ToLower().IndexOf(searchKey.ToLower())).ToArray();

                SelectedAppIndex = SearchApps.Length > 0 ? 0 : -1;
            }
            else
            {
                SearchApps = new SearchApp[0];
                SelectedAppIndex = -1;
            }
        }

        private void SavePosition()
        {
            try
            {
                File.WriteAllLines(positionPath, new string[] { WindowLeft.ToString(), WindowTop.ToString() });
            }
            catch { }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
