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

        public TestCaseProject Project
        {
            get { return _project; }
            set { _project = value; OnPropertyChanged(nameof(Project)); }
        }

        public TestCaseProjectEditDialog(TestCaseProject project)
        {
            InitializeComponent();
            DataContext = this;
            
            // 创建项目副本以避免直接修改原始对象
            Project = new TestCaseProject
            {
                Name = project.Name,
                Description = project.Description,
                ProjectPath = project.ProjectPath,
                AddedDate = project.AddedDate
            };
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