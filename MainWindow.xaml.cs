using Shell32;
using StdOttFramework.Hotkey;
using StdOttStandard.CommendlinePaser;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AppSearch
{
    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool hideOnStartup, keepActivated;
        private string[] sources;
        private ViewModel viewModel;
        private HotKey showHotKey;

        public MainWindow()
        {
            InitializeComponent();

            Option showOpt = new Option("h", "hide", "Do hide window on startup.", false, 0);
            Option sourceOpt = new Option("s", "source", "The application sources.", true, -1, 1);
            Option keyOpt = new Option("k", "searchKey", "SearchKey set on startup", false, 1, 1);
            Option hkOpt = new Option("hk", "hotKey", "HotKey the show window", true, -1, 1);

            OptionParseResult result = new Options(showOpt, sourceOpt, keyOpt, hkOpt)
                .Parse(Environment.GetCommandLineArgs().Skip(1));

            string searchKey = string.Empty;
            OptionParsed p;
            if (result.TryGetFirstValidOptionParseds(showOpt, out p)) hideOnStartup = true;
            if (result.TryGetFirstValidOptionParseds(keyOpt, out p)) searchKey = p.Values[0];

            sources = result.GetValidOptionParseds(sourceOpt).SelectMany(o => o.Values).ToArray();
            showHotKey = GetHotKey(result.GetValidOptionParseds(hkOpt).First().Values);

            DataContext = viewModel = new ViewModel()
            {
                SearchKey = searchKey
            };

            showHotKey.Pressed += ShowHotKey_PressedAsync;

            UpdateAllApps();
        }

        private HotKey GetHotKey(IEnumerable<string> parts)
        {
            string keyString = parts.FirstOrDefault();
            int allModifier = 0;
            Key key = (Key)Enum.Parse(typeof(Key), keyString, true);

            foreach (string modifierString in parts.Skip(1).Select(v => v.ToLower()))
            {
                allModifier += (int)Enum.Parse(typeof(KeyModifier), modifierString, true);
            }

            return new HotKey(key, (KeyModifier)allModifier);
        }

        private void ShowHotKey_PressedAsync(object sender, KeyPressedEventArgs e)
        {
            Show();
            tbxSearchKey.Focus();
            KeepActivated();

            UpdateAllApps();
        }

        public void UpdateAllApps()
        {
            string[] files = sources.SelectMany(a => GetFiles(a)).Where(IsNotHidden).Distinct().ToArray();

            foreach (SearchApp app in viewModel.AllApps.Where(a => !files.Contains(a.FullPath)).ToArray())
            {
                viewModel.AllApps.Remove(app);
            }

            SearchApp[] newApps = files.Where(f => viewModel.AllApps.All(a => a.FullPath != f)).Select(f => new SearchApp(f)).ToArray();
            foreach (SearchApp app in newApps) viewModel.AllApps.Add(app);

            viewModel.RaiseSearchAppsChanged();
        }

        private static IEnumerable<string> GetFiles(string path)
        {
            if (File.Exists(path)) yield return path;
            else foreach (string file in Directory.GetFiles(path)) yield return file;
        }

        private static bool IsNotHidden(string path)
        {
            return (new FileInfo(path).Attributes & FileAttributes.Hidden) == 0;
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            e.Handled = true;

            int selectedIndex = viewModel?.SelectedAppIndex ?? -1;
            SearchApp selectedApp = viewModel.SelectedApp;

            if (e.Key == Key.Enter && selectedApp != null)
            {
                try
                {
                    TryHide();

                    if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                    {
                        Process.Start(Path.GetDirectoryName(selectedApp.FullPath));
                    }
                    else Process.Start(selectedApp.FullPath);
                }
                catch (Exception exc)
                {
                    MessageBox.Show(exc.ToString(), "Process starting error", MessageBoxButton.OK, MessageBoxImage.Error);
                    Show();
                }
            }
            else if (e.Key == Key.Down && selectedIndex != -1)
            {
                viewModel.SelectedAppIndex = (selectedIndex + 1) % viewModel.SearchResult.Length;
            }
            else if (e.Key == Key.Up && selectedIndex != -1)
            {
                int length = viewModel.SearchApps.Length;
                viewModel.SelectedAppIndex = (viewModel.SelectedAppIndex - 1 + length) % length;
            }
            else if (e.Key == Key.Escape)
            {
                TryHide();
            }
            else if (e.Key == Key.Tab && selectedApp != null)
            {
                string path = GetShortcutTargetFile(selectedApp.FullPath);

                if (File.Exists(path)) path = Path.GetDirectoryName(path);
                if (Directory.Exists(path)) viewModel.FileSystemSearchBase = path;
            }
            else e.Handled = false;

            base.OnPreviewKeyDown(e);
        }

        public static string GetShortcutTargetFile(string path)
        {
            string pathOnly = Path.GetDirectoryName(path);
            string filenameOnly = Path.GetFileName(path);

            Shell shell = new Shell();
            Folder folder = shell.NameSpace(pathOnly);
            FolderItem folderItem = folder.ParseName(filenameOnly);

            if (folderItem == null) return path;

            try
            {
                ShellLinkObject link = (ShellLinkObject)folderItem.GetLink;

                return link.Path;
            }
            catch
            {
                return path;
            }
        }

        private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ListBox lbx = (ListBox)sender;

            lbx.ScrollIntoView(lbx.SelectedItem);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            tbxSearchKey.Focus();

            showHotKey.Register();

            if (hideOnStartup) TryHide();
            else KeepActivated();
        }

        private async void KeepActivated()
        {
            keepActivated = true;

            while (keepActivated)
            {
                Activate();

                await Task.Delay(100);
            }
        }

        private void TryHide()
        {
            keepActivated = false;

            if (showHotKey.RegistrationSucessful)
            {
                Hide();

                viewModel.SearchKey = string.Empty;
                viewModel.FileSystemSearchBase = null;
            }
        }

        private void Window_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            MessageBox.Show(string.Join("\r\n", Environment.GetCommandLineArgs()));
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            TryHide();
        }
    }
}
