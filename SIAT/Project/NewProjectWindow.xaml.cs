using System.ComponentModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SIAT.TSET;

namespace SIAT
{
    /// <summary>
    /// NewProjectWindow.xaml 的交互逻辑
    /// </summary>
    public partial class NewProjectWindow : Window, INotifyPropertyChanged
    {
        private string _projectName = string.Empty;
        private string _projectDescription = string.Empty;

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

        public NewProjectWindow()
        {
            InitializeComponent();
            DataContext = this;

            // 绑定按钮点击事件
            CreateButton.Click += CreateButton_Click;
            CancelButton.Click += CancelButton_Click;

            // 添加文本框输入限制
            ProjectNameTextBox.PreviewTextInput += ProjectNameTextBox_PreviewTextInput;
            ProjectNameTextBox.TextChanged += ProjectNameTextBox_TextChanged;

            // 窗口加载完成后不执行验证，避免初始显示错误信息
            // Loaded += (sender, e) => ValidateProjectName();
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

            CreateButton.IsEnabled = isValid;

            return isValid;
        }

        private void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            if (ValidateProjectName(true))
            {
                try
                {
                    // 创建项目目录
                    string projectsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Projects");
                    string projectDirectory = Path.Combine(projectsDirectory, ProjectName);
                    
                    if (!Directory.Exists(projectsDirectory))
                    {
                        Directory.CreateDirectory(projectsDirectory);
                    }
                    
                    if (!Directory.Exists(projectDirectory))
                    {
                        Directory.CreateDirectory(projectDirectory);
                    }
                    
                    // 创建项目基本结构
                    CreateProjectStructure(projectDirectory);
                    
                    // 创建项目文件
                    SaveProject(projectDirectory);
                    
                    // 创建成功，创建ProjectConfig对象
                    var projectConfig = new ProjectConfig
                    {
                        Name = ProjectName,
                        Description = ProjectDescription,
                        CreatedDate = DateTime.Now,
                        ModifiedDate = DateTime.Now
                    };
                    
                    // 打开项目编辑界面
                    var projectEditWindow = new ProjectEditWindow(projectConfig, projectDirectory)
                    {
                        Owner = this.Owner
                    };
                    
                    // 设置结果并关闭当前窗口
                    DialogResult = true;
                    Close();
                    
                    // 显示新的项目编辑窗口
                    projectEditWindow.Show();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"创建项目失败: {ex.Message}", "错误", 
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

        private void SaveProject(string projectDirectory)
        {
            // 创建项目配置文件
            string projectConfigPath = Path.Combine(projectDirectory, "project.config");
            
            // 保存项目基本信息
            string projectConfig = $"ProjectName={ProjectName}\n" +
                                  $"ProjectDescription={ProjectDescription}\n" +
                                  $"CreationDate={DateTime.Now:yyyy-MM-dd HH:mm:ss}\n";
            
            File.WriteAllText(projectConfigPath, projectConfig);
        }
    }

    /// <summary>
    /// 项目名称验证规则
    /// </summary>
    public class ProjectNameValidationRule : ValidationRule
    {
        public bool Required { get; set; }

        public override System.Windows.Controls.ValidationResult Validate(object value, System.Globalization.CultureInfo cultureInfo)
        {
            string projectName = value as string ?? string.Empty;

            // 只有当用户开始输入后才进行验证
            if (projectName == null || projectName == string.Empty)
            {
                return System.Windows.Controls.ValidationResult.ValidResult;
            }

            if (Required && string.IsNullOrWhiteSpace(projectName))
            {
                return new System.Windows.Controls.ValidationResult(false, "项目名称不能为空");
            }

            if (!string.IsNullOrWhiteSpace(projectName))
            {
                // 只有当用户输入至少一个字符但不是空白字符时，才检查格式和存在性
                // 长度检查将在ValidateProjectName方法中处理，而不是在这里
                if (projectName.StartsWith(" "))
                {
                    return new System.Windows.Controls.ValidationResult(false, "项目名称不能以空格开头");
                }

                if (!Regex.IsMatch(projectName, @"^[a-zA-Z0-9_\u4e00-\u9fa5]+$"))
                {
                    return new System.Windows.Controls.ValidationResult(false, "项目名称只能包含字母、数字、下划线和中文字符");
                }

                // 检查项目名称是否已存在
                string projectsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Projects");
                if (Directory.Exists(projectsDirectory))
                {
                    if (Directory.Exists(Path.Combine(projectsDirectory, projectName)))
                    {
                        return new System.Windows.Controls.ValidationResult(false, "该项目名称已存在");
                    }
                }
            }

            return System.Windows.Controls.ValidationResult.ValidResult;
        }
    }
}