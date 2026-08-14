using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SIAT.ResourceManagement;
using SIAT.TSET;

namespace SIAT.Devices
{
    /// <summary>
    /// 烧录器设备 - 通讯协议写死在各步骤方法中，通过设置的通讯方式（网络）发送
    /// </summary>
    [DeviceDefinition("烧录器", Description = "程序烧录设备", CommunicationType = CommunicationType.Network)]
    public class ProgrammerDevice : DeviceBase
    {
        /// <summary>
        /// 程序烧录步骤
        /// 协议: AT+PROG[序号]
        /// </summary>
        [StepDefinition("程序烧录", Description = "烧录程序到目标设备", StepType = StepType.SendAndReceive)]
        [InputBinding("ProgramId", "程序序号")]
        [OutputBinding("Result", "烧录结果")]
        [OutputBinding("Status", "设备状态")]
        public async Task<TestStepResult> 程序烧录Step()
        {
            string deviceName = GetInputValue("DeviceName", "");
            string programId = GetInputValue("ProgramId", "0000000000001111");

            if (!IsDeviceConnected())
            {
                return new TestStepResult
                {
                    StepName = "程序烧录",
                    IsSuccess = false,
                    ErrorMessage = "设备未连接",
                    ActualValue = "执行失败: 设备未连接"
                };
            }

            try
            {
                // 发送密码验证协议
                await SendDataAsync( "AT+CPASSWORD[12345678]");
                string response1 = await ReceiveDataAsync();

                if (!response1.Contains("OK"))
                {
                    SetOutputValue("Result", "密码验证失败");
                    SetOutputValue("Status", "错误");
                    return new TestStepResult
                    {
                        StepName = "程序烧录",
                        IsSuccess = false,
                        ErrorMessage = "密码验证失败",
                        ActualValue = "密码验证失败"
                    };
                }

                // 发送烧录命令协议
                await SendDataAsync( $"AT+PROG[{programId}]");
                string response2 = await ReceiveDataAsync();

                // 轮询查询烧录状态
                int maxRetries = 30;
                for (int i = 0; i < maxRetries; i++)
                {
                    await SendDataAsync( "AT+GET_STATE");
                    string currentState = await ReceiveDataAsync();

                    if (currentState.Contains("DONE"))
                    {
                        SetOutputValue("Result", "烧录成功");
                        SetOutputValue("Status", "完成");
                        return new TestStepResult
                        {
                            StepName = "程序烧录",
                            IsSuccess = true,
                            ActualValue = "烧录成功"
                        };
                    }

                    if (currentState.Contains("ERROR"))
                    {
                        SetOutputValue("Result", "烧录失败");
                        SetOutputValue("Status", "错误");
                        return new TestStepResult
                        {
                            StepName = "程序烧录",
                            IsSuccess = false,
                            ErrorMessage = "烧录失败",
                            ActualValue = "烧录失败"
                        };
                    }

                    await Task.Delay(500);
                }

                SetOutputValue("Result", "烧录超时");
                SetOutputValue("Status", "超时");
                return new TestStepResult
                {
                    StepName = "程序烧录",
                    IsSuccess = false,
                    ErrorMessage = "烧录超时",
                    ActualValue = "烧录超时"
                };
            }
            catch (Exception ex)
            {
                SetOutputValue("Result", "执行异常");
                SetOutputValue("Status", "错误");
                return new TestStepResult
                {
                    StepName = "程序烧录",
                    IsSuccess = false,
                    ErrorMessage = ex.Message,
                    ActualValue = "执行失败"
                };
            }
        }

        /// <summary>
        /// SN写入步骤
        /// 协议: AT+WRSN[SN码]
        /// </summary>
        [StepDefinition("SN写入", Description = "写入产品SN码", StepType = StepType.SendAndReceive)]
        [InputBinding("Barcode", "产品条码")]
        [OutputBinding("Result", "写入结果")]
        public async Task<TestStepResult> SN写入Step()
        {
            string deviceName = GetInputValue("DeviceName", "");
            string barcode = GetInputValue("Barcode", "");

            if (!IsDeviceConnected())
            {
                return new TestStepResult
                {
                    StepName = "SN写入",
                    IsSuccess = false,
                    ErrorMessage = "设备未连接",
                    ActualValue = "执行失败: 设备未连接"
                };
            }

            try
            {
                // 发送SN写入协议
                await SendDataAsync( $"AT+WRSN[{barcode}]");
                string response = await ReceiveDataAsync();

                if (response.Contains("OK"))
                {
                    SetOutputValue("Result", "SN写入成功");
                    return new TestStepResult
                    {
                        StepName = "SN写入",
                        IsSuccess = true,
                        ActualValue = "SN写入成功"
                    };
                }

                SetOutputValue("Result", "SN写入失败");
                return new TestStepResult
                {
                    StepName = "SN写入",
                    IsSuccess = false,
                    ErrorMessage = $"SN写入失败: {response}",
                    ActualValue = "SN写入失败"
                };
            }
            catch (Exception ex)
            {
                SetOutputValue("Result", "执行异常");
                return new TestStepResult
                {
                    StepName = "SN写入",
                    IsSuccess = false,
                    ErrorMessage = ex.Message,
                    ActualValue = "执行失败"
                };
            }
        }

        /// <summary>
        /// 设备编码步骤
        /// 协议: AT+GET_ID
        /// </summary>
        [StepDefinition("设备编码", Description = "读取设备编码", StepType = StepType.ReadOnly)]
        [OutputBinding("DeviceId", "设备编码")]
        public async Task<TestStepResult> 设备编码Step()
        {
            if (!IsDeviceConnected())
            {
                return new TestStepResult
                {
                    StepName = "设备编码",
                    IsSuccess = false,
                    ErrorMessage = "设备未连接",
                    ActualValue = "执行失败: 设备未连接"
                };
            }

            try
            {
                // 发送读取设备编码协议
                await SendDataAsync( "AT+GET_ID");
                string response = await ReceiveDataAsync();

                // 解析设备编码
                string deviceId = response.Replace("ID:", "").Trim();
                SetOutputValue("DeviceId", deviceId);

                return new TestStepResult
                {
                    StepName = "设备编码",
                    IsSuccess = true,
                    ActualValue = deviceId
                };
            }
            catch (Exception ex)
            {
                return new TestStepResult
                {
                    StepName = "设备编码",
                    IsSuccess = false,
                    ErrorMessage = ex.Message,
                    ActualValue = "执行失败"
                };
            }
        }
    }
}
