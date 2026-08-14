using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SIAT
{
    /// <summary>
    /// 项目数据类
    /// </summary>
    public class Project : INotifyPropertyChanged
    {
        private string _projectName = string.Empty;
        private string _projectDescription = string.Empty;
        private string _creationDate = string.Empty;
        private string _projectPath = string.Empty;

        public string ProjectName
        {
            get { return _projectName; }
            set { _projectName = value; OnPropertyChanged(nameof(ProjectName)); }
        }

        public string ProjectDescription
        {
            get { return _projectDescription; }
            set { _projectDescription = value; OnPropertyChanged(nameof(ProjectDescription)); }
        }

        public string CreationDate
        {
            get { return _creationDate; }
            set { _creationDate = value; OnPropertyChanged(nameof(CreationDate)); }
        }

        public string ProjectPath
        {
            get { return _projectPath; }
            set { _projectPath = value; OnPropertyChanged(nameof(ProjectPath)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// OpenProjectWindow.xaml 的交互逻辑
    /// </summary>
    public partial class OpenProjectWindow : Window, INotifyPropertyChanged
    {
        private List<Project> _projects = new();
        private List<Project> _filteredProjects = new();
        private Project? _selectedProject = null;
        private string _searchText = string.Empty;

        public List<Project> Projects
        {
            get { return _projects; }
            set { _projects = value; OnPropertyChanged(nameof(Projects)); }
        }

        public List<Project> FilteredProjects
        {
            get { return _filteredProjects; }
            set { _filteredProjects = value; OnPropertyChanged(nameof(FilteredProjects)); }
        }

        public Project? SelectedProject
        {
            get { return _selectedProject; }
            set { _selectedProject = value; OnPropertyChanged(nameof(SelectedProject)); }
        }

        public string SearchText
        {
            get { return _searchText; }
            set { _searchText = value; OnPropertyChanged(nameof(SearchText)); FilterProjects(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public OpenProjectWindow()
        {
            InitializeComponent();
            DataContext = this;
            LoadProjects();
        }

        /// <summary>
        /// 从项目文件夹加载所有项目
        /// </summary>
        private void LoadProjects()
        {
            try
            {
                string projectsFolder = GetProjectsFolderPath();
                Projects = new();

                if (Directory.Exists(projectsFolder))
                {
                    // 获取所有项目文件夹
                    string[] projectDirectories = Directory.GetDirectories(projectsFolder);

                    foreach (string projectDir in projectDirectories)
                    {
                        string configFile = Path.Combine(projectDir, "project.config");
                        if (File.Exists(configFile))
                        {
                            Project? project = ParseProjectConfig(configFile, projectDir);
                            if (project != null)
                            {
                                Projects.Add(project);
                            }
                        }
                    }

                    // 按创建日期排序（最新的在前面）
                    Projects = Projects.OrderByDescending(p => p.CreationDate).ToList();
                }

                FilteredProjects = new(Projects);
                
                // 控制空列表提示的显示/隐藏
                EmptyListPanel.Visibility = (FilteredProjects == null || FilteredProjects.Count == 0) ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                MessageBox.Show("加载项目列表失败：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 解析项目配置文件
        /// </summary>
        /// <param name="configFile">配置文件路径</param>
        /// <param name="projectDir">项目目录</param>
        /// <returns>项目对象</returns>
        private static Project? ParseProjectConfig(string configFile, string projectDir)
        {
            try
            {
                string[] lines = File.ReadAllLines(configFile);
                Project project = new Project
                {
                    ProjectPath = projectDir,
                    ProjectName = Path.GetFileName(projectDir), // 默认使用文件夹名称
                    ProjectDescription = "",
                    CreationDate = Directory.GetCreationTime(projectDir).ToString("yyyy-MM-dd HH:mm:ss")
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
                                project.ProjectName = value;
                                break;
                            case "ProjectDescription":
                                project.ProjectDescription = value;
                                break;
                            case "CreationDate":
                                project.CreationDate = value;
                                break;
                        }
                    }
                }

                return project;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"解析项目配置文件失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }
        }

        /// <summary>
        /// 获取项目文件夹路径
        /// </summary>
        /// <returns>项目文件夹路径</returns>
        private static string GetProjectsFolderPath()
        {
            // 获取应用程序运行目录
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string projectsFolder = Path.Combine(appDir, "Projects");

            // 如果项目文件夹不存在，尝试从bin/Debug目录下获取
            if (!Directory.Exists(projectsFolder))
            {
                projectsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..\\..\\..\\bin\\Debug\\net8.0-windows\\Projects");
            }

            return projectsFolder;
        }

        /// <summary>
        /// 筛选项目
        /// </summary>
        private void FilterProjects()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                FilteredProjects = new(Projects);
            }
            else
            {
                FilteredProjects = Projects.Where(p => 
                    p.ProjectName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) || 
                    p.ProjectDescription.Contains(SearchText, StringComparison.OrdinalIgnoreCase)).ToList();
            }
            
            // 控制空列表提示的显示/隐藏
            EmptyListPanel.Visibility = (FilteredProjects == null || FilteredProjects.Count == 0) ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// 搜索按钮点击事件
        /// </summary>
        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            SearchText = SearchTextBox.Text;
        }

        /// <summary>
        /// 搜索文本变化事件
        /// </summary>
        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SearchText = SearchTextBox.Text;
        }

        /// <summary>
        /// 项目选择变化事件
        /// </summary>
        private void ProjectListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProjectListView.SelectedItem is Project selectedProject)
            {
                SelectedProject = selectedProject;
                OpenButton.IsEnabled = true;
                DeleteButton.IsEnabled = true;
            }
            else
            {
                SelectedProject = null;
                OpenButton.IsEnabled = false;
                DeleteButton.IsEnabled = false;
            }
        }

        /// <summary>
        /// 打开按钮点击事件
        /// </summary>
        private void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedProject != null)
            {
                try
                {
                    ProjectConfig projectConfig;
                    string projectXmlPath = Path.Combine(SelectedProject.ProjectPath, $"{SelectedProject.ProjectName}.xml");
                    string projectConfigPath = Path.Combine(SelectedProject.ProjectPath, "project.config");
                    
                    // 首先尝试从XML文件加载配置
                    if (File.Exists(projectXmlPath))
                    {
                        projectConfig = XmlHelper.DeserializeFromFile<ProjectConfig>(projectXmlPath);
                    }
                    // 如果XML文件不存在，尝试从project.config文件加载
                    else if (File.Exists(projectConfigPath))
                    {
                        // 读取配置文件内容
                        var configLines = File.ReadAllLines(projectConfigPath);
                        var config = new Dictionary<string, string>();
                        foreach (var line in configLines)
                        {
                            if (string.IsNullOrWhiteSpace(line) || !line.Contains('='))
                                continue;
                            var parts = line.Split('=', 2);
                            if (parts.Length == 2)
                            {
                                config[parts[0].Trim()] = parts[1].Trim();
                            }
                        }

                        // 创建ProjectConfig对象
                        projectConfig = new ProjectConfig
                        {
                            Name = config.TryGetValue("ProjectName", out var name) ? name : SelectedProject.ProjectName,
                            Description = config.TryGetValue("ProjectDescription", out var desc) ? desc : string.Empty
                        };

                        // 尝试解析创建日期
                        if (config.TryGetValue("CreationDate", out var creationDateStr) &&
                            DateTime.TryParse(creationDateStr, out var creationDate))
                        {
                            projectConfig.CreatedDate = creationDate;
                            projectConfig.ModifiedDate = creationDate;
                        }
                        else
                        {
                            projectConfig.CreatedDate = DateTime.Now;
                            projectConfig.ModifiedDate = DateTime.Now;
                        }
                    }
                    else
                    {
                        // 如果都不存在，创建一个新的配置
                        projectConfig = new ProjectConfig
                        {
                            Name = SelectedProject.ProjectName,
                            Description = string.Empty,
                            CreatedDate = DateTime.Now,
                            ModifiedDate = DateTime.Now
                        };
                    }

                    // 打开项目编辑界面
                    ProjectEditWindow projectEditWindow = new ProjectEditWindow(projectConfig, SelectedProject.ProjectPath);
                    projectEditWindow.Show();
                    this.Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"打开项目失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// 取消按钮点击事件
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        /// <summary>
        /// 删除按钮点击事件
        /// </summary>
        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedProject != null)
            {
                // 确认用户是否真的要删除项目
                var result = MessageBox.Show($"确定要删除项目 '{SelectedProject.ProjectName}' 吗？此操作不可恢复！", "删除确认", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        // 删除项目文件夹及其所有内容
                        if (Directory.Exists(SelectedProject.ProjectPath))
                        {
                            Directory.Delete(SelectedProject.ProjectPath, true);
                            MessageBox.Show("项目删除成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                            
                            // 重新加载项目列表
                            LoadProjects();
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"删除项目失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        /// <summary>
        /// 双击项目列表项打开项目
        /// </summary>
        private void ProjectListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (SelectedProject != null)
            {
                OpenButton_Click(sender, e);
            }
        }
    }
}