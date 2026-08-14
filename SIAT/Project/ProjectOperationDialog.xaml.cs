using System;
using System.Windows;
using System.Windows.Controls;

namespace SIAT
{
    /// <summary>
    /// ProjectOperationDialog.xaml 的交互逻辑
    /// </summary>
    public partial class ProjectOperationDialog : Window
    {
        public enum ProjectOperation
        {
            None,
            NewProject,
            OpenProject
        }

        public ProjectOperation SelectedOperation { get; private set; }

        public ProjectOperationDialog()
        {
            InitializeComponent();
            SelectedOperation = ProjectOperation.None;
            
            // 绑定按钮点击事件
            NewProjectButton.Click += NewProjectButton_Click;
            OpenProjectButton.Click += OpenProjectButton_Click;
        }

        private void NewProjectButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedOperation = ProjectOperation.NewProject;
            DialogResult = true;
            Close();
        }

        private void OpenProjectButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedOperation = ProjectOperation.OpenProject;
            DialogResult = true;
            Close();
        }
    }
}