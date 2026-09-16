using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SIAT
{
    /// <summary>
    /// 用例项目项（带索引）
    /// </summary>
    [Serializable]
    public class TestCaseProjectItem : INotifyPropertyChanged
    {
        private int _index;
        private TestCaseProject _project = new TestCaseProject();

        public int Index
        {
            get { return _index; }
            set { _index = value; OnPropertyChanged(nameof(Index)); }
        }

        public TestCaseProject Project
        {
            get { return _project; }
            set { _project = value; OnPropertyChanged(nameof(Project)); }
        }

        // 用于绑定的属性
        public string Name
        {
            get { return _project.Name; }
            set { _project.Name = value; OnPropertyChanged(nameof(Name)); }
        }

        public string Description
        {
            get { return _project.Description; }
            set { _project.Description = value; OnPropertyChanged(nameof(Description)); }
        }

        public string ProjectPath
        {
            get { return _project.ProjectPath; }
            set { _project.ProjectPath = value; OnPropertyChanged(nameof(ProjectPath)); }
        }

        public DateTime AddedDate
        {
            get { return _project.AddedDate; }
            set { _project.AddedDate = value; OnPropertyChanged(nameof(AddedDate)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// TestCaseEditWindow.xaml 的交互逻辑
    /// </summary>
    public partial class TestCaseEditWindow : Window, INotifyPropertyChanged
    {
        private string _testCasePath;
        private TestCaseConfig _testCaseConfig = new TestCaseConfig();
        private ObservableCollection<ProjectConfig> _projects = new ObservableCollection<ProjectConfig>();
        private ObservableCollection<ProjectConfig> _filteredProjects = new ObservableCollection<ProjectConfig>();
        private ObservableCollection<TestCaseProjectItem> _testCaseProjects = new ObservableCollection<TestCaseProjectItem>();
        private TestCaseProjectItem? _selectedTestCaseProject;
        private ProjectConfig? _selectedProject;
        private string _projectSearchText = string.Empty;
        private bool _isModified = false;
        
        // 合格值判断相关属性
        private ObservableCollection<ProjectVariable> _selectedTestCaseProjectVariables = new ObservableCollection<ProjectVariable>();
        private ProjectVariable? _selectedVariable;
        private bool _isSingleValue = true;
        private bool _isRange = false;
        private string _singleValue = string.Empty;
        private string _rangeStart = string.Empty;
        private string _rangeEnd = string.Empty;

        public TestCaseConfig TestCaseConfig
        {
            get { return _testCaseConfig; }
            set { _testCaseConfig = value; OnPropertyChanged(nameof(TestCaseConfig)); }
        }

        public ObservableCollection<ProjectConfig> Projects
        {
            get { return _projects; }
            set { _projects = value; OnPropertyChanged(nameof(Projects)); }
        }

        public ObservableCollection<ProjectConfig> FilteredProjects
        {
            get { return _filteredProjects; }
            set { _filteredProjects = value; OnPropertyChanged(nameof(FilteredProjects)); }
        }

        public ObservableCollection<TestCaseProjectItem> TestCaseProjects
        {
            get { return _testCaseProjects; }
            set { _testCaseProjects = value; OnPropertyChanged(nameof(TestCaseProjects)); UpdateButtonStates(); }
        }

        public TestCaseProjectItem? SelectedTestCaseProject
        {
            get { return _selectedTestCaseProject; }
            set { _selectedTestCaseProject = value; OnPropertyChanged(nameof(SelectedTestCaseProject)); UpdateButtonStates(); }
        }

        public ProjectConfig? SelectedProject
        {
            get { return _selectedProject; }
            set { _selectedProject = value; OnPropertyChanged(nameof(SelectedProject)); }
        }

        public bool IsModified
        {
            get { return _isModified; }
            set { _isModified = value; OnPropertyChanged(nameof(IsModified)); UpdateTitle(); }
        }

        // 合格值判断相关属性
        public ObservableCollection<ProjectVariable> SelectedTestCaseProjectVariables
        {
            get { return _selectedTestCaseProjectVariables; }
            set { _selectedTestCaseProjectVariables = value; OnPropertyChanged(nameof(SelectedTestCaseProjectVariables)); }
        }

        public ProjectVariable? SelectedVariable
        {
            get { return _selectedVariable; }
            set { _selectedVariable = value; OnPropertyChanged(nameof(SelectedVariable)); UpdateInputControlsVisibility(); }
        }

        public bool IsSingleValue
        {
            get { return _isSingleValue; }
            set { _isSingleValue = value; OnPropertyChanged(nameof(IsSingleValue)); }
        }

        public bool IsRange
        {
            get { return _isRange; }
            set { _isRange = value; OnPropertyChanged(nameof(IsRange)); }
        }

        public string SingleValue
        {
            get { return _singleValue; }
            set { _singleValue = value; OnPropertyChanged(nameof(SingleValue)); }
        }

        public string RangeStart
        {
            get { return _rangeStart; }
            set { _rangeStart = value; OnPropertyChanged(nameof(RangeStart)); }
        }

        public string RangeEnd
        {
            get { return _rangeEnd; }
            set { _rangeEnd = value; OnPropertyChanged(nameof(RangeEnd)); }
        }

        public TestCaseEditWindow(string testCasePath)
        {
            InitializeComponent();
            DataContext = this;
            
            _testCasePath = testCasePath;
            LoadTestCase();
            LoadProjects();
            UpdateButtonStates();
            UpdateTitle();
        }

        private void LoadTestCase()
        {
            try
            {
                TestCaseConfig = XmlHelper.DeserializeFromFile<TestCaseConfig>(_testCasePath) ?? new TestCaseConfig();
                
                // 加载用例中的项目
                TestCaseProjects.Clear();
                for (int i = 0; i < TestCaseConfig.Projects.Count; i++)
                {
                    TestCaseProjects.Add(new TestCaseProjectItem
                    {
                        Index = i + 1,
                        Project = TestCaseConfig.Projects[i]
                    });
                }
                
                // 更新所有项目的索引和Order属性
                UpdateProjectIndexes();
                
                IsModified = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载用例失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                TestCaseConfig = new TestCaseConfig();
            }
        }

        private void LoadProjects()
        {
            Projects.Clear();
            FilteredProjects.Clear();

            string projectsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Projects");
            if (!Directory.Exists(projectsFolder))
            {
                Directory.CreateDirectory(projectsFolder);
                return;
            }

            var projectFolders = Directory.GetDirectories(projectsFolder);
            foreach (var folder in projectFolders)
            {
                try
                {
                    string configPath = Path.Combine(folder, "project.config");
                    if (File.Exists(configPath))
                    {
                        // 使用现有的项目解析逻辑
                        var projectConfig = ParseProjectConfig(configPath, folder);
                        if (projectConfig != null)
                        {
                            Projects.Add(projectConfig);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // 忽略无法加载的项目
                    System.Diagnostics.Debug.WriteLine($"加载项目失败: {folder}, 错误: {ex.Message}");
                }
            }

            FilteredProjects = new ObservableCollection<ProjectConfig>(Projects);
        }

        /// <summary>
        /// 解析项目配置文件
        /// </summary>
        /// <param name="configFile">配置文件路径</param>
        /// <param name="projectDir">项目目录</param>
        /// <returns>项目对象</returns>
        private static ProjectConfig? ParseProjectConfig(string configFile, string projectDir)
        {
            try
            {
                string[] lines = File.ReadAllLines(configFile);
                var projectConfig = new ProjectConfig
                {
                    Name = Path.GetFileName(projectDir), // 默认使用文件夹名称
                    Description = "",
                    ProjectPath = projectDir,
                    CreatedDate = Directory.GetCreationTime(projectDir),
                    ModifiedDate = Directory.GetLastWriteTime(projectDir)
                };

                foreach (string line in lines)
                {
                    string trimmedLine = line.Trim();
                    if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith('#'))
                        continue;

                    string[] parts = trimmedLine.Split(new[] { '=' }, 2);
                    if (parts.Length == 2)
                    {
                        string key = parts[0].Trim();
                        string value = parts[1].Trim();

                        switch (key)
                        {
                            case "ProjectName":
                                projectConfig.Name = value;
                                break;
                            case "ProjectDescription":
                                projectConfig.Description = value;
                                break;
                            case "CreationDate":
                                if (DateTime.TryParse(value, out var creationDate))
                                {
                                    projectConfig.CreatedDate = creationDate;
                                    projectConfig.ModifiedDate = creationDate;
                                }
                                break;
                        }
                    }
                }

                return projectConfig;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"解析项目配置文件失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }
        }

        private void UpdateButtonStates()
        {
            bool hasSelectedProject = SelectedTestCaseProject != null;
            bool hasMultipleProjects = TestCaseProjects.Count > 1;
            
            MoveUpButton.IsEnabled = hasSelectedProject && SelectedTestCaseProject!.Index > 1;
            MoveDownButton.IsEnabled = hasSelectedProject && SelectedTestCaseProject!.Index < TestCaseProjects.Count;
            RemoveButton.IsEnabled = hasSelectedProject;
        }

        private void UpdateTitle()
        {
            string modifiedIndicator = IsModified ? "*" : "";
            Title = $"用例编辑 - {TestCaseConfig.Name}{modifiedIndicator}";
        }

        private void ProjectSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterProjects();
            UpdateSearchPlaceholder();
        }

        private void UpdateSearchPlaceholder()
        {
            if (SearchPlaceholder != null)
            {
                SearchPlaceholder.Visibility = string.IsNullOrEmpty(ProjectSearchTextBox.Text) ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void FilterProjects()
        {
            string searchText = ProjectSearchTextBox.Text.Trim().ToLower();
            
            if (string.IsNullOrEmpty(searchText))
            {
                FilteredProjects = new ObservableCollection<ProjectConfig>(Projects);
            }
            else
            {
                var filtered = Projects.Where(p => 
                    p.Name.ToLower().Contains(searchText) || 
                    p.Description.ToLower().Contains(searchText)
                ).ToList();
                FilteredProjects = new ObservableCollection<ProjectConfig>(filtered);
            }
        }

        private void ProjectList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProjectList.SelectedItem is ProjectConfig projectConfig)
            {
                SelectedProject = projectConfig;
            }
            else
            {
                SelectedProject = null;
            }
        }

        private void ProjectList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (SelectedProject != null)
            {
                AddProjectToTestCase();
            }
        }

        private void AddProjectButton_Click(object sender, RoutedEventArgs e)
        {
            AddProjectToTestCase();
        }

        /// <summary>
        /// 新建测试项按钮点击：弹出对话框输入名称/描述，在用例下记录待创建的空白测试项
        /// 不立即创建文件夹，保存用例时才生成物理文件夹
        /// </summary>
        private void NewProjectButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new TestCaseProjectEditDialog(new TestCaseProject(), isNewMode: true)
            {
                Owner = this
            };

            if (dialog.ShowDialog() != true) return;

            string name = dialog.Project.Name;
            string description = dialog.Project.Description;

            // 不立即创建文件夹：只在内存中记录，保存时才创建
            var testCaseProject = new TestCaseProject
            {
                Name = name,
                Description = description,
                ProjectPath = string.Empty, // 留空，保存时才创建
                AddedDate = DateTime.Now,
                IsPendingCreate = true,
                IsNewBlank = true
            };

            var testCaseProjectItem = new TestCaseProjectItem
            {
                Project = testCaseProject
            };

            TestCaseProjects.Add(testCaseProjectItem);
            UpdateProjectIndexes();
            IsModified = true;

            StatusText.Text = $"已添加测试项: {name}（保存后可双击编辑）";
        }

        private void AddProjectToTestCase()
        {
            if (SelectedProject == null) return;

            // 不立即创建文件夹：只在内存中记录模板源，保存时才创建物理文件夹
            string baseName = string.IsNullOrWhiteSpace(SelectedProject.Name) ? "测试项" : SelectedProject.Name;

            var testCaseProject = new TestCaseProject
            {
                Name = baseName,
                Description = SelectedProject.Description,
                ProjectPath = string.Empty, // 留空，保存时才创建
                AddedDate = DateTime.Now,
                SourceTemplatePath = SelectedProject.ProjectPath,
                IsPendingCreate = true,
                IsNewBlank = false
            };

            var testCaseProjectItem = new TestCaseProjectItem
            {
                Project = testCaseProject
            };

            // 如果有选中的测试用例项目，则插入到选中项目下方；否则添加到末尾
            if (SelectedTestCaseProject != null)
            {
                int insertIndex = TestCaseProjects.IndexOf(SelectedTestCaseProject) + 1;
                if (insertIndex <= TestCaseProjects.Count)
                {
                    TestCaseProjects.Insert(insertIndex, testCaseProjectItem);
                }
                else
                {
                    TestCaseProjects.Add(testCaseProjectItem);
                }
            }
            else
            {
                TestCaseProjects.Add(testCaseProjectItem);
            }

            // 更新所有项目的索引
            UpdateProjectIndexes();
            IsModified = true;

            StatusText.Text = $"已添加测试项: {baseName}（保存后生效）";
        }

        /// <summary>
        /// 获取当前用例专属的测试项目录：TestCases\{用例名}\Projects\
        /// 每个用例的测试项独立存储在该目录下，互不共享
        /// </summary>
        private string GetTestCaseProjectsDir()
        {
            string testCaseName = Path.GetFileNameWithoutExtension(_testCasePath);
            string testCaseParent = Path.GetDirectoryName(_testCasePath) ?? AppDomain.CurrentDomain.BaseDirectory;
            string testCaseDir = Path.Combine(testCaseParent, testCaseName);
            string projectsDir = Path.Combine(testCaseDir, "Projects");
            if (!Directory.Exists(projectsDir))
            {
                Directory.CreateDirectory(projectsDir);
            }
            return projectsDir;
        }

        /// <summary>
        /// 从全局模板复制一份独立副本到当前用例目录，返回新目录路径与最终名称
        /// 目录名用后缀保持文件系统唯一，但显示名/文件名/config名都用原名(无后缀)
        /// </summary>
        private string CopyTemplateToTestCase(ProjectConfig template, out string finalName)
        {
            string projectsDir = GetTestCaseProjectsDir();
            string baseName = string.IsNullOrWhiteSpace(template.Name) ? "测试项" : template.Name;
            string destName = baseName;
            string destDir = Path.Combine(projectsDir, destName);

            // 避免同用例下重名：仅目录名加后缀保持文件系统唯一
            int suffix = 1;
            while (Directory.Exists(destDir))
            {
                suffix++;
                destName = $"{baseName}_{suffix}";
                destDir = Path.Combine(projectsDir, destName);
            }

            Directory.CreateDirectory(destDir);

            string templateDir = template.ProjectPath;
            if (Directory.Exists(templateDir))
            {
                foreach (var file in Directory.GetFiles(templateDir, "*", SearchOption.AllDirectories))
                {
                    string rel = Path.GetRelativePath(templateDir, file);
                    string destFile = Path.Combine(destDir, rel);
                    Directory.CreateDirectory(Path.GetDirectoryName(destFile));
                    File.Copy(file, destFile, true);
                }

                // 文件名保持原名(与 baseName 一致)，仅更新 project.config 中的 ProjectName 为原名
                UpdateProjectConfigName(destDir, baseName);
            }

            // 显示名用原名(无后缀)
            finalName = baseName;
            return destDir;
        }

        /// <summary>
        /// 在当前用例下创建一个空白测试项，返回新目录路径与最终名称
        /// 目录名用后缀保持文件系统唯一，但显示名/config名都用原名(无后缀)
        /// </summary>
        private string CreateNewProjectInTestCase(string projectName, string description, out string finalName)
        {
            string projectsDir = GetTestCaseProjectsDir();
            string baseName = string.IsNullOrWhiteSpace(projectName) ? "测试项" : projectName;
            string destName = baseName;
            string destDir = Path.Combine(projectsDir, destName);

            int suffix = 1;
            while (Directory.Exists(destDir))
            {
                suffix++;
                destName = $"{baseName}_{suffix}";
                destDir = Path.Combine(projectsDir, destName);
            }

            Directory.CreateDirectory(destDir);

            // 创建 project.config 配置文件，ProjectName 用原名(无后缀)
            string configPath = Path.Combine(destDir, "project.config");
            string config = $"ProjectName={baseName}\nProjectDescription={description}\nCreationDate={DateTime.Now:yyyy-MM-dd HH:mm:ss}\n";
            File.WriteAllText(configPath, config);

            // 显示名用原名(无后缀)
            finalName = baseName;
            return destDir;
        }

        /// <summary>
        /// 更新 project.config 中的 ProjectName 为指定名称
        /// </summary>
        private void UpdateProjectConfigName(string projectDir, string name)
        {
            string configPath = Path.Combine(projectDir, "project.config");
            if (File.Exists(configPath))
            {
                string content = File.ReadAllText(configPath);
                // 替换 ProjectName= 行
                int idx = content.IndexOf("ProjectName=", StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    int lineEnd = content.IndexOf('\n', idx);
                    if (lineEnd < 0) lineEnd = content.Length;
                    content = content.Substring(0, idx) + $"ProjectName={name}" + content.Substring(lineEnd);
                }
                else
                {
                    content = $"ProjectName={name}\n" + content;
                }
                File.WriteAllText(configPath, content);
            }
            else
            {
                File.WriteAllText(configPath, $"ProjectName={name}\n");
            }
        }

        /// <summary>
        /// 重命名测试项目录下以旧名为前缀的 _Steps.xml/_Variables.xml 文件，并同步 project.config 中的 ProjectName
        /// </summary>
        private void RenameProjectFiles(string projectDir, string oldName, string newName)
        {
            if (string.IsNullOrEmpty(oldName) || oldName == newName) return;

            string[] suffixes = { "_Steps.xml", "_Variables.xml" };
            foreach (var sfx in suffixes)
            {
                string oldFile = Path.Combine(projectDir, oldName + sfx);
                string newFile = Path.Combine(projectDir, newName + sfx);
                if (File.Exists(oldFile))
                {
                    File.Move(oldFile, newFile);
                }
            }

            string configPath = Path.Combine(projectDir, "project.config");
            if (File.Exists(configPath))
            {
                string content = File.ReadAllText(configPath);
                content = content.Replace($"ProjectName={oldName}", $"ProjectName={newName}");
                File.WriteAllText(configPath, content);
            }
        }

        /// <summary>
        /// 打开测试项编辑界面（ProjectEditWindow），编辑指定测试项的步骤
        /// </summary>
        private void OpenProjectEditor(ProjectConfig projectConfig)
        {
            if (projectConfig == null || string.IsNullOrWhiteSpace(projectConfig.ProjectPath))
            {
                MessageBox.Show("测试项路径无效，无法打开编辑器", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!Directory.Exists(projectConfig.ProjectPath))
            {
                Directory.CreateDirectory(projectConfig.ProjectPath);
            }

            var editWindow = new ProjectEditWindow(projectConfig, projectConfig.ProjectPath)
            {
                Owner = this
            };
            editWindow.ShowDialog();

            // 编辑器关闭后，同步当前选中测试项的描述（从 project.config 读取）
            if (SelectedTestCaseProject != null)
            {
                string configPath = Path.Combine(projectConfig.ProjectPath, "project.config");
                if (File.Exists(configPath))
                {
                    foreach (var line in File.ReadAllLines(configPath))
                    {
                        if (line.StartsWith("ProjectDescription="))
                        {
                            SelectedTestCaseProject.Description = line.Substring("ProjectDescription=".Length);
                            break;
                        }
                    }
                }
                IsModified = true;
            }
        }

        private string GetProjectPath(string projectName)
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Projects", projectName);
        }

        /// <summary>
        /// 删除左侧模板项目列表中选中的模板（同时删除全局 Projects 目录下对应的测试项文件夹）
        /// </summary>
        private void DeleteTestCaseProjectButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedProject == null)
            {
                MessageBox.Show("请先在模板项目列表中选中要删除的测试项", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var confirm = MessageBox.Show(
                $"确认删除模板项目「{SelectedProject.Name}」吗？\n\n此操作将删除全局 Projects 目录下对应的测试项文件夹，不可恢复。",
                "删除确认",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                // 1. 删除全局 Projects 目录下的测试项文件夹
                string projectPath = SelectedProject.ProjectPath;
                if (!string.IsNullOrWhiteSpace(projectPath) && Directory.Exists(projectPath))
                {
                    Directory.Delete(projectPath, recursive: true);
                }

                // 2. 从模板项目集合和筛选集合中移除
                Projects.Remove(SelectedProject);
                FilteredProjects.Remove(SelectedProject);
                SelectedProject = null;

                StatusText.Text = "已删除模板项目";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"删除模板项目失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TestCaseProjectList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TestCaseProjectList.SelectedItem is TestCaseProjectItem projectItem)
            {
                SelectedTestCaseProject = projectItem;
                LoadProjectVariables(projectItem);
            }
            else
            {
                SelectedTestCaseProject = null;
                SelectedTestCaseProjectVariables.Clear();
                SelectedVariable = null;
            }
        }

        /// <summary>
        /// 加载项目的变量列表
        /// </summary>
        private void LoadProjectVariables(TestCaseProjectItem projectItem)
        {
            SelectedTestCaseProjectVariables.Clear();
            SelectedVariable = null;

            try
            {
                // 首先尝试从TestCaseProject的Variables属性中加载变量
                if (projectItem.Project.Variables != null && projectItem.Project.Variables.Count > 0)
                {
                    foreach (var variable in projectItem.Project.Variables)
                    {
                        SelectedTestCaseProjectVariables.Add(variable);
                    }
                }
                else
                {
                    // 如果TestCaseProject中没有变量，尝试从项目文件中加载
                    string projectPath = projectItem.ProjectPath;
                    string projectName = projectItem.Name;

                    // 构建变量文件的路径
                    string variablesFilePath = System.IO.Path.Combine(projectPath, $"{projectName}_Variables.xml");

                    // 检查变量文件是否存在
                    if (System.IO.File.Exists(variablesFilePath))
                    {
                        // 从XML文件中加载变量
                        var loadedVariables = XmlHelper.DeserializeFromFile<List<ProjectVariable>>(variablesFilePath);
                        if (loadedVariables != null && loadedVariables.Count > 0)
                        {
                            // 将加载的变量添加到TestCaseProject的Variables属性中
                            projectItem.Project.Variables = loadedVariables;
                            
                            // 将变量添加到SelectedTestCaseProjectVariables集合中
                            foreach (var variable in loadedVariables)
                            {
                                SelectedTestCaseProjectVariables.Add(variable);
                            }
                        }
                    }
                    else
                    {
                        // 如果变量文件不存在，显示空列表
                        StatusText.Text = $"项目 '{projectName}' 没有变量文件";
                    }
                }
            }
            catch (Exception ex)
            {
                // 处理加载变量时的异常
                StatusText.Text = $"加载变量失败: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"加载变量失败: {ex.Message}");
            }

            // 默认选中第一个变量
            if (SelectedTestCaseProjectVariables.Count > 0)
            {
                SelectedVariable = SelectedTestCaseProjectVariables[0];
            }
        }

        /// <summary>
        /// 根据变量类型更新输入控件的可见性
        /// </summary>
        private void UpdateInputControlsVisibility()
        {
            if (SelectedVariable == null) return;

            // 重置所有输入控件的可见性
            SingleValueTextBox.Visibility = Visibility.Collapsed;
            SingleValueNumericTextBox.Visibility = Visibility.Collapsed;
            SingleValueBoolComboBox.Visibility = Visibility.Collapsed;
            RangeStartTextBox.Visibility = Visibility.Collapsed;
            RangeStartNumericTextBox.Visibility = Visibility.Collapsed;
            RangeEndTextBox.Visibility = Visibility.Collapsed;
            RangeEndNumericTextBox.Visibility = Visibility.Collapsed;

            switch (SelectedVariable.VariableType)
            {
                case "Int":
                case "Double":
                    // 数值类型，显示数值输入框
                    SingleValueNumericTextBox.Visibility = Visibility.Visible;
                    RangeStartNumericTextBox.Visibility = Visibility.Visible;
                    RangeEndNumericTextBox.Visibility = Visibility.Visible;
                    break;
                case "Bool":
                    // 布尔类型，显示下拉框
                    SingleValueBoolComboBox.Visibility = Visibility.Visible;
                    // 范围不支持布尔类型
                    RangeStartTextBox.Visibility = Visibility.Collapsed;
                    RangeEndTextBox.Visibility = Visibility.Collapsed;
                    break;
                default:
                    // 默认使用文本输入框
                    SingleValueTextBox.Visibility = Visibility.Visible;
                    RangeStartTextBox.Visibility = Visibility.Visible;
                    RangeEndTextBox.Visibility = Visibility.Visible;
                    break;
            }

            // 初始化合格值设置
            IsSingleValue = !SelectedVariable.IsRange;
            IsRange = SelectedVariable.IsRange;
            SingleValue = string.Empty;
            RangeStart = string.Empty;
            RangeEnd = string.Empty;

            if (SelectedVariable.IsRange)
            {
                // 解析范围值
                if (!string.IsNullOrEmpty(SelectedVariable.QualifiedValue))
                {
                    string[] rangeValues = SelectedVariable.QualifiedValue.Split('-');
                    if (rangeValues.Length == 2)
                    {
                        RangeStart = rangeValues[0].Trim();
                        RangeEnd = rangeValues[1].Trim();
                    }
                }
            }
            else
            {
                SingleValue = SelectedVariable.QualifiedValue ?? string.Empty;
            }

            // 初始化布尔类型ComboBox的选中项
            if (SelectedVariable.VariableType == "Bool")
            {
                SingleValueBoolComboBox.SelectedIndex = (SingleValue?.Equals("True", StringComparison.OrdinalIgnoreCase) == true) ? 0 : 1;
            }
        }

        /// <summary>
        /// 数值输入验证
        /// </summary>
        private void Numeric_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // 允许输入数字和小数点
            bool isDigit = char.IsDigit(e.Text[0]);
            bool isDecimalPoint = e.Text == ".";
            bool canAddDecimal = false;

            // 检查是否已经有小数点
            if (isDecimalPoint)
            {
                TextBox? textBox = sender as TextBox;
                canAddDecimal = textBox != null && !textBox.Text.Contains(".");
            }

            // 允许输入数字或小数点（如果还没有的话）
            e.Handled = !(isDigit || (isDecimalPoint && canAddDecimal));
        }

        private void SingleValue_Checked(object sender, RoutedEventArgs e)
        {
            IsSingleValue = true;
            IsRange = false;
        }

        private void Range_Checked(object sender, RoutedEventArgs e)
        {
            IsRange = true;
            IsSingleValue = false;
        }

        private void VariableList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (VariableList.SelectedItem is ProjectVariable variable)
            {
                SelectedVariable = variable;
            }
        }

        private void SaveQualifiedValueButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedVariable == null || SelectedTestCaseProject == null) return;

            // 更新合格值
            if (IsSingleValue)
            {
                SelectedVariable.IsRange = false;

                // 根据当前显示的输入控件获取值
                if (SingleValueTextBox.Visibility == Visibility.Visible)
                {
                    SingleValue = SingleValueTextBox.Text;
                }
                else if (SingleValueNumericTextBox.Visibility == Visibility.Visible)
                {
                    SingleValue = SingleValueNumericTextBox.Text;
                }
                else if (SingleValueBoolComboBox.Visibility == Visibility.Visible)
                {
                    SingleValue = SingleValueBoolComboBox.SelectedItem?.ToString() ?? "False";
                }

                SelectedVariable.QualifiedValue = SingleValue;
            }
            else
            {
                SelectedVariable.IsRange = true;

                // 根据当前显示的输入控件获取范围值
                if (RangeStartTextBox.Visibility == Visibility.Visible)
                {
                    RangeStart = RangeStartTextBox.Text;
                    RangeEnd = RangeEndTextBox.Text;
                }
                else if (RangeStartNumericTextBox.Visibility == Visibility.Visible)
                {
                    RangeStart = RangeStartNumericTextBox.Text;
                    RangeEnd = RangeEndNumericTextBox.Text;
                }

                SelectedVariable.QualifiedValue = $"{RangeStart} - {RangeEnd}";
            }

            // 保存变量到项目文件
            SaveVariablesToProjectFile();

            // 通知变量列表更新
            OnPropertyChanged(nameof(SelectedTestCaseProjectVariables));
            StatusText.Text = $"已更新并保存变量 '{SelectedVariable.VariableName}' 的合格值设置";
            IsModified = true;
        }

        /// <summary>
        /// 保存变量到TestCaseProject的Variables属性中
        /// </summary>
        private void SaveVariablesToProjectFile()
        {
            if (SelectedTestCaseProject == null || SelectedTestCaseProjectVariables.Count == 0)
                return;

            try
            {
                // 将变量保存到TestCaseProject的Variables属性中
                SelectedTestCaseProject.Project.Variables = SelectedTestCaseProjectVariables.ToList();

                // 更新状态文本
                StatusText.Text = $"已保存变量到测试项: {SelectedTestCaseProject.Name}";
            }
            catch (Exception ex)
            {
                // 处理保存变量时的异常
                StatusText.Text = $"保存变量失败: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"保存变量失败: {ex.Message}");
            }
        }

        private void TestCaseProjectList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (SelectedTestCaseProject != null)
            {
                // 未保存的待创建项目：先保存用例创建物理文件夹，再打开编辑器
                if (SelectedTestCaseProject.Project.IsPendingCreate)
                {
                    if (!SaveTestCase())
                    {
                        MessageBox.Show("保存用例失败，无法编辑测试项", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                var projectConfig = new ProjectConfig
                {
                    Name = SelectedTestCaseProject.Name,
                    Description = SelectedTestCaseProject.Description,
                    ProjectPath = SelectedTestCaseProject.ProjectPath,
                    CreatedDate = SelectedTestCaseProject.AddedDate,
                    ModifiedDate = DateTime.Now
                };

                OpenProjectEditor(projectConfig);
                StatusText.Text = $"已编辑测试项: {SelectedTestCaseProject.Name}";
            }
        }

        private void MoveUpButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedTestCaseProject == null || SelectedTestCaseProject.Index <= 1) return;

            int currentIndex = SelectedTestCaseProject.Index - 1;
            int targetIndex = currentIndex - 1;

            var temp = TestCaseProjects[currentIndex];
            TestCaseProjects[currentIndex] = TestCaseProjects[targetIndex];
            TestCaseProjects[targetIndex] = temp;

            // 更新索引
            UpdateProjectIndexes();
            IsModified = true;
        }

        private void MoveDownButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedTestCaseProject == null || SelectedTestCaseProject.Index >= TestCaseProjects.Count) return;

            int currentIndex = SelectedTestCaseProject.Index - 1;
            int targetIndex = currentIndex + 1;

            var temp = TestCaseProjects[currentIndex];
            TestCaseProjects[currentIndex] = TestCaseProjects[targetIndex];
            TestCaseProjects[targetIndex] = temp;

            // 更新索引
            UpdateProjectIndexes();
            IsModified = true;
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedTestCaseProject == null) return;

            string projectName = SelectedTestCaseProject.Name;
            string projectPath = SelectedTestCaseProject.ProjectPath;
            var result = MessageBox.Show($"确定要移除项目 '{projectName}' 吗？\n\n这将删除对应的测试项文件夹，不可恢复。", "确认移除", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                // 删除用例独立目录下的测试项文件夹
                try
                {
                    if (!string.IsNullOrWhiteSpace(projectPath) && Directory.Exists(projectPath))
                    {
                        Directory.Delete(projectPath, recursive: true);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"删除测试项文件失败: {ex.Message}", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                TestCaseProjects.Remove(SelectedTestCaseProject);
                UpdateProjectIndexes();
                IsModified = true;
                StatusText.Text = $"已移除项目: {projectName}";
            }
        }

        private void UpdateProjectIndexes()
        {
            for (int i = 0; i < TestCaseProjects.Count; i++)
            {
                TestCaseProjects[i].Index = i + 1;
                TestCaseProjects[i].Project.Order = i + 1;
            }
            UpdateButtonStates();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            SaveTestCase();
        }

        private void SaveAsButton_Click(object sender, RoutedEventArgs e)
        {
            SaveTestCaseAs();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// 窗口关闭前事件：未保存时提示是否保存（覆盖关闭按钮、X 按钮、ALT+F4 等所有关闭路径）
        /// </summary>
        protected override void OnClosing(CancelEventArgs e)
        {
            if (IsModified)
            {
                var result = MessageBox.Show("用例已修改，是否保存？", "保存确认", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    if (!SaveTestCase())
                    {
                        e.Cancel = true; // 保存失败，不关闭窗口
                        return;
                    }
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    e.Cancel = true; // 取消关闭
                    return;
                }
            }
            DialogResult = true;
            base.OnClosing(e);
        }

        private bool SaveTestCase()
        {
            try
            {
                // 保存前：为待创建的测试项生成物理文件夹
                foreach (var projectItem in TestCaseProjects)
                {
                    var proj = projectItem.Project;
                    if (proj.IsPendingCreate)
                    {
                        string destDir;
                        if (proj.IsNewBlank)
                        {
                            // 新建空白测试项
                            destDir = CreateNewProjectInTestCase(proj.Name, proj.Description, out _);
                        }
                        else
                        {
                            // 从模板复制
                            var template = new ProjectConfig { Name = proj.Name, Description = proj.Description, ProjectPath = proj.SourceTemplatePath };
                            destDir = CopyTemplateToTestCase(template, out _);
                        }
                        proj.ProjectPath = destDir;
                        proj.IsPendingCreate = false;
                    }
                }

                // 更新用例配置中的项目列表
                TestCaseConfig.Projects.Clear();
                foreach (var projectItem in TestCaseProjects)
                {
                    TestCaseConfig.Projects.Add(projectItem.Project);
                }

                TestCaseConfig.ModifiedDate = DateTime.Now;

                XmlHelper.SerializeToFile(TestCaseConfig, _testCasePath);
                IsModified = false;
                StatusText.Text = "用例已保存";

                // 显示保存成功提示
                MessageBox.Show("用例保存成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存用例失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private bool SaveTestCaseAs()
        {
            // 使用另存为测试用例窗口来获取新的用例名称和描述
            var saveAsWindow = new SaveAsTestCaseWindow(TestCaseConfig)
            {
                Owner = this
            };
            
            if (saveAsWindow.ShowDialog() == true)
            {
                string newTestCaseName = saveAsWindow.TestCaseName;
                string newTestCaseDescription = saveAsWindow.TestCaseDescription;
                string newTestCasePath = saveAsWindow.NewTestCasePath;
                
                // 保存当前状态到新文件
                string originalName = TestCaseConfig.Name;
                string originalDescription = TestCaseConfig.Description;
                string originalPath = _testCasePath;
                
                try
                {
                    // 更新用例配置
                    TestCaseConfig.Name = newTestCaseName;
                    TestCaseConfig.Description = newTestCaseDescription;
                    _testCasePath = newTestCasePath;
                    
                    // 保存到新文件
                    if (SaveTestCase())
                    {
                        MessageBox.Show("用例另存为成功", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                        UpdateTitle();
                        IsModified = false; // 重置修改状态
                        return true;
                    }
                    else
                    {
                        // 保存失败，恢复原始状态
                        TestCaseConfig.Name = originalName;
                        TestCaseConfig.Description = originalDescription;
                        _testCasePath = originalPath;
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    // 发生异常，恢复原始状态
                    TestCaseConfig.Name = originalName;
                    TestCaseConfig.Description = originalDescription;
                    _testCasePath = originalPath;
                    MessageBox.Show($"另存为失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
            }
            
            return false;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}