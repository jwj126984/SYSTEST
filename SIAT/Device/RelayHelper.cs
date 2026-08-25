using SIAT.ResourceManagement;
using SIAT.TSET;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SIAT.Devices
{
    /// <summary>
    /// 继电器控制设备(双板卡)
    /// 通讯协议：Modbus RTU，线圈功能码(0x05写单个线圈/0x0F写多个线圈/0x01读线圈)
    /// 板卡1: 从站0x05, 线圈地址偏移0x0500, 最多24路
    /// 板卡2: 从站0x06, 线圈地址偏移0x0600, 最多32路
    /// 示例(板卡1第一个继电器打开): 05 05 05 00 FF 00 CRC16
    /// </summary>
    [DeviceDefinition("继电器控制", Description = "Modbus RTU多路继电器控制设备(双板卡)", CommunicationType = CommunicationType.Serial)]
    public class RelayDevice : DeviceBase
    {
        /// <summary>
        /// 板卡配置: 板卡1 从站0x05 偏移0x0500 最多24路; 板卡2 从站0x06 偏移0x0600 最多32路
        /// </summary>
        private static (byte Slave, ushort Offset, int MaxRelays) GetBoardInfo(int board)
        {
            return board switch
            {
                1 => (0x05, (ushort)0x0500, 24),
                2 => (0x06, (ushort)0x0600, 32),
                _ => throw new ArgumentOutOfRangeException(nameof(board), "板卡编号必须为1或2"),
            };
        }

        private int ParseBoard() => int.Parse(GetInputValue("Board", "1"));

        /// <summary>
        /// 写单个继电器
        /// 协议: Modbus功能码05(写单个线圈)
        /// 示例(板卡1第一个继电器打开): 05 05 05 00 FF 00 CRC16
        /// </summary>
        [StepDefinition("写单个继电器", Description = "控制指定继电器开/关", StepType = StepType.SendAndReceive)]
        [InputBinding("Board", "板卡编号(1或2,默认1)")]
        [InputBinding("RelayIndex", "继电器索引(板内,卡1:0-23,卡2:0-31)")]
        [InputBinding("IsOn", "是否开启(true/false)")]
        [OutputBinding("Result", "设置结果")]
        public async Task<TestStepResult> 写单个继电器Step()
        {
            int board = ParseBoard();
            int relayIndex = int.Parse(GetInputValue("RelayIndex", "0"));
            bool isOn = bool.Parse(GetInputValue("IsOn", "false"));

            if (!IsDeviceConnected())
                return FailResult("写单个继电器", "设备未连接");

            try
            {
                var (slave, offset, maxRelays) = GetBoardInfo(board);
                if (relayIndex < 0 || relayIndex >= maxRelays)
                    return FailResult("写单个继电器", $"继电器索引必须在0-{maxRelays - 1}之间");

                ushort address = (ushort)(offset + relayIndex);
                byte[] frame = BuildSingleCoilFrame(slave, address, isOn);
                byte[] response = await SendFrameAsync(frame);

                bool success = VerifyEcho(response, frame, 8);
                SetOutputValue("Result", success ? "成功" : "失败");

                return new TestStepResult
                {
                    StepName = "写单个继电器",
                    IsSuccess = success,
                    ActualValue = success ? $"板卡{board}继电器{relayIndex}{(isOn ? "开启" : "关闭")}成功" : "设置失败"
                };
            }
            catch (Exception ex)
            {
                return FailResult("写单个继电器", ex.Message);
            }
        }

        /// <summary>
        /// 写多个继电器
        /// 协议: Modbus功能码0F(写多个线圈),起始地址为板卡偏移
        /// </summary>
        [StepDefinition("写多个继电器", Description = "批量设置继电器状态", StepType = StepType.SendAndReceive)]
        [InputBinding("Board", "板卡编号(1或2,默认1)")]
        [InputBinding("RelayStates", "继电器状态(逗号分隔,如1,0,1)")]
        [OutputBinding("Result", "设置结果")]
        public async Task<TestStepResult> 写多个继电器Step()
        {
            int board = ParseBoard();
            bool[] relayStates = ParseRelayStates(GetInputValue("RelayStates", ""));

            if (relayStates == null || relayStates.Length == 0)
                return FailResult("写多个继电器", "继电器状态不能为空");

            if (!IsDeviceConnected())
                return FailResult("写多个继电器", "设备未连接");

            try
            {
                var (slave, offset, maxRelays) = GetBoardInfo(board);
                if (relayStates.Length > maxRelays)
                    return FailResult("写多个继电器", $"继电器数量不能超过{maxRelays}");
                byte[] frame = BuildMultipleCoilsFrame(slave, offset, relayStates);
                byte[] response = await SendFrameAsync(frame);

                bool success = VerifyMultiCoilsResponse(response, frame);
                SetOutputValue("Result", success ? "成功" : "失败");

                return new TestStepResult
                {
                    StepName = "写多个继电器",
                    IsSuccess = success,
                    ActualValue = success ? $"板卡{board}批量设置{relayStates.Length}路继电器成功" : "设置失败"
                };
            }
            catch (Exception ex)
            {
                return FailResult("写多个继电器", ex.Message);
            }
        }

        /// <summary>
        /// 写所有继电器
        /// 协议: Modbus功能码0F(写多个线圈,全部通道)
        /// </summary>
        [StepDefinition("写所有继电器", Description = "全部继电器同时开/关", StepType = StepType.SendAndReceive)]
        [InputBinding("Board", "板卡编号(1或2,默认1)")]
        [InputBinding("IsOn", "是否全部开启(true/false)")]
        [OutputBinding("Result", "设置结果")]
        public async Task<TestStepResult> 写所有继电器Step()
        {
            int board = ParseBoard();
            bool isOn = bool.Parse(GetInputValue("IsOn", "false"));

            if (!IsDeviceConnected())
                return FailResult("写所有继电器", "设备未连接");

            try
            {
                var (slave, offset, maxRelays) = GetBoardInfo(board);
                bool[] states = new bool[maxRelays];
                for (int i = 0; i < maxRelays; i++)
                    states[i] = isOn;

                byte[] frame = BuildMultipleCoilsFrame(slave, offset, states);
                byte[] response = await SendFrameAsync(frame);

                bool success = VerifyMultiCoilsResponse(response, frame);
                SetOutputValue("Result", success ? "成功" : "失败");

                return new TestStepResult
                {
                    StepName = "写所有继电器",
                    IsSuccess = success,
                    ActualValue = success ? $"板卡{board}全部继电器{(isOn ? "开启" : "关闭")}成功" : "设置失败"
                };
            }
            catch (Exception ex)
            {
                return FailResult("写所有继电器", ex.Message);
            }
        }

        /// <summary>
        /// 读取所有继电器
        /// 协议: Modbus功能码01(读线圈),从板卡偏移读取该板卡全部继电器
        /// </summary>
        [StepDefinition("读取所有继电器", Description = "读取全部继电器状态", StepType = StepType.ReadOnly)]
        [InputBinding("Board", "板卡编号(1或2,默认1)")]
        [OutputBinding("States", "继电器状态(逗号分隔,1=开0=关)")]
        public async Task<TestStepResult> 读取所有继电器Step()
        {
            int board = ParseBoard();

            if (!IsDeviceConnected())
                return FailResult("读取所有继电器", "设备未连接");

            try
            {
                var (slave, offset, maxRelays) = GetBoardInfo(board);
                byte[] frame = BuildReadCoilsFrame(slave, offset, maxRelays);
                byte[] response = await SendFrameAsync(frame);

                if (response == null || response.Length < 4)
                    return FailResult("读取所有继电器", "返回数据无效");

                bool[] states = ParseCoilsFromResponse(response, maxRelays);
                string statesStr = string.Join(",", states.Select(s => s ? 1 : 0));
                SetOutputValue("States", statesStr);

                return OkResult("读取所有继电器", $"板卡{board}: {statesStr}");
            }
            catch (Exception ex)
            {
                return FailResult("读取所有继电器", ex.Message);
            }
        }

        /// <summary>
        /// 读取继电器
        /// 协议: Modbus功能码01(读线圈,指定起始索引与数量)
        /// </summary>
        [StepDefinition("读取继电器", Description = "读取指定范围的继电器状态", StepType = StepType.ReadOnly)]
        [InputBinding("Board", "板卡编号(1或2,默认1)")]
        [InputBinding("StartIndex", "起始索引(板内,卡1:0-23,卡2:0-31)")]
        [InputBinding("Count", "读取数量")]
        [OutputBinding("States", "继电器状态(逗号分隔,1=开0=关)")]
        public async Task<TestStepResult> 读取继电器Step()
        {
            int board = ParseBoard();
            int startIndex = int.Parse(GetInputValue("StartIndex", "0"));
            int count = int.Parse(GetInputValue("Count", "1"));

            if (!IsDeviceConnected())
                return FailResult("读取继电器", "设备未连接");

            try
            {
                var (slave, offset, maxRelays) = GetBoardInfo(board);
                if (startIndex < 0 || startIndex >= maxRelays)
                    return FailResult("读取继电器", $"起始索引必须在0-{maxRelays - 1}之间");
                if (count <= 0 || startIndex + count > maxRelays)
                    return FailResult("读取继电器", "读取数量超出范围");
                ushort address = (ushort)(offset + startIndex);
                byte[] frame = BuildReadCoilsFrame(slave, address, count);
                byte[] response = await SendFrameAsync(frame);

                if (response == null || response.Length < 4)
                    return FailResult("读取继电器", "返回数据无效");

                bool[] states = ParseCoilsFromResponse(response, count);
                string statesStr = string.Join(",", states.Select(s => s ? 1 : 0));
                SetOutputValue("States", statesStr);

                return OkResult("读取继电器", $"板卡{board}: {statesStr}");
            }
            catch (Exception ex)
            {
                return FailResult("读取继电器", ex.Message);
            }
        }

        

        #region 帧构建与解析

        /// <summary>
        /// 构建写单个线圈帧(0x05)
        /// ON=0xFF00, OFF=0x0000
        /// </summary>
        private static byte[] BuildSingleCoilFrame(byte slave, ushort address, bool isOn)
        {
            byte[] frame = new byte[8];
            frame[0] = slave;
            frame[1] = 0x05;
            frame[2] = (byte)((address >> 8) & 0xFF);
            frame[3] = (byte)(address & 0xFF);
            frame[4] = isOn ? (byte)0xFF : (byte)0x00;
            frame[5] = 0x00;

            byte[] crc = CalculateCRC(frame, 6);
            frame[6] = crc[0];
            frame[7] = crc[1];
            return frame;
        }

        /// <summary>
        /// 构建写多个线圈帧(0x0F),线圈按位打包(LSB优先)
        /// </summary>
        private static byte[] BuildMultipleCoilsFrame(byte slave, ushort startAddress, bool[] states)
        {
            int qty = states.Length;
            int byteCount = (qty + 7) / 8;
            byte[] frame = new byte[7 + byteCount + 2];
            frame[0] = slave;
            frame[1] = 0x0F;
            frame[2] = (byte)((startAddress >> 8) & 0xFF);
            frame[3] = (byte)(startAddress & 0xFF);
            frame[4] = (byte)((qty >> 8) & 0xFF);
            frame[5] = (byte)(qty & 0xFF);
            frame[6] = (byte)byteCount;

            for (int i = 0; i < qty; i++)
            {
                if (states[i])
                    frame[7 + i / 8] |= (byte)(1 << (i % 8));
            }

            byte[] crc = CalculateCRC(frame, 7 + byteCount);
            frame[7 + byteCount] = crc[0];
            frame[8 + byteCount] = crc[1];
            return frame;
        }

        /// <summary>
        /// 构建读线圈帧(0x01)
        /// </summary>
        private static byte[] BuildReadCoilsFrame(byte slave, ushort startAddress, int count)
        {
            byte[] frame = new byte[8];
            frame[0] = slave;
            frame[1] = 0x01;
            frame[2] = (byte)((startAddress >> 8) & 0xFF);
            frame[3] = (byte)(startAddress & 0xFF);
            frame[4] = (byte)((count >> 8) & 0xFF);
            frame[5] = (byte)(count & 0xFF);

            byte[] crc = CalculateCRC(frame, 6);
            frame[6] = crc[0];
            frame[7] = crc[1];
            return frame;
        }

        /// <summary>
        /// 从读线圈响应解析状态(按位打包,LSB优先)
        /// 响应: [slave][0x01][byteCount][data...][CRC]
        /// </summary>
        private static bool[] ParseCoilsFromResponse(byte[] response, int count)
        {
            bool[] states = new bool[count];
            if (response == null || response.Length < 4)
                return states;

            for (int i = 0; i < count; i++)
            {
                int byteIdx = 3 + i / 8;
                if (byteIdx < response.Length)
                {
                    states[i] = (response[byteIdx] & (1 << (i % 8))) != 0;
                }
            }
            return states;
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

        /// <summary>
        /// 校验写单个线圈的回显响应(8字节完全一致)
        /// </summary>
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

        /// <summary>
        /// 校验写多个线圈响应(0x0F): 响应前6字节(从站,功能码,起始地址,数量)须与请求一致
        /// </summary>
        private static bool VerifyMultiCoilsResponse(byte[] response, byte[] sentFrame)
        {
            const int checkLength = 6;
            if (response == null || response.Length < 8 || sentFrame.Length < checkLength)
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
