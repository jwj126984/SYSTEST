using System;
using System.ComponentModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SIAT
{
    /// <summary>
    /// SaveAsProjectWindow.xaml 的交互逻辑
    /// </summary>
    public partial class SaveAsProjectWindow : Window, INotifyPropertyChanged
    {
        private string _projectName = string.Empty;
        private string _projectDescription = string.Empty;
        private ProjectConfig _originalProjectConfig;
        private string _originalProjectPath;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string ProjectName
        {
            get { return _projectName; }
            set
            {
                if (_projectName != value)
                {
                    _projectName = value;
                    OnPropertyChanged(nameof(ProjectName));
                    ValidateProjectName();
                }
            }
        }

        public string ProjectDescription
        {
            get { return _projectDescription; }
            set
            {
                if (_projectDescription != value)
                {
                    _projectDescription = value;
                    OnPropertyChanged(nameof(ProjectDescription));
                }
            }
        }

        public ProjectConfig NewProjectConfig { get; private set; }
        public string NewProjectPath { get; private set; }

        public SaveAsProjectWindow(ProjectConfig originalProjectConfig, string originalProjectPath)
        {
            InitializeComponent();
            DataContext = this;

            _originalProjectConfig = originalProjectConfig;
            _originalProjectPath = originalProjectPath;

            // 初始化默认值为原始项目的名称和描述
            ProjectName = originalProjectConfig.Name;
            ProjectDescription = originalProjectConfig.Description;

            // 绑定按钮点击事件
            SaveButton.Click += SaveButton_Click;
            CancelButton.Click += CancelButton_Click;

            // 添加文本框输入限制
            ProjectNameTextBox.PreviewTextInput += ProjectNameTextBox_PreviewTextInput;
            ProjectNameTextBox.TextChanged += ProjectNameTextBox_TextChanged;
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void ProjectNameTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // 只允许字母、数字、下划线和中文字符
            e.Handled = !Regex.IsMatch(e.Text, @"^[a-zA-Z0-9_\u4e00-\u9fa5]+$");
        }

        private void ProjectNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ValidateProjectName();
        }

        private bool ValidateProjectName(bool isFinalValidation = false)
        {
            bool isValid = true;
            string errorMessage = string.Empty;

            // 检查项目名称是否为空或空白字符
            if (string.IsNullOrWhiteSpace(ProjectName))
            {
                if (isFinalValidation)
                {
                    isValid = false;
                    errorMessage = "项目名称不能为空";
                }
            }
            // 长度检查只在最终验证时进行
            else if (isFinalValidation && ProjectName.Length < 3)
            {
                isValid = false;
                errorMessage = "项目名称长度不能少于3个字符";
            }
            else if (ProjectName.Length > 50)
            {
                isValid = false;
                errorMessage = "项目名称长度不能超过50个字符";
            }
            else if (Regex.IsMatch(ProjectName, @"^\d+$") || ProjectName.StartsWith(" "))
            {
                isValid = false;
                errorMessage = "项目名称不能以数字或空格开头";
            }
            else
            {
                // 检查项目名称是否已存在
                string projectsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Projects");
                if (Directory.Exists(projectsDirectory))
                {
                    if (Directory.Exists(Path.Combine(projectsDirectory, ProjectName)))
                    {
                        isValid = false;
                        errorMessage = "该项目名称已存在";
                    }
                }
            }

            // 实时验证时，只有格式和存在性错误才显示
            // 最终验证时，所有错误都显示
            if (isFinalValidation || (!string.IsNullOrEmpty(ProjectName) && !string.IsNullOrWhiteSpace(ProjectName)))
            {
                ProjectNameErrorText.Text = errorMessage;
            }
            else
            {
                ProjectNameErrorText.Text = "";
            }

            SaveButton.IsEnabled = isValid;

            return isValid;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (ValidateProjectName(true))
            {
                try
                {
                    // 创建项目目录
                    string projectsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Projects");
                    string newProjectDirectory = Path.Combine(projectsDirectory, ProjectName);
                    
                    if (!Directory.Exists(projectsDirectory))
                    {
                        Directory.CreateDirectory(projectsDirectory);
                    }
                    
                    if (Directory.Exists(newProjectDirectory))
                    {
                        // 如果目录已存在，删除它（这里应该不会发生，因为前面已经验证过了）
                        Directory.Delete(newProjectDirectory, true);
                    }
                    
                    // 创建新的项目目录
                    Directory.CreateDirectory(newProjectDirectory);
                    
                    // 创建项目子目录结构
                    CreateProjectStructure(newProjectDirectory);
                    
                    // 复制原始项目的文件到新目录
                    CopyProjectFiles(_originalProjectPath, newProjectDirectory);
                    
                    // 创建新的项目配置
                    NewProjectConfig = new ProjectConfig
                    {
                        Name = ProjectName,
                        Description = ProjectDescription,
                        CreatedDate = _originalProjectConfig.CreatedDate,
                        ModifiedDate = DateTime.Now
                    };
                    
                    // 保存新的项目配置
                    SaveProjectConfig(newProjectDirectory, NewProjectConfig);
                    
                    // 更新新项目路径
                    NewProjectPath = newProjectDirectory;
                    
                    // 设置结果并关闭窗口
                    DialogResult = true;
                    Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"保存项目失败: {ex.Message}", "错误", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void CreateProjectStructure(string projectDirectory)
        {
            // 创建项目子目录结构
            Directory.CreateDirectory(Path.Combine(projectDirectory, "Steps"));
            Directory.CreateDirectory(Path.Combine(projectDirectory, "Variables"));
            Directory.CreateDirectory(Path.Combine(projectDirectory, "TestCases"));
            Directory.CreateDirectory(Path.Combine(projectDirectory, "Resources"));
        }

        private void CopyProjectFiles(string sourceDirectory, string destinationDirectory)
        {
            // 复制目录中的所有文件
            foreach (string file in Directory.GetFiles(sourceDirectory))
            {
                string fileName = Path.GetFileName(file);
                string destinationFile = Path.Combine(destinationDirectory, fileName);
                File.Copy(file, destinationFile, true);
            }
            
            // 复制子目录
            foreach (string directory in Directory.GetDirectories(sourceDirectory))
            {
                string directoryName = Path.GetFileName(directory);
                string destinationSubDirectory = Path.Combine(destinationDirectory, directoryName);
                
                if (!Directory.Exists(destinationSubDirectory))
                {
                    Directory.CreateDirectory(destinationSubDirectory);
                }
                
                CopyProjectFiles(directory, destinationSubDirectory);
            }
        }

        private void SaveProjectConfig(string projectDirectory, ProjectConfig projectConfig)
        {
            // 创建项目配置文件
            string projectConfigPath = Path.Combine(projectDirectory, "project.config");
            
            // 保存项目基本信息
            string configContent = $"ProjectName={projectConfig.Name}\n" +
                                  $"ProjectDescription={projectConfig.Description}\n" +
                                  $"CreationDate={projectConfig.CreatedDate.ToString("yyyy-MM-dd HH:mm:ss")}\n" +
                                  $"ModifiedDate={projectConfig.ModifiedDate.ToString("yyyy-MM-dd HH:mm:ss")}\n";
            
            File.WriteAllText(projectConfigPath, configContent);
        }
    }
}
