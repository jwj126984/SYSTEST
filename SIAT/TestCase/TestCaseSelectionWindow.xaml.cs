using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace SIAT
{
    /// <summary>
    /// TestCaseSelectionWindow.xaml 的交互逻辑
    /// 用于在测试界面选择测试用例：列出已有用例，双击或点击"打开"确认选择
    /// </summary>
    public partial class TestCaseSelectionWindow : Window, INotifyPropertyChanged
    {
        private ObservableCollection<TestCaseConfig> _testCases = new ObservableCollection<TestCaseConfig>();
        private ObservableCollection<TestCaseConfig> _filteredTestCases = new ObservableCollection<TestCaseConfig>();
        private TestCaseConfig? _selectedTestCase;

        /// <summary>
        /// 用户确认选择后的用例文件完整路径
        /// </summary>
        public string SelectedTestCasePath { get; private set; } = string.Empty;

        public ObservableCollection<TestCaseConfig> TestCases
        {
            get { return _testCases; }
            set { _testCases = value; OnPropertyChanged(nameof(TestCases)); }
        }

        public ObservableCollection<TestCaseConfig> FilteredTestCases
        {
            get { return _filteredTestCases; }
            set { _filteredTestCases = value; OnPropertyChanged(nameof(FilteredTestCases)); }
        }

        public TestCaseConfig? SelectedTestCase
        {
            get { return _selectedTestCase; }
            set { _selectedTestCase = value; OnPropertyChanged(nameof(SelectedTestCase)); UpdateOpenButtonState(); }
        }

        public TestCaseSelectionWindow()
        {
            InitializeComponent();
            DataContext = this;
            LoadTestCases();
            UpdateOpenButtonState();
        }

        /// <summary>
        /// 从 TestCases 目录加载所有 .testcase 文件
        /// </summary>
        private void LoadTestCases()
        {
            TestCases.Clear();
            FilteredTestCases.Clear();

            string testCasesFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestCases");
            if (!Directory.Exists(testCasesFolder))
            {
                Directory.CreateDirectory(testCasesFolder);
                StatusText.Text = "未找到任何用例";
                return;
            }

            var testCaseFiles = Directory.GetFiles(testCasesFolder, "*.testcase");
            foreach (var filePath in testCaseFiles)
            {
                try
                {
                    var testCaseConfig = XmlHelper.DeserializeFromFile<TestCaseConfig>(filePath);
                    if (testCaseConfig != null)
                    {
                        TestCases.Add(testCaseConfig);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"加载用例文件失败: {filePath}, 错误: {ex.Message}");
                }
            }

            FilteredTestCases = new ObservableCollection<TestCaseConfig>(TestCases);
            StatusText.Text = $"共 {TestCases.Count} 个用例";
        }

        private void UpdateOpenButtonState()
        {
            OpenButton.IsEnabled = SelectedTestCase != null;
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterTestCases();
        }

        private void FilterTestCases()
        {
            string searchText = SearchTextBox.Text.Trim().ToLower();

            if (string.IsNullOrEmpty(searchText))
            {
                FilteredTestCases = new ObservableCollection<TestCaseConfig>(TestCases);
            }
            else
            {
                var filtered = TestCases.Where(tc =>
                    tc.Name.ToLower().Contains(searchText) ||
                    tc.Description.ToLower().Contains(searchText)
                ).ToList();
                FilteredTestCases = new ObservableCollection<TestCaseConfig>(filtered);
            }

            StatusText.Text = $"匹配 {FilteredTestCases.Count} / {TestCases.Count} 个用例";
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadTestCases();
            SearchTextBox.Text = string.Empty;
        }

        private void TestCaseList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TestCaseList.SelectedItem is TestCaseConfig testCase)
            {
                SelectedTestCase = testCase;
            }
            else
            {
                SelectedTestCase = null;
            }
        }

        private void TestCaseList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (SelectedTestCase != null)
            {
                ConfirmSelection();
            }
        }

        private void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedTestCase != null)
            {
                ConfirmSelection();
            }
        }

        /// <summary>
        /// 确认选择当前用例，计算文件路径并关闭窗口
        /// </summary>
        private void ConfirmSelection()
        {
            if (SelectedTestCase == null) return;

            string testCasesFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestCases");
            string testCaseFileName = SelectedTestCase.Name + ".testcase";
            string testCasePath = Path.Combine(testCasesFolder, testCaseFileName);

            if (!File.Exists(testCasePath))
            {
                MessageBox.Show("用例文件不存在，可能已被删除", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                LoadTestCases();
                return;
            }

            SelectedTestCasePath = testCasePath;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
