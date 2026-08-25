using SIAT.ResourceManagement;
using SIAT.TSET;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SIAT.Devices
{
    /// <summary>
    /// 继电器控制设备
    /// 通讯协议：Modbus RTU（二进制帧），通过串口发送
    /// </summary>
    [DeviceDefinition("继电器控制", Description = "Modbus RTU多路继电器控制设备", CommunicationType = CommunicationType.Serial)]
    public class RelayDevice : DeviceBase
    {
        private const int MAX_RELAY_COUNT = 22;

        /// <summary>
        /// 写单个继电器
        /// 协议: Modbus功能码06(写单个线圈/保持寄存器)
        /// </summary>
        [StepDefinition("写单个继电器", Description = "控制指定继电器开/关", StepType = StepType.SendAndReceive)]
        [InputBinding("RelayIndex", "继电器索引(0-21)")]
        [InputBinding("IsOn", "是否开启(true/false)")]
        [InputBinding("SlaveAddress", "从机地址(1-247,默认1)")]
        [OutputBinding("Result", "设置结果")]
        public async Task<TestStepResult> 写单个继电器Step()
        {
            int relayIndex = int.Parse(GetInputValue("RelayIndex", "0"));
            bool isOn = bool.Parse(GetInputValue("IsOn", "false"));
            byte slaveAddress = ParseSlaveAddress();

            if (relayIndex < 0 || relayIndex >= MAX_RELAY_COUNT)
                return FailResult("写单个继电器", $"继电器索引必须在0-{MAX_RELAY_COUNT - 1}之间");

            if (!IsDeviceConnected())
                return FailResult("写单个继电器", "设备未连接");

            try
            {
                byte[] frame = BuildSingleRelayFrame(slaveAddress, relayIndex, isOn);
                byte[] response = await SendFrameAsync(frame);

                bool success = VerifyEcho(response, frame, 8);
                SetOutputValue("Result", success ? "成功" : "失败");

                return new TestStepResult
                {
                    StepName = "写单个继电器",
                    IsSuccess = success,
                    ActualValue = success ? $"继电器{relayIndex}{(isOn ? "开启" : "关闭")}成功" : "设置失败"
                };
            }
            catch (Exception ex)
            {
                return FailResult("写单个继电器", ex.Message);
            }
        }

        /// <summary>
        /// 写多个继电器
        /// 协议: Modbus功能码10(写多个线圈)
        /// </summary>
        [StepDefinition("写多个继电器", Description = "批量设置继电器状态", StepType = StepType.SendAndReceive)]
        [InputBinding("RelayStates", "继电器状态(逗号分隔,如1,0,1)")]
        [InputBinding("SlaveAddress", "从机地址(1-247,默认1)")]
        [OutputBinding("Result", "设置结果")]
        public async Task<TestStepResult> 写多个继电器Step()
        {
            byte slaveAddress = ParseSlaveAddress();
            bool[] relayStates = ParseRelayStates(GetInputValue("RelayStates", ""));

            if (relayStates == null || relayStates.Length == 0)
                return FailResult("写多个继电器", "继电器状态不能为空");

            if (!IsDeviceConnected())
                return FailResult("写多个继电器", "设备未连接");

            try
            {
                byte[] frame = BuildMultipleRelaysFrame(slaveAddress, relayStates);
                byte[] response = await SendFrameAsync(frame);

                bool success = VerifyEcho(response, frame, 8);
                SetOutputValue("Result", success ? "成功" : "失败");

                return new TestStepResult
                {
                    StepName = "写多个继电器",
                    IsSuccess = success,
                    ActualValue = success ? $"批量设置{relayStates.Length}路继电器成功" : "设置失败"
                };
            }
            catch (Exception ex)
            {
                return FailResult("写多个继电器", ex.Message);
            }
        }

        /// <summary>
        /// 写所有继电器
        /// 协议: Modbus功能码10(写多个线圈,全部通道)
        /// </summary>
        [StepDefinition("写所有继电器", Description = "全部继电器同时开/关", StepType = StepType.SendAndReceive)]
        [InputBinding("IsOn", "是否全部开启(true/false)")]
        [InputBinding("SlaveAddress", "从机地址(1-247,默认1)")]
        [OutputBinding("Result", "设置结果")]
        public async Task<TestStepResult> 写所有继电器Step()
        {
            bool isOn = bool.Parse(GetInputValue("IsOn", "false"));
            byte slaveAddress = ParseSlaveAddress();

            if (!IsDeviceConnected())
                return FailResult("写所有继电器", "设备未连接");

            try
            {
                bool[] states = new bool[MAX_RELAY_COUNT];
                for (int i = 0; i < MAX_RELAY_COUNT; i++)
                    states[i] = isOn;

                byte[] frame = BuildMultipleRelaysFrame(slaveAddress, states);
                byte[] response = await SendFrameAsync(frame);

                bool success = VerifyEcho(response, frame, 8);
                SetOutputValue("Result", success ? "成功" : "失败");

                return new TestStepResult
                {
                    StepName = "写所有继电器",
                    IsSuccess = success,
                    ActualValue = success ? $"全部继电器{(isOn ? "开启" : "关闭")}成功" : "设置失败"
                };
            }
            catch (Exception ex)
            {
                return FailResult("写所有继电器", ex.Message);
            }
        }

        /// <summary>
        /// 读取所有继电器
        /// 协议: Modbus功能码03(读保持寄存器,从地址0读取MAX_RELAY_COUNT路)
        /// </summary>
        [StepDefinition("读取所有继电器", Description = "读取全部继电器状态", StepType = StepType.ReadOnly)]
        [InputBinding("SlaveAddress", "从机地址(1-247,默认1)")]
        [OutputBinding("States", "继电器状态(逗号分隔,1=开0=关)")]
        public async Task<TestStepResult> 读取所有继电器Step()
        {
            byte slaveAddress = ParseSlaveAddress();

            if (!IsDeviceConnected())
                return FailResult("读取所有继电器", "设备未连接");

            try
            {
                byte[] frame = BuildReadFrame(slaveAddress, 0x0000, MAX_RELAY_COUNT);
                byte[] response = await SendFrameAsync(frame);

                if (response == null || response.Length < 5)
                    return FailResult("读取所有继电器", "返回数据无效");

                bool[] states = ParseRelayStatesFromResponse(response, MAX_RELAY_COUNT);
                string statesStr = string.Join(",", states.Select(s => s ? 1 : 0));
                SetOutputValue("States", statesStr);

                return OkResult("读取所有继电器", statesStr);
            }
            catch (Exception ex)
            {
                return FailResult("读取所有继电器", ex.Message);
            }
        }

        /// <summary>
        /// 读取继电器
        /// 协议: Modbus功能码03(读保持寄存器,指定起始索引与数量)
        /// </summary>
        [StepDefinition("读取继电器", Description = "读取指定范围的继电器状态", StepType = StepType.ReadOnly)]
        [InputBinding("StartIndex", "起始索引(0-21)")]
        [InputBinding("Count", "读取数量")]
        [InputBinding("SlaveAddress", "从机地址(1-247,默认1)")]
        [OutputBinding("States", "继电器状态(逗号分隔,1=开0=关)")]
        public async Task<TestStepResult> 读取继电器Step()
        {
            int startIndex = int.Parse(GetInputValue("StartIndex", "0"));
            int count = int.Parse(GetInputValue("Count", "1"));
            byte slaveAddress = ParseSlaveAddress();

            if (startIndex < 0 || startIndex >= MAX_RELAY_COUNT)
                return FailResult("读取继电器", $"起始索引必须在0-{MAX_RELAY_COUNT - 1}之间");
            if (count <= 0 || startIndex + count > MAX_RELAY_COUNT)
                return FailResult("读取继电器", "读取数量超出范围");

            if (!IsDeviceConnected())
                return FailResult("读取继电器", "设备未连接");

            try
            {
                byte[] frame = BuildReadFrame(slaveAddress, startIndex, count);
                byte[] response = await SendFrameAsync(frame);

                if (response == null || response.Length < 5)
                    return FailResult("读取继电器", "返回数据无效");

                bool[] states = ParseRelayStatesFromResponse(response, count);
                string statesStr = string.Join(",", states.Select(s => s ? 1 : 0));
                SetOutputValue("States", statesStr);

                return OkResult("读取继电器", statesStr);
            }
            catch (Exception ex)
            {
                return FailResult("读取继电器", ex.Message);
            }
        }

        /// <summary>
        /// 读取电阻
        /// 协议: Modbus功能码03(读保持寄存器,地址0x1240,数量0x10,返回float)
        /// </summary>
        [StepDefinition("读取电阻", Description = "读取电阻值", StepType = StepType.ReadOnly)]
        [InputBinding("SlaveAddress", "从机地址(1-247,默认1)")]
        [OutputBinding("Resistance", "电阻值")]
        public async Task<TestStepResult> 读取电阻Step()
        {
            byte slaveAddress = ParseSlaveAddress();

            if (!IsDeviceConnected())
                return FailResult("读取电阻", "设备未连接");

            try
            {
                byte[] frame = new byte[8];
                frame[0] = slaveAddress;
                frame[1] = 0x03;
                frame[2] = 0x12;
                frame[3] = 0x40;
                frame[4] = 0x00;
                frame[5] = 0x10;

                byte[] crc = CalculateCRC(frame, 6);
                frame[6] = crc[0];
                frame[7] = crc[1];

                byte[] response = await SendFrameAsync(frame);

                if (response == null || response.Length < 7)
                    return FailResult("读取电阻", "返回数据无效");

                byte[] data = new byte[4];
                Array.Copy(response, 3, data, 0, 4);
                float floatValue = BitConverter.ToSingle(data, 0);
                double resistance = floatValue;

                SetOutputValue("Resistance", resistance);
                return OkResult("读取电阻", $"{resistance}");
            }
            catch (Exception ex)
            {
                return FailResult("读取电阻", ex.Message);
            }
        }

        #region 帧构建与解析

        private static byte[] BuildSingleRelayFrame(byte slaveAddress, int relayIndex, bool isOn)
        {
            byte[] frame = new byte[8];
            frame[0] = slaveAddress;
            frame[1] = 0x06;
            frame[2] = (byte)((relayIndex >> 8) & 0xFF);
            frame[3] = (byte)(relayIndex & 0xFF);
            frame[4] = (byte)((isOn ? 1 : 0) >> 8 & 0xFF);
            frame[5] = (byte)((isOn ? 1 : 0) & 0xFF);

            byte[] crc = CalculateCRC(frame, 6);
            frame[6] = crc[0];
            frame[7] = crc[1];
            return frame;
        }

        private static byte[] BuildMultipleRelaysFrame(byte slaveAddress, bool[] relayStates)
        {
            int count = Math.Min(relayStates.Length, MAX_RELAY_COUNT);
            int byteCount = count * 2;

            byte[] frame = new byte[7 + byteCount + 2];
            frame[0] = slaveAddress;
            frame[1] = 0x10;
            frame[2] = 0x00;
            frame[3] = 0x00;
            frame[4] = (byte)((count >> 8) & 0xFF);
            frame[5] = (byte)(count & 0xFF);
            frame[6] = (byte)byteCount;

            for (int i = 0; i < count; i++)
            {
                int dataIndex = 7 + i * 2;
                frame[dataIndex] = 0x00;
                frame[dataIndex + 1] = relayStates[i] ? (byte)0x01 : (byte)0x00;
            }

            byte[] crc = CalculateCRC(frame, 7 + byteCount);
            frame[7 + byteCount] = crc[0];
            frame[8 + byteCount] = crc[1];
            return frame;
        }

        private static byte[] BuildReadFrame(byte slaveAddress, int startIndex, int count)
        {
            byte[] frame = new byte[8];
            frame[0] = slaveAddress;
            frame[1] = 0x03;
            frame[2] = (byte)((startIndex >> 8) & 0xFF);
            frame[3] = (byte)(startIndex & 0xFF);
            frame[4] = (byte)((count >> 8) & 0xFF);
            frame[5] = (byte)(count & 0xFF);

            byte[] crc = CalculateCRC(frame, 6);
            frame[6] = crc[0];
            frame[7] = crc[1];
            return frame;
        }

        private static bool[] ParseRelayStatesFromResponse(byte[] response, int count)
        {
            bool[] states = new bool[count];
            for (int i = 0; i < count && i * 2 + 3 < response.Length; i++)
            {
                int value = (response[i * 2 + 3] << 8) | response[i * 2 + 4];
                states[i] = value != 0;
            }
            return states;
        }

        private byte ParseSlaveAddress()
        {
            byte addr = byte.Parse(GetInputValue("SlaveAddress", "1"));
            if (addr < 1 || addr > 247)
                throw new ArgumentOutOfRangeException(nameof(addr), "从机地址必须在1-247之间");
            return addr;
        }

        private static bool[] ParseRelayStates(string statesStr)
        {
            if (string.IsNullOrWhiteSpace(statesStr))
                return Array.Empty<bool>();

            return statesStr
                .Split(',')
                .Select(p => p.Trim())
                .Where(p => p.Length > 0)
                .Select(p => p == "1" || p.Equals("true", StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        private static bool VerifyEcho(byte[] response, byte[] sentFrame, int checkLength)
        {
            if (response == null || response.Length < checkLength || sentFrame.Length < checkLength)
                return false;

            for (int i = 0; i < checkLength; i++)
            {
                if (response[i] != sentFrame[i])
                    return false;
            }
            return true;
        }

        private async Task<byte[]> SendFrameAsync(byte[] frame)
        {
            await SendDataAsync(frame);
            await Task.Delay(600);
            return await ReceiveDataAsync(true);
        }

        private static byte[] CalculateCRC(byte[] data, int length)
        {
            ushort crc = 0xFFFF;
            for (int i = 0; i < length; i++)
            {
                crc ^= data[i];
                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 0x0001) == 1)
                    {
                        crc >>= 1;
                        crc ^= 0xA001;
                    }
                    else
                    {
                        crc >>= 1;
                    }
                }
            }
            return new byte[] { (byte)(crc & 0xFF), (byte)((crc >> 8) & 0xFF) };
        }

        #endregion

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
