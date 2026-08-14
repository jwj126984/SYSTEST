using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SIAT.ResourceManagement;
using SIAT.TSET;

namespace SIAT.Devices
{
    /// <summary>
    /// N8331电池模拟器/电源设备
    /// 通讯协议：SCPI命令（文本），通过串口发送
    /// </summary>
    [DeviceDefinition("N8331电池模拟器", Description = "N8331四通道电池模拟器", CommunicationType = CommunicationType.Serial)]
    public class N8331Device : DeviceBase
    {
        /// <summary>
        /// 查询通道电压
        /// 协议: MEASure{n}:VOLTage?
        /// </summary>
        [StepDefinition("查询通道电压", Description = "查询指定通道的回显电压值(V)", StepType = StepType.ReadOnly)]
        [InputBinding("Channel", "通道号(1-4)")]
        [OutputBinding("Voltage", "电压值(V)")]
        public async Task<TestStepResult> 查询通道电压Step()
        {
            int channel = int.Parse(GetInputValue("Channel", "1"));

            if (!IsDeviceConnected())
            {
                return FailResult("查询通道电压", "设备未连接");
            }

            try
            {
                await SendDataAsync($"MEASure{channel}:VOLTage?\n");
                string response = await ReceiveDataAsync();

                if (double.TryParse(response.Trim(), out double voltage))
                {
                    SetOutputValue("Voltage", voltage);
                    return OkResult("查询通道电压", $"{voltage} V");
                }
                return FailResult("查询通道电压", $"返回值解析失败: {response}");
            }
            catch (Exception ex)
            {
                return FailResult("查询通道电压", ex.Message);
            }
        }

        /// <summary>
        /// 查询通道电流
        /// 协议: MEASure{n}:CURRent?
        /// </summary>
        [StepDefinition("查询通道电流", Description = "查询指定通道的回显电流值(mA)", StepType = StepType.ReadOnly)]
        [InputBinding("Channel", "通道号(1-4)")]
        [OutputBinding("Current", "电流值(mA)")]
        public async Task<TestStepResult> 查询通道电流Step()
        {
            int channel = int.Parse(GetInputValue("Channel", "1"));

            if (!IsDeviceConnected())
            {
                return FailResult("查询通道电流", "设备未连接");
            }

            try
            {
                await SendDataAsync( $"MEASure{channel}:CURRent?\n");
                string response = await ReceiveDataAsync();

                if (double.TryParse(response.Trim(), out double current))
                {
                    SetOutputValue("Current", current);
                    return OkResult("查询通道电流", $"{current} mA");
                }
                return FailResult("查询通道电流", $"返回值解析失败: {response}");
            }
            catch (Exception ex)
            {
                return FailResult("查询通道电流", ex.Message);
            }
        }

        /// <summary>
        /// 查询通道功率
        /// 协议: MEASure{n}:POWer?
        /// </summary>
        [StepDefinition("查询通道功率", Description = "查询指定通道的实时功率值(W)", StepType = StepType.ReadOnly)]
        [InputBinding("Channel", "通道号(1-4)")]
        [OutputBinding("Power", "功率值(W)")]
        public async Task<TestStepResult> 查询通道功率Step()
        {
            int channel = int.Parse(GetInputValue("Channel", "1"));

            if (!IsDeviceConnected())
            {
                return FailResult("查询通道功率", "设备未连接");
            }

            try
            {
                await SendDataAsync( $"MEASure{channel}:POWer?\n");
                string response = await ReceiveDataAsync();

                if (double.TryParse(response.Trim(), out double power))
                {
                    SetOutputValue("Power", power);
                    return OkResult("查询通道功率", $"{power} W");
                }
                return FailResult("查询通道功率", $"返回值解析失败: {response}");
            }
            catch (Exception ex)
            {
                return FailResult("查询通道功率", ex.Message);
            }
        }

        /// <summary>
        /// 设置通道输出开关
        /// 协议: OUTPut{n}:ONOFF {0|1} + OUTP{n}:ONOFF?
        /// </summary>
        [StepDefinition("设置通道输出", Description = "开启或关闭指定通道的输出", StepType = StepType.SendAndReceive)]
        [InputBinding("Channel", "通道号(1-4)")]
        [InputBinding("Enable", "是否使能(true/false)")]
        [OutputBinding("Result", "设置结果")]
        public async Task<TestStepResult> 设置通道输出Step()
        {
            int channel = int.Parse(GetInputValue("Channel", "1"));
            bool enable = bool.Parse(GetInputValue("Enable", "true"));

            if (!IsDeviceConnected())
            {
                return FailResult("设置通道输出", "设备未连接");
            }

            try
            {
                await SendDataAsync( $"OUTPut{channel}:ONOFF {(enable ? "1" : "0")}\n");
                await SendDataAsync( $"OUTP{channel}:ONOFF?\n");
                string response = await ReceiveDataAsync();

                bool success = response.Trim() == (enable ? "1" : "0");
                SetOutputValue("Result", success ? "成功" : "失败");

                return new TestStepResult
                {
                    StepName = "设置通道输出",
                    IsSuccess = success,
                    ActualValue = success ? $"通道{channel}输出{(enable ? "开启" : "关闭")}成功" : "设置失败"
                };
            }
            catch (Exception ex)
            {
                return FailResult("设置通道输出", ex.Message);
            }
        }

        /// <summary>
        /// 设置功能模式
        /// 协议: OUTPut{n}:MODE {0|1} + OUTP{n}:MODE?
        /// </summary>
        [StepDefinition("设置功能模式", Description = "设置通道为电源模式(false)或扫描模式(true)", StepType = StepType.SendAndReceive)]
        [InputBinding("Channel", "通道号(1-4)")]
        [InputBinding("Mode", "模式(false=电源模式, true=扫描模式)")]
        [OutputBinding("Result", "设置结果")]
        public async Task<TestStepResult> 设置功能模式Step()
        {
            int channel = int.Parse(GetInputValue("Channel", "1"));
            bool mode = bool.Parse(GetInputValue("Mode", "false"));

            if (!IsDeviceConnected())
            {
                return FailResult("设置功能模式", "设备未连接");
            }

            try
            {
                await SendDataAsync( $"OUTPut{channel}:MODE {(mode ? "1" : "0")}\n");
                await SendDataAsync( $"OUTP{channel}:MODE?\n");
                string response = await ReceiveDataAsync();

                bool success = response.Trim() == (mode ? "1" : "0");
                SetOutputValue("Result", success ? "成功" : "失败");

                return new TestStepResult
                {
                    StepName = "设置功能模式",
                    IsSuccess = success,
                    ActualValue = success ? $"通道{channel}模式设置成功" : "设置失败"
                };
            }
            catch (Exception ex)
            {
                return FailResult("设置功能模式", ex.Message);
            }
        }

        /// <summary>
        /// 查询通道运行状态
        /// 协议: OUTP{n}:STAT?
        /// </summary>
        [StepDefinition("查询通道状态", Description = "查询指定通道的运行状态", StepType = StepType.ReadOnly)]
        [InputBinding("Channel", "通道号(1-4)")]
        [OutputBinding("State", "通道状态")]
        public async Task<TestStepResult> 查询通道状态Step()
        {
            int channel = int.Parse(GetInputValue("Channel", "1"));

            if (!IsDeviceConnected())
            {
                return FailResult("查询通道状态", "设备未连接");
            }

            try
            {
                await SendDataAsync( $"OUTP{channel}:STAT?\n");
                string response = await ReceiveDataAsync();

                SetOutputValue("State", response.Trim());
                return OkResult("查询通道状态", response.Trim());
            }
            catch (Exception ex)
            {
                return FailResult("查询通道状态", ex.Message);
            }
        }

        /// <summary>
        /// 设置通道电压
        /// 协议: SOURce{n}:VOLTage {v} + SOUR{n}:VOLT?
        /// </summary>
        [StepDefinition("设置通道电压", Description = "设置指定通道电源模式恒压值(V)", StepType = StepType.SendAndReceive)]
        [InputBinding("Channel", "通道号(1-4)")]
        [InputBinding("Voltage", "电压值(V)")]
        [OutputBinding("Result", "设置结果")]
        public async Task<TestStepResult> 设置通道电压Step()
        {
            int channel = int.Parse(GetInputValue("Channel", "1"));
            double voltage = double.Parse(GetInputValue("Voltage", "0"));

            if (!IsDeviceConnected())
            {
                return FailResult("设置通道电压", "设备未连接");
            }

            try
            {
                await SendDataAsync( $"SOURce{channel}:VOLTage {voltage}\n");
                await SendDataAsync( $"SOUR{channel}:VOLT?\n");
                string response = await ReceiveDataAsync();

                bool success = response.Trim() == voltage.ToString();
                SetOutputValue("Result", success ? "成功" : "失败");

                return new TestStepResult
                {
                    StepName = "设置通道电压",
                    IsSuccess = success,
                    ActualValue = success ? $"通道{channel}电压设置为{voltage}V" : $"设置失败,回读:{response}"
                };
            }
            catch (Exception ex)
            {
                return FailResult("设置通道电压", ex.Message);
            }
        }

        /// <summary>
        /// 设置通道电流
        /// 协议: SOURce{n}:OUTCURR {c} + SOUR{n}:OUTCURR?
        /// </summary>
        [StepDefinition("设置通道电流", Description = "设置指定通道输出限流值(mA)", StepType = StepType.SendAndReceive)]
        [InputBinding("Channel", "通道号(1-4)")]
        [InputBinding("Current", "电流值(mA)")]
        [OutputBinding("Result", "设置结果")]
        public async Task<TestStepResult> 设置通道电流Step()
        {
            int channel = int.Parse(GetInputValue("Channel", "1"));
            double current = double.Parse(GetInputValue("Current", "0"));

            if (!IsDeviceConnected())
            {
                return FailResult("设置通道电流", "设备未连接");
            }

            try
            {
                await SendDataAsync( $"SOURce{channel}:OUTCURR {current}\n");
                await SendDataAsync( $"SOUR{channel}:OUTCURR?\n");
                string response = await ReceiveDataAsync();

                bool success = response.Trim() == current.ToString();
                SetOutputValue("Result", success ? "成功" : "失败");

                return new TestStepResult
                {
                    StepName = "设置通道电流",
                    IsSuccess = success,
                    ActualValue = success ? $"通道{channel}电流设置为{current}mA" : $"设置失败,回读:{response}"
                };
            }
            catch (Exception ex)
            {
                return FailResult("设置通道电流", ex.Message);
            }
        }

        /// <summary>
        /// 设置量程
        /// 协议: SOURce{n}:RANGe {r} + SOUR{n}:RANG?
        /// </summary>
        [StepDefinition("设置量程", Description = "设置通道量程(0=大量程, 1=小量程, 3=自动量程)", StepType = StepType.SendAndReceive)]
        [InputBinding("Channel", "通道号(1-4)")]
        [InputBinding("Range", "量程(0/1/3)")]
        [OutputBinding("Result", "设置结果")]
        public async Task<TestStepResult> 设置量程Step()
        {
            int channel = int.Parse(GetInputValue("Channel", "1"));
            int range = int.Parse(GetInputValue("Range", "3"));

            if (!IsDeviceConnected())
            {
                return FailResult("设置量程", "设备未连接");
            }

            try
            {
                await SendDataAsync( $"SOURce{channel}:RANGe {range}\n");
                await SendDataAsync( $"SOUR{channel}:RANG?\n");
                string response = await ReceiveDataAsync();

                bool success = response.Trim() == range.ToString();
                SetOutputValue("Result", success ? "成功" : "失败");

                return new TestStepResult
                {
                    StepName = "设置量程",
                    IsSuccess = success,
                    ActualValue = success ? $"通道{channel}量程设置为{range}" : $"设置失败,回读:{response}"
                };
            }
            catch (Exception ex)
            {
                return FailResult("设置量程", ex.Message);
            }
        }

        /// <summary>
        /// 设置4通道电压
        /// 对通道1-4依次设置电源模式、恒压值和限流值
        /// </summary>
        [StepDefinition("设置4通道电压", Description = "同时设置1-4通道的电压和限流值", StepType = StepType.SendAndReceive)]
        [InputBinding("Voltage", "电压值(V)")]
        [InputBinding("Current", "限流值(mA,默认1000)")]
        [OutputBinding("Result", "设置结果")]
        public async Task<TestStepResult> 设置4通道电压Step()
        {
            double voltage = double.Parse(GetInputValue("Voltage", "0"));
            double current = double.Parse(GetInputValue("Current", "1000"));

            if (!IsDeviceConnected())
            {
                return FailResult("设置4通道电压", "设备未连接");
            }

            try
            {
                for (int i = 1; i <= 4; i++)
                {
                    // 设置电源模式
                    await SendDataAsync( $"OUTPut{i}:MODE 0\n");
                    await SendDataAsync( $"OUTP{i}:MODE?\n");
                    string modeResp = await ReceiveDataAsync();
                    if (modeResp.Trim() != "0")
                    {
                        SetOutputValue("Result", $"通道{i}模式设置失败");
                        return FailResult("设置4通道电压", $"通道{i}模式设置失败");
                    }

                    // 设置恒压值
                    await SendDataAsync( $"SOURce{i}:VOLTage {voltage}\n");
                    await SendDataAsync( $"SOUR{i}:VOLT?\n");
                    string volResp = await ReceiveDataAsync();
                    if (volResp.Trim() != voltage.ToString())
                    {
                        SetOutputValue("Result", $"通道{i}电压设置失败");
                        return FailResult("设置4通道电压", $"通道{i}电压设置失败");
                    }

                    // 设置限流值
                    await SendDataAsync( $"SOURce{i}:OUTCURR {current}\n");
                    await SendDataAsync( $"SOUR{i}:OUTCURR?\n");
                    string currResp = await ReceiveDataAsync();
                    if (currResp.Trim() != current.ToString())
                    {
                        SetOutputValue("Result", $"通道{i}电流设置失败");
                        return FailResult("设置4通道电压", $"通道{i}电流设置失败");
                    }
                }

                SetOutputValue("Result", "成功");
                return OkResult("设置4通道电压", $"4通道电压={voltage}V, 限流={current}mA");
            }
            catch (Exception ex)
            {
                return FailResult("设置4通道电压", ex.Message);
            }
        }

        /// <summary>
        /// 获取4通道电压
        /// 依次查询通道1-4的电压值
        /// </summary>
        [StepDefinition("获取4通道电压", Description = "获取1-4通道的回显电压值", StepType = StepType.ReadOnly)]
        [OutputBinding("Vol1", "通道1电压(V)")]
        [OutputBinding("Vol2", "通道2电压(V)")]
        [OutputBinding("Vol3", "通道3电压(V)")]
        [OutputBinding("Vol4", "通道4电压(V)")]
        public async Task<TestStepResult> 获取4通道电压Step()
        {

            if (!IsDeviceConnected())
            {
                return FailResult("获取4通道电压", "设备未连接");
            }

            try
            {
                await Task.Delay(800); // 等待电压稳定

                var voltages = new List<double>();
                for (int i = 1; i <= 4; i++)
                {
                    await SendDataAsync( $"MEASure{i}:VOLTage?\n");
                    string response = await ReceiveDataAsync();

                    if (double.TryParse(response.Trim(), out double vol))
                    {
                        voltages.Add(vol);
                        SetOutputValue($"Vol{i}", vol);
                    }
                    else
                    {
                        return FailResult("获取4通道电压", $"通道{i}电压解析失败: {response}");
                    }
                }

                return OkResult("获取4通道电压", $"V1={voltages[0]}, V2={voltages[1]}, V3={voltages[2]}, V4={voltages[3]}");
            }
            catch (Exception ex)
            {
                return FailResult("获取4通道电压", ex.Message);
            }
        }

        /// <summary>
        /// 设置4通道输出开关
        /// 对通道1-4依次设置输出使能
        /// </summary>
        [StepDefinition("设置4通道输出", Description = "同时开启或关闭1-4通道的输出", StepType = StepType.SendAndReceive)]
        [InputBinding("Enable", "是否使能(true/false)")]
        [OutputBinding("Result", "设置结果")]
        public async Task<TestStepResult> 设置4通道输出Step()
        {
            bool enable = bool.Parse(GetInputValue("Enable", "true"));

            if (!IsDeviceConnected())
            {
                return FailResult("设置4通道输出", "设备未连接");
            }

            try
            {
                for (int i = 1; i <= 4; i++)
                {
                    await SendDataAsync( $"OUTPut{i}:ONOFF {(enable ? "1" : "0")}\n");
                    await SendDataAsync( $"OUTP{i}:ONOFF?\n");
                    string response = await ReceiveDataAsync();

                    if (response.Trim() != (enable ? "1" : "0"))
                    {
                        SetOutputValue("Result", $"通道{i}设置失败");
                        return FailResult("设置4通道输出", $"通道{i}输出设置失败");
                    }
                }

                SetOutputValue("Result", "成功");
                return OkResult("设置4通道输出", $"4通道输出{(enable ? "开启" : "关闭")}");
            }
            catch (Exception ex)
            {
                return FailResult("设置4通道输出", ex.Message);
            }
        }

        #region 辅助方法

        private static TestStepResult OkResult(string stepName, string actualValue)
        {
            return new TestStepResult
            {
                StepName = stepName,
                IsSuccess = true,
                ActualValue = actualValue
            };
        }

        private static TestStepResult FailResult(string stepName, string errorMessage)
        {
            return new TestStepResult
            {
                StepName = stepName,
                IsSuccess = false,
                ErrorMessage = errorMessage,
                ActualValue = "执行失败"
            };
        }

        #endregion
    }
}
