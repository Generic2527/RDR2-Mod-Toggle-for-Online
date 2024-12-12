using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Collections.ObjectModel;
using System.IO;
using Path = System.IO.Path;
using System.Windows.Interop;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.ComponentModel;
using System.Linq;
using System.Globalization;
using System;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RDR2_Mod_Toggle_for_Online
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string CheckStateFilePath = "checkState.json";
        private readonly string backupPath = Path.Combine(Directory.GetCurrentDirectory(), "Backup");

        public MainWindow()
        {
            InitializeComponent();
            this.PreviewKeyDown += new KeyEventHandler(HandleKeyPress);
        }

        private void HandleKeyPress(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.S &&
                (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control &&
                (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift &&
                (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
            {
                try
                {
                    GamePathStateSaver.SaveGamePathState(tbGamePath.Text);
                    AppendLog("Game path state saved successfully.");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"An error occurred while saving game path state: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    AppendLog($"Error occurred while saving game path state: {ex.Message}");
                }
            }
        }

        private void btnDetectPathSteam_Click(object sender, RoutedEventArgs e)
        {
            string defaultPath = "C:\\Program Files (x86)\\Steam\\steamapps\\common\\Red Dead Redemption 2";

            if (System.IO.File.Exists(defaultPath + "\\RDR2.exe"))
            {
                tbGamePath.Text = defaultPath;
                LoadFileTree(tbGamePath.Text);
                AppendLog("Detected game in Steam path.");
            }
            else
            {
                MessageBox.Show("Red Dead Redemption 2 is not installed in the default Steam directory. Please click the Browse button to manually specify the installation path.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                AppendLog("Failed to detect game in Steam path.");
            }
        }

        private void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                Title = "Select the folder where Red Dead Redemption 2 is installed",
                InitialDirectory = tbGamePath.Text
            };
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                tbGamePath.Text = dialog.FileName;
                AppendLog($"User selected path: {dialog.FileName}");
            }

            LoadFileTree(tbGamePath.Text);
        }

        private void CheckBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is CheckBox checkBox && checkBox.DataContext is FileTreeItem item)
            {
                bool? newValue = (item.IsChecked == true) ? false : true;
                if (newValue == true && GamePathStateSaver.IsBaseGameFile(item.GetRelativePath(tbGamePath.Text)))
                {
                    MessageBoxResult result = MessageBox.Show("This is a base game file. Modifying it may cause issues. Do you want to continue?", "Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (result == MessageBoxResult.No)
                    {
                        e.Handled = true;
                        return;
                    }
                }
                item.SetIsChecked(newValue, true, true);
                e.Handled = true;
            }
        }

        private void LoadFileTree(string path)
        {
            var root = new FileTreeItem { Name = path, IsChecked = false, Icon = IconHelper.GetIcon(path, true) };
            LoadChildren(root, path);
            fileTreeView.ItemsSource = root.Children;

            // Check backup folder and add missing items
            if (Directory.Exists(backupPath))
            {
                AddMissingItemsFromBackup(root, backupPath, path);
                //AppendLog("Added missing items from backup folder.");
            }

            LoadCheckState(); // Load the previous check state

            // Ensure all items in the backup folder are checked
            CheckBackupItems(fileTreeView.ItemsSource as ObservableCollection<FileTreeItem>, backupPath);

            // Save the game folder path to properties.settings
            Properties.Settings.Default.gamePath = path;
            Properties.Settings.Default.Save(); // Save the settings

            AppendLog($"Loaded game folder: {path}");
        }

        private void AddMissingItemsFromBackup(FileTreeItem root, string backupPath, string gamePath)
        {
            foreach (var directory in Directory.GetDirectories(backupPath, "*", SearchOption.AllDirectories))
            {
                var relativePath = directory.Substring(backupPath.Length + 1);
                var gameFullPath = Path.Combine(gamePath, relativePath);
                if (!Directory.Exists(gameFullPath))
                {
                    AddMissingItem(root, relativePath, true);
                    AppendLog($"Added missing directory from backup: {relativePath}");
                }
            }

            foreach (var file in Directory.GetFiles(backupPath, "*.*", SearchOption.AllDirectories))
            {
                var relativePath = file.Substring(backupPath.Length + 1);
                var gameFullPath = Path.Combine(gamePath, relativePath);
                if (!File.Exists(gameFullPath))
                {
                    AddMissingItem(root, relativePath, false);
                    AppendLog($"Added missing file from backup: {relativePath}");
                }
            }

            // Update the check state and unloaded state of all parent items
            UpdateParentCheckState(root);
        }
        private void UpdateParentCheckState(FileTreeItem item)
        {
            foreach (var child in item.Children)
            {
                UpdateParentCheckState(child);
            }
            item.UpdateCheckStateFromChildren();
        }

        private void AddMissingItem(FileTreeItem root, string relativePath, bool isDirectory)
        {
            var parts = relativePath.Split(Path.DirectorySeparatorChar);
            var current = root;

            foreach (var part in parts)
            {
                var child = current.Children.FirstOrDefault(c => c.Name == part);
                if (child == null)
                {
                    var fullPath = Path.Combine(current.GetFullPath(), part);
                    child = new FileTreeItem
                    {
                        Name = part,
                        IsChecked = true,
                        IsUnloaded = true,
                        Icon = IconHelper.GetIcon(fullPath, isDirectory), // 아이콘 설정
                        Parent = current
                    };
                    current.Children.Add(child);
                }
                current = child;
            }
        }

        private void LoadChildren(FileTreeItem item, string path)
        {
            foreach (var directory in Directory.GetDirectories(path))
            {
                var dirItem = new FileTreeItem
                {
                    Name = Path.GetFileName(directory),
                    IsChecked = false,
                    Icon = IconHelper.GetIcon(directory, true),
                    Parent = item
                };
                LoadChildren(dirItem, directory);
                item.Children.Add(dirItem);
            }

            foreach (var file in Directory.GetFiles(path))
            {
                item.Children.Add(new FileTreeItem
                {
                    Name = Path.GetFileName(file),
                    IsChecked = false,
                    Icon = IconHelper.GetIcon(file, false),
                    Parent = item
                });
            }
        }

        private async void btnUnloadMods_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await Task.Run(() => BackupCheckedItems(fileTreeView.ItemsSource as ObservableCollection<FileTreeItem>, backupPath));
                AppendLog("Mods backup completed.");

                // Remove all files and directories in the game directory
                await Task.Run(() =>
                {
                    foreach (var item in fileTreeView.ItemsSource as ObservableCollection<FileTreeItem>)
                    {
                        if (item.IsChecked == true)
                        {
                            var fullPath = item.GetFullPath();
                            if (Directory.Exists(fullPath))
                            {
                                Directory.Delete(fullPath, true);
                            }
                            else if (File.Exists(fullPath))
                            {
                                File.Delete(fullPath);
                            }
                            item.SetIsUnloaded(true); // Mark as unloaded
                            AppendLog($"Unloaded mod: {fullPath}");
                        }
                    }
                });

                SaveCheckState(); // Save the current check state
                                  //AppendLog("Check state saved.");

                AppendLog("Mods unloaded.");
            }
            catch (IOException ex)
            {
                MessageBox.Show("An error occurred while unloading mods. Please make sure that the game is not running and that no files are in use.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                AppendLog($"Error occurred while unloading mods: {ex.Message}");
            }
        }

        private async void btnLoadMods_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string gamePath = tbGamePath.Text;
                await Task.Run(() => RestoreCheckedItems(fileTreeView.ItemsSource as ObservableCollection<FileTreeItem>, backupPath, gamePath));
                AppendLog("Mods loaded.");
            }
            catch (IOException ex)
            {
                MessageBox.Show("An error occurred while loading mods. Please make sure that the game is not running and that no files are in use.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                AppendLog($"Error occurred while loading mods: {ex.Message}");
            }
        }

        private void BackupCheckedItems(ObservableCollection<FileTreeItem> items, string backupPath)
        {
            foreach (var item in items)
            {
                if (item.IsChecked == true)
                {
                    var sourcePath = item.GetFullPath();
                    string destinationPath = null;

                    // Access tbGamePath.Text on the UI thread
                    Dispatcher.Invoke(() =>
                    {
                        destinationPath = Path.Combine(backupPath, item.GetRelativePath(tbGamePath.Text));
                    });

                    if (Directory.Exists(sourcePath))
                    {
                        DirectoryCopy(sourcePath, destinationPath, true);
                    }
                    else if (File.Exists(sourcePath))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                        File.Copy(sourcePath, destinationPath, true); // Overwrite if the file already exists
                    }
                }
                BackupCheckedItems(item.Children, backupPath);
            }
        }

        private void RestoreCheckedItems(ObservableCollection<FileTreeItem> items, string backupPath, string restorePath)
        {
            foreach (var item in items)
            {
                if (item.IsChecked == true)
                {
                    var relativePath = item.GetRelativePath(restorePath);
                    var sourcePath = Path.Combine(backupPath, relativePath);
                    var destinationPath = Path.Combine(restorePath, relativePath);

                    if (Directory.Exists(sourcePath))
                    {
                        DirectoryCopy(sourcePath, destinationPath, true);
                    }
                    else if (File.Exists(sourcePath))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                        File.Copy(sourcePath, destinationPath, true);
                    }

                    item.SetIsUnloaded(false);
                }

                RestoreCheckedItems(item.Children, backupPath, restorePath);
            }
        }

        private static void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs)
        {
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);
            DirectoryInfo[] dirs = dir.GetDirectories();

            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException("Source directory does not exist or could not be found: " + sourceDirName);
            }

            Directory.CreateDirectory(destDirName);

            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string tempPath = Path.Combine(destDirName, file.Name);
                file.CopyTo(tempPath, true); // Overwrite if the file already exists
            }

            if (copySubDirs)
            {
                foreach (DirectoryInfo subdir in dirs)
                {
                    string tempPath = Path.Combine(destDirName, subdir.Name);
                    DirectoryCopy(subdir.FullName, tempPath, copySubDirs);
                }
            }
        }

        private void SaveCheckState()
        {
            var items = fileTreeView.ItemsSource as ObservableCollection<FileTreeItem>;
            var checkState = items.Select(item => new { item.Name, item.IsChecked, item.IsUnloaded }).ToList();
            File.WriteAllText(CheckStateFilePath, JsonConvert.SerializeObject(checkState));
            //AppendLog("Check state saved.");
        }

        private void LoadCheckState()
        {
            if (File.Exists(CheckStateFilePath))
            {
                var checkState = JsonConvert.DeserializeObject<List<dynamic>>(File.ReadAllText(CheckStateFilePath));
                var items = fileTreeView.ItemsSource as ObservableCollection<FileTreeItem>;
                if (items != null)
                {
                    foreach (var state in checkState)
                    {
                        var item = items.FirstOrDefault(i => i.Name == (string)state.Name);
                        if (item != null)
                        {
                            item.IsChecked = state.IsChecked;

                            // Check if the file or directory exists
                            var fullPath = item.GetFullPath();
                            if (Directory.Exists(fullPath) || File.Exists(fullPath))
                            {
                                item.IsUnloaded = false;
                            }
                            else
                            {
                                item.IsUnloaded = true;
                            }
                        }
                    }
                }
                else
                {
                    AppendLog("Error occurred while loading check state: items is null.");
                }
            }
        }

        private void CheckBackupItems(ObservableCollection<FileTreeItem> items, string backupPath)
        {
            foreach (var directory in Directory.GetDirectories(backupPath, "*", SearchOption.AllDirectories))
            {
                var relativePath = directory.Substring(backupPath.Length + 1);
                var item = FindItem(items, relativePath);
                if (item != null)
                {
                    item.IsChecked = true;
                }
            }

            foreach (var file in Directory.GetFiles(backupPath, "*.*", SearchOption.AllDirectories))
            {
                var relativePath = file.Substring(backupPath.Length + 1);
                var item = FindItem(items, relativePath);
                if (item != null)
                {
                    item.IsChecked = true;
                }
            }
        }


        private void AppendLog(string message)
        {
            if (tbLog.Dispatcher.CheckAccess())
            {
                tbLog.AppendText($"{DateTime.Now}: {message}\n");
                tbLog.ScrollToEnd();
            }
            else
            {
                tbLog.Dispatcher.Invoke(() => AppendLog(message));
            }
        }
        private FileTreeItem FindItem(ObservableCollection<FileTreeItem> items, string relativePath)
        {
            string gamePathText = string.Empty;
            Dispatcher.Invoke(() => gamePathText = tbGamePath.Text);

            foreach (var item in items)
            {
                if (item.GetRelativePath(gamePathText) == relativePath)
                {
                    return item;
                }

                var found = FindItem(item.Children, relativePath);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(Properties.Settings.Default.gamePath))
            {
                tbGamePath.Text = Properties.Settings.Default.gamePath;
                LoadFileTree(tbGamePath.Text);
            }
        }
    }
    public class FileTreeItem : INotifyPropertyChanged
    {
        private bool? _isChecked;
        private bool _isUpdating = false;
        private bool _isUnloaded = false;

        public string Name { get; set; } = string.Empty;
        public bool? IsChecked
        {
            get => _isChecked;
            set
            {
                SetIsChecked(value, true, true);
            }
        }

        public bool IsUnloaded
        {
            get => _isUnloaded;
            set
            {
                if (_isUnloaded != value)
                {
                    _isUnloaded = value;
                    OnPropertyChanged(nameof(IsUnloaded));
                }
            }
        }

        public BitmapImage Icon { get; set; } = new BitmapImage();
        public ObservableCollection<FileTreeItem> Children { get; set; } = new ObservableCollection<FileTreeItem>();
        public FileTreeItem Parent { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        public void SetIsChecked(bool? value, bool updateChildren, bool updateParent)
        {
            if (_isChecked != value)
            {
                _isChecked = value;

                if (!_isUpdating)
                {
                    _isUpdating = true;

                    if (updateChildren && _isChecked.HasValue)
                    {
                        foreach (var child in Children)
                        {
                            child.SetIsChecked(_isChecked, true, false);
                        }
                    }

                    if (updateParent)
                    {
                        Parent?.UpdateCheckStateFromChildren();
                    }

                    _isUpdating = false;

                    OnPropertyChanged(nameof(IsChecked));
                }
            }
        }

        public void SetIsUnloaded(bool value)
        {
            if (_isUnloaded != value)
            {
                _isUnloaded = value;
                OnPropertyChanged(nameof(IsUnloaded));

                foreach (var child in Children)
                {
                    child.SetIsUnloaded(value);
                }
            }
        }


        public void UpdateCheckStateFromChildren()
        {
            if (_isUpdating)
                return;

            if (Children != null && Children.Count > 0)
            {
                bool allChecked = Children.All(c => c.IsChecked == true);
                bool noneChecked = Children.All(c => c.IsChecked == false);
                bool allUnloaded = Children.All(c => c.IsUnloaded == true);

                bool? state = allChecked ? true : noneChecked ? (bool?)false : null;

                if (_isChecked != state)
                {
                    _isChecked = state;
                    OnPropertyChanged(nameof(IsChecked));
                }

                if (_isUnloaded != allUnloaded)
                {
                    _isUnloaded = allUnloaded;
                    OnPropertyChanged(nameof(IsUnloaded));
                }

                Parent?.UpdateCheckStateFromChildren();
            }
        }

        protected void OnPropertyChanged(string propertyName)
        {
            if (!_isUpdating)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public string GetFullPath()
        {
            return Parent == null ? Name : Path.Combine(Parent.GetFullPath(), Name);
        }

        public string GetRelativePath(string basePath)
        {
            return GetFullPath().Substring(basePath.Length + 1);
        }
    }

    public class UnloadedToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isUnloaded && isUnloaded)
            {
                return Brushes.Gray;
            }
            return Brushes.Black;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class UnloadedToTextDecorationConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isUnloaded && isUnloaded)
            {
                return TextDecorations.Strikethrough;
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}