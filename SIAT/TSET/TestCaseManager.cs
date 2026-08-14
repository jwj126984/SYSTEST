using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;
using SIAT.ResourceManagement;

namespace SIAT.TSET
{
    /// <summary>
    /// 测试用例管理器
    /// </summary>
    public class TestCaseManager
    {
        /// <summary>
        /// 加载用例文件
        /// </summary>
        public static TestCaseConfig LoadTestCase(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException("用例文件不存在: " + filePath);
                }

                var serializer = new XmlSerializer(typeof(TestCaseConfig));
                using var reader = new StreamReader(filePath);
                var result = serializer.Deserialize(reader) as TestCaseConfig;
                return result ?? throw new Exception("反序列化用例文件失败: 结果为null");
            }
            catch (Exception ex)
            {
                throw new Exception("加载用例文件失败: " + ex.Message, ex);
            }
        }

        /// <summary>
        /// 验证用例文件格式
        /// </summary>
        public static bool ValidateTestCase(TestCaseConfig testCase)
        {
            if (testCase == null) return false;
            if (string.IsNullOrEmpty(testCase.Name)) return false;
            if (testCase.Projects == null || testCase.Projects.Count == 0) return false;

            foreach (var project in testCase.Projects)
            {
                if (string.IsNullOrEmpty(project.Name)) return false;
                if (string.IsNullOrEmpty(project.ProjectPath)) return false;
                if (!Directory.Exists(project.ProjectPath)) return false;
                
                // 检查项目文件夹中是否至少有一个配置文件
                var projectName = Path.GetFileName(project.ProjectPath);
                var stepsFile = Path.Combine(project.ProjectPath, projectName + "_Steps.xml");
                var variablesFile = Path.Combine(project.ProjectPath, projectName + "_Variables.xml");
                
                if (!File.Exists(stepsFile) && !File.Exists(variablesFile))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 获取用例文件扩展名
        /// </summary>
        public static string GetTestCaseFilter()
        {
            return "用例文件 (*.testcase)|*.testcase|所有文件 (*.*)|*.*";
        }
    }

    /// <summary>
    /// 测试步骤类型
    /// </summary>
    public enum TestStepType
    {
        SendAndReceive,
        SendOnly,
        ReadOnly,
        Plugin
    }

    /// <summary>
    /// 测试步骤配置
    /// </summary>
    [Serializable]
    public class TestStepConfig : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _description = string.Empty;
        private int _order = 0;
        private bool _isVisible = true;
        private string _deviceName = string.Empty;
        private string _protocolContent = string.Empty;
        private string _protocolType = string.Empty;
        private string _expectedValue = string.Empty;
        private string _actualValue = string.Empty;
        private TestStepStatus _status = TestStepStatus.Pending;
        private TimeSpan _duration = TimeSpan.Zero;
        private ObservableCollection<TestVariable> _variables = new ObservableCollection<TestVariable>();
        private bool _waitForResponse = true;
        private TestStepType _stepType = TestStepType.SendAndReceive;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(); }
        }

        public int Order
        {
            get => _order;
            set { _order = value; OnPropertyChanged(); }
        }

        public bool IsVisible
        {
            get => _isVisible;
            set { _isVisible = value; OnPropertyChanged(); }
        }

        public string DeviceName
        {
            get => _deviceName;
            set { _deviceName = value; OnPropertyChanged(); }
        }

        public string ProtocolContent
        {
            get => _protocolContent;
            set { _protocolContent = value; OnPropertyChanged(); }
        }

        public string ProtocolType
        {
            get => _protocolType;
            set { _protocolType = value; OnPropertyChanged(); }
        }

        public string ExpectedValue
        {
            get => _expectedValue;
            set { _expectedValue = value; OnPropertyChanged(); }
        }

        public string ActualValue
        {
            get => _actualValue;
            set { _actualValue = value; OnPropertyChanged(); }
        }

        public TestStepStatus Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        public TimeSpan Duration
        {
            get => _duration;
            set { _duration = value; OnPropertyChanged(); }
        }

        [XmlArray("Variables")]
        [XmlArrayItem("Variable")]
        public ObservableCollection<TestVariable> Variables
        {
            get => _variables;
            set { _variables = value; OnPropertyChanged(); }
        }

        // 结果变量配置
        [XmlArray("ResultVariables")]
        [XmlArrayItem("ResultVariable")]
        public List<ResultVariable> ResultVariables { get; set; } = new List<ResultVariable>();

        // 输入绑定
        [XmlArray("InputBindings")]
        [XmlArrayItem("BindingItem")]
        public List<BindingItem> InputBindings { get; set; } = new List<BindingItem>();

        // 输出绑定
        [XmlArray("OutputBindings")]
        [XmlArrayItem("BindingItem")]
        public List<BindingItem> OutputBindings { get; set; } = new List<BindingItem>();

        public bool WaitForResponse
        {
            get => _waitForResponse;
            set { _waitForResponse = value; OnPropertyChanged(); }
        }

        public TestStepType StepType
        {
            get => _stepType;
            set { _stepType = value; OnPropertyChanged(); }
        }



        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public TestStepConfig() { }

        public TestStepConfig(string name, string description, int order)
        {
            Name = name;
            Description = description;
            Order = order;
        }
    }

    /// <summary>
    /// 测试变量
    /// </summary>
    [Serializable]
    public class TestVariable : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _type = "string";
        private string _value = string.Empty;
        private bool _isVisible = true;
        private string _description = string.Empty;
        private string _qualifiedValue = string.Empty;
        private string _unit = string.Empty;
        private string _actualValue = string.Empty;
        private TestStepStatus _status = TestStepStatus.Pending;
        private string _testTime = string.Empty;
        private TimeSpan _duration = TimeSpan.Zero;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string Type
        {
            get => _type;
            set { _type = value; OnPropertyChanged(); }
        }

        public string Value
        {
            get => _value;
            set { _value = value; OnPropertyChanged(); }
        }

        public bool IsVisible
        {
            get => _isVisible;
            set { _isVisible = value; OnPropertyChanged(); }
        }

        public string Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(); }
        }

        public string QualifiedValue
        {
            get => _qualifiedValue;
            set { _qualifiedValue = value; OnPropertyChanged(); }
        }

        public string Unit
        {
            get => _unit;
            set { _unit = value; OnPropertyChanged(); }
        }

        public string ActualValue
        {
            get => _actualValue;
            set { _actualValue = value; OnPropertyChanged(); }
        }

        public TestStepStatus Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        public string TestTime
        {
            get => _testTime;
            set { _testTime = value; OnPropertyChanged(); }
        }

        public TimeSpan Duration
        {
            get => _duration;
            set { _duration = value; OnPropertyChanged(); }
        }

        public TestVariable() { }

        public TestVariable(string name, string type, string value, bool isVisible = true)
        {
            Name = name;
            Type = type;
            Value = value;
            IsVisible = isVisible;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// 测试步骤状态
    /// </summary>
    public enum TestStepStatus
    {
        Pending,
        Running,
        Passed,
        Failed,
        Skipped
    }

    /// <summary>
    /// 测试步骤项（用于反序列化现有步骤文件）
    /// </summary>
    [Serializable]
    public class TestCaseStepItem
    {
        public int Index { get; set; }
        public TestCaseStep Step { get; set; } = new TestCaseStep();
        public string DeviceName { get; set; } = string.Empty;
        public bool IsVisible { get; set; } = true;
        public List<ResultVariableItem> ResultVariableBindings { get; set; } = new List<ResultVariableItem>();
        
        [XmlArray("InputBindings")]
        [XmlArrayItem("InputBindingItem")]
        public List<SIAT.InputBindingItem> InputBindings { get; set; } = new List<SIAT.InputBindingItem>();
        
        [XmlArray("OutputBindings")]
        [XmlArrayItem("OutputBindingItem")]
        public List<SIAT.OutputBindingItem> OutputBindings { get; set; } = new List<SIAT.OutputBindingItem>();
    }

    /// <summary>
    /// 测试步骤
    /// </summary>
    [Serializable]
    public class TestCaseStep
    {
        public string Name { get; set; } = string.Empty;
        public TestCaseProtocol Protocol { get; set; } = new TestCaseProtocol();
        public List<ResultVariable> ResultVariables { get; set; } = new List<ResultVariable>();
        public bool IsEnabled { get; set; } = true;
    }

    /// <summary>
    /// 测试协议
    /// </summary>
    [Serializable]
    public class TestCaseProtocol
    {
        public string Type { get; set; } = string.Empty;
        public bool IsCanFd { get; set; } = false;
        public string Id { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    /// <summary>
    /// 结果变量
    /// </summary>
    [Serializable]
    public class ResultVariable
    {
        public string Name { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public int StartBit { get; set; } = 0;
        public int EndBit { get; set; } = 0;
        public int Length { get; set; } = 0;
        public double Resolution { get; set; } = 1.0;
        public double Offset { get; set; } = 0.0;
        public string Endian { get; set; } = string.Empty;
        public string CanId { get; set; } = string.Empty;
    }

    /// <summary>
    /// 结果变量项
    /// </summary>
    [Serializable]
    public class ResultVariableItem
    {
        public string Name { get; set; } = string.Empty;
        public bool IsBound { get; set; } = false;
        public ProjectVariable SelectedVariable { get; set; } = new ProjectVariable();
    }

    /// <summary>
    /// 绑定项
    /// </summary>
    [Serializable]
    public class BindingItem
    {
        public string Name { get; set; } = string.Empty;
        public bool IsBound { get; set; } = false;
        public ProjectVariable SelectedVariable { get; set; } = new ProjectVariable();
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// 项目变量（用于反序列化现有变量文件）
    /// </summary>
    [Serializable]
    [XmlType(TypeName = "TSETProjectVariable")]
    public class ProjectVariable
    {
        public string VariableName { get; set; } = string.Empty;
        public string VariableType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsVisible { get; set; } = true;
        public string QualifiedValue { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public bool IsRange { get; set; } = false;
        public string Value { get; set; } = string.Empty;
    }

    /// <summary>
    /// 测试项目配置
    /// </summary>
    [Serializable]
    public class TestProjectConfig
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public int Order { get; set; } = 0;

        [XmlArray("Steps")]
        [XmlArrayItem("Step")]
        public List<TestStepConfig> Steps { get; set; } = new List<TestStepConfig>();

        [XmlArray("Variables")]
        [XmlArrayItem("Variable")]
        public List<TestVariable> Variables { get; set; } = new List<TestVariable>();

        public TestProjectConfig() { }

        public TestProjectConfig(string name, string description)
        {
            Name = name;
            Description = description;
            Order = 0;
        }

        public TestProjectConfig(string name, string description, int order)
        {
            Name = name;
            Description = description;
            Order = order;
        }

        /// <summary>
        /// 从文件加载项目配置
        /// </summary>
        public static TestProjectConfig LoadFromFile(string projectFolderPath)
        {
            try
            {
                if (!Directory.Exists(projectFolderPath))
                {
                    throw new DirectoryNotFoundException("项目文件夹不存在: " + projectFolderPath);
                }

                var projectConfig = new TestProjectConfig();
                
                // 从文件夹名获取项目名称
                projectConfig.Name = Path.GetFileName(projectFolderPath);
                projectConfig.Description = "从文件夹加载的项目配置";
                
                // 加载所有设备文件
                var devices = LoadAllDevices();
                
                // 加载步骤文件
                var stepsFilePath = Path.Combine(projectFolderPath, projectConfig.Name + "_Steps.xml");
                if (File.Exists(stepsFilePath))
                {
                    try
                    {
                        var stepsSerializer = new XmlSerializer(typeof(List<TestCaseStepItem>));
                        using var stepsReader = new StreamReader(stepsFilePath);
                        var stepItems = stepsSerializer.Deserialize(stepsReader) as List<TestCaseStepItem>;
                        
                        if (stepItems != null)
                        {
                            // 转换为TestStepConfig
                            foreach (var stepItem in stepItems)
                            {
                                // 获取设备名称
                                string deviceName = stepItem.DeviceName;
                                // 获取步骤名称
                                string stepName = stepItem.Step?.Name ?? "未命名步骤";
                                
                                // 从设备中查找对应的步骤解析信息
                                List<ResultVariable>? resultVariables = null;
                                string protocolContent = string.Empty;
                                bool waitForResponse = true; // 默认等待回传
                                SIAT.ResourceManagement.Step? originalStep = null;
                                
                                var device = devices.FirstOrDefault(d => d.Name == deviceName);
                                if (device != null)
                                {
                                    originalStep = device.Steps.FirstOrDefault(s => s.Name == stepName);
                                    if (originalStep != null)
                                    {
                                        // 使用设备文件中的原始解析信息
                                    string protocolId = originalStep.Protocol?.Id ?? "0x100";
                                    string protocolData = originalStep.Protocol?.Content ?? string.Empty;
                                    
                                    // 根据协议类型生成不同的ProtocolContent格式
                                    if (originalStep.Protocol?.Type == SIAT.ResourceManagement.ProtocolType.CAN)
                                    {
                                        // 对于CAN协议，使用ID+data格式
                                        protocolContent = $"ID={protocolId},DATA={protocolData}";
                                    }
                                    else
                                    {
                                        // 对于其他协议（如UART），直接使用数据内容
                                        protocolContent = protocolData;
                                    }
                                    waitForResponse = originalStep.WaitForResponse;
                                        if (originalStep.ResultVariables != null)
                                        {
                                            resultVariables = new List<ResultVariable>();
                                            foreach (var rmVar in originalStep.ResultVariables)
                                            {
                                                // 创建TSET命名空间的ResultVariable对象
                                                var tsetVar = new ResultVariable
                                                {
                                                    Name = rmVar.Name,
                                                    Unit = rmVar.Unit,
                                                    StartBit = rmVar.StartBit,
                                                    EndBit = rmVar.EndBit,
                                                    Length = rmVar.Length,
                                                    Resolution = rmVar.Resolution,
                                                    Offset = rmVar.Offset,
                                                    Endian = rmVar.Endian.ToString(),
                                                    CanId = rmVar.CanId
                                                };
                                                resultVariables.Add(tsetVar);
                                            }
                                        }
                                    }
                                }
                                
                                // 确定步骤类型
                                TestStepType stepType = TestStepType.SendAndReceive;
                                
                                // 优先检查是否为插件步骤（通过设备名称判断）
                                if (deviceName == "插件步骤")
                                {
                                    stepType = TestStepType.Plugin;
                                }
                                else if (originalStep != null)
                                {
                                    switch (originalStep.StepType)
                                    {
                                        case SIAT.ResourceManagement.StepType.ReadOnly:
                                            stepType = TestStepType.ReadOnly;
                                            break;
                                        case SIAT.ResourceManagement.StepType.SendOnly:
                                            stepType = TestStepType.SendOnly;
                                            break;
                                        default:
                                            stepType = TestStepType.SendAndReceive;
                                            break;
                                    }
                                }
                                

                                // 处理输入绑定
                                var inputBindings = new List<BindingItem>();
                                if (stepItem.InputBindings != null)
                                {
                                    foreach (var inputBindingItem in stepItem.InputBindings)
                                    {
                                        if (inputBindingItem.InputVariable != null)
                                        {
                                            // 转换SIAT.ProjectVariable到SIAT.TSET.ProjectVariable
                                            var tsetProjectVar = new ProjectVariable
                                            {
                                                VariableName = inputBindingItem.InputVariable.VariableName,
                                                VariableType = inputBindingItem.InputVariable.VariableType,
                                                Description = inputBindingItem.InputVariable.Description,
                                                IsVisible = inputBindingItem.InputVariable.IsVisible,
                                                QualifiedValue = inputBindingItem.InputVariable.QualifiedValue,
                                                Unit = inputBindingItem.InputVariable.Unit,
                                                IsRange = inputBindingItem.InputVariable.IsRange,
                                                Value = inputBindingItem.InputVariable.Value
                                            };

                                            // 确保NAME值正确设置，优先使用绑定项的Name属性
                                            string bindingName = !string.IsNullOrEmpty(inputBindingItem.Name) ? inputBindingItem.Name : 
                                                (!string.IsNullOrEmpty(inputBindingItem.InputDescription) ? inputBindingItem.InputDescription : 
                                                (inputBindingItem.InputVariable.VariableName ?? "未命名输入"));

                                            inputBindings.Add(new BindingItem
                                            {
                                                Name = bindingName,
                                                IsBound = true,
                                                SelectedVariable = tsetProjectVar,
                                                Description = inputBindingItem.InputDescription
                                            });
                                        }
                                    }
                                }

                                // 处理输出绑定
                                var outputBindings = new List<BindingItem>();
                                // 优先使用新的OutputBindings属性
                                if (stepItem.OutputBindings != null && stepItem.OutputBindings.Count > 0)
                                {
                                    foreach (var outputBindingItem in stepItem.OutputBindings)
                                    {
                                        if (outputBindingItem.OutputVariable != null)
                                        {
                                            // 转换SIAT.ProjectVariable到SIAT.TSET.ProjectVariable
                                            var tsetProjectVar = new ProjectVariable
                                            {
                                                VariableName = outputBindingItem.OutputVariable.VariableName,
                                                VariableType = outputBindingItem.OutputVariable.VariableType,
                                                Description = outputBindingItem.OutputVariable.Description,
                                                IsVisible = outputBindingItem.OutputVariable.IsVisible,
                                                QualifiedValue = outputBindingItem.OutputVariable.QualifiedValue,
                                                Unit = outputBindingItem.OutputVariable.Unit,
                                                IsRange = outputBindingItem.OutputVariable.IsRange,
                                                Value = outputBindingItem.OutputVariable.Value
                                            };

                                            // 确保NAME值正确设置，优先使用绑定项的Name属性
                                            string bindingName = !string.IsNullOrEmpty(outputBindingItem.Name) ? outputBindingItem.Name : 
                                                (!string.IsNullOrEmpty(outputBindingItem.OutputDescription) ? outputBindingItem.OutputDescription : 
                                                (outputBindingItem.OutputVariable.VariableName ?? "未命名输出"));

                                            outputBindings.Add(new BindingItem
                                            {
                                                Name = bindingName,
                                                IsBound = true,
                                                SelectedVariable = tsetProjectVar,
                                                Description = outputBindingItem.OutputDescription
                                            });
                                        }
                                    }
                                }
                                // 兼容旧的ResultVariableBindings
                                else if (stepItem.ResultVariableBindings != null)
                                {
                                    foreach (var bindingItem in stepItem.ResultVariableBindings)
                                    {
                                        outputBindings.Add(new BindingItem
                                        {
                                            Name = bindingItem.Name,
                                            IsBound = bindingItem.IsBound,
                                            SelectedVariable = bindingItem.SelectedVariable,
                                            Description = $"Output binding for {bindingItem.Name}"
                                        });
                                    }
                                }

                                var stepConfig = new TestStepConfig
                                {
                                    Name = stepName,
                                    Description = stepName,
                                    Order = stepItem.Index,
                                    IsVisible = stepItem.IsVisible,
                                    DeviceName = deviceName,
                                    ProtocolContent = protocolContent,
                                    ProtocolType = originalStep?.Protocol?.Type.ToString() ?? string.Empty,
                                    WaitForResponse = waitForResponse,
                                    StepType = stepType,
                                    ResultVariables = resultVariables ?? new List<ResultVariable>(),
                                    InputBindings = inputBindings,
                                    OutputBindings = outputBindings
                                };
                                
                                projectConfig.Steps.Add(stepConfig);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("加载步骤文件失败: " + ex.Message, ex);
                    }
                }
                
                // 加载变量文件
                var variablesFilePath = Path.Combine(projectFolderPath, projectConfig.Name + "_Variables.xml");
                if (File.Exists(variablesFilePath))
                {
                    try
                    {
                        // 使用SIAT.ProjectVariable类型来反序列化变量文件
                        var variablesSerializer = new XmlSerializer(typeof(List<SIAT.ProjectVariable>));
                        using var variablesReader = new StreamReader(variablesFilePath);
                        var projectVariables = variablesSerializer.Deserialize(variablesReader) as List<SIAT.ProjectVariable>;
                        
                        if (projectVariables != null)
                        {
                            // 转换为TestVariable
                            foreach (var projectVar in projectVariables)
                            {
                                var testVar = new TestVariable
                                {
                                    Name = projectVar.VariableName ?? "未命名变量",
                                    Type = projectVar.VariableType ?? "string",
                                    Value = projectVar.Value ?? "", // 使用项目变量的Value属性值
                                    QualifiedValue = projectVar.QualifiedValue ?? "", // 判断值
                                    IsVisible = projectVar.IsVisible,
                                    Description = projectVar.Description ?? "",
                                    Unit = projectVar.Unit ?? "-", // 使用变量的实际单位，默认值为"-"
                                    ActualValue = "", // 实测值初始为空
                                    Status = TestStepStatus.Pending // 初始状态
                                };
                                
                                projectConfig.Variables.Add(testVar);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("加载变量文件失败: " + ex.Message, ex);
                    }
                }
                
                return projectConfig;
            }
            catch (Exception ex)
            {
                throw new Exception("加载项目文件失败: " + ex.Message, ex);
            }
        }
        
        /// <summary>
        /// 加载所有设备文件
        /// </summary>
        private static List<Device> LoadAllDevices()
        {
            var devices = new List<Device>();
            
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
                            devices.Add(device);
                        }
                    }
                    catch (Exception ex)
                    {
                        // 忽略单个设备加载失败
                        System.Diagnostics.Debug.WriteLine($"加载设备文件失败: {ex.Message}");
                    }
                }
            }
            
            return devices;
        }
    }
}