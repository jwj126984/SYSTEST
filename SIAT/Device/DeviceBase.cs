using SIAT.ResourceManagement;
using SIAT.TSET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace SIAT.Devices
{
    /// <summary>
    /// 设备定义特性，用于标记一个类为设备
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class DeviceDefinitionAttribute : Attribute
    {
        public string Name { get; set; }
        public string? Description { get; set; }
        public CommunicationType CommunicationType { get; set; } = CommunicationType.Serial;
        public DeviceType DeviceType { get; set; } = DeviceType.Generic;

        public DeviceDefinitionAttribute(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }
    }

    /// <summary>
    /// 步骤定义特性，用于标记一个方法为设备步骤
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class StepDefinitionAttribute : Attribute
    {
        public string Name { get; set; }
        public string? Description { get; set; }
        public StepType StepType { get; set; } = StepType.SendAndReceive;

        public StepDefinitionAttribute(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }
    }

    /// <summary>
    /// 设备基类，所有设备类应继承此类并在Device文件夹下定义。
    /// 提供输入输出绑定和通讯基础设施（与PluginStepExecutor一致）。
    /// </summary>
    public abstract class DeviceBase
    {
        private readonly Dictionary<string, object> _inputParams = new();
        private CommunicationManagement.ICommunication? _communication;
        private object? _stepProgressCallback;

        /// <summary>
        /// 设备名称（从DeviceDefinitionAttribute读取）
        /// </summary>
        public string DeviceName
        {
            get
            {
                var attr = GetType().GetCustomAttribute<DeviceDefinitionAttribute>();
                return attr?.Name ?? GetType().Name;
            }
        }

        /// <summary>
        /// 设置步骤进度回调对象（TestExecutionEngine实例）
        /// </summary>
        public void SetStepProgressCallback(object? callback)
        {
            _stepProgressCallback = callback;
        }

        /// <summary>
        /// 设置设备通信实例（直接绑定到本设备）
        /// </summary>
        public void SetCommunication(CommunicationManagement.ICommunication? communication)
        {
            _communication = communication;
        }

        /// <summary>
        /// 从通信字典中找到本设备对应的通信实例并绑定
        /// </summary>
        public void SetCommunicationFromDict(Dictionary<string, CommunicationManagement.ICommunication> deviceCommunications)
        {
            if (deviceCommunications != null && deviceCommunications.TryGetValue(DeviceName, out var comm))
            {
                _communication = comm;
            }
        }

        /// <summary>
        /// 执行设备步骤
        /// </summary>
        public async Task<TestStepResult> ExecuteStepAsync(string stepName, Dictionary<string, object>? inputParams)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var result = new TestStepResult { StepName = stepName };

            try
            {
                NotifyStepProgress(stepName, 0, $"开始执行设备步骤: {stepName}");

                // 合并输入参数
                if (inputParams != null)
                {
                    foreach (var param in inputParams)
                    {
                        _inputParams[param.Key] = param.Value;
                    }
                }

                // 查找带StepDefinition特性的方法
                var method = FindStepMethod(stepName);

                if (method != null)
                {
                    object? methodResult = method.Invoke(this, null);

                    if (methodResult is Task<TestStepResult> taskResult)
                    {
                        result = await taskResult;
                    }
                    else if (methodResult is TestStepResult syncResult)
                    {
                        result = syncResult;
                    }
                    else
                    {
                        result.IsSuccess = true;
                        result.ActualValue = "执行完成";
                    }
                }
                else
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = $"未找到设备步骤方法: {stepName}";
                    result.ActualValue = "方法不存在";
                }

                stopwatch.Stop();
                result.Duration = stopwatch.Elapsed;
                result.OutputValues = new Dictionary<string, object>(_inputParams);

                NotifyStepProgress(stepName, 100, result.IsSuccess ? "执行完成" : "执行失败");
                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                result.Duration = stopwatch.Elapsed;
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
                result.ActualValue = "执行异常";
                NotifyStepProgress(stepName, 100, "执行失败");
                return result;
            }
        }

        /// <summary>
        /// 查找步骤对应的方法
        /// </summary>
        private MethodInfo? FindStepMethod(string stepName)
        {
            var methods = GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            foreach (var method in methods)
            {
                var stepAttr = method.GetCustomAttribute<StepDefinitionAttribute>();
                if (stepAttr != null && stepAttr.Name == stepName)
                {
                    return method;
                }
            }
            return null;
        }

        #region 通讯辅助方法

        /// <summary>
        /// 发送字符串数据
        /// </summary>
        protected async Task SendDataAsync(string data)
        {
            if (_communication != null)
            {
                await _communication.SendAsync(data);
                return;
            }
            throw new InvalidOperationException($"设备 {DeviceName} 未绑定通信实例");
        }

        /// <summary>
        /// 发送字节数据
        /// </summary>
        protected async Task SendDataAsync(byte[] data)
        {
            if (_communication != null)
            {
                await _communication.SendAsync(data);
                return;
            }
            throw new InvalidOperationException($"设备 {DeviceName} 未绑定通信实例");
        }

        /// <summary>
        /// 接收字符串数据
        /// </summary>
        protected async Task<string> ReceiveDataAsync()
        {
            if (_communication != null)
            {
                return await _communication.ReceiveAsync();
            }
            throw new InvalidOperationException($"设备 {DeviceName} 未绑定通信实例");
        }

        /// <summary>
        /// 接收字节数据
        /// </summary>
        protected async Task<byte[]> ReceiveDataAsync(bool asBytes = true)
        {
            if (_communication != null)
            {
                string response = await _communication.ReceiveAsync();
                return System.Text.Encoding.UTF8.GetBytes(response);
            }
            throw new InvalidOperationException($"设备 {DeviceName} 未绑定通信实例");
        }

        /// <summary>
        /// 检查设备是否已连接
        /// </summary>
        protected bool IsDeviceConnected()
        {
            return _communication?.IsConnected ?? false;
        }

        #endregion

        #region 输入输出绑定辅助方法

        /// <summary>
        /// 获取输入绑定变量的值
        /// </summary>
        protected string GetInputValue(string name, string defaultValue = "")
        {
            try
            {
                if (_inputParams.TryGetValue(name, out var value))
                {
                    return value?.ToString() ?? defaultValue;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取输入值失败: {ex.Message}");
            }
            return defaultValue;
        }

        /// <summary>
        /// 设置输出绑定变量的值
        /// </summary>
        protected void SetOutputValue(string variableName, object value)
        {
            _inputParams[variableName] = value;
        }

        /// <summary>
        /// 检查输入变量是否存在
        /// </summary>
        protected bool HasInputValue(string variableName)
        {
            return _inputParams.ContainsKey(variableName);
        }

        #endregion

        /// <summary>
        /// 通知步骤进度
        /// </summary>
        private void NotifyStepProgress(string stepName, int progress, string message)
        {
            if (_stepProgressCallback != null)
            {
                try
                {
                    var stepProgressField = _stepProgressCallback.GetType().GetField("StepProgress", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (stepProgressField != null)
                    {
                        var stepProgressDelegate = stepProgressField.GetValue(_stepProgressCallback) as EventHandler<TestStepProgressEventArgs>;
                        stepProgressDelegate?.Invoke(this, new TestStepProgressEventArgs(stepName, progress, message));
                    }
                }
                catch { }
            }
        }

        #region 静态方法 - 设备和步骤发现

        /// <summary>
        /// 获取指定设备类中定义的所有步骤信息（包含输入输出绑定）
        /// </summary>
        public static List<Step> GetSteps(Type deviceType)
        {
            var steps = new List<Step>();
            var methods = deviceType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            foreach (var method in methods)
            {
                var stepAttr = method.GetCustomAttribute<StepDefinitionAttribute>();
                if (stepAttr != null)
                {
                    var step = new Step
                    {
                        Name = stepAttr.Name,
                        StepType = stepAttr.StepType,
                        IsEnabled = true
                    };

                    // 提取输入绑定
                    var inputAttrs = method.GetCustomAttributes<InputBindingAttribute>(false);
                    foreach (var attr in inputAttrs)
                    {
                        step.InputBindings.Add(new StepBindingInfo { Name = attr.Name, Description = attr.Description, IsInput = true });
                    }

                    // 提取输出绑定
                    var outputAttrs = method.GetCustomAttributes<OutputBindingAttribute>(false);
                    foreach (var attr in outputAttrs)
                    {
                        step.OutputBindings.Add(new StepBindingInfo { Name = attr.Name, Description = attr.Description, IsInput = false });
                    }

                    steps.Add(step);
                }
            }
            return steps;
        }

        /// <summary>
        /// 从程序集中扫描所有继承自DeviceBase且带有DeviceDefinition特性的设备类
        /// </summary>
        public static List<(Type Type, DeviceDefinitionAttribute Definition)> DiscoverDevices()
        {
            var result = new List<(Type, DeviceDefinitionAttribute)>();
            var assembly = Assembly.GetExecutingAssembly();

            Type?[]? types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types;
            }

            if (types == null) return result;

            foreach (var type in types)
            {
                if (type == null) continue;
                if (type.IsClass && !type.IsAbstract && typeof(DeviceBase).IsAssignableFrom(type))
                {
                    var attr = type.GetCustomAttribute<DeviceDefinitionAttribute>();
                    if (attr != null)
                    {
                        result.Add((type, attr));
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// 分析设备步骤方法的输入和输出绑定
        /// </summary>
        public static (List<InputBindingItem> inputBindings, List<OutputBindingItem> outputBindings) AnalyzeBindings(string sourceClassName, string stepName)
        {
            var inputBindings = new List<InputBindingItem>();
            var outputBindings = new List<OutputBindingItem>();

            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var deviceType = assembly.GetType(sourceClassName);
                if (deviceType == null) return (inputBindings, outputBindings);

                var methods = deviceType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
                foreach (var method in methods)
                {
                    var stepAttr = method.GetCustomAttribute<StepDefinitionAttribute>();
                    if (stepAttr != null && stepAttr.Name == stepName)
                    {
                        var inputAttrs = method.GetCustomAttributes<InputBindingAttribute>(false);
                        foreach (var attr in inputAttrs)
                        {
                            inputBindings.Add(new InputBindingItem
                            {
                                Name = attr.Name,
                                InputDescription = attr.Description,
                                InputVariable = new ProjectVariable()
                            });
                        }

                        var outputAttrs = method.GetCustomAttributes<OutputBindingAttribute>(false);
                        foreach (var attr in outputAttrs)
                        {
                            outputBindings.Add(new OutputBindingItem
                            {
                                Name = attr.Name,
                                OutputDescription = attr.Description,
                                OutputVariable = new ProjectVariable()
                            });
                        }
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"分析设备步骤绑定失败: {ex.Message}");
            }

            return (inputBindings, outputBindings);
        }

        /// <summary>
        /// 根据SourceClassName创建DeviceBase实例
        /// </summary>
        public static DeviceBase? CreateInstance(string sourceClassName)
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var deviceType = assembly.GetType(sourceClassName);
                if (deviceType != null && typeof(DeviceBase).IsAssignableFrom(deviceType))
                {
                    return (DeviceBase?)Activator.CreateInstance(deviceType);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"创建设备实例失败: {ex.Message}");
            }
            return null;
        }

        #endregion
    }
}
