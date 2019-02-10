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
        private bool hideOnStartup;
        private string[] source;
        private ViewModel viewModel;
        private HotKey showHotKey;

        public MainWindow()
        {
            InitializeComponent();

            Option showOpt = new Option("h", "hide", "Do hide window on startup.", false, 0);
            Option sourcewOpt = new Option("s", "source", "The application sources.", true, -1, 1);
            Option keyOpt = new Option("k", "searchKey", "SearchKey set on startup", false, 1, 1);
            Option hkOpt = new Option("hk", "hotKey", "HotKey the show window", true, -1, 1);

            OptionParseResult result = new Options(showOpt, sourcewOpt, keyOpt, hkOpt).Parse(Environment.GetCommandLineArgs().Skip(1));

            string searchKey = string.Empty;
            OptionParsed p;
            if (result.TryGetFirstValidOptionParseds(showOpt, out p)) hideOnStartup = true;
            if (result.TryGetFirstValidOptionParseds(keyOpt, out p)) searchKey = p.Values[0];

            source = result.GetValidOptionParseds(sourcewOpt).SelectMany(o => o.Values).ToArray();
            showHotKey = GetHotKey(result.GetValidOptionParseds(hkOpt).First());

            DataContext = viewModel = new ViewModel()
            {
                SearchKey = searchKey
            };

            showHotKey.Pressed += ShowHotKey_PressedAsync;

            LoadAllAppsFastAsync();
        }

        private HotKey GetHotKey(OptionParsed parsed)
        {
            string keyString = parsed.Values[0];
            int allModifier = 0;
            Key key = (Key)Enum.Parse(typeof(Key), keyString, true);

            foreach (string modifierString in parsed.Values.Skip(1).Select(v => v.ToLower()))
            {
                allModifier += (int)Enum.Parse(typeof(KeyModifier), modifierString, true);
            }

            return new HotKey(key, (KeyModifier)allModifier);
        }

        private async void ShowHotKey_PressedAsync(object sender, KeyPressedEventArgs e)
        {
            viewModel.SearchKey = string.Empty;

            Show();
            tbxSearchKey.Focus();

            await LoadAllAppsSlowAsync();
        }

        private async Task LoadAllAppsFastAsync()
        {
            SearchApp[] allApps;
            viewModel.AllApps = allApps = await Task.Run(new Func<SearchApp[]>(GetAllAppsSimple));

            await Task.Run(() => Parallel.ForEach(allApps, a => a.LoadRawData()));

            foreach (SearchApp app in allApps) app.LoadThumbnail();
        }

        private SearchApp[] GetAllAppsSimple()
        {
            string[] files = source.SelectMany(a => GetFiles(a)).Where(IsNotHidden).Distinct().ToArray();
            SearchApp[] allApps = new SearchApp[files.Length];

            Parallel.For(0, files.Length, (i) => allApps[i] = SearchApp.Simple(files[i]));

            return allApps;
        }

        private async Task LoadAllAppsSlowAsync()
        {
            SearchApp[] allApps = await Task.Run(new Func<SearchApp[]>(GetAllAppsWithData));

            foreach (SearchApp app in allApps) app.LoadThumbnail();

            viewModel.AllApps = allApps;
        }

        private SearchApp[] GetAllAppsWithData()
        {
            string[] files = source.SelectMany(a => GetFiles(a)).Where(IsNotHidden).Distinct().ToArray();
            SearchApp[] allApps = new SearchApp[files.Length];

            Parallel.For(0, files.Length, (i) => allApps[i] = SearchApp.WithData(files[i]));

            return allApps;
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

            if (e.Key == Key.Enter && viewModel.SelectedAppIndex != -1)
            {
                Process.Start(viewModel.SearchApps[viewModel.SelectedAppIndex].FullPath);
            }
            else if (e.Key == Key.Down && viewModel?.SelectedAppIndex != -1)
            {
                viewModel.SelectedAppIndex = (viewModel.SelectedAppIndex + 1) % viewModel.SearchApps.Length;
            }
            else if (e.Key == Key.Up && viewModel?.SelectedAppIndex != -1)
            {
                viewModel.SelectedAppIndex = (viewModel.SelectedAppIndex - 1 + viewModel.SearchApps.Length) % viewModel.SearchApps.Length;
            }
            else if (e.Key == Key.Escape)
            {
                TryHide();
            }
            else e.Handled = false;

            base.OnPreviewKeyDown(e);
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
        }

        private void TryHide()
        {
            if (showHotKey.RegistrationSucessful) Hide();
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
