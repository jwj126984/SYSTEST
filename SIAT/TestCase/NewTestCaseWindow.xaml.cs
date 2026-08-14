using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace SIAT
{
    /// <summary>
    /// NewTestCaseWindow.xaml 的交互逻辑
    /// </summary>
    public partial class NewTestCaseWindow : Window
    {
        public string TestCaseName { get; private set; } = string.Empty;
        public string TestCaseDescription { get; private set; } = string.Empty;

        public NewTestCaseWindow()
        {
            InitializeComponent();
            
            // 设置默认值
            TestCaseNameTextBox.Text = "新用例" + DateTime.Now.ToString("yyyyMMddHHmmss");
            TestCaseDescriptionTextBox.Text = string.Empty;
            
            // 初始状态
            UpdateCreateButtonState();
        }

        private void TestCaseNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateCreateButtonState();
        }

        private void UpdateCreateButtonState()
        {
            CreateButton.IsEnabled = !string.IsNullOrWhiteSpace(TestCaseNameTextBox.Text);
        }

        private void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TestCaseNameTextBox.Text))
            {
                MessageBox.Show("请输入用例名称", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                TestCaseNameTextBox.Focus();
                return;
            }

            // 检查用例名称是否已存在
            string testCasesFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestCases");
            if (!Directory.Exists(testCasesFolder))
            {
                Directory.CreateDirectory(testCasesFolder);
            }

            string testCaseFileName = TestCaseNameTextBox.Text.Trim() + ".testcase";
            string testCasePath = Path.Combine(testCasesFolder, testCaseFileName);

            if (File.Exists(testCasePath))
            {
                MessageBox.Show("用例名称已存在，请选择其他名称", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                TestCaseNameTextBox.Focus();
                TestCaseNameTextBox.SelectAll();
                return;
            }

            TestCaseName = TestCaseNameTextBox.Text.Trim();
            TestCaseDescription = TestCaseDescriptionTextBox.Text.Trim();
            
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}