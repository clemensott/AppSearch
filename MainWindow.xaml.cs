﻿using Shell32;
using StdOttFramework.Hotkey;
using StdOttFramework.RestoreWindow;
using StdOttStandard.CommandlineParser;
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
        private readonly bool hideOnStartup;
        private bool keepActivated;
        private readonly string[] sources;
        private readonly ViewModel viewModel;
        private readonly HotKey showHotKey;
        private readonly RestoreWindowHandler restoreHandler;

        public MainWindow()
        {
            InitializeComponent();


            RestoreWindowSettings restoreSettings = RestoreWindowSettings.GetDefault(triggerType: StorePropertiesTriggerType.Manuel);
            restoreHandler = RestoreWindowHandler.Activate(this, restoreSettings);

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

        private static HotKey GetHotKey(IList<string> parts)
        {
            string keyString = parts[0];
            Key key = (Key)Enum.Parse(typeof(Key), keyString, true);

            int allModifier = parts.Skip(1).Select(v => v.ToLower())
                .Sum(modifierString => (int)Enum.Parse(typeof(KeyModifier), modifierString, true));

            return HotKey.GetInstance(key, (KeyModifier)allModifier);
        }

        private void ShowHotKey_PressedAsync(object sender, KeyPressedEventArgs e)
        {
            Show();
            tbxSearchKey.Focus();
            KeepActivated();

            UpdateAllApps();
            restoreHandler.Restore();
        }

        private void UpdateAllApps()
        {
            string[] files = sources.SelectMany(GetFiles).Where(ViewModel.IsNotHidden).Distinct().ToArray();

            foreach (SearchApp app in viewModel.AllApps.Where(a => !files.Contains(a.FullPath)).ToArray())
            {
                viewModel.AllApps.Remove(app);
            }

            SearchApp[] newApps = files.Where(f => viewModel.AllApps.All(a => a.FullPath != f))
                .Select(ViewModel.CreateFileApp).ToArray();
            foreach (SearchApp app in newApps) viewModel.AllApps.Add(app);

            viewModel.RaiseSearchAppsChanged();
        }

        private static IEnumerable<string> GetFiles(string path)
        {
            if (File.Exists(path)) yield return path;
            else
            {
                foreach (string file in Directory.GetFiles(path))
                {
                    yield return file;
                }
            }
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            e.Handled = true;

            int selectedIndex = viewModel?.SelectedAppIndex ?? -1;
            int length = viewModel.SearchResult.Length;
            SearchApp selectedApp = viewModel.SelectedApp;

            if (e.Key == Key.Enter &&
                (selectedApp != null || !string.IsNullOrWhiteSpace(viewModel.FileSystemSearchBase)))
            {
                string path = selectedApp?.FullPath ?? viewModel.FileSystemSearchBase;

                try
                {
                    TryHide();

                    if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                    {
                        string args = $"/select,\"{path}\"";
                        Process.Start("explorer.exe", args);
                    }
                    else Process.Start(path);
                }
                catch (Exception exc)
                {
                    string message = $"Path: {path}\r\n{exc}";
                    MessageBox.Show(message, "Process starting error", MessageBoxButton.OK, MessageBoxImage.Error);
                    Show();
                }
            }
            else if (e.Key == Key.Down && selectedIndex != -1 && length != 0)
            {
                viewModel.SelectedAppIndex = (selectedIndex + 1) % length;
            }
            else if (e.Key == Key.Up && selectedIndex != -1 && length != 0)
            {
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

        private static string GetShortcutTargetFile(string path)
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

        private void BtnSavePosition_Click(object sender, RoutedEventArgs e)
        {
            restoreHandler.Store();
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
                CheckWinKey();

                await Task.Delay(100);
            }
        }

        private void CheckWinKey()
        {
            bool wasWinKeyDown = viewModel.IsWinKeyDown;
            viewModel.IsWinKeyDown = Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin);

            if (!viewModel.IsWinKeyDown && wasWinKeyDown)
            {
                tbxSearchKey.Focus();
            }
        }

        private void TryHide()
        {
            keepActivated = false;

            if (showHotKey.IsRegistrated)
            {
                Hide();

                viewModel.SearchKey = string.Empty;
                viewModel.FileSystemSearchBase = null;
            }

            viewModel.LoadThumbnails(viewModel.AllApps.ToArray());
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