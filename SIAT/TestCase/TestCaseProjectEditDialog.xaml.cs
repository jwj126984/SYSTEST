using System;
using System.ComponentModel;
using System.Windows;

namespace SIAT
{
    /// <summary>
    /// TestCaseProjectEditDialog.xaml 的交互逻辑
    /// </summary>
    public partial class TestCaseProjectEditDialog : Window, INotifyPropertyChanged
    {
        private TestCaseProject _project = new TestCaseProject();
        private bool _isNewMode = false;

        public TestCaseProject Project
        {
            get { return _project; }
            set { _project = value; OnPropertyChanged(nameof(Project)); }
        }

        /// <summary>
        /// 是否为新建模式
        /// </summary>
        public bool IsNewMode => _isNewMode;

        public TestCaseProjectEditDialog(TestCaseProject project, bool isNewMode = false)
        {
            InitializeComponent();
            DataContext = this;

            _isNewMode = isNewMode;

            // 创建项目副本以避免直接修改原始对象
            Project = new TestCaseProject
            {
                Name = project.Name,
                Description = project.Description,
                ProjectPath = project.ProjectPath,
                AddedDate = project.AddedDate
            };

            // 新建模式下调整界面：隐藏路径/时间信息，修改标题与按钮文本
            if (_isNewMode)
            {
                Title = "新建测试项";
                if (ProjectInfoPanel != null)
                {
                    ProjectInfoPanel.Visibility = Visibility.Collapsed;
                }
                if (OKButton != null)
                {
                    OKButton.Content = "创建";
                }
            }
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            // 验证项目名称
            if (string.IsNullOrWhiteSpace(Project.Name))
            {
                MessageBox.Show("项目名称不能为空", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                ProjectNameTextBox.Focus();
                return;
            }

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