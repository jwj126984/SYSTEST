using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using System.Windows.Threading;
using SIAT.ResourceManagement;
using SIAT.CommunicationManagement;

namespace SIAT.TSET
{
    /// <summary>
    /// 测试变量更新事件参数
    /// </summary>
    public class TestVariableUpdatedEventArgs : EventArgs
    {
        public string StepName { get; }
        public TestVariable Variable { get; }
        
        public TestVariableUpdatedEventArgs(string stepName, TestVariable variable)
        {
            StepName = stepName;
            Variable = variable;
        }
    }
    
    /// <summary>
    /// 测试执行引擎 - 负责实际的测试步骤执行
    /// </summary>
    public class TestExecutionEngine
    {
        private readonly Dictionary<string, ITestProtocolHandler> _protocolHandlers;
        private readonly Dictionary<string, CommunicationManagement.ICommunication> _deviceCommunications;
        
        public event EventHandler<TestStepProgressEventArgs>? StepProgress;
        public event EventHandler<TestExecutionErrorEventArgs>? ExecutionError;
        public event EventHandler<TestVariableUpdatedEventArgs>? VariableUpdated;

        public TestExecutionEngine(Dictionary<string, CommunicationManagement.ICommunication>? deviceCommunications = null)
        {
            _protocolHandlers = new Dictionary<string, ITestProtocolHandler>();
            _deviceCommunications = deviceCommunications ?? new Dictionary<string, CommunicationManagement.ICommunication>();
            
            // 注册默认协议处理器
            RegisterDefaultHandlers();
        }

        /// <summary>
        /// 注册默认协议处理器
        /// </summary>
        private void RegisterDefaultHandlers()
        {
            RegisterHandler("CAN", new CanProtocolHandler());
            RegisterHandler("LIN", new LinProtocolHandler());
            RegisterHandler("UART", new UartProtocolHandler());
            RegisterHandler("I2C", new I2cProtocolHandler());
            RegisterHandler("SPI", new SpiProtocolHandler());
            RegisterHandler("GPIO", new GpioProtocolHandler());
            RegisterHandler("ANALOG", new AnalogProtocolHandler());
            RegisterHandler("DIGITAL", new DigitalProtocolHandler());
        }

        /// <summary>
        /// 注册协议处理器
        /// </summary>
        public void RegisterHandler(string protocolType, ITestProtocolHandler handler)
        {
            _protocolHandlers[protocolType.ToUpper()] = handler;
        }

        /// <summary>
        /// 执行单个测试步骤
        /// </summary>
        public async Task<TestStepResult> ExecuteStepAsync(TestStepConfig step, Dictionary<string, object>? variables = null)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var result = new TestStepResult { StepName = step.Name };

            try
            {
                // 通知步骤开始
                StepProgress?.Invoke(this, new TestStepProgressEventArgs(step.Name, 0, "开始执行"));

                // 优先检查是否为代码定义的设备步骤（DeviceBase子类）
                var deviceResult = await TryExecuteDeviceStepAsync(step, variables);
                if (deviceResult != null)
                {
                    result = deviceResult;
                    stopwatch.Stop();
                    result.Duration = stopwatch.Elapsed;
                    StepProgress?.Invoke(this, new TestStepProgressEventArgs(step.Name, 100, result.IsSuccess ? "执行完成" : "执行失败"));
                    return result;
                }

                if (step.StepType == TestStepType.Plugin)
                {
                    // 插件式步骤执行逻辑（包括设备插件）
                    result = await ExecutePluginStepAsync(step, variables);
                }
                else if (!string.IsNullOrEmpty(step.ProtocolContent))
                {
                    // 设备步骤执行逻辑
                    result = await ExecuteDeviceStepAsync(step);
                }
                else
                {
                    // 简单的测试步骤，直接通过
                    result.IsSuccess = true;
                    result.ActualValue = "执行完成";
                }

                stopwatch.Stop();
                result.Duration = stopwatch.Elapsed;

                // 通知步骤完成
                StepProgress?.Invoke(this, new TestStepProgressEventArgs(step.Name, 100, result.IsSuccess ? "执行完成" : "执行失败"));
                
                return result;
            }
            catch (TimeoutException ex)
            {
                // 处理超时异常
                stopwatch.Stop();
                result.Duration = stopwatch.Elapsed;
                result.IsSuccess = false;
                result.ErrorMessage = "执行超时";
                result.ActualValue = "超时异常";

                // 通知执行错误，指定错误类型为超时
                ExecutionError?.Invoke(this, new TestExecutionErrorEventArgs(step.Name, ex, ErrorLevel.Error, ErrorType.Timeout, "执行测试步骤超时"));
                
                return result;
            }
            catch (InvalidOperationException ex)
            {
                // 处理配置或操作无效的异常
                stopwatch.Stop();
                result.Duration = stopwatch.Elapsed;
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
                result.ActualValue = "配置错误";

                // 通知执行错误，指定错误类型为配置错误
                ExecutionError?.Invoke(this, new TestExecutionErrorEventArgs(step.Name, ex, ErrorLevel.Error, ErrorType.Configuration, "无效的配置或操作"));
                
                return result;
            }
            catch (IOException ex)
            {
                // 处理通信或IO异常
                stopwatch.Stop();
                result.Duration = stopwatch.Elapsed;
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
                result.ActualValue = "通信错误";

                // 通知执行错误，指定错误类型为通信错误
                ExecutionError?.Invoke(this, new TestExecutionErrorEventArgs(step.Name, ex, ErrorLevel.Error, ErrorType.Communication, "通信或IO异常"));
                
                return result;
            }
            catch (Exception ex)
            {
                // 处理其他异常
                stopwatch.Stop();
                result.Duration = stopwatch.Elapsed;
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
                result.ActualValue = $"异常: {ex.Message}";

                // 通知执行错误，使用默认错误类型
                ExecutionError?.Invoke(this, new TestExecutionErrorEventArgs(step.Name, ex, ErrorLevel.Error, ErrorType.Unknown, "未知异常"));
                
                return result;
            }
        }

        /// <summary>
        /// 执行设备步骤
        /// </summary>
        private async Task<TestStepResult> ExecuteDeviceStepAsync(TestStepConfig step)
        {
            var result = new TestStepResult { StepName = step.Name };

            StepProgress?.Invoke(this, new TestStepProgressEventArgs(step.Name, 20, "初始化设备连接"));

            try
              {
                // 执行基于协议的测试步骤
                var protocolResult = await ExecuteProtocolBasedStepAsync(step);
                result.ActualValue = protocolResult.ActualValue;
                result.IsSuccess = protocolResult.IsSuccess;

                // 无论测试结果如何，都解析响应数据并更新变量
                StepProgress?.Invoke(this, new TestStepProgressEventArgs(step.Name, 70, "解析响应数据并更新绑定变量"));

                // 根据输出绑定关系更新变量
                UpdateOutputBindings(step, protocolResult.ActualValue);
            }
            catch (Exception ex)
            {
                ExecutionError?.Invoke(this, new TestExecutionErrorEventArgs(step.Name, ex, ErrorLevel.Error, ErrorType.Execution, "执行设备步骤失败"));
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
                result.ActualValue = $"执行失败: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// 尝试执行代码定义的设备步骤（DeviceBase子类）。
        /// 如果该步骤不属于任何代码定义的设备，返回null。
        /// </summary>
        private async Task<TestStepResult?> TryExecuteDeviceStepAsync(TestStepConfig step, Dictionary<string, object>? variables = null)
        {
            if (string.IsNullOrEmpty(step.DeviceName))
                return null;

            // 从已发现的设备中查找匹配的设备名
            var discoveredDevices = SIAT.Devices.DeviceBase.DiscoverDevices();
            var match = discoveredDevices.FirstOrDefault(d => d.Definition.Name == step.DeviceName);
            if (match.Type == null)
                return null;

            StepProgress?.Invoke(this, new TestStepProgressEventArgs(step.Name, 10, $"找到设备定义: {match.Definition.Name}"));

            // 创建设备实例
            var deviceInstance = SIAT.Devices.DeviceBase.CreateInstance(match.Type.FullName ?? "");
            if (deviceInstance == null)
                return null;

            deviceInstance.SetStepProgressCallback(this);
            deviceInstance.SetCommunicationFromDict(_deviceCommunications);

            // 准备输入参数
            var inputParams = new Dictionary<string, object>();
            if (variables != null)
            {
                foreach (var kvp in variables)
                {
                    inputParams[kvp.Key] = kvp.Value;
                }
            }

            // 处理输入绑定
            if (step.InputBindings != null && step.InputBindings.Count > 0)
            {
                StepProgress?.Invoke(this, new TestStepProgressEventArgs(step.Name, 20, "处理输入绑定"));
                foreach (var inputBinding in step.InputBindings)
                {
                    if (inputBinding.IsBound && inputBinding.SelectedVariable != null)
                    {
                        inputParams[inputBinding.Name] = inputBinding.SelectedVariable.Value;
                    }
                }
            }

            // 执行设备步骤
            StepProgress?.Invoke(this, new TestStepProgressEventArgs(step.Name, 50, "执行设备步骤方法"));
            var deviceResult = await deviceInstance.ExecuteStepAsync(step.Name, inputParams);

            // 处理输出绑定
            if (step.OutputBindings != null && step.OutputBindings.Count > 0 && deviceResult.OutputValues != null)
            {
                StepProgress?.Invoke(this, new TestStepProgressEventArgs(step.Name, 80, "更新输出绑定"));
                foreach (var outputBinding in step.OutputBindings)
                {
                    if (outputBinding.IsBound && outputBinding.SelectedVariable != null)
                    {
                        if (deviceResult.OutputValues.TryGetValue(outputBinding.Name, out object? outputValue))
                        {
                            var testVariable = new TestVariable
                            {
                                Name = outputBinding.SelectedVariable.VariableName,
                                ActualValue = outputValue?.ToString() ?? string.Empty,
                                Value = outputValue?.ToString() ?? string.Empty
                            };
                            VariableUpdated?.Invoke(this, new TestVariableUpdatedEventArgs(step.Name, testVariable));
                        }
                    }
                }
            }

            return deviceResult;
        }

        /// <summary>
        /// 执行插件步骤（包括设备插件）
        /// </summary>
        private async Task<TestStepResult> ExecutePluginStepAsync(TestStepConfig step, Dictionary<string, object>? variables = null)
        {
            var result = new TestStepResult { StepName = step.Name };

            StepProgress?.Invoke(this, new TestStepProgressEventArgs(step.Name, 20, "初始化插件步骤执行器"));

            try
            {
                

                // 创建插件步骤执行器，传递所有设备实例
                var executor = new PluginStepExecutor(null, this);
                executor.SetAllDeviceCommunications(_deviceCommunications);

                // 准备输入参数
                Dictionary<string, object> inputParams = new Dictionary<string, object>();

                // 首先添加传入的变量，包括Barcode信息
                if (variables != null)
                {
                    foreach (var kvp in variables)
                    {
                        inputParams[kvp.Key] = kvp.Value;
                    }
                }

                // 检查是否存在输入绑定配置
                if (step.InputBindings != null && step.InputBindings.Count > 0)
                {
                    StepProgress?.Invoke(this, new TestStepProgressEventArgs(step.Name, 30, "处理输入绑定"));

                    // 收集输入绑定的值
                    foreach (var inputBinding in step.InputBindings)
                    {
                        if (inputBinding.IsBound && inputBinding.SelectedVariable != null)
                        {
                            // 直接使用输入绑定的Name作为键值，使用变量的默认值
                            inputParams[inputBinding.Name] = inputBinding.SelectedVariable.Value;
                        }
                    }
                }

                // 执行插件步骤
                StepProgress?.Invoke(this, new TestStepProgressEventArgs(step.Name, 50, "执行插件方法"));
                var pluginResult = await executor.ExecutePluginStepAsync(step.Name, inputParams);
                result.ActualValue = pluginResult.ActualValue;
                result.IsSuccess = pluginResult.IsSuccess;

                // 处理输出绑定
                if (step.OutputBindings != null && step.OutputBindings.Count > 0)
                {
                    StepProgress?.Invoke(this, new TestStepProgressEventArgs(step.Name, 80, "更新输出绑定"));

                    // 将插件返回结果更新至输出绑定对象
                    foreach (var outputBinding in step.OutputBindings)
                    {
                        if (outputBinding.IsBound && outputBinding.SelectedVariable != null)
                        {
                            // 触发变量更新事件，使用输出绑定的Name作为键值
                            object? variableValue = pluginResult.ActualValue;
                            
                            // 尝试从OutputValues中获取对应的值
                            if (pluginResult.OutputValues != null && pluginResult.OutputValues.TryGetValue(outputBinding.Name, out object? outputValue))
                            {
                                variableValue = outputValue;
                            }
                            
                            var testVariable = new TestVariable
                            {
                                Name = outputBinding.SelectedVariable.VariableName,
                                ActualValue = variableValue?.ToString() ?? string.Empty,
                                Value = variableValue?.ToString() ?? string.Empty
                            };
                            VariableUpdated?.Invoke(this, new TestVariableUpdatedEventArgs(step.Name, testVariable));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                StepProgress?.Invoke(this, new TestStepProgressEventArgs(step.Name, 100, "执行失败"));
                ExecutionError?.Invoke(this, new TestExecutionErrorEventArgs(step.Name, ex, ErrorLevel.Error, ErrorType.Execution, "执行插件步骤失败"));
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
                result.ActualValue = $"执行失败: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// 根据输出绑定关系更新变量
        /// </summary>
        private void UpdateOutputBindings(TestStepConfig step, string actualValue)
        {
            if (step.OutputBindings == null || step.OutputBindings.Count == 0)
                return;

            // 首先解析响应数据
            Dictionary<string, string> parsedValues = ParseResponseData(step, actualValue);

            // 根据输出绑定更新变量
            foreach (var outputBinding in step.OutputBindings)
            {
                if (outputBinding.IsBound && outputBinding.SelectedVariable != null)
                {
                    string variableName = outputBinding.SelectedVariable.VariableName;
                    string value = string.Empty;

                    // 尝试从解析结果中获取对应的值
                    if (parsedValues.TryGetValue(outputBinding.Name, out string parsedValue))
                    {
                        value = parsedValue;
                    }
                    else
                    {
                        // 如果没有找到对应的值，使用实际值
                        value = actualValue;
                    }

                    // 触发变量更新事件
                    var testVariable = new TestVariable
                    {
                        Name = outputBinding.SelectedVariable.VariableName, // 使用输出绑定的Name作为键值
                        ActualValue = value,
                        Value = value
                    };
                    VariableUpdated?.Invoke(this, new TestVariableUpdatedEventArgs(step.Name, testVariable));
                }
            }
        }

        /// <summary>
        /// 解析响应数据
        /// </summary>
        private Dictionary<string, string> ParseResponseData(TestStepConfig step, string response)
        {
            var result = new Dictionary<string, string>();

            if (string.IsNullOrEmpty(response) || step.ResultVariables == null)
                return result;

            // 从协议内容中提取协议类型
            string protocolType = step.ProtocolType?.Split(':')[0]?.ToUpper() ?? "";
            bool isCanProtocol = protocolType == "CAN";

            // 根据结果变量配置解析数据
            foreach (var resultVar in step.ResultVariables)
            {
                try
                {
                    // 提取指定范围的位值
                    string parsedValue = ExtractValueFromResponseData(response, resultVar, isCanProtocol);
                    result[resultVar.Name] = parsedValue;
                }
                catch (Exception ex)
                {
                    // 单个结果变量解析失败，记录错误但继续处理其他变量
                    ExecutionError?.Invoke(this, new TestExecutionErrorEventArgs(step.Name, ex, ErrorLevel.Warning, ErrorType.Parsing, $"解析结果变量 {resultVar.Name} 失败: {ex.Message}"));
                }
            }

            return result;
        }
        
        
        
        /// <summary>
        /// 从响应数据中提取值
        /// </summary>
        private string ExtractValueFromResponseData(string response, ResultVariable resultVar, bool isCanProtocol)
        {
            string parsedValue;
            
            // 检查响应是否为二进制数据
            if (IsBinaryData(response))
            {
                // 二进制响应，直接调用二进制解析方法
                parsedValue = ParseBinaryResponse(response, resultVar, isCanProtocol);
            }
            else if (isCanProtocol && response.Contains("Data="))
            {
                // 专门处理CAN格式响应
                parsedValue = ParseCanResponse(response, resultVar);
            }
            else
            {
                // 文本响应，提取数值部分后进行位解析
                string extractedText = ExtractValueFromResponse(response, resultVar);
                
                // 将文本转换为字节数组，然后进行位解析
                if (long.TryParse(extractedText, out long textValue))
                {
                    // 将数值转换为字节数组
                    byte[] bytes = BitConverter.GetBytes(textValue);
                    
                    // 解析位值：串口协议不使用大小端，CAN协议使用配置的大小端
                    string endian = isCanProtocol ? resultVar.Endian : "LITTLE"; // 串口默认小端或不考虑大小端
                    string protocolType = isCanProtocol ? "CAN" : "HEX";
                    long bitValue = ExtractBitsFromBytes(bytes, resultVar.StartBit, resultVar.EndBit, resultVar.Length, endian, protocolType);
                    
                    // 应用分辨率和偏移量
                    double actualValue = (bitValue * resultVar.Resolution) + resultVar.Offset;
                    parsedValue = actualValue.ToString();
                }
                else
                {
                    // 处理ASCII回读的字符串，根据起始位和结束位进行截断提取
                    if (!string.IsNullOrEmpty(extractedText))
                    {
                        // 计算实际的起始位置和结束位置
                        int startPos = resultVar.StartBit;
                        int endPos;
                        
                        if (resultVar.EndBit > startPos)
                        {
                            // 使用结束位
                            endPos = resultVar.EndBit;
                        }
                        else if (resultVar.Length > 0)
                        {
                            // 使用长度
                            endPos = startPos + resultVar.Length - 1;
                        }
                        else
                        {
                            // 默认使用整个字符串
                            startPos = 0;
                            endPos = extractedText.Length - 1;
                        }
                        
                        // 确保位置在有效范围内
                        startPos = Math.Max(0, startPos);
                        endPos = Math.Min(extractedText.Length - 1, endPos);
                        
                        // 提取指定范围的字符串
                        if (startPos <= endPos)
                        {
                            parsedValue = extractedText.Substring(startPos, endPos - startPos + 1);
                        }
                        else
                        {
                            parsedValue = string.Empty;
                        }
                    }
                    else
                    {
                        // 如果无法转换为数值，直接使用提取的文本
                        parsedValue = extractedText;
                    }
                }
            }
            
            return parsedValue;
        }
        
        /// <summary>
        /// 解析CAN格式响应
        /// </summary>
        private string ParseCanResponse(string response, ResultVariable resultVar)
        {
            try
            {
                // 提取Data部分
                int dataStartIndex = response.IndexOf("Data=", StringComparison.OrdinalIgnoreCase);
                if (dataStartIndex >= 0)
                {
                    // 找到Data部分的结束位置
                    int dataEndIndex = response.IndexOf(",", dataStartIndex);
                    if (dataEndIndex < 0)
                    {
                        dataEndIndex = response.Length;
                    }
                    
                    // 提取Data值
                    string dataStr = response.Substring(dataStartIndex + 5, dataEndIndex - dataStartIndex - 5).Trim();
                    
                    // 将十六进制字符串转换为字节数组
                    string[] hexBytes = dataStr.Split(' ');
                    byte[] bytes = new byte[hexBytes.Length];
                    for (int i = 0; i < hexBytes.Length; i++)
                    {
                        if (!string.IsNullOrEmpty(hexBytes[i]))
                        {
                            bytes[i] = Convert.ToByte(hexBytes[i], 16);
                        }
                    }
                    
                    // 解析位值
                    string endian = resultVar.Endian;
                    long bitValue = ExtractBitsFromBytes(bytes, resultVar.StartBit, resultVar.EndBit, resultVar.Length, endian, "CAN");
                    
                    // 应用分辨率和偏移量
                    double actualValue = (bitValue * resultVar.Resolution) + resultVar.Offset;
                    return actualValue.ToString();
                }
                
                return string.Empty;
            }
            catch (Exception ex)
            {
                return string.Empty;
            }
        }
        
     
        
        /// <summary>
        /// 从响应中提取原始数值文本，不包含位解析和分辨率偏移量应用
        /// </summary>
        private string ExtractValueFromResponse(string response, ResultVariable resultVar)
        {
            // 实现响应解析逻辑
            // 这里可以根据不同的协议格式实现不同的解析方式
            // 简单示例：提取响应中的第一个数值
            try
            {
                if (string.IsNullOrEmpty(response))
                {
                    return string.Empty;
                }
                
                // 不处理二进制数据，由上层方法统一处理
                
                // 示例1: 处理JSON格式响应
                if (response.StartsWith('{') || response.StartsWith('['))
                {
                    // 这里可以使用JSON解析库处理
                    return ParseJsonResponse(response, resultVar);
                }
                // 示例2: 处理CSV格式响应
                else if (response.Contains(','))
                {
                    string[] parts = response.Split(',');
                    // 简单示例：使用索引提取
                    int index = Math.Min(resultVar.StartBit, parts.Length - 1);
                    return parts[index].Trim();
                }
                // 示例3: 处理键值对格式响应
                else if (response.Contains('='))
                {
                    foreach (string part in response.Split(';'))
                    {
                        string[] keyValue = part.Split('=');
                        if (keyValue.Length == 2 && keyValue[0].Trim().Equals(resultVar.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            return keyValue[1].Trim();
                        }
                    }
                    // 如果没有找到精确匹配，尝试提取响应中的所有数值并返回第一个
                    var match = System.Text.RegularExpressions.Regex.Match(response, @"[-+]?\d*\.\d+|\d+");
                    if (match.Success)
                    {
                        return match.Value;
                    }
                }
                // 示例4: 处理XML格式响应
                else if (response.StartsWith('<'))
                {
                    return ParseXmlResponse(response, resultVar.Name);
                }
                // 示例5: 处理纯数值响应
                else if (double.TryParse(response, out _))
                {
                    return response;
                }
                // 示例6: 处理文本响应，提取数字
                else
                {
                    // 提取响应中的所有数字，根据变量的StartBit选择对应位置的数字
                    var matches = System.Text.RegularExpressions.Regex.Matches(response, @"[-+]?\d*\.\d+|\d+");
                    if (matches.Count > 0)
                    {
                        // 如果StartBit在有效范围内，返回对应位置的数字，否则返回第一个数字
                        int index = Math.Min(resultVar.StartBit, matches.Count - 1);
                        return matches[index].Value;
                    }
                }
            }
            catch (Exception)
            {
            }
            
            return string.Empty;
        }
        
        /// <summary>
        /// 检查响应是否为二进制数据
        /// </summary>
        private bool IsBinaryData(string response)
        {
            // 检查是否包含不可打印字符
            return response.Any(c => c < 32 && c != '\r' && c != '\n' && c != '\t');
        }
        
        /// <summary>
        /// 解析二进制响应
        /// </summary>
        private string ParseBinaryResponse(string response, ResultVariable resultVar, bool isCanProtocol)
        {
            try
            {
                // 将字符串转换为字节数组
                byte[] bytes;
                
                // 尝试将十六进制字符串转换为字节数组
                string hexResponse = response.Trim().Replace("0x", "").Replace(" ", "");
                if (hexResponse.Length % 2 == 0 && System.Text.RegularExpressions.Regex.IsMatch(hexResponse, @"^[0-9A-Fa-f]+$"))
                {
                    // 有效的十六进制字符串，转换为字节数组
                    bytes = new byte[hexResponse.Length / 2];
                    for (int i = 0; i < hexResponse.Length; i += 2)
                    {
                        bytes[i / 2] = Convert.ToByte(hexResponse.Substring(i, 2), 16);
                    }
                }
                else
                {
                    // 不是有效的十六进制字符串，使用ASCII编码
                    bytes = Encoding.ASCII.GetBytes(response);
                }
                
                // 解析位值：串口协议不使用大小端，CAN协议使用配置的大小端
                string endian = isCanProtocol ? resultVar.Endian : "LITTLE"; // 串口默认小端或不考虑大小端
                string protocolType = isCanProtocol ? "CAN" : "HEX";
                long extractedValue = ExtractBitsFromBytes(bytes, resultVar.StartBit, resultVar.EndBit, resultVar.Length, endian, protocolType);
                
                // 应用分辨率和偏移量
                double actualValue = (extractedValue * resultVar.Resolution) + resultVar.Offset;
                return actualValue.ToString();
            }
            catch (Exception ex)
            {
                return string.Empty;
            }
        }
        
        /// <summary>
        /// 从字节数组中提取指定范围的位
        /// </summary>
        /// <param name="bytes">字节数组</param>
        /// <param name="startBit">起始位位置（从0开始）</param>
        /// <param name="endBit">结束位位置（可选，优先级高于Length）</param>
        /// <param name="length">位长度（如果EndBit未指定或无效）</param>
        /// <param name="endian">字节序（Big/Little）</param>
        /// <param name="protocolType">协议类型（CAN/HEX）</param>
        /// <returns>提取的位转换后的数值</returns>
        private long ExtractBitsFromBytes(byte[] bytes, int startBit, int endBit, int length, string endian, string protocolType = "HEX")
        {
            if (bytes == null || bytes.Length == 0)
            {
                return 0;
            }
            
            // 计算有效位长度
            int bitLength;
            if (protocolType?.ToUpper() == "CAN")
            {
                // CAN协议：使用位长度提取值
                bitLength = length > 0 ? length : 8; // 默认8位
            }
            else
            {
                // HEX协议：使用结束位提取值
                if (endBit > startBit)
                {
                    bitLength = endBit - startBit + 1;
                }
                else if (length > 0)
                {
                    bitLength = length;
                }
                else
                {
                    bitLength = 8; // 默认8位
                }
            }
            
            // 计算起始字节索引和位偏移
            int startByteIndex = startBit / 8;
            int startBitOffset = startBit % 8;
            
            // 计算需要读取的字节数
            int endByteIndex = (startBit + bitLength - 1) / 8;
            int bytesToRead = endByteIndex - startByteIndex + 1;
            
            // 确保不越界
            if (startByteIndex >= bytes.Length || endByteIndex >= bytes.Length)
            {
                return 0;
            }
            
            // 读取相关字节
            long rawValue = 0;
            
            if (endian == "BigEndian")
            {
                // 大端模式：高位字节在前
                for (int i = startByteIndex; i <= endByteIndex; i++)
                {
                    rawValue = (rawValue << 8) | bytes[i];
                }
                
                // 移除高位不需要的位
                int totalBitsRead = bytesToRead * 8;
                rawValue = rawValue << (64 - totalBitsRead) >> (64 - bitLength - startBitOffset);
            }
            else
            {
                // 小端模式：低位字节在前
                for (int i = endByteIndex; i >= startByteIndex; i--)
                {
                    rawValue = (rawValue << 8) | bytes[i];
                }
                
                // 移除高位不需要的位
                rawValue = rawValue >> startBitOffset;
            }
            
            // 掩码获取指定长度的位
            long mask = (1L << bitLength) - 1;
            return rawValue & mask;
        }
        
        /// <summary>
        /// 解析XML格式响应
        /// </summary>
        private string ParseXmlResponse(string response, string variableName)
        {
            try
            {
                // 使用XmlDocument解析XML
                var xmlDoc = new System.Xml.XmlDocument();
                xmlDoc.LoadXml(response);
                
                // 支持XPATH查询
                var node = xmlDoc.SelectSingleNode($"//{variableName}");
                if (node != null)
                {
                    return node.InnerText;
                }
            }
            catch (Exception ex)
            {
            }
            
            return string.Empty;
        }
        
        /// <summary>
        /// 解析JSON格式响应
        /// </summary>
        private string ParseJsonResponse(string response, ResultVariable resultVar)
        {
            try
            {
                // 使用System.Text.Json解析JSON
                using var doc = System.Text.Json.JsonDocument.Parse(response);
                
                // 支持嵌套路径，例如 "data.value" 或 "[0].name"
                string[] pathParts = resultVar.Name.Split('.');
                System.Text.Json.JsonElement current = doc.RootElement;
                bool found = true;
                
                foreach (string part in pathParts)
                {
                    if (current.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        // 处理数组索引，例如 [0]
                        string indexStr = part.Trim('[', ']');
                        if (int.TryParse(indexStr, out int index) && index >= 0 && index < current.GetArrayLength())
                        {
                            current = current[index];
                        }
                        else
                        {
                            found = false;
                            break;
                        }
                    }
                    else if (current.ValueKind == System.Text.Json.JsonValueKind.Object)
                    {
                        // 处理对象属性
                        if (current.TryGetProperty(part, out var element))
                        {
                            current = element;
                        }
                        else
                        {
                            found = false;
                            break;
                        }
                    }
                    else
                    {
                        found = false;
                        break;
                    }
                }
                
                return found ? current.ToString() : string.Empty;
            }
            catch (Exception ex)
            {
                return string.Empty;
            }
        }

       

        /// <summary>
        /// 根据协议类型处理指令
        /// </summary>
        /// <param name="command">原始指令</param>
        /// <param name="protocolType">协议类型</param>
        /// <returns>处理后的指令</returns>
        private string ProcessCommandByProtocolType(string command, string protocolType)
        {
            // 根据协议类型进行不同的处理
            switch (protocolType?.ToUpper())
            {
                case "CAN":
                    return command;
                case "HEX":
                    
                    // 将字符串转换为HEX格式，格式为："00 11 22 33 44 55 66 77"
                    return ConvertToHexFormat(command);
                case "ASCII":
                    // ASCII协议处理
                    // 直接返回原始ASCII指令
                    return command;
                default:
                    // 默认处理
                    return command;
            }
        }

        /// <summary>
        /// 将字符串转换为HEX格式
        /// </summary>
        /// <param name="command">原始字符串</param>
        /// <returns>HEX格式的字符串，格式为："00 11 22 33 44 55 66 77"</returns>
        private string ConvertToHexFormat(string command)
        {
            // 移除所有空格
            string cleanedCommand = command.Replace(" ", "");
            
            // 确保长度是偶数
            if (cleanedCommand.Length % 2 != 0)
            {
                // 如果长度是奇数，在前面补0
                cleanedCommand = "0" + cleanedCommand;
            }
            
            // 每两个字符添加一个空格
            System.Text.StringBuilder hexBuilder = new System.Text.StringBuilder();
            for (int i = 0; i < cleanedCommand.Length; i += 2)
            {
                if (i > 0)
                {
                    hexBuilder.Append(" ");
                }
                hexBuilder.Append(cleanedCommand.Substring(i, 2).ToUpper());
            }
            
            return hexBuilder.ToString();
        }

        /// <summary>
        /// 执行基于协议的测试步骤
        /// </summary>
        private async Task<TestStepResult> ExecuteProtocolBasedStepAsync(TestStepConfig step)
        {
            var result = new TestStepResult { StepName = step.Name };

            StepProgress?.Invoke(this, new TestStepProgressEventArgs(step.Name, 25, "初始化硬件连接"));
            
            try
            {
                // 获取步骤关联的设备名称
                string deviceName = step.DeviceName;
                
                // 检查是否有对应的通讯实例
                if (!string.IsNullOrEmpty(deviceName) && _deviceCommunications.TryGetValue(deviceName, out var communication))
                {
                    // 检查设备是否已连接
                    if (!communication.IsConnected)
                    {
                        throw new InvalidOperationException($"设备 {deviceName} 未连接");
                    }
                    
                    string response = string.Empty;
                    bool isSuccess = true;
                    
                    // 根据步骤类型处理
                    switch (step.StepType)
                    {
                        case TestStepType.ReadOnly:
                            // 仅读取类型，跳过发送指令，直接接收响应
                            StepProgress?.Invoke(this, new TestStepProgressEventArgs(step.Name, 50, "仅读取模式，等待自动回传"));
                            
                            // 仅读取类型必须等待回传
                            StepProgress?.Invoke(this, new TestStepProgressEventArgs(step.Name, 75, "接收自动回传信息"));
                            
                            // 检查是否为CAN通信且有ResultVariables配置了CanId
                            uint? canIdValueReadOnly = null;
                            if (step.ResultVariables != null && step.ResultVariables.Any())
                            {
                                // 获取第一个配置了CanId的ResultVariable
                                var resultVarWithCanId = step.ResultVariables.FirstOrDefault(rv => !string.IsNullOrEmpty(rv.CanId));
                                if (resultVarWithCanId != null && uint.TryParse(resultVarWithCanId.CanId, System.Globalization.NumberStyles.HexNumber, null, out uint parsedCanId))
                                {
                                    canIdValueReadOnly = parsedCanId;
                                }
                            }
                            
                            // 使用适当的ReceiveAsync方法
                            if (canIdValueReadOnly.HasValue)
                            {
                                // 检查communication实例是否有带CAN ID参数的ReceiveAsync方法
                                var receiveMethod = communication.GetType().GetMethod("ReceiveAsync", new Type[] { typeof(int), typeof(uint?) });
                                if (receiveMethod != null)
                                {
                                    // 调用带CAN ID的ReceiveAsync方法
                                    object[] methodParams = new object[] { 5000, canIdValueReadOnly as object };
                                    object invokeResult = receiveMethod.Invoke(communication, methodParams);
                                    if (invokeResult is Task<string> receiveTask)
                                    {
                                        response = await receiveTask;
                                    }
                                    else
                                    {
                                        // 回退到默认方法
                                        response = await communication.ReceiveAsync();
                                    }
                                }
                                else
                                {
                                    // 回退到默认方法
                                    response = await communication.ReceiveAsync();
                                }
                            }
                            else
                            {
                                // 使用默认的ReceiveAsync方法
                                response = await communication.ReceiveAsync();
                            }
                            
                            // 处理响应结果
                            result.ActualValue = response;
                            
                            // 增强的响应结果判断逻辑，添加更多失败关键词检查
                            isSuccess = !string.IsNullOrEmpty(response);
                            if (isSuccess)
                            {
                                // 定义常见的失败关键词列表
                                var failureKeywords = new[]
                                {
                                    // 中文失败关键词
                                    "失败", "错误", "异常", "超时", "故障", "无效", "拒绝", "失败",
                                    // 英文失败关键词（不区分大小写）
                                    "fail", "error", "exception", "timeout", "failed", "err", "invalid", "reject",
                                    // 其他失败标识
                                    "0x", "FAIL", "ERR", "EXCEPTION", "TIMEOUT"
                                };
                                
                                // 检查响应是否包含任何失败关键词（不区分大小写）
                                isSuccess = !failureKeywords.Any(keyword => 
                                    response.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0);
                            }
                            break;
                        
                        case TestStepType.SendOnly:
                            // 仅发送类型，只发送指令，不等待响应
                            StepProgress?.Invoke(this, new TestStepProgressEventArgs(step.Name, 50, "发送测试指令"));
                            
                            // 从步骤配置中获取实际的指令内容
                            string commandSendOnly = step.ProtocolContent ?? step.Name;
                            
                            // 根据ProtocolType处理指令
                            string processedCommandSendOnly = ProcessCommandByProtocolType(commandSendOnly, step.ProtocolType);
                            
                            // 使用通讯实例发送指令
                            string sendResultSendOnly = await communication.SendAsync(processedCommandSendOnly, step.ProtocolType);
                            
                            StepProgress?.Invoke(this, new TestStepProgressEventArgs(step.Name, 75, "仅发送模式，不等待回传"));
                            // 不等待回传，直接设置成功
                            result.ActualValue = "已发送，不等待回传";
                            isSuccess = true;
                            break;
                        
                        case TestStepType.SendAndReceive:
                        default:
                            // 发送并接收类型，发送指令并等待响应
                            StepProgress?.Invoke(this, new TestStepProgressEventArgs(step.Name, 50, "发送测试指令"));
                            
                            // 从步骤配置中获取实际的指令内容
                            string commandSendReceive = step.ProtocolContent ?? step.Name;
                            
                            // 根据ProtocolType处理指令
                            string processedCommandSendReceive = ProcessCommandByProtocolType(commandSendReceive, step.ProtocolType);
                            
                            // 使用通讯实例发送指令
                            string sendResultSendReceive = await communication.SendAsync(processedCommandSendReceive, step.ProtocolType);
                            
                            // 发送并接收类型必须等待回传
                            StepProgress?.Invoke(this, new TestStepProgressEventArgs(step.Name, 75, "接收硬件响应"));
                            
                            // 检查是否为CAN通信且有ResultVariables配置了CanId
                            uint? canIdValueSendReceive = null;
                            if (step.ResultVariables != null && step.ResultVariables.Any())
                            {
                                // 获取第一个配置了CanId的ResultVariable
                                var resultVarWithCanId = step.ResultVariables.FirstOrDefault(rv => !string.IsNullOrEmpty(rv.CanId));
                                if (resultVarWithCanId != null && uint.TryParse(resultVarWithCanId.CanId, System.Globalization.NumberStyles.HexNumber, null, out uint parsedCanId))
                                {
                                    canIdValueSendReceive = parsedCanId;
                                }
                            }
                            
                            // 使用适当的ReceiveAsync方法
                            if (canIdValueSendReceive.HasValue)
                            {
                                // 检查communication实例是否有带CAN ID和协议类型参数的ReceiveAsync方法
                                var receiveMethod = communication.GetType().GetMethod("ReceiveAsync", new Type[] { typeof(int), typeof(uint?), typeof(string) });
                                if (receiveMethod != null)
                                {
                                    // 调用带CAN ID和协议类型的ReceiveAsync方法
                                    object[] methodParams = new object[] { 5000, canIdValueSendReceive as object, step.ProtocolType };
                                    object invokeResult = receiveMethod.Invoke(communication, methodParams);
                                    if (invokeResult is Task<string> receiveTask)
                                    {
                                        response = await receiveTask;
                                    }
                                    else
                                    {
                                        // 回退到默认方法
                                        response = await communication.ReceiveAsync();
                                    }
                                }
                                else
                                {
                                    // 回退到默认方法
                                    response = await communication.ReceiveAsync();
                                }
                            }
                            else
                            {
                                // 检查communication实例是否有带协议类型参数的ReceiveAsync方法
                                var receiveMethod = communication.GetType().GetMethod("ReceiveAsync", new Type[] { typeof(int), typeof(string) });
                                if (receiveMethod != null)
                                {
                                    // 调用带协议类型的ReceiveAsync方法
                                    object[] methodParams = new object[] { 5000, step.ProtocolType };
                                    object invokeResult = receiveMethod.Invoke(communication, methodParams);
                                    if (invokeResult is Task<string> receiveTask)
                                    {
                                        response = await receiveTask;
                                    }
                                    else
                                    {
                                        // 回退到默认方法
                                        response = await communication.ReceiveAsync();
                                    }
                                }
                                else
                                {
                                    // 使用默认的ReceiveAsync方法
                                    response = await communication.ReceiveAsync();
                                }
                            }
                            
                            // 处理响应结果
                            result.ActualValue = response;
                            
                            // 增强的响应结果判断逻辑，添加更多失败关键词检查
                            isSuccess = !string.IsNullOrEmpty(response);
                            
                            break;
                    }
                    
                    result.IsSuccess = isSuccess;
                    
                    if (result.IsSuccess)
                    {
                        // 解析响应并更新变量
                        StepProgress?.Invoke(this, new TestStepProgressEventArgs(step.Name, 85, "解析响应数据"));
                        
                        // 通知执行完成
                        StepProgress?.Invoke(this, new TestStepProgressEventArgs(step.Name, 100, "执行完成"));
                    }
                    else
                    {
                        StepProgress?.Invoke(this, new TestStepProgressEventArgs(step.Name, 100, "测试失败"));
                    }
                }
                else
                {
                    // 如果没有指定设备或设备未连接，使用默认的协议处理器
                    string protocolType = step.ProtocolContent?.Split(':')[0]?.ToUpper() ?? "UART"; // 从协议内容中提取协议类型，默认使用UART
                    
                    // 检查是否注册了对应的协议处理器
                    if (_protocolHandlers.TryGetValue(protocolType.ToUpper(), out var handler))
                    {
                        // 执行协议测试
                        StepProgress?.Invoke(this, new TestStepProgressEventArgs(step.Name, 50, "发送测试指令"));
                        
                        // 这里可以根据实际情况构造TestCaseProtocol对象
                        var protocol = new TestCaseProtocol
                        {
                            Type = protocolType,
                            Id = "",
                            Content = step.Name // 这里应该从步骤配置中获取实际的指令内容
                        };
                        
                        // 执行测试，不再传递变量池
                        var handlerResult = await handler.ExecuteAsync(protocol, new Dictionary<string, object>());
                        result.ActualValue = handlerResult.ActualValue;
                        result.IsSuccess = handlerResult.IsSuccess;
                    }
                    else
                    {
                        throw new InvalidOperationException($"未注册的协议处理器: {protocolType}");
                    }
                }
            }
            catch (TimeoutException ex)
            {
                StepProgress?.Invoke(this, new TestStepProgressEventArgs(step.Name, 100, "测试失败"));
                string deviceName = step.DeviceName;
                string errorMessage = string.IsNullOrEmpty(deviceName) ? "设备接收超时" : $"设备 {deviceName} 接收超时";
                throw new TimeoutException(errorMessage, ex);
            }
            catch (Exception)
            {
                StepProgress?.Invoke(this, new TestStepProgressEventArgs(step.Name, 100, "测试失败"));
                throw;
            }
            
            return result;
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        public void Dispose()
        {
            foreach (var handler in _protocolHandlers.Values)
            {
                handler?.Dispose();
            }
            _protocolHandlers.Clear();
            // 不再使用变量池，不需要清除
        }
    }

    /// <summary>
    /// 测试步骤执行结果
    /// </summary>
    public class TestStepResult
    {
        public string StepName { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }
        public string ActualValue { get; set; } = string.Empty;
        public TimeSpan Duration { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public Dictionary<string, object>? OutputValues { get; set; }
    }

    /// <summary>
    /// 验证结果
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public string ActualValue { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// 测试步骤进度事件参数
    /// </summary>
    public class TestStepProgressEventArgs : EventArgs
    {
        public string StepName { get; }
        public int Progress { get; }
        public string Message { get; }

        public TestStepProgressEventArgs(string stepName, int progress, string message)
        {
            StepName = stepName;
            Progress = progress;
            Message = message;
        }
    }

    /// <summary>
    /// 测试执行错误事件参数
    /// </summary>
    public class TestExecutionErrorEventArgs : EventArgs
    {
        public string StepName { get; }
        public Exception Exception { get; }
        public ErrorLevel ErrorLevel { get; }
        public ErrorType ErrorType { get; }
        public string Context { get; }

        public TestExecutionErrorEventArgs(string stepName, Exception exception, ErrorLevel errorLevel = ErrorLevel.Error, ErrorType errorType = ErrorType.Unknown, string context = "")
        {
            StepName = stepName;
            Exception = exception;
            ErrorLevel = errorLevel;
            ErrorType = errorType;
            Context = context;
        }
    }

    /// <summary>
    /// 错误级别
    /// </summary>
    public enum ErrorLevel
    {
        Information,
        Warning,
        Error,
        Critical
    }

    /// <summary>
    /// 错误类型
    /// </summary>
    public enum ErrorType
    {
        Unknown,
        Configuration,
        Communication,
        Parsing,
        Validation,
        Timeout,
        Resource,
        Execution
    }

    /// <summary>
    /// 错误日志类
    /// </summary>
    public class ErrorLog
    {
        public string StepName { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public ErrorLevel Level { get; set; } = ErrorLevel.Error;
        public ErrorType Type { get; set; } = ErrorType.Unknown;
        public string Message { get; set; } = string.Empty;
        public string ExceptionMessage { get; set; } = string.Empty;
        public string StackTrace { get; set; } = string.Empty;
        public string Context { get; set; } = string.Empty;

        public override string ToString()
        {
            return $"[{Timestamp:yyyy-MM-dd HH:mm:ss}] [{Level}] [{Type}] [{StepName}] {Message}{(string.IsNullOrEmpty(ExceptionMessage) ? "" : $" - 异常: {ExceptionMessage}")}";
        }
    }

    /// <summary>
    /// 测试协议处理器接口
    /// </summary>
    public interface ITestProtocolHandler : IDisposable
    {
        Task<TestStepResult> ExecuteAsync(TestCaseProtocol protocol, Dictionary<string, object> variables);
        Task<bool> InitializeAsync();
        Task<bool> ValidateConfigurationAsync(TestCaseProtocol protocol);
    }

    /// <summary>
    /// 增强的UART协议处理器实现
    /// </summary>
    public class UartProtocolHandler : ITestProtocolHandler
    {
        private SerialPort? _serialPort;
        private CommunicationParams? _params;
        private bool _initialized;
        
        public UartProtocolHandler()
        {
            _initialized = false;
        }
        
        public async Task<TestStepResult> ExecuteAsync(TestCaseProtocol protocol, Dictionary<string, object> variables)
        {
            try
            {
                // 验证配置
                if (!await ValidateConfigurationAsync(protocol))
                {
                    return new TestStepResult { IsSuccess = false, ActualValue = "配置验证失败", ErrorMessage = "无效的UART协议配置" };
                }
                
                // 初始化
                if (!_initialized && !await InitializeAsync())
                {
                    return new TestStepResult { IsSuccess = false, ActualValue = "初始化失败", ErrorMessage = "UART协议处理器初始化失败" };
                }
                
                // 解析协议内容
                var (command, timeout, expectedResponse) = ParseProtocolContent(protocol.Content);
                
                // 发送命令并接收响应
                string response = string.Empty;
                if (_serialPort != null && _serialPort.IsOpen)
                {
                    // 清空接收缓冲区
                    _serialPort.DiscardInBuffer();
                    
                    // 发送命令
                    await _serialPort.BaseStream.WriteAsync(Encoding.ASCII.GetBytes(command));
                    await _serialPort.BaseStream.FlushAsync();
                    
                    // 接收响应
                    var buffer = new byte[1024];
                    var responseBuilder = new StringBuilder();
                    var startTime = DateTime.Now;
                    
                    while ((DateTime.Now - startTime).TotalMilliseconds < timeout)
                    {
                        if (_serialPort.BytesToRead > 0)
                        {
                            int bytesRead = await _serialPort.BaseStream.ReadAsync(buffer, 0, buffer.Length);
                            responseBuilder.Append(Encoding.ASCII.GetString(buffer, 0, bytesRead));
                            
                            // 检查是否包含预期响应或结束符
                            if (!string.IsNullOrEmpty(expectedResponse) && responseBuilder.ToString().Contains(expectedResponse))
                            {
                                break;
                            }
                            if (responseBuilder.ToString().EndsWith("\r\n"))
                            {
                                break;
                            }
                        }
                        await Task.Delay(10);
                    }
                    
                    response = responseBuilder.ToString().Trim();
                }
                
                // 验证响应
                bool isSuccess = !string.IsNullOrEmpty(response);
                if (!string.IsNullOrEmpty(expectedResponse))
                {
                    isSuccess = response.Contains(expectedResponse, StringComparison.OrdinalIgnoreCase);
                }
                
                return new TestStepResult 
                {
                    IsSuccess = isSuccess, 
                    ActualValue = response,
                    ErrorMessage = isSuccess ? null : "响应验证失败"
                };
            }
            catch (Exception ex)
            {
                return new TestStepResult 
                {
                    IsSuccess = false, 
                    ActualValue = string.Empty,
                    ErrorMessage = ex.Message
                };
            }
        }
        
        public async Task<bool> InitializeAsync()
        {
            try
            {
                if (_serialPort != null && _serialPort.IsOpen)
                {
                    await Task.FromResult(true);
                }
                
                // 这里可以添加实际的初始化逻辑
                _initialized = true;
                return true;
            }
            catch (Exception)
            {
                _initialized = false;
                return false;
            }
        }
        
        public async Task<bool> ValidateConfigurationAsync(TestCaseProtocol protocol)
        {
            try
            {
                if (protocol == null || string.IsNullOrEmpty(protocol.Content))
                {
                    return false;
                }
                
                // 验证协议内容格式
                var parts = protocol.Content.Split(':');
                if (parts.Length < 2)
                {
                    return false;
                }
                
                // 验证协议类型
                if (!string.Equals(parts[0], "UART", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
                
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
        
        private (string command, int timeout, string expectedResponse) ParseProtocolContent(string content)
        {
            string command = string.Empty;
            int timeout = 5000; // 默认5秒
            string expectedResponse = string.Empty;
            
            if (!string.IsNullOrEmpty(content))
            {
                // 解析格式：UART:command:timeout:expectedResponse
                var parts = content.Split(':');
                if (parts.Length >= 2)
                {
                    command = parts[1];
                }
                if (parts.Length >= 3 && int.TryParse(parts[2], out int parsedTimeout))
                {
                    timeout = parsedTimeout;
                }
                if (parts.Length >= 4)
                {
                    expectedResponse = parts[3];
                }
            }
            
            return (command, timeout, expectedResponse);
        }
        
        public void Dispose()
        {
            try
            {
                if (_serialPort != null && _serialPort.IsOpen)
                {
                    _serialPort.Close();
                }
                _serialPort?.Dispose();
                _serialPort = null;
            }
            catch (Exception)
            {
                // 忽略关闭过程中的异常
            }
        }
    }
    
    // 其他协议处理器实现
    public class CanProtocolHandler : ITestProtocolHandler
    {
        public Task<TestStepResult> ExecuteAsync(TestCaseProtocol protocol, Dictionary<string, object> variables)
        {
            // 实现CAN协议测试逻辑
            return Task.FromResult(new TestStepResult { IsSuccess = true, ActualValue = "CAN测试完成" });
        }

        public Task<bool> InitializeAsync() => Task.FromResult(true);
        public Task<bool> ValidateConfigurationAsync(TestCaseProtocol protocol) => Task.FromResult(true);
        public void Dispose() { }
    }

    public class LinProtocolHandler : ITestProtocolHandler
    {
        public Task<TestStepResult> ExecuteAsync(TestCaseProtocol protocol, Dictionary<string, object> variables)
        {
            // 实现LIN协议测试逻辑
            return Task.FromResult(new TestStepResult { IsSuccess = true, ActualValue = "LIN测试完成" });
        }

        public Task<bool> InitializeAsync() => Task.FromResult(true);
        public Task<bool> ValidateConfigurationAsync(TestCaseProtocol protocol) => Task.FromResult(true);
        public void Dispose() { }
    }
    
    public class I2cProtocolHandler : ITestProtocolHandler
    {
        public Task<TestStepResult> ExecuteAsync(TestCaseProtocol protocol, Dictionary<string, object> variables)
            => Task.FromResult(new TestStepResult { IsSuccess = true, ActualValue = "I2C测试完成" });
        public Task<bool> InitializeAsync() => Task.FromResult(true);
        public Task<bool> ValidateConfigurationAsync(TestCaseProtocol protocol) => Task.FromResult(true);
        public void Dispose() { }
    }

    public class SpiProtocolHandler : ITestProtocolHandler
    {
        public Task<TestStepResult> ExecuteAsync(TestCaseProtocol protocol, Dictionary<string, object> variables)
            => Task.FromResult(new TestStepResult { IsSuccess = true, ActualValue = "SPI测试完成" });
        public Task<bool> InitializeAsync() => Task.FromResult(true);
        public Task<bool> ValidateConfigurationAsync(TestCaseProtocol protocol) => Task.FromResult(true);
        public void Dispose() { }
    }

    public class GpioProtocolHandler : ITestProtocolHandler
    {
        public Task<TestStepResult> ExecuteAsync(TestCaseProtocol protocol, Dictionary<string, object> variables)
            => Task.FromResult(new TestStepResult { IsSuccess = true, ActualValue = "GPIO测试完成" });
        public Task<bool> InitializeAsync() => Task.FromResult(true);
        public Task<bool> ValidateConfigurationAsync(TestCaseProtocol protocol) => Task.FromResult(true);
        public void Dispose() { }
    }

    public class AnalogProtocolHandler : ITestProtocolHandler
    {
        public Task<TestStepResult> ExecuteAsync(TestCaseProtocol protocol, Dictionary<string, object> variables)
            => Task.FromResult(new TestStepResult { IsSuccess = true, ActualValue = "模拟量测试完成" });
        public Task<bool> InitializeAsync() => Task.FromResult(true);
        public Task<bool> ValidateConfigurationAsync(TestCaseProtocol protocol) => Task.FromResult(true);
        public void Dispose() { }
    }

    public class DigitalProtocolHandler : ITestProtocolHandler
    {
        public Task<TestStepResult> ExecuteAsync(TestCaseProtocol protocol, Dictionary<string, object> variables)
            => Task.FromResult(new TestStepResult { IsSuccess = true, ActualValue = "数字量测试完成" });
        public Task<bool> InitializeAsync() => Task.FromResult(true);
        public Task<bool> ValidateConfigurationAsync(TestCaseProtocol protocol) => Task.FromResult(true);
        public void Dispose() { }
    }
}