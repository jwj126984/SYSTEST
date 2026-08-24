using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SIAT.TSET
{
    /// <summary>
    /// 插件步骤执行器 - 负责执行插件式步骤，根据步骤名称调用对应的方法
    /// </summary>
    public class PluginStepExecutor
    {
        private readonly Dictionary<string, object> _inputParams;
        private readonly Dictionary<string, object> _outputParams = new Dictionary<string, object>();
        private readonly object _stepProgressCallback;
        private readonly Dictionary<string, SIAT.CommunicationManagement.ICommunication> _deviceCommunications;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="variables">输入参数字典</param>
        /// <param name="stepProgressCallback">步骤进度回调对象</param>
        public PluginStepExecutor(Dictionary<string, object>? variables = null, object? stepProgressCallback = null)
        {
            _inputParams = variables ?? new Dictionary<string, object>();
            _stepProgressCallback = stepProgressCallback;
            _deviceCommunications = new Dictionary<string, SIAT.CommunicationManagement.ICommunication>();
        }

        /// <summary>
        /// 设置设备通信实例
        /// </summary>
        /// <param name="deviceName">设备名称</param>
        /// <param name="communication">通信实例</param>
        public void SetDeviceCommunication(string deviceName, SIAT.CommunicationManagement.ICommunication communication)
        {
            if (!string.IsNullOrEmpty(deviceName) && communication != null)
            {
                _deviceCommunications[deviceName] = communication;
            }
        }

        /// <summary>
        /// 设置所有设备通信实例
        /// </summary>
        /// <param name="deviceCommunications">设备通信实例字典</param>
        public void SetAllDeviceCommunications(Dictionary<string, SIAT.CommunicationManagement.ICommunication> deviceCommunications)
        {
            if (deviceCommunications != null)
            {
                foreach (var item in deviceCommunications)
                {
                    _deviceCommunications[item.Key] = item.Value;
                }
            }
        }

        /// <summary>
        /// 执行插件步骤（支持输入参数）
        /// </summary>
        /// <param name="stepName">步骤名称</param>
        /// <param name="inputParams">输入参数</param>
        /// <returns>测试步骤执行结果</returns>
        public async Task<TestStepResult> ExecutePluginStepAsync(string stepName, Dictionary<string, object>? inputParams)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var result = new TestStepResult { StepName = stepName };

            try
            {
                // 通知步骤开始
                NotifyStepProgress(stepName, 0, "开始执行插件步骤");

                // 清空输出参数，确保每次步骤执行的输出独立
                _outputParams.Clear();

                // 根据步骤名称调用对应的方法
                var methodName = GetMethodNameFromStepName(stepName);
                var method = GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);

                if (method != null)
                {
                    // 如果有输入参数，将其合并到_inputParams中
            if (inputParams != null)
            {
                foreach (var param in inputParams)
                {
                    _inputParams[param.Key] = param.Value;
                }
            }

                    // 调用对应方法执行步骤
                    object? methodResult = method.Invoke(this, null);

                    // 处理方法返回值
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
                        // 方法没有返回值，默认设置为成功
                        result.IsSuccess = true;
                        result.ActualValue = "执行完成";
                    }
                }
                else
                {
                    // 未找到对应的方法
                    result.IsSuccess = false;
                    result.ErrorMessage = $"未找到对应的插件步骤方法: {methodName}";
                    result.ActualValue = "方法不存在";
                }

                stopwatch.Stop();
                result.Duration = stopwatch.Elapsed;
                
                // 设置输出值，仅包含通过SetOutputValue设置的输出参数
                result.OutputValues = new Dictionary<string, object>(_outputParams);

                // 通知步骤完成
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

                // 通知步骤失败
                NotifyStepProgress(stepName, 100, "执行失败");

                return result;
            }
        }

        /// <summary>
        /// 通过设备发送数据
        /// </summary>
        /// <param name="deviceName">设备名称</param>
        /// <param name="data">要发送的数据</param>
        /// <returns>发送结果</returns>
        protected async Task<string> SendDataAsync(string deviceName, string data)
        {
            if (_deviceCommunications.TryGetValue(deviceName, out var communication))
            {
                return await communication.SendAsync(data);
            }
            throw new InvalidOperationException($"设备 {deviceName} 未绑定或未连接");
        }

        /// <summary>
        /// 通过设备发送字节数据
        /// </summary>
        /// <param name="deviceName">设备名称</param>
        /// <param name="data">要发送的字节数据</param>
        /// <returns>发送结果</returns>
        protected async Task<string> SendDataAsync(string deviceName, byte[] data)
        {
            if (_deviceCommunications.TryGetValue(deviceName, out var communication))
            {
                return await communication.SendAsync(data);
            }
            throw new InvalidOperationException($"设备 {deviceName} 未绑定或未连接");
        }

        /// <summary>
        /// 通过设备接收数据
        /// </summary>
        /// <param name="deviceName">设备名称</param>
        /// <returns>接收到的数据</returns>
        protected async Task<string> ReceiveDataAsync(string deviceName)
        {
            if (_deviceCommunications.TryGetValue(deviceName, out var communication))
            {
                return await communication.ReceiveAsync();
            }
            throw new InvalidOperationException($"设备 {deviceName} 未绑定或未连接");
        }

        /// <summary>
        /// 通过设备接收字节数据
        /// </summary>
        /// <param name="deviceName">设备名称</param>
        /// <param name="asBytes">是否以字节形式接收</param>
        /// <returns>接收到的字节数据</returns>
        protected async Task<byte[]> ReceiveDataAsync(string deviceName, bool asBytes = true)
        {
            if (_deviceCommunications.TryGetValue(deviceName, out var communication))
            {
                // 调用现有的ReceiveAsync方法获取字符串数据
                string response = await communication.ReceiveAsync();
                // 将字符串转换为字节数组
                return System.Text.Encoding.UTF8.GetBytes(response);
            }
            throw new InvalidOperationException($"设备 {deviceName} 未绑定或未连接");
        }

        /// <summary>
        /// 检查设备是否已连接
        /// </summary>
        /// <param name="deviceName">设备名称</param>
        /// <returns>设备是否已连接</returns>
        protected bool IsDeviceConnected(string deviceName)
        {
            if (_deviceCommunications.TryGetValue(deviceName, out var communication))
            {
                return communication.IsConnected;
            }
            return false;
        }

        /// <summary>
        /// 从步骤名称获取方法名称
        /// </summary>
        /// <param name="stepName">步骤名称</param>
        /// <returns>方法名称</returns>
        private string GetMethodNameFromStepName(string stepName)
        {
            // 简单处理：将步骤名称转换为PascalCase格式作为方法名
            // 移除特殊字符，将空格后的首字母大写
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

            // 添加Step后缀以区分方法
            return methodName + "Step";
        }

        /// <summary>
        /// 通知步骤进度
        /// </summary>
        /// <param name="stepName">步骤名称</param>
        /// <param name="progress">进度百分比</param>
        /// <param name="message">进度消息</param>
        private void NotifyStepProgress(string stepName, int progress, string message)
        {
            if (_stepProgressCallback != null)
            {
                try
                {
                    // 直接调用TestExecutionEngine的StepProgress事件
                    // 查找StepProgress事件字段
                    var stepProgressField = _stepProgressCallback.GetType().GetField("StepProgress", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (stepProgressField != null)
                    {
                        var stepProgressDelegate = stepProgressField.GetValue(_stepProgressCallback) as EventHandler<TestStepProgressEventArgs>;
                        if (stepProgressDelegate != null)
                        {
                            // 创建进度事件参数并触发事件
                            var eventArgs = new TestStepProgressEventArgs(stepName, progress, message);
                            stepProgressDelegate.Invoke(this, eventArgs);
                        }
                    }
                }
                catch { }
            }
        }

        // 用于记录绑定使用情况的字典
        private static readonly Dictionary<string, (List<string> inputVariables, List<string> outputVariables)> _methodBindingsCache = new Dictionary<string, (List<string> inputVariables, List<string> outputVariables)>();

        /// <summary>
        /// 分析插件步骤方法的输入和输出绑定
        /// 通过特性（Attribute）识别输入和输出变量
        /// </summary>
        /// <param name="methodName">插件步骤方法名称</param>
        /// <returns>输入绑定和输出绑定列表</returns>
        public static (List<SIAT.InputBindingItem>, List<SIAT.OutputBindingItem>) AnalyzeBindingsFromMethodBody(string methodName)
        {
            var inputBindings = new List<SIAT.InputBindingItem>();
            var outputBindings = new List<SIAT.OutputBindingItem>();

            try
            {
                // 检查缓存中是否有该方法的绑定信息
                if (_methodBindingsCache.TryGetValue(methodName, out var cachedBindings))
                {
                    // 使用缓存的绑定信息
                    foreach (var inputVar in cachedBindings.inputVariables)
                    {
                        inputBindings.Add(new SIAT.InputBindingItem
                        {
                            Name = inputVar,
                            InputDescription = inputVar,
                            InputVariable = new SIAT.ProjectVariable()
                        });
                    }
                    foreach (var outputVar in cachedBindings.outputVariables)
                    {
                        outputBindings.Add(new SIAT.OutputBindingItem
                        {
                            Name = outputVar,
                            OutputDescription = outputVar,
                            OutputVariable = new SIAT.ProjectVariable()
                        });
                    }
                    return (inputBindings, outputBindings);
                }

                // 获取当前类型
                var executorType = typeof(PluginStepExecutor);
                // 获取指定的方法
                var method = executorType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
                
                if (method != null)
                {
                    var inputVars = new List<string>();
                    var outputVars = new List<string>();

                    // 从特性中读取输入绑定信息
                    var inputAttributes = method.GetCustomAttributes<InputBindingAttribute>(false);
                    foreach (var attr in inputAttributes)
                    {
                        inputBindings.Add(new SIAT.InputBindingItem
                        {
                            Name = attr.Name,
                            InputDescription = attr.Description,
                            InputVariable = new SIAT.ProjectVariable()
                        });
                        inputVars.Add(attr.Name);
                    }

                    // 从特性中读取输出绑定信息
                    var outputAttributes = method.GetCustomAttributes<OutputBindingAttribute>(false);
                    foreach (var attr in outputAttributes)
                    {
                        outputBindings.Add(new SIAT.OutputBindingItem
                        {
                            Name = attr.Name,
                            OutputDescription = attr.Description,
                            OutputVariable = new SIAT.ProjectVariable()
                        });
                        outputVars.Add(attr.Name);
                    }
                    
                    // 缓存绑定信息
                    _methodBindingsCache[methodName] = (
                        inputVariables: inputVars,
                        outputVariables: outputVars
                    );
                }
            }
            catch (Exception ex)
            {
                // 分析失败时返回空列表
                System.Diagnostics.Debug.WriteLine($"分析绑定失败: {ex.Message}");
            }

            return (inputBindings, outputBindings);
        }



        

        /// <summary>
        /// 获取输入绑定变量的值
        /// </summary>
        /// <param name="name">输入绑定的Name属性值</param>
        /// <param name="defaultValue">默认值</param>
        /// <returns>变量值，如果不存在返回默认值</returns>
        protected string GetInputValue(string name, string defaultValue = default)
        {
            try
            {
                // 直接从_inputParams中获取对应的值
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
        /// <param name="variableName">变量名称</param>
        /// <param name="value">变量值</param>
        /// <remarks>输出值将通过返回结果传递给TestExecutionEngine，此方法现在仅用于内部记录</remarks>
        protected void SetOutputValue(string variableName, object value)
        {
            // 记录输出值，实际输出将通过返回结果传递
            _outputParams[variableName] = value;
        }

        /// <summary>
        /// 检查输入变量是否存在
        /// </summary>
        /// <param name="variableName">变量名称</param>
        /// <returns>是否存在</returns>
        protected bool HasInputValue(string variableName)
        {
            return _inputParams.ContainsKey(variableName);
        }

        /// <summary>
        /// 检测插件步骤执行时使用的绑定
        /// 通过特性（Attribute）识别输入和输出变量
        /// </summary>
        /// <param name="stepName">步骤名称</param>
        /// <returns>实际使用的输入和输出绑定</returns>
        public (List<string> usedInputVariables, List<string> usedOutputVariables) DetectUsedBindings(string stepName)
        {
            var usedInputVariables = new List<string>();
            var usedOutputVariables = new List<string>();

            try
            {
                // 获取对应的方法名称
                var methodName = GetMethodNameFromStepName(stepName);
                var method = GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);

                if (method != null)
                {
                    // 检查缓存中是否有该方法的绑定信息
                    if (_methodBindingsCache.TryGetValue(methodName, out var cachedBindings))
                    {
                        usedInputVariables = cachedBindings.inputVariables;
                        usedOutputVariables = cachedBindings.outputVariables;
                    }
                    else
                    {
                        // 从特性中读取输入绑定信息
                        var inputAttributes = method.GetCustomAttributes<InputBindingAttribute>(false);
                        foreach (var attr in inputAttributes)
                        {
                            usedInputVariables.Add(attr.Name);
                        }

                        // 从特性中读取输出绑定信息
                        var outputAttributes = method.GetCustomAttributes<OutputBindingAttribute>(false);
                        foreach (var attr in outputAttributes)
                        {
                            usedOutputVariables.Add(attr.Name);
                        }

                        // 缓存绑定信息
                        _methodBindingsCache[methodName] = (inputVariables: usedInputVariables, outputVariables: usedOutputVariables);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"检测绑定使用情况失败: {ex.Message}");
            }

            return (usedInputVariables, usedOutputVariables);
        }









        #region 插件步骤方法
        // ==================== 插件步骤方法 ==================== // ==================== // ==================== // ==================== // ==================== // ====================
        // 注意：以下方法为示例，实际使用时需要根据具体的插件步骤名称创建对应的方法
        // 方法命名规则：步骤名称转换为PascalCase后添加Step后缀



        [InputBinding("time", "延时时间(ms)")]
        public async Task<TestStepResult> 延时Step()
        {
            string time = GetInputValue("time", "");

            Thread.Sleep(int.Parse(time));

            return new TestStepResult
            {
                StepName = "延时时间",
                IsSuccess = true,
                ActualValue = "成功",
                Duration = TimeSpan.FromMilliseconds(1000)
            };

        }

        [OutputBinding("CurrentTime", "当前时间")]
        public async Task<TestStepResult> 获取当前时间Step()
        {
            string currentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

            SetOutputValue("CurrentTime", currentTime);

            return new TestStepResult
            {
                StepName = "获取当前时间",
                IsSuccess = true,
                ActualValue = currentTime,
                Duration = TimeSpan.FromMilliseconds(100)
            };
        }

        /// <summary>
        /// 变量显示步骤：读取绑定变量的当前值并作为本步骤的实际值返回。
        /// 在测试项中每添加一次该步骤，测试时就会显示该步骤当前绑定变量的当前值。
        /// </summary>
        [InputBinding("Variable", "绑定变量")]
        public async Task<TestStepResult> 变量显示Step()
        {
            // 从输入绑定中读取所绑定变量的当前值
            string variableValue = GetInputValue("Variable", string.Empty);

            await Task.CompletedTask;

            return new TestStepResult
            {
                StepName = "变量显示",
                IsSuccess = true,
                ActualValue = variableValue,
                Duration = TimeSpan.FromMilliseconds(100)
            };
        }

        /// <summary>
        /// 加法步骤：计算两个值的和。输入A + 输入B，结果可通过输出绑定写回变量。
        /// </summary>
        [InputBinding("ValueA", "值A")]
        [InputBinding("ValueB", "值B")]
        [OutputBinding("Result", "计算结果")]
        public async Task<TestStepResult> 加法Step()
        {
            string a = GetInputValue("ValueA", "0");
            string b = GetInputValue("ValueB", "0");
            double result = ParseDouble(a) + ParseDouble(b);
            string resultStr = FormatResult(result);

            SetOutputValue("Result", resultStr);
            await Task.CompletedTask;

            return new TestStepResult
            {
                StepName = "加法",
                IsSuccess = true,
                ActualValue = resultStr,
                Duration = TimeSpan.FromMilliseconds(100)
            };
        }

        /// <summary>
        /// 减法步骤：计算两个值的差。输入A - 输入B，结果可通过输出绑定写回变量。
        /// </summary>
        [InputBinding("ValueA", "值A")]
        [InputBinding("ValueB", "值B")]
        [OutputBinding("Result", "计算结果")]
        public async Task<TestStepResult> 减法Step()
        {
            string a = GetInputValue("ValueA", "0");
            string b = GetInputValue("ValueB", "0");
            double result = ParseDouble(a) - ParseDouble(b);
            string resultStr = FormatResult(result);

            SetOutputValue("Result", resultStr);
            await Task.CompletedTask;

            return new TestStepResult
            {
                StepName = "减法",
                IsSuccess = true,
                ActualValue = resultStr,
                Duration = TimeSpan.FromMilliseconds(100)
            };
        }

        /// <summary>
        /// 乘法步骤：计算两个值的积。输入A × 输入B，结果可通过输出绑定写回变量。
        /// </summary>
        [InputBinding("ValueA", "值A")]
        [InputBinding("ValueB", "值B")]
        [OutputBinding("Result", "计算结果")]
        public async Task<TestStepResult> 乘法Step()
        {
            string a = GetInputValue("ValueA", "0");
            string b = GetInputValue("ValueB", "0");
            double result = ParseDouble(a) * ParseDouble(b);
            string resultStr = FormatResult(result);

            SetOutputValue("Result", resultStr);
            await Task.CompletedTask;

            return new TestStepResult
            {
                StepName = "乘法",
                IsSuccess = true,
                ActualValue = resultStr,
                Duration = TimeSpan.FromMilliseconds(100)
            };
        }

        /// <summary>
        /// 除法步骤：计算两个值的商。输入A ÷ 输入B，结果可通过输出绑定写回变量。
        /// </summary>
        [InputBinding("ValueA", "值A")]
        [InputBinding("ValueB", "值B")]
        [OutputBinding("Result", "计算结果")]
        public async Task<TestStepResult> 除法Step()
        {
            string a = GetInputValue("ValueA", "0");
            string b = GetInputValue("ValueB", "0");
            double divisor = ParseDouble(b);
            string resultStr;
            if (divisor == 0)
            {
                resultStr = "除数为0";
                SetOutputValue("Result", resultStr);
                await Task.CompletedTask;
                return new TestStepResult
                {
                    StepName = "除法",
                    IsSuccess = false,
                    ActualValue = resultStr,
                    Duration = TimeSpan.FromMilliseconds(100)
                };
            }
            double result = ParseDouble(a) / divisor;
            resultStr = FormatResult(result);

            SetOutputValue("Result", resultStr);
            await Task.CompletedTask;

            return new TestStepResult
            {
                StepName = "除法",
                IsSuccess = true,
                ActualValue = resultStr,
                Duration = TimeSpan.FromMilliseconds(100)
            };
        }

        /// <summary>
        /// 将字符串安全解析为double，解析失败时返回0。
        /// </summary>
        private static double ParseDouble(string value)
        {
            return double.TryParse(value, out double result) ? result : 0;
        }

        /// <summary>
        /// 格式化计算结果：整数时省略小数部分，否则保留必要精度。
        /// </summary>
        private static string FormatResult(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return value.ToString();
            if (Math.Abs(value - Math.Round(value)) < 1e-9)
                return Math.Round(value).ToString("0");
            return value.ToString("0.######");
        }

        #endregion



        #region 插件扩展函数

        // ==================== 插件扩展函数 ====================
        // ==================== // ==================== // ==================== // ==================== // ==================== // ==================== // ====================

        public static class CRC8Calculator
        {
            private static readonly byte[] Table = new byte[256];
            private const byte Polynomial = 0x07; // 常用的CRC8多项式

            static CRC8Calculator()
            {
                for (int i = 0; i < 256; i++)
                {
                    byte value = (byte)i;
                    for (int j = 0; j < 8; j++)
                    {
                        if ((value & 0x80) != 0)
                        {
                            value = (byte)((value << 1) ^ Polynomial);
                        }
                        else
                        {
                            value <<= 1;
                        }
                    }
                    Table[i] = value;
                }
            }

            public static byte CalculateCRC8(byte[] data)
            {
                byte crc = 0;
                foreach (byte b in data)
                {
                    crc = Table[(crc ^ b) & 0xFF];
                }
                return crc;
            }

            public static byte CalculateCRC8(List<byte> data)
            {
                return CalculateCRC8(data.ToArray());
            }
        }

        public enum ChannelState
        {
            Empty,      // EM
            Success,    // OK
            Error,      // ER
            Unstart,      // Unstart
            Unknown     // 其他状态
        }

        private Dictionary<int, ChannelState> ParseChannelStates(string response)
        {
            var states = new Dictionary<int, ChannelState>();
            var matches = Regex.Matches(response, @"CH\[(\d+)\]=(EM|OK|ER)");

            foreach (Match match in matches)
            {
                if (match.Groups.Count == 3)
                {
                    int channelNumber = int.Parse(match.Groups[1].Value);
                    string stateCode = match.Groups[2].Value;

                    ChannelState state =
                    stateCode == "EM" ? ChannelState.Empty :
                    stateCode == "OK" ? ChannelState.Success :
                    stateCode == "ER" ? ChannelState.Error :
                    ChannelState.Unknown;

                    states[channelNumber] = state;
                }
            }
            return states;
        }
        public static (double, double)? GetAAndB(double[] x, double[] y)
        {
            // 确保数组长度一致
            if (x.Length != y.Length)
            {
                Console.WriteLine("X和Y的数组长度必须相同！");
                return (double.NaN, double.NaN);
            }
            if (x.Distinct().Count() == 1)
            {
                Console.WriteLine("所有的 x 值相同，无法拟合直线。");
                return null;
            }

            if (y.Distinct().Count() == 1)
            {
                Console.WriteLine("所有的 y 值相同，拟合水平线。");
                double c = y[0];
                return (0, c);
            }

            int n = x.Length;

            double sX = x.Sum();
            double sY = y.Sum();
            double sXY = 0;
            for (int i = 0; i < x.Length; i++)
            {
                sXY += x[i] * y[i];
            }
            double sX2 = x.Sum(m => m * m);
            double slope = (n * sXY - sX * sY) / (n * sX2 - sX * sX);
            double intercept = (sY - slope * sX) / n;

            ushort aW = (ushort)(Math.Abs(slope) * 1000);
            var aWTemp = aW / 1000.0;
            short bW = (short)(intercept * 1000);
            var bWTemp = bW / 1000.0;


            // 计算所需的和
            double sumX = x.Sum();
            double sumY = y.Sum();
            double sumXY = x.Zip(y, (xi, yi) => xi * yi).Sum();
            double sumX2 = x.Sum(xi => xi * xi);

            // 计算斜率a和截距b
            double a = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
            double b = (sumY - a * sumX) / n;
            return (aWTemp, bWTemp);
        }
        #endregion
    }
}
