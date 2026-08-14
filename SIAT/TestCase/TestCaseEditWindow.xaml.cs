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

        private void AddProjectToTestCase()
        {
            if (SelectedProject == null) return;

            var testCaseProject = new TestCaseProject
            {
                Name = SelectedProject.Name,
                Description = SelectedProject.Description,
                ProjectPath = SelectedProject.ProjectPath,
                AddedDate = DateTime.Now
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
            
            StatusText.Text = $"已添加项目: {SelectedProject.Name}";
        }

        private string GetProjectPath(string projectName)
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Projects", projectName);
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
                // 打开项目编辑对话框
                var editDialog = new TestCaseProjectEditDialog(SelectedTestCaseProject.Project);
                if (editDialog.ShowDialog() == true)
                {
                    // 更新项目信息
                    SelectedTestCaseProject.Name = editDialog.Project.Name;
                    SelectedTestCaseProject.Description = editDialog.Project.Description;
                    IsModified = true;
                    StatusText.Text = $"已更新项目: {SelectedTestCaseProject.Name}";
                }
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
            var result = MessageBox.Show($"确定要移除项目 '{projectName}' 吗？", "确认移除", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
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
            if (IsModified)
            {
                var result = MessageBox.Show("用例已修改，是否保存？", "保存确认", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    if (!SaveTestCase())
                    {
                        return; // 保存失败，不关闭窗口
                    }
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    return; // 取消关闭
                }
            }
            
            DialogResult = true;
            Close();
        }

        private bool SaveTestCase()
        {
            try
            {
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