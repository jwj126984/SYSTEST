using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace SIAT
{
    /// <summary>
    /// SaveAsTestCaseWindow.xaml 的交互逻辑
    /// </summary>
    public partial class SaveAsTestCaseWindow : Window
    {
        public string TestCaseName { get; private set; } = string.Empty;
        public string TestCaseDescription { get; private set; } = string.Empty;
        public string NewTestCasePath { get; private set; } = string.Empty;

        public SaveAsTestCaseWindow(TestCaseConfig originalTestCaseConfig)
        {
            InitializeComponent();
            
            // 设置默认值为原始测试用例的名称和描述
            TestCaseNameTextBox.Text = originalTestCaseConfig.Name + "_副本";
            TestCaseDescriptionTextBox.Text = originalTestCaseConfig.Description;
            
            // 初始状态
            UpdateSaveButtonState();
            
            // 绑定事件
            TestCaseNameTextBox.TextChanged += TestCaseNameTextBox_TextChanged;
            SaveButton.Click += SaveButton_Click;
            CancelButton.Click += CancelButton_Click;
        }

        private void TestCaseNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateSaveButtonState();
            ValidateTestCaseName();
        }

        private void UpdateSaveButtonState()
        {
            SaveButton.IsEnabled = !string.IsNullOrWhiteSpace(TestCaseNameTextBox.Text);
        }

        private bool ValidateTestCaseName(bool isFinalValidation = false)
        {
            bool isValid = true;
            string errorMessage = string.Empty;

            string testCaseName = TestCaseNameTextBox.Text.Trim();

            // 检查用例名称是否为空或空白字符
            if (string.IsNullOrWhiteSpace(testCaseName))
            {
                if (isFinalValidation)
                {
                    isValid = false;
                    errorMessage = "用例名称不能为空";
                }
            }
            // 检查用例名称长度
            else if (testCaseName.Length < 1)
            {
                isValid = false;
                errorMessage = "用例名称长度不能少于1个字符";
            }
            else if (testCaseName.Length > 50)
            {
                isValid = false;
                errorMessage = "用例名称长度不能超过50个字符";
            }
            else
            {
                // 检查用例名称是否已存在
                string testCasesFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestCases");
                if (Directory.Exists(testCasesFolder))
                {
                    string testCaseFileName = testCaseName + ".testcase";
                    string testCasePath = Path.Combine(testCasesFolder, testCaseFileName);

                    if (File.Exists(testCasePath))
                    {
                        isValid = false;
                        errorMessage = "用例名称已存在，请选择其他名称";
                    }
                }
            }

            // 显示错误信息
            TestCaseNameErrorText.Text = errorMessage;

            return isValid;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (ValidateTestCaseName(true))
            {
                string testCaseName = TestCaseNameTextBox.Text.Trim();
                string testCaseDescription = TestCaseDescriptionTextBox.Text.Trim();
                
                // 确保测试用例文件夹存在
                string testCasesFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestCases");
                if (!Directory.Exists(testCasesFolder))
                {
                    Directory.CreateDirectory(testCasesFolder);
                }

                // 构建新的测试用例路径
                string testCaseFileName = testCaseName + ".testcase";
                string testCasePath = Path.Combine(testCasesFolder, testCaseFileName);

                TestCaseName = testCaseName;
                TestCaseDescription = testCaseDescription;
                NewTestCasePath = testCasePath;
                
                DialogResult = true;
                Close();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
