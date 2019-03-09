using StdOttStandard;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AppSearch
{
    class ViewModel : INotifyPropertyChanged
    {
        private const int viewAppsCount = 15;
        private const string windowRectFileName = "Position.txt", dontShareFileName = "dontShare.txt";
        private static readonly string windowRectPath = StdOttFramework.Utils.GetFullPath(windowRectFileName);

        private string fileSystemSearchBase;
        private string searchKey;
        private List<SearchApp> allApps, allFileSystemApps;
        private SearchApp[] searchApps, searchFileSystemApps, searchResult;
        private IconsService iconsService;
        private Stack<SearchApp> loadApps;
        private int selectedAppIndex;
        private double windowLeft, windowTop, windowWidth, windowHeight;

        public string FileSystemSearchBase
        {
            get { return fileSystemSearchBase; }
            set
            {
                if (value == fileSystemSearchBase) return;

                fileSystemSearchBase = value;
                OnPropertyChanged(nameof(FileSystemSearchBase));

                if (FileSystemSearchBase != null)
                {
                    SearchKey = string.Empty;
                    UpdateAllFileSystemApps();
                }
            }
        }

        public bool IsFileSystemSearching => FileSystemSearchBase != null;

        public string SearchKey
        {
            get { return searchKey; }
            set
            {
                if (value == searchKey) return;

                searchKey = value;
                OnPropertyChanged(nameof(SearchKey));

                UpdateSearchApps();
                UpdateSearchFileSystemApps();

                SearchResult = IsFileSystemSearching ? SearchFileSystemApps : SearchApps;
            }
        }

        public List<SearchApp> AllApps
        {
            get { return allApps; }
            private set
            {
                if (value == allApps) return;

                allApps = value;

                OnPropertyChanged(nameof(AllApps));
                UpdateSearchApps();
            }
        }

        public SearchApp[] SearchApps
        {
            get { return searchApps; }
            private set
            {
                if (value.BothNullOrSequenceEqual(searchApps)) return;

                searchApps = value;
                RaiseSearchAppsChanged();
            }
        }

        public List<SearchApp> AllFileSystemApps
        {
            get { return allFileSystemApps; }
            set
            {
                if (value == allFileSystemApps) return;

                allFileSystemApps = value;

                OnPropertyChanged(nameof(AllFileSystemApps));
                UpdateSearchFileSystemApps();
            }
        }

        public SearchApp[] SearchFileSystemApps
        {
            get { return searchFileSystemApps; }
            set
            {
                if (value.BothNullOrSequenceEqual(searchFileSystemApps)) return;

                searchFileSystemApps = value;
                RaiseFileSystemSearchAppsChanged();
            }
        }

        public SearchApp[] SearchResult
        {
            get { return searchResult; }
            set
            {
                if (value.BothNullOrSequenceEqual(searchResult)) return;

                SearchApp selectedApp = SelectedAppIndex > 0 ? SelectedApp : null;

                searchResult = value;
                OnPropertyChanged(nameof(SearchResult));

                if (SearchResult.Length == 0) SelectedAppIndex = -1;
                else if (selectedApp == null) SelectedAppIndex = 0;
                else SelectedAppIndex = Math.Max(0, SearchResult.IndexOf(selectedApp));

                LoadThumbnails(SearchResult);
            }
        }

        public int SelectedAppIndex
        {
            get { return selectedAppIndex; }
            set
            {
                if (value != selectedAppIndex) { }
                selectedAppIndex = value;
                OnPropertyChanged(nameof(SelectedAppIndex));
            }
        }

        public SearchApp SelectedApp => SearchResult.ElementAtOrDefault(SelectedAppIndex);

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

                SaveWindowRect();
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

                SaveWindowRect();
            }
        }

        public double WindowWidth
        {
            get { return windowWidth; }
            set
            {
                if (value != windowWidth)
                {
                    windowWidth = value;
                    OnPropertyChanged(nameof(WindowWidth));
                }

                SaveWindowRect();
            }
        }

        public double WindowHeight
        {
            get { return windowHeight; }
            set
            {
                if (value != windowHeight)
                {
                    windowHeight = value;
                    OnPropertyChanged(nameof(WindowHeight));
                }

                SaveWindowRect();
            }
        }

        public ViewModel()
        {
            IEnumerable<string> extensions;

            try
            {
                string path = StdOttFramework.Utils.GetFullPath(dontShareFileName);
                extensions = File.ReadAllLines(path);
            }
            catch
            {
                extensions = Enumerable.Empty<string>();
            }

            iconsService = new IconsService(extensions);
            loadApps = new Stack<SearchApp>();
            AllApps = new List<SearchApp>();
            AllFileSystemApps = new List<SearchApp>();

            try
            {
                string[] pos = File.ReadAllLines(windowRectPath);

                WindowLeft = int.Parse(pos[0]);
                WindowTop = int.Parse(pos[1]);
                WindowWidth = int.Parse(pos[2]);
                WindowHeight = int.Parse(pos[3]);
            }
            catch
            {
                WindowLeft = 100;
                WindowTop = 100;
                windowWidth = 1000;
                WindowHeight = 500;
            }
        }

        private void UpdateSearchApps()
        {
            SearchApps = GetSearchResult(AllApps, SearchKey);
        }

        private void UpdateSearchFileSystemApps()
        {
            SearchFileSystemApps = GetSearchResult(AllFileSystemApps, SearchKey);
        }

        private static SearchApp[] GetSearchResult(IList<SearchApp> src, string searchKey)
        {
            if (!string.IsNullOrEmpty(searchKey) && src != null && src.Count > 0)
            {
                lock (src)
                {
                    return src?.Where(a => a.Name.ToLower().Contains(searchKey.ToLower())).Take(viewAppsCount)
                        .OrderBy(a => a.Name.ToLower().IndexOf(searchKey.ToLower())).ToArray();
                }
            }
            else return new SearchApp[0];
        }

        private async Task UpdateAllFileSystemApps()
        {
            AllFileSystemApps.Clear();

            string basePath = FileSystemSearchBase;
            Queue<string> dirs = new Queue<string>();
            object lockObj = new object();
            SearchApp[] searchFileSystemApps = null;

            dirs.Enqueue(FileSystemSearchBase);

            Task producerTask = Task.Run(() =>
            {
                while (dirs.Count > 0)
                {
                    try
                    {
                        if (basePath != FileSystemSearchBase) return;

                        string dir = dirs.Dequeue();
                        string[] addFiles = Directory.GetFiles(dir).Where(IsNotHidden).ToArray();
                        string[] addDirs = Directory.GetDirectories(dir).Where(IsNotHidden).ToArray();

                        if (basePath != FileSystemSearchBase) return;

                        if (addFiles.Length == 0 && addDirs.Length == 0) continue;

                        lock (AllFileSystemApps)
                        {
                            AllFileSystemApps.AddRange(addFiles.Select(CreateApp));
                            AllFileSystemApps.AddRange(addDirs.Select(CreateApp));
                        }

                        searchFileSystemApps = GetSearchResult(AllFileSystemApps, SearchKey);

                        if (!SearchFileSystemApps.BothNullOrSequenceEqual(searchFileSystemApps))
                        {
                            lock (lockObj)
                            {
                                Monitor.Pulse(lockObj);
                            }
                        }

                        foreach (string subDir in addDirs) dirs.Enqueue(subDir);
                    }
                    catch { }
                }
            });

            do
            {
                await Task.WhenAny(Utils.WaitAsync(lockObj), producerTask);

                if (searchFileSystemApps == null || basePath != FileSystemSearchBase) continue;

                SearchFileSystemApps = searchFileSystemApps;
            } while (!producerTask.IsCompleted);
        }

        public static bool IsNotHidden(string path)
        {
            return (new FileInfo(path).Attributes & FileAttributes.Hidden) == 0;
        }

        public static SearchApp CreateApp(string path)
        {
            return new SearchApp(path)
            {
                Thumbnail = IconsService.GetGenericIcon(path)
            };
        }

        public async void LoadThumbnails(SearchApp[] apps)
        {
            await Task.Delay(100);

            for (int i = 0; i < apps.Length && apps == SearchResult; i++)
            {
                await LoadThumbnail(apps[i], 40);
            }

            for (int i = apps.Length - 1; i >= 0; i--)
            {
                if (!IsThumbnailLoaded(apps[i])) loadApps.Push(apps[i]);
            }

            while (loadApps.Count > 0 && apps == SearchResult)
            {
                await LoadThumbnail(loadApps.Pop(), 100);
            }
        }

        public async Task LoadThumbnail(SearchApp app, int delay)
        {
            if (IsThumbnailLoaded(app)) return;

            app.Thumbnail = await iconsService.GetIcon(app.FullPath, delay);
        }

        private static bool IsThumbnailLoaded(SearchApp app)
        {
            return app.Thumbnail != IconsService.GenericFileIcon;
        }

        private async void SaveWindowRect()
        {
            double left = WindowLeft, top = WindowTop;

            await Task.Delay(200);

            if (left != WindowLeft || top != WindowTop) return;

            SaveWindowRect(windowRectPath, (int)WindowLeft, (int)WindowTop, (int)WindowWidth, (int)WindowHeight);
        }

        private static void SaveWindowRect(string path, int left, int top, int width, int height)
        {
            try
            {
                string[] lines = new string[] { left.ToString(), top.ToString(), width.ToString(), height.ToString() };
                File.WriteAllLines(windowRectPath, lines);
            }
            catch { }
        }

        public void RaiseSearchAppsChanged()
        {
            OnPropertyChanged(nameof(SearchApps));
            if (!IsFileSystemSearching) SearchResult = SearchApps;
        }

        public void RaiseFileSystemSearchAppsChanged()
        {
            OnPropertyChanged(nameof(SearchFileSystemApps));
            if (IsFileSystemSearching) SearchResult = SearchFileSystemApps;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
