using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SIAT.ResourceManagement;

namespace SIAT
{
    /// <summary>
    /// 测试用例步骤项
    /// </summary>
    [Serializable]
    public class TestCaseStepItem : INotifyPropertyChanged
    {
        private int _index;
        private Step _step = new Step();
        private string _deviceName = string.Empty;
        private List<InputBindingItem> _inputBindings;
        private List<OutputBindingItem> _outputBindings;

        /// <summary>
        /// 构造函数
        /// </summary>
        public TestCaseStepItem()
        {
            // 初始化绑定列表
            _inputBindings = new List<InputBindingItem>();
            _outputBindings = new List<OutputBindingItem>();
        }

        public int Index
        {
            get { return _index; }
            set { _index = value; OnPropertyChanged(nameof(Index)); }
        }

        public Step Step
        {
            get { return _step; }
            set { _step = value; OnPropertyChanged(nameof(Step)); }
        }

        public string DeviceName
        {
            get { return _deviceName; }
            set { _deviceName = value; OnPropertyChanged(nameof(DeviceName)); }
        }

        public List<InputBindingItem> InputBindings
        {
            get { return _inputBindings; }
            set { _inputBindings = value; OnPropertyChanged(nameof(InputBindings)); }
        }

        public List<OutputBindingItem> OutputBindings
        {
            get { return _outputBindings; }
            set { _outputBindings = value; OnPropertyChanged(nameof(OutputBindings)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// ProjectEditWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ProjectEditWindow : Window, INotifyPropertyChanged
    {
      
        private string _projectPath;
        private ProjectConfig _projectConfig;
        private ObservableCollection<Device> _devices = new ObservableCollection<Device>();
        private List<Step> _allSteps = new List<Step>();
        private ObservableCollection<Step> _filteredSteps = new ObservableCollection<Step>();
        private ObservableCollection<TestCaseStepItem> _testCaseSteps = new ObservableCollection<TestCaseStepItem>();
        private ObservableCollection<ProjectVariable> _variables = new ObservableCollection<ProjectVariable>();
        private Device? _selectedDevice;
        private TestCaseStepItem? _selectedTestCaseStep;
        private ProjectVariable? _selectedVariable;
        private string _stepSearchText = string.Empty;
        private bool _isModified = false;

      

        public ObservableCollection<Device> Devices
        {
            get { return _devices; }
            set { _devices = value; OnPropertyChanged(nameof(Devices)); }
        }

        public ObservableCollection<Step> FilteredSteps
        {
            get { return _filteredSteps; }
            set { _filteredSteps = value; OnPropertyChanged(nameof(FilteredSteps)); }
        }

        public ObservableCollection<TestCaseStepItem> TestCaseSteps
        {
            get { return _testCaseSteps; }
            set { _testCaseSteps = value; OnPropertyChanged(nameof(TestCaseSteps)); }
        }

        public ObservableCollection<ProjectVariable> Variables
        {
            get { return _variables; }
            set { _variables = value; OnPropertyChanged(nameof(Variables)); }
        }

        public Device? SelectedDevice
        {
            get { return _selectedDevice; }
            set { _selectedDevice = value; OnPropertyChanged(nameof(SelectedDevice)); FilterStepsByDevice(); }
        }

        public TestCaseStepItem? SelectedTestCaseStep
        {
            get { return _selectedTestCaseStep; }
            set { _selectedTestCaseStep = value; OnPropertyChanged(nameof(SelectedTestCaseStep)); }
        }

        public ProjectVariable? SelectedVariable
        {
            get { return _selectedVariable; }
            set { _selectedVariable = value; OnPropertyChanged(nameof(SelectedVariable)); }
        }

        public string StepSearchText
        {
            get { return _stepSearchText; }
            set { _stepSearchText = value; OnPropertyChanged(nameof(StepSearchText)); FilterSteps(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="projectConfig">项目配置</param>
        /// <param name="projectPath">项目路径</param>
        public ProjectEditWindow(ProjectConfig projectConfig, string projectPath)
        {
            InitializeComponent();
            DataContext = this;

            _projectConfig = projectConfig;
            _projectPath = projectPath;

            // 初始化搜索框占位符文本
            if (SearchPlaceholder != null)
            {
                SearchPlaceholder.Visibility = Visibility.Visible;
            }

            // 加载设备和步骤数据
            LoadDevicesAndSteps();
            // 加载变量数据
            LoadVariables();
            // 加载测试用例步骤
            LoadTestCaseSteps();

            // 初始化绑定 - 所有列表已通过XAML数据绑定，此处无需重复设置

            // 绑定事件
            StepSearchTextBox.TextChanged += (sender, e) => StepSearchText = StepSearchTextBox.Text;
            ProjectNameText.Text = _projectConfig.Name;
        }

        /// <summary>
        /// 加载设备和步骤数据
        /// </summary>
        private void LoadDevicesAndSteps()
        {
            try
            {
                // 先添加插件步骤（置顶）
                AddPluginSteps();

                // 从设备目录加载设备数据
                string devicesDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Devices");
                if (!Directory.Exists(devicesDirectory))
                {
                    // 尝试从bin/Debug目录下获取
                    devicesDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..\\..\\..\\bin\\Debug\\net8.0-windows\\Devices");
                }

                if (Directory.Exists(devicesDirectory))
                {
                    // 加载所有设备文件
                    string[] deviceFiles = Directory.GetFiles(devicesDirectory, "*.xml");
                    foreach (string deviceFile in deviceFiles)
                    {
                        try
                        {
                            Device device = XmlHelper.DeserializeFromFile<Device>(deviceFile);
                            if (device != null)
                            {
                                Devices.Add(device);
                                // 收集所有步骤
                                if (device.Steps != null)
                                {
                                    _allSteps.AddRange(device.Steps);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"加载设备文件失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }

                // 初始化步骤列表
                FilteredSteps.Clear();
                foreach (var step in _allSteps)
                {
                    FilteredSteps.Add(step);
                }

                // 默认选择第一个设备（插件步骤）
                if (Devices.Count > 0)
                {
                    SelectedDevice = Devices[0];
                    DeviceList.SelectedItem = SelectedDevice;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载设备和步骤失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }



        /// <summary>
        /// 添加插件步骤
        /// </summary>
        private void AddPluginSteps()
        {
            // 创建插件步骤设备
            var pluginDevice = new Device
            {
                Name = "插件步骤",
                Steps = new List<Step>()
            };

            // 加载插件步骤
            LoadPluginSteps(pluginDevice);

            Devices.Add(pluginDevice);
            _allSteps.AddRange(pluginDevice.Steps);
        }

        /// <summary>
        /// 加载插件步骤
        /// </summary>
        private void LoadPluginSteps(Device pluginDevice)
        {
            try
            {
                // 1. 从Plugins目录加载XML文件形式的插件步骤
                string pluginsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
                if (!Directory.Exists(pluginsDirectory))
                {
                    // 尝试从bin/Debug目录下获取
                    pluginsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..\\..\\..\\bin\\Debug\\net8.0-windows\\Plugins");
                }

                if (Directory.Exists(pluginsDirectory))
                {
                    // 加载所有插件步骤文件
                    string[] pluginFiles = Directory.GetFiles(pluginsDirectory, "*.xml");
                    foreach (string pluginFile in pluginFiles)
                    {
                        try
                        {
                            Step pluginStep = XmlHelper.DeserializeFromFile<Step>(pluginFile);
                            if (pluginStep != null)
                            {
                                pluginDevice.Steps.Add(pluginStep);
                            }
                        }
                        catch (Exception ex)
                        {
                            // 记录错误但继续加载其他插件
                            System.Diagnostics.Debug.WriteLine($"加载插件步骤失败: {ex.Message}");
                        }
                    }
                }

                // 2. 从PluginStepExecutor类中加载方法作为插件步骤
                LoadPluginStepsFromExecutor(pluginDevice);
            }
            catch (Exception ex)
            {
                // 记录错误但不影响系统运行
                System.Diagnostics.Debug.WriteLine($"加载插件步骤目录失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从PluginStepExecutor类中加载方法作为插件步骤
        /// </summary>
        private void LoadPluginStepsFromExecutor(Device pluginDevice)
        {
            try
            {
                // 获取PluginStepExecutor类型
                Type executorType = Type.GetType("SIAT.TSET.PluginStepExecutor, SIAT");
                if (executorType != null)
                {
                    // 获取所有公共实例方法
                    MethodInfo[] methods = executorType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
                    
                    foreach (MethodInfo method in methods)
                    {
                        // 过滤掉不需要的方法
                        if (!IsPluginStepMethod(method))
                        {
                            continue;
                        }
                        
                        // 从方法名中提取步骤名称
                        string stepName = GetStepNameFromMethodName(method.Name);
                        if (!string.IsNullOrEmpty(stepName))
                        {
                            // 创建对应的Step对象
                            Step pluginStep = new Step
                            {
                                Name = stepName,
                                StepType = ResourceManagement.StepType.SendAndReceive,
                                Protocol = new Protocol(),
                                ResultVariables = new List<ResultVariable>(),
                                IsEnabled = true
                            };
                            
                            // 检查是否已存在同名步骤
                            if (!pluginDevice.Steps.Any(s => s.Name == stepName))
                            {
                                pluginDevice.Steps.Add(pluginStep);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // 记录错误但继续执行
                System.Diagnostics.Debug.WriteLine($"从PluginStepExecutor加载步骤失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 判断是否为插件步骤方法
        /// </summary>
        /// <param name="method">方法信息</param>
        /// <returns>是否为插件步骤方法</returns>
        private bool IsPluginStepMethod(MethodInfo method)
        {
            // 排除构造函数和非插件步骤方法
            if (method.IsConstructor)
                return false;
            
            // 排除内部方法和便捷访问方法
            string methodName = method.Name;
            if (methodName == "ExecutePluginStepAsync" || 
                methodName == "GetMethodNameFromStepName" || 
                methodName == "NotifyStepProgress" ||
                methodName == "GetInputValue" ||
                methodName == "SetOutputValue" ||
                methodName == "HasInputValue" ||
                methodName == "AnalyzeBindingsFromMethodBody" ||
                methodName == "DetectUsedBindings")
                return false;
            
            // 排除Object类的方法
            if (method.DeclaringType == typeof(object))
                return false;
            
            // 只包含以Step结尾的方法
            return methodName.EndsWith("Step", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 从方法名中提取步骤名称
        /// </summary>
        /// <param name="methodName">方法名称</param>
        /// <returns>步骤名称</returns>
        private string GetStepNameFromMethodName(string methodName)
        {
            // 移除Step后缀
            if (methodName.EndsWith("Step", StringComparison.OrdinalIgnoreCase))
            {
                string stepName = methodName.Substring(0, methodName.Length - 4);
                
                // 将PascalCase转换为普通文本
                return ConvertPascalCaseToSentence(stepName);
            }
            return string.Empty;
        }

        /// <summary>
        /// 将PascalCase转换为普通文本
        /// </summary>
        /// <param name="pascalCase">PascalCase字符串</param>
        /// <returns>转换后的文本</returns>
        private string ConvertPascalCaseToSentence(string pascalCase)
        {
            if (string.IsNullOrEmpty(pascalCase))
                return string.Empty;
            
            System.Text.StringBuilder result = new System.Text.StringBuilder();
            result.Append(pascalCase[0]);
            
            for (int i = 1; i < pascalCase.Length; i++)
            {
                if (char.IsUpper(pascalCase[i]))
                {
                    result.Append(' ');
                }
                result.Append(pascalCase[i]);
            }
            
            return result.ToString();
        }

        /// <summary>
        /// 将步骤名称转换为方法名称
        /// </summary>
        /// <param name="stepName">步骤名称</param>
        /// <returns>方法名称</returns>
        private string GetMethodNameFromStepName(string stepName)
        {
            // 将步骤名称转换为PascalCase，然后添加Step后缀
            var parts = stepName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var methodName = string.Empty;

            foreach (var part in parts)
            {
                if (!string.IsNullOrEmpty(part))
                {
                    // 将首字母大写，其余小写
                    methodName += char.ToUpper(part[0]) + part.Substring(1).ToLower();
                }
            }

            // 添加Step后缀
            return methodName + "Step";
        }

        /// <summary>
        /// 根据设备筛选步骤
        /// </summary>
        private void FilterStepsByDevice()
        {
            // 清空当前筛选结果
            FilteredSteps.Clear();
            
            List<Step> stepsToAdd;
            
            if (SelectedDevice != null)
            {
                // 根据选中的设备筛选步骤
                if (SelectedDevice.Name == "插件步骤")
                {
                    // 显示插件步骤
                    stepsToAdd = _allSteps.Where(s => 
                        Devices.FirstOrDefault(d => d.Name == "插件步骤")?.Steps.Contains(s) ?? false).ToList();
                }
                else
                {
                    // 显示选中设备的专属步骤
                    stepsToAdd = SelectedDevice.Steps.ToList();
                }
            }
            else
            {
                // 显示所有步骤
                stepsToAdd = _allSteps.ToList();
            }
            
            // 添加筛选结果
            foreach (var step in stepsToAdd)
            {
                FilteredSteps.Add(step);
            }
        }

        /// <summary>
        /// 搜索筛选步骤
        /// </summary>
        private void FilterSteps()
        {
            // 清空当前筛选结果
            FilteredSteps.Clear();
            
            List<Step> stepsToAdd;
            
            if (string.IsNullOrWhiteSpace(StepSearchText))
            {
                // 先根据设备筛选
                if (SelectedDevice != null)
                {
                    if (SelectedDevice.Name == "插件步骤")
                    {
                        stepsToAdd = _allSteps.Where(s => 
                            Devices.FirstOrDefault(d => d.Name == "插件步骤")?.Steps.Contains(s) ?? false).ToList();
                    }
                    else
                    {
                        stepsToAdd = SelectedDevice.Steps.ToList();
                    }
                }
                else
                {
                    stepsToAdd = _allSteps.ToList();
                }
            }
            else
            {
                var searchText = StepSearchText.ToLower();
                // 先根据设备筛选，再根据搜索文本筛选
                List<Step> deviceFilteredSteps;
                if (SelectedDevice != null)
                {
                    if (SelectedDevice.Name == "插件步骤")
                    {
                        deviceFilteredSteps = _allSteps.Where(s => 
                            Devices.FirstOrDefault(d => d.Name == "插件步骤")?.Steps.Contains(s) ?? false).ToList();
                    }
                    else
                    {
                        deviceFilteredSteps = SelectedDevice.Steps.ToList();
                    }
                }
                else
                {
                    deviceFilteredSteps = _allSteps.ToList();
                }
                
                stepsToAdd = deviceFilteredSteps.Where(s => 
                    s.Name.ToLower().Contains(searchText)).ToList();
            }
            
            // 添加筛选结果
            foreach (var step in stepsToAdd)
            {
                FilteredSteps.Add(step);
            }
        }

        /// <summary>
        /// 加载变量数据
        /// </summary>
        private void LoadVariables()
        {
            try
            {
                // 尝试从一个XML文件中加载所有变量
                string variablesFilePath = Path.Combine(_projectPath, $"{_projectConfig.Name}_Variables.xml");
                if (File.Exists(variablesFilePath))
                {
                    try
                    {
                        var loadedVariables = XmlHelper.DeserializeFromFile<List<ProjectVariable>>(variablesFilePath);
                        if (loadedVariables != null && loadedVariables.Count > 0)
                        {
                            Variables = new ObservableCollection<ProjectVariable>(loadedVariables);
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"加载变量文件失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }

                // 如果没有保存的变量或加载失败，初始化空列表
                Variables = new ObservableCollection<ProjectVariable>();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载变量失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 加载测试用例步骤
        /// </summary>
        private void LoadTestCaseSteps()
        {
            // 尝试从XML文件加载之前保存的步骤
            string testCaseStepsPath = Path.Combine(_projectPath, $"{_projectConfig.Name}_Steps.xml");
            if (File.Exists(testCaseStepsPath))
            {
                try
                {
                    // 反序列化测试用例步骤
                    var loadedSteps = XmlHelper.DeserializeFromFile<List<TestCaseStepItem>>(testCaseStepsPath);
                    if (loadedSteps != null && loadedSteps.Count > 0)
                    {
                        TestCaseSteps = new ObservableCollection<TestCaseStepItem>(loadedSteps);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"加载测试用例步骤失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            // 如果没有保存的步骤或加载失败，初始化空列表
            TestCaseSteps = new ObservableCollection<TestCaseStepItem>();
        }

        /// <summary>
        /// 保存项目
        /// </summary>
        private void SaveProject()
        {
            try
            {
                // 更新测试用例配置
                _projectConfig.ModifiedDate = DateTime.Now;



                // 保存测试用例步骤文件（用于测试时按照文件进行测试）
                string testCaseStepsPath = Path.Combine(_projectPath, $"{_projectConfig.Name}_Steps.xml");
                
                // 创建简化的步骤列表，仅包含步骤名称和设备关联，不保存完整的ResultVariables解析信息
                var simplifiedSteps = new List<TestCaseStepItem>();
                foreach (var stepItem in TestCaseSteps)
                {
                    // 查找原始设备中的对应步骤，确保保存正确的步骤引用
                    var originalStep = _allSteps.FirstOrDefault(s => s.Name == stepItem.Step.Name);
                    if (originalStep != null)
                    {
                        var simplifiedStepItem = new TestCaseStepItem
                        {
                            Index = stepItem.Index,
                            Step = new Step
                            {
                                Name = stepItem.Step.Name,
                                IsEnabled = stepItem.Step.IsEnabled
                                // 不保存ResultVariables，仅保存步骤名称和基本信息
                            },
                            DeviceName = stepItem.DeviceName,
                            InputBindings = stepItem.InputBindings,
                            OutputBindings = stepItem.OutputBindings
                        };
                        simplifiedSteps.Add(simplifiedStepItem);
                    }
                }
                
                bool stepsSaved = XmlHelper.SerializeToFile(simplifiedSteps, testCaseStepsPath);
                if (!stepsSaved)
                {
                    throw new Exception("保存测试用例步骤失败");
                }

                // 保存变量文件（用于测试的时候显示使用）
                SaveVariables();

                MessageBox.Show("项目保存成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            UpdateStatus("项目保存成功");
            // 保存成功，重置修改标志
            _isModified = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"项目保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateStatus("项目保存失败");
            }
        }
        


        /// <summary>
        /// 另存为项目
        /// </summary>
        private void SaveProjectAs()
        {
            // 打开另存为项目窗口
            var saveAsWindow = new SaveAsProjectWindow(_projectConfig, _projectPath)
            {
                Owner = this
            };
            
            if (saveAsWindow.ShowDialog() == true)
            {
                // 更新项目配置和路径
                _projectConfig = saveAsWindow.NewProjectConfig;
                _projectPath = saveAsWindow.NewProjectPath;
                
                // 保存项目
                SaveProject();
                // 保存成功，重置修改标志
                _isModified = false;
                
                // 更新界面上的项目名称
                ProjectNameText.Text = _projectConfig.Name;
                
                // 显示成功消息
                MessageBox.Show("项目另存为成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        /// <summary>
        /// 保存变量
        /// </summary>
        private void SaveVariables()
        {
            try
            {
                // 将所有变量保存到一个XML文件中
                string variablesFilePath = Path.Combine(_projectPath, $"{_projectConfig.Name}_Variables.xml");
                bool variablesSaved = XmlHelper.SerializeToFile(Variables.ToList(), variablesFilePath);
                if (!variablesSaved)
                {
                    throw new Exception("保存变量文件失败");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"保存变量失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 更新状态栏
        /// </summary>
        /// <param name="message">状态消息</param>
        private void UpdateStatus(string message)
        {
            StatusText.Text = $"{DateTime.Now.ToString("HH:mm:ss")} - {message}";
        }

        /// <summary>
        /// 设备选择变化事件
        /// </summary>
        private void DeviceList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DeviceList.SelectedItem is Device selectedDevice)
            {
                SelectedDevice = selectedDevice;
            }
        }

        /// <summary>
        /// 步骤搜索文本变化事件
        /// </summary>
        private void StepSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            StepSearchText = StepSearchTextBox.Text;
            
            // 控制占位符文本的显示/隐藏
            if (SearchPlaceholder != null)
            {
                SearchPlaceholder.Visibility = string.IsNullOrEmpty(StepSearchTextBox.Text) ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        /// <summary>
        /// 双击步骤添加到项目列表
        /// </summary>
        private void StepList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (StepList.SelectedItem is Step selectedStep && SelectedDevice != null)
            {
                // 添加到用例列表，同时设置设备名称
                var newStepItem = new TestCaseStepItem
                {
                    Index = TestCaseSteps.Count + 1,
                    Step = selectedStep,
                    DeviceName = SelectedDevice.Name // 设置步骤关联的设备名称
                };
                
                // 如果是插件步骤，自动生成输入和输出绑定项
                if (SelectedDevice.Name == "插件步骤")
                {
                    // 获取对应的方法名称
                    string methodName = GetMethodNameFromStepName(selectedStep.Name);
                    if (!string.IsNullOrEmpty(methodName))
                    {
                        // 使用PluginStepExecutor分析绑定项
                        try
                        {
                            // 调用PluginStepExecutor的静态方法分析绑定
                            var (inputBindings, outputBindings) = SIAT.TSET.PluginStepExecutor.AnalyzeBindingsFromMethodBody(methodName);
                            newStepItem.InputBindings = inputBindings;
                            newStepItem.OutputBindings = outputBindings;
                        }
                        catch (Exception ex)
                        {
                            // 记录错误但继续执行
                            System.Diagnostics.Debug.WriteLine($"分析插件步骤绑定失败: {ex.Message}");
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(SelectedDevice.SourceClassName))
                {
                    // 代码定义的设备步骤，使用DeviceBase分析绑定项
                    try
                    {
                        var (inputBindings, outputBindings) = SIAT.Devices.DeviceBase.AnalyzeBindings(SelectedDevice.SourceClassName, selectedStep.Name);
                        newStepItem.InputBindings = inputBindings;
                        newStepItem.OutputBindings = outputBindings;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"分析设备步骤绑定失败: {ex.Message}");
                    }
                }
                
                TestCaseSteps.Add(newStepItem);
                UpdateStepIndices();
                // 设置项目已修改
                _isModified = true;
            }
        }

        /// <summary>
        /// 更新步骤序号
        /// </summary>
        private void UpdateStepIndices()
        {
            for (int i = 0; i < TestCaseSteps.Count; i++)
            {
                TestCaseSteps[i].Index = i + 1;
            }
        }

        /// <summary>
        /// 用例步骤选择变化事件
        /// </summary>
        private void TestCaseList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TestCaseList.SelectedItem is TestCaseStepItem selectedStep)
            {
                SelectedTestCaseStep = selectedStep;
            }
        }

        /// <summary>
        /// 双击用例步骤打开配置窗口
        /// </summary>
        private void TestCaseList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (SelectedTestCaseStep != null)
            {
                // 打开步骤配置窗口
                var configWindow = new StepConfigWindow(SelectedTestCaseStep.Step, Variables.ToList(), 
                    SelectedTestCaseStep.InputBindings, SelectedTestCaseStep.OutputBindings)
                {
                    Owner = this
                };

                if (configWindow.ShowDialog() == true)
                {
                    // 保存输入绑定配置
                    SelectedTestCaseStep.InputBindings = configWindow.StepInputBindings;
                    // 保存输出绑定配置
                    SelectedTestCaseStep.OutputBindings = configWindow.StepOutputBindings;
                    // 设置项目已修改
                    _isModified = true;
                }
            }
        }

        /// <summary>
        /// 上移步骤
        /// </summary>
        private void MoveUpButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedTestCaseStep != null)
            {
                int index = TestCaseSteps.IndexOf(SelectedTestCaseStep);
                if (index > 0)
                {
                    // 交换位置
                    var temp = TestCaseSteps[index - 1];
                    TestCaseSteps[index - 1] = SelectedTestCaseStep;
                    TestCaseSteps[index] = temp;
                    // 更新索引
                    UpdateStepIndices();
                    // 更新选中项
                    TestCaseList.SelectedItem = SelectedTestCaseStep;
                    // 设置项目已修改
                    _isModified = true;
                }
            }
        }

        /// <summary>
        /// 下移步骤
        /// </summary>
        private void MoveDownButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedTestCaseStep != null)
            {
                int index = TestCaseSteps.IndexOf(SelectedTestCaseStep);
                if (index < TestCaseSteps.Count - 1)
                {
                    // 交换位置
                    var temp = TestCaseSteps[index + 1];
                    TestCaseSteps[index + 1] = SelectedTestCaseStep;
                    TestCaseSteps[index] = temp;
                    // 更新索引
                    UpdateStepIndices();
                    // 更新选中项
                    TestCaseList.SelectedItem = SelectedTestCaseStep;
                    // 设置项目已修改
                    _isModified = true;
                }
            }
        }

        /// <summary>
        /// 删除步骤
        /// </summary>
        private void DeleteStepButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedTestCaseStep != null)
            {
                TestCaseSteps.Remove(SelectedTestCaseStep);
                UpdateStepIndices();
                SelectedTestCaseStep = null;
                // 设置项目已修改
                _isModified = true;
            }
        }

        /// <summary>
        /// 变量选择变化事件
        /// </summary>
        private void VariableList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (VariableList.SelectedItem is ProjectVariable selectedVariable)
            {
                SelectedVariable = selectedVariable;
            }
        }

        /// <summary>
        /// 添加变量
        /// </summary>
        private void AddVariableButton_Click(object sender, RoutedEventArgs e)
        {
            // 创建新变量
            var newVariable = new ProjectVariable
            {
                VariableName = "新变量",
                VariableType = "String",
                Description = "",
                IsVisible = true,
                QualifiedValue = "",
                IsRange = false,
                Value = "" // 初始化Value属性的默认值
            };
            
            // 打开编辑变量窗口
            VariableEditWindow editWindow = new VariableEditWindow(newVariable);
            if (editWindow.ShowDialog() == true)
            {
                // 如果用户点击了确定，才添加到列表中
                Variables.Add(newVariable);
                VariableList.SelectedItem = newVariable;
                // 设置项目已修改
                _isModified = true;
            }
        }

        /// <summary>
        /// 编辑变量
        /// </summary>
        private void EditVariableButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedVariable != null)
            {
                // 打开编辑变量窗口
                VariableEditWindow editWindow = new VariableEditWindow(SelectedVariable);
                if (editWindow.ShowDialog() == true)
                {
                    // 变量已更新，刷新列表
                    VariableList.Items.Refresh();
                    // 设置项目已修改
                    _isModified = true;
                }
            }
        }

        /// <summary>
        /// 删除变量
        /// </summary>
        private void DeleteVariableButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedVariable != null)
            {
                Variables.Remove(SelectedVariable);
                SelectedVariable = null;
                // 设置项目已修改
                _isModified = true;
            }
        }

        /// <summary>
        /// 双击变量进入编辑
        /// </summary>
        private void VariableList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (SelectedVariable != null)
            {
                // 打开编辑变量窗口
                VariableEditWindow editWindow = new VariableEditWindow(SelectedVariable);
                if (editWindow.ShowDialog() == true)
                {
                    // 变量已更新，刷新列表
                    VariableList.Items.Refresh();
                    // 设置项目已修改
                    _isModified = true;
                }
            }
        }

        /// <summary>
        /// 保存按钮点击事件
        /// </summary>
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            SaveProject();
        }

        /// <summary>
        /// 另存为按钮点击事件
        /// </summary>
        private void SaveAsButton_Click(object sender, RoutedEventArgs e)
        {
            SaveProjectAs();
        }

        /// <summary>
        /// 关闭按钮点击事件
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// 窗口关闭前事件
        /// </summary>
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // 只有当项目被修改时，才询问是否保存
            if (_isModified)
            {
                // 询问是否保存
                MessageBoxResult result = MessageBox.Show("是否保存当前项目的修改？", "保存提示", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    SaveProject();
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    e.Cancel = true;
                }
            }
        }
    }
}