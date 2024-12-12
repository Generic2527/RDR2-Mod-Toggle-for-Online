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

namespace RDR2_Mod_Toggle_for_Online
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void btnDetectPathSteam_Click(object sender, RoutedEventArgs e)
        {
            string defaultPath = "C:\\Program Files (x86)\\Steam\\steamapps\\common\\Red Dead Redemption 2";

            if (System.IO.File.Exists(defaultPath + "\\RDR2.exe"))
            {
                tbGamePath.Text = defaultPath;
                LoadFileTree(tbGamePath.Text);
            }
            else
            {
                MessageBox.Show("Red Dead Redemption 2 is not installed in the default Steam directory. Please click the Browse button to manually specify the installation path.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            }

            LoadFileTree(tbGamePath.Text);
        }

        private void CheckBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is CheckBox checkBox && checkBox.DataContext is FileTreeItem item)
            {
                bool? newValue = (item.IsChecked == true) ? false : true;
                item.SetIsChecked(newValue, true, true);
                e.Handled = true;
            }
        }

        private void LoadFileTree(string path)
        {
            var root = new FileTreeItem { Name = path, IsChecked = false, Icon = IconHelper.GetIcon(path, true) };
            LoadChildren(root, path);
            fileTreeView.ItemsSource = root.Children;
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

            UpdateCheckState(item);
        }

        private void UpdateCheckState(FileTreeItem item)
        {
            if (item.Children.Any())
            {
                bool allChecked = item.Children.All(child => child.IsChecked == true);
                bool noneChecked = item.Children.All(child => child.IsChecked == false);
                item.IsChecked = allChecked ? true : noneChecked ? (bool?)false : null;
            }
        }

        private void btnUnloadMods_Click(object sender, RoutedEventArgs e)
        {
            var backupPath = Path.Combine(Directory.GetCurrentDirectory(), "Backup");
            try
            {
                BackupCheckedItems(fileTreeView.ItemsSource as ObservableCollection<FileTreeItem>, backupPath);

                // Remove all files and directories in the game directory
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
                    }
                }

                MessageBox.Show("Mods have been unloaded successfully. You can restore them at any time by clicking the Load Mods button.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (IOException ex)
            {
                MessageBox.Show("An error occurred while unloading mods. Please make sure that the game is not running and that no files are in use.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnLoadMods_Click(object sender, RoutedEventArgs e)
        {
            var backupPath = Path.Combine(Directory.GetCurrentDirectory(), "Backup");
            RestoreCheckedItems(backupPath, tbGamePath.Text);
        }

        private void BackupCheckedItems(ObservableCollection<FileTreeItem> items, string backupPath)
        {
            foreach (var item in items)
            {
                if (item.IsChecked == true)
                {
                    var sourcePath = item.GetFullPath();
                    var destinationPath = Path.Combine(backupPath, item.GetRelativePath(tbGamePath.Text));
                    if (Directory.Exists(sourcePath))
                    {
                        DirectoryCopy(sourcePath, destinationPath, true);
                    }
                    else if (File.Exists(sourcePath))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                        File.Copy(sourcePath, destinationPath, true);
                    }
                }
                BackupCheckedItems(item.Children, backupPath);
            }
        }

        private void RestoreCheckedItems(string backupPath, string restorePath)
        {
            foreach (var directory in Directory.GetDirectories(backupPath, "*", SearchOption.AllDirectories))
            {
                var relativePath = directory.Substring(backupPath.Length + 1);
                var destinationPath = Path.Combine(restorePath, relativePath);
                Directory.CreateDirectory(destinationPath);
            }

            foreach (var file in Directory.GetFiles(backupPath, "*.*", SearchOption.AllDirectories))
            {
                var relativePath = file.Substring(backupPath.Length + 1);
                var destinationPath = Path.Combine(restorePath, relativePath);
                File.Copy(file, destinationPath, true);
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
                file.CopyTo(tempPath, false);
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
    }
    public class FileTreeItem : INotifyPropertyChanged
    {
        private bool? _isChecked;
        private bool _isUpdating = false;

        public string Name { get; set; } = string.Empty;
        public bool? IsChecked
        {
            get => _isChecked;
            set
            {
                SetIsChecked(value, true, true);
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

        public void UpdateCheckStateFromChildren()
        {
            if (_isUpdating)
                return;

            if (Children != null && Children.Count > 0)
            {
                bool allChecked = Children.All(c => c.IsChecked == true);
                bool noneChecked = Children.All(c => c.IsChecked == false);

                bool? state = allChecked ? true : noneChecked ? (bool?)false : null;

                if (_isChecked != state)
                {
                    _isChecked = state;
                    OnPropertyChanged(nameof(IsChecked));

                    Parent?.UpdateCheckStateFromChildren();
                }
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
}