using StdOttStandard;
using StdOttStandard.Linq;
using StdOttFramework;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AppSearch
{
    internal class ViewModel : INotifyPropertyChanged
    {
        private const int viewAppsCount = 15;
        private const string dontShareFileName = "dontShare.txt";

        private string fileSystemSearchBase;
        private string searchKey;
        private List<SearchApp> allApps, allFileSystemApps;
        private SearchApp[] searchApps, searchFileSystemApps, searchResult;
        private readonly IconsService iconsService;
        private readonly Stack<SearchApp> loadApps;
        private int selectedAppIndex;

        public string FileSystemSearchBase
        {
            get => fileSystemSearchBase;
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
            get => searchKey;
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
            get => allApps;
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
            get => searchApps;
            private set
            {
                if (value.BothNullOrSequenceEqual(searchApps)) return;

                searchApps = value;
                RaiseSearchAppsChanged();
            }
        }

        public List<SearchApp> AllFileSystemApps
        {
            get => allFileSystemApps;
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
            get => searchFileSystemApps;
            set
            {
                if (value.BothNullOrSequenceEqual(searchFileSystemApps)) return;

                searchFileSystemApps = value;
                RaiseFileSystemSearchAppsChanged();
            }
        }

        public SearchApp[] SearchResult
        {
            get => searchResult;
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
            get => selectedAppIndex;
            set
            {
                if (value == selectedAppIndex) return;

                selectedAppIndex = value;
                OnPropertyChanged(nameof(SelectedAppIndex));
            }
        }

        public SearchApp SelectedApp => SearchResult.ElementAtOrDefault(SelectedAppIndex);

        public ViewModel()
        {
            IEnumerable<string> extensions;

            try
            {
                string path = FrameworkUtils.GetFullPath(dontShareFileName);
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
                        .OrderBy(a => a.Name.ToLower().IndexOf(searchKey, StringComparison.OrdinalIgnoreCase)).ToArray();
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
                            AllFileSystemApps.AddRange(addFiles.Select(CreateFileApp));
                            AllFileSystemApps.AddRange(addDirs.Select(CreateFolderApp));
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
                await Task.WhenAny(StdUtils.WaitAsync(lockObj), producerTask);

                if (searchFileSystemApps == null || basePath != FileSystemSearchBase) continue;

                SearchFileSystemApps = searchFileSystemApps;
            } while (!producerTask.IsCompleted);
        }

        public static bool IsNotHidden(string path)
        {
            return (new FileInfo(path).Attributes & FileAttributes.Hidden) == 0;
        }

        public static SearchApp CreateFileApp(string path)
        {
            return new SearchApp(path)
            {
                Thumbnail = IconsService.GenericFileIcon,
            };
        }

        public static SearchApp CreateFolderApp(string path)
        {
            return new SearchApp(path)
            {
                Thumbnail = IconsService.GenericFolderIcon,
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