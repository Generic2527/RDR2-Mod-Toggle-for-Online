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

            // if "RDR2.exe" exists in the default path then set the path to the default path
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
            // 폴더 브라우저 다이얼로그를 사용하여 경로 선택
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
                // 현재 체크 상태에 따라 토글
                bool? newValue = (item.IsChecked == true) ? false : true;

                // SetIsChecked 메서드를 사용하여 업데이트
                item.SetIsChecked(newValue, true, true);

                // 이벤트가 계속 전달되지 않도록 처리
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
                    Parent = item // Parent 속성 설정
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

                    // 자식들에게 체크 상태 전파
                    if (updateChildren && _isChecked.HasValue)
                    {
                        foreach (var child in Children)
                        {
                            child.SetIsChecked(_isChecked, true, false);
                        }
                    }

                    // 부모의 체크 상태 업데이트
                    if (updateParent)
                    {
                        Parent?.UpdateCheckStateFromChildren();
                    }

                    _isUpdating = false;

                    // 변경 알림
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

                    // 부모에게 전파
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
    }
}