using System;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using SIAT.ResourceManagement;

namespace SIAT.CommunicationManagement
{
    public class CXKJCanCommunication : CommunicationBase
    {
        [DllImport("ControlCAN.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern uint VCI_OpenDevice(uint DeviceType, uint DeviceInd, uint Reserved);

        [DllImport("ControlCAN.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern uint VCI_CloseDevice(uint DeviceType, uint DeviceInd);

        [DllImport("ControlCAN.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern uint VCI_InitCAN(uint DeviceType, uint DeviceInd, uint CANInd, IntPtr pInitConfig);

        [DllImport("ControlCAN.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern uint VCI_StartCAN(uint DeviceType, uint DeviceInd, uint CANInd);

        [DllImport("ControlCAN.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern uint VCI_Transmit(uint DeviceType, uint DeviceInd, uint CANInd, IntPtr pSend, uint Len);

        [DllImport("ControlCAN.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern uint VCI_Receive(uint DeviceType, uint DeviceInd, uint CANInd, IntPtr pReceive, uint Len, int WaitTime);

        [DllImport("ControlCAN.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern uint VCI_ClearBuffer(uint DeviceType, uint DeviceInd, uint CANInd);

        [DllImport("ControlCAN.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern uint VCI_ResetCAN(uint DeviceType, uint DeviceInd, uint CANInd);

        [DllImport("ControlCAN.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern uint VCI_ReadBoardInfo(uint DeviceType, uint DeviceInd, IntPtr pInfo);

        [DllImport("ControlCAN.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern uint VCI_SetReference(uint DeviceType, uint DeviceInd, uint CANInd, uint RefType, IntPtr pData);

        [DllImport("ControlCAN.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern uint VCI_GetReceiveNum(uint DeviceType, uint DeviceInd, uint CANInd);

        // 创芯科技设备类型常量
        private const uint VCI_USBCAN1 = 3;
        private const uint VCI_USBCAN2 = 4; // CANalyst-Ⅱ对应的设备类型
        private const uint VCI_USBCAN_E_U = 20;
        private const uint VCI_USBCAN_2E_U = 21;

        // 函数调用返回状态值
        private const uint STATUS_OK = 1;
        private const uint STATUS_ERR = 0;

        // 定义初始化CAN的数据类型
        [StructLayout(LayoutKind.Sequential)]
        private struct VCI_INIT_CONFIG
        {
            public uint AccCode;      // 验收码
            public uint AccMask;      // 验收屏蔽码
            public uint Reserved;     // 保留
            public byte Filter;       // 过滤模式
            public byte Timing0;      // 波特率定时器0
            public byte Timing1;      // 波特率定时器1
            public byte Mode;         // 工作模式
        }

        // 定义CAN信息帧的数据类型
        [StructLayout(LayoutKind.Sequential)]
        private struct VCI_CAN_OBJ
        {
            public uint ID;           // CAN ID
            public uint TimeStamp;    // 时间戳
            public byte TimeFlag;     // 时间标志
            public byte SendType;     // 发送类型
            public byte RemoteFlag;   // 是否是远程帧
            public byte ExternFlag;   // 是否是扩展帧
            public byte DataLen;      // 数据长度
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public byte[] Data;       // 数据
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
            public byte[] Reserved;   // 保留
        }

        private bool _isConnected;
        private uint _deviceType;
        private uint _deviceIndex;
        private uint _channelIndex;
        private string _deviceModel;
        
        // 线程接收相关字段
        private ConcurrentQueue<VCI_CAN_OBJ> _receiveQueue;
        private CancellationTokenSource _cts;
        private Thread? _receiveThread;
        private bool _isReceiveThreadRunning;

        public CXKJCanCommunication(CommunicationParams parameters) : base(parameters)
        {
            _deviceType = VCI_USBCAN2; // CANalyst-Ⅱ对应的设备类型
            _deviceIndex = (uint)parameters.DeviceIndex;
            _channelIndex = parameters.CanChannel == CanChannelType.CAN0 ? 0u : 1u;
            _deviceModel = "CANalyst-Ⅱ"; // 创芯科技设备型号
            
            // 初始化线程接收相关字段
            _receiveQueue = new ConcurrentQueue<VCI_CAN_OBJ>();
            _cts = new CancellationTokenSource();
            _isReceiveThreadRunning = false;
        }

        public override async Task<bool> ConnectAsync(CommunicationParams parameters)
        {
            try
            {
                _deviceIndex = (uint)parameters.DeviceIndex;
                _channelIndex = parameters.CanChannel == CanChannelType.CAN0 ? 0u : 1u;



                // 1. 打开CAN设备
                uint openResult = VCI_OpenDevice(_deviceType, _deviceIndex, 0);
                if (openResult != STATUS_OK)
                {
                    throw new Exception($"打开创芯科技{_deviceModel}设备失败，错误码: {openResult}");
                }


                // 2. 复位CAN通道（确保通道处于初始状态）
                uint resetResult = VCI_ResetCAN(_deviceType, _deviceIndex, _channelIndex);


                // 3. 配置CAN通道参数
                VCI_INIT_CONFIG initConfig = new VCI_INIT_CONFIG
                {
                    AccCode = parameters.AcceptanceCode,
                    AccMask = parameters.AcceptanceMask,
                    Filter = (byte)parameters.CanFilterMode,
                    Mode = parameters.CanWorkMode == CanWorkMode.Normal ? (byte)0 : (byte)1,
                    Reserved = 0
                };

                // 根据波特率计算对应的timing0和timing1值
                (byte timing0, byte timing1) = CalculateCanTiming(parameters.CanBaudRate);
                initConfig.Timing0 = timing0;
                initConfig.Timing1 = timing1;



                // 4. 初始化CAN通道
                IntPtr initConfigPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(VCI_INIT_CONFIG)));
                Marshal.StructureToPtr(initConfig, initConfigPtr, false);

                uint initResult = VCI_InitCAN(_deviceType, _deviceIndex, _channelIndex, initConfigPtr);
                Marshal.FreeHGlobal(initConfigPtr);

                if (initResult != STATUS_OK)
                {
                    VCI_CloseDevice(_deviceType, _deviceIndex);
                    throw new Exception($"初始化CAN通道失败，错误码: {initResult}");
                }


                // 5. 清空接收缓冲区
                uint clearResult = VCI_ClearBuffer(_deviceType, _deviceIndex, _channelIndex);


                // 6. 启动CAN通道
                uint startResult = VCI_StartCAN(_deviceType, _deviceIndex, _channelIndex);
                if (startResult != STATUS_OK)
                {
                    VCI_CloseDevice(_deviceType, _deviceIndex);
                    throw new Exception($"启动CAN通道失败，错误码: {startResult}");
                }


                _isConnected = true;
                
                // 启动接收线程
                StartReceiveThread();
                
                UpdateConnectionStatus(true, $"已连接到创芯科技{_deviceModel}设备");
                return true;
            }
            catch (Exception ex)
            {
                _isConnected = false;
                UpdateConnectionStatus(false, $"连接失败: {ex.Message}");
                return false;
            }
        }

        public override async Task<bool> DisconnectAsync()
        {
            try
            {
                if (!_isConnected)
                {
                    UpdateConnectionStatus(false, "设备未连接");
                    return true;
                }



                // 清理操作：重置CAN通道
                try
                {
                    // 这里不需要检查返回值，因为通道可能已经停止
                    VCI_ResetCAN(_deviceType, _deviceIndex, _channelIndex);

                }
                catch (Exception)
                {
                    // 忽略重置通道时的错误
                }

                // 清理操作：清空缓冲区
                try
                {
                    VCI_ClearBuffer(_deviceType, _deviceIndex, _channelIndex);

                }
                catch (Exception)
                {
                    // 忽略清空缓冲区时的错误
                }

                // 停止接收线程
                StopReceiveThread();
                
                // 关闭设备
                uint closeResult = VCI_CloseDevice(_deviceType, _deviceIndex);
                if (closeResult != STATUS_OK)
                {
                    throw new Exception($"关闭设备失败，错误码: {closeResult}");
                }

                _isConnected = false;
                UpdateConnectionStatus(false, "已断开连接");

                return true;
            }
            catch (Exception ex)
            {
                _isConnected = false;
                UpdateConnectionStatus(false, $"断开失败: {ex.Message}");

                return false;
            }
        }

        public override async Task<string> SendAsync(string data)
        {
            // 调用带协议类型的重载方法，默认使用CAN协议
            return await SendAsync(data, "CAN");
        }
        
        public override async Task<string> SendAsync(string data, string protocolType)
        {
            try
            {
                if (!_isConnected)
                {
                    throw new InvalidOperationException("CAN未连接");
                }

                // 发送前清空接收缓冲区，确保接收到的是该报文对应的响应报文
                VCI_ClearBuffer(_deviceType, _deviceIndex, _channelIndex);

                // 解析字符串数据为CAN帧
                VCI_CAN_OBJ canFrame = ParseCanFrameFromString(data);

                // 发送CAN帧
                IntPtr transmitPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(VCI_CAN_OBJ)));
                Marshal.StructureToPtr(canFrame, transmitPtr, false);

                uint sendResult = VCI_Transmit(_deviceType, _deviceIndex, _channelIndex, transmitPtr, 1);
                Marshal.FreeHGlobal(transmitPtr);

                if (sendResult != 1)
                {
                    throw new Exception($"发送CAN帧失败，错误码: {sendResult}");
                }

                return $"[{_deviceModel}] 发送成功: {data}";
            }
            catch (Exception ex)
            {
                return $"[{_deviceModel}] 发送失败: {ex.Message}";
            }
        }

        public override async Task<string> SendAsync(byte[] data)
        {
            // 调用带协议类型的重载方法，默认使用CAN协议
            return await SendAsync(data, "CAN");
        }
        
        public override async Task<string> SendAsync(byte[] data, string protocolType)
        {
            try
            {
                if (!_isConnected)
                {
                    throw new InvalidOperationException("CAN未连接");
                }

                // 发送前清空接收缓冲区，确保接收到的是该报文对应的响应报文
                VCI_ClearBuffer(_deviceType, _deviceIndex, _channelIndex);

                string hexData = BitConverter.ToString(data).Replace("-", " ");

                // 创建默认CAN帧
                VCI_CAN_OBJ canFrame = new VCI_CAN_OBJ
                {
                    ID = 0x100,
                    TimeStamp = 0,
                    TimeFlag = 0,
                    SendType = 0,
                    RemoteFlag = 0,
                    ExternFlag = 0,
                    DataLen = 8,
                    Data = new byte[8],
                    Reserved = new byte[3]
                };

                // 复制数据，不足8字节时自动补0
                int copyLength = Math.Min(data.Length, 8);
                Array.Copy(data, canFrame.Data, copyLength);
                // 确保剩余字节用00补全
                for (int i = copyLength; i < 8; i++)
                {
                    canFrame.Data[i] = 0;
                }

                // 发送CAN帧
                IntPtr transmitPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(VCI_CAN_OBJ)));
                Marshal.StructureToPtr(canFrame, transmitPtr, false);

                uint sendResult = VCI_Transmit(_deviceType, _deviceIndex, _channelIndex, transmitPtr, 1);
                Marshal.FreeHGlobal(transmitPtr);

                if (sendResult != 1)
                {
                    throw new Exception($"发送CAN帧失败，错误码: {sendResult}");
                }

                return $"[{_deviceModel}] 发送成功: {hexData}";
            }
            catch (Exception ex)
            {
                return $"[{_deviceModel}] 发送失败: {ex.Message}";
            }
        }

        public override async Task<string> ReceiveAsync()
        {
            return await ReceiveAsync(5000, null);
        }
        
        public override async Task<string> ReceiveAsync(int timeout)
        {
            return await ReceiveAsync(timeout, null);
        }
        
        public override async Task<string> ReceiveAsync(int timeout, string protocolType)
        {
            return await ReceiveAsync(timeout, null);
        }
        
        public override async Task<string> ReceiveAsync(int timeout, uint? canId, string protocolType)
        {
            return await ReceiveAsync(timeout, canId);
        }
        
        /// <summary>
        /// 启动接收线程
        /// </summary>
        private void StartReceiveThread()
        {
            if (!_isReceiveThreadRunning)
            {
                _cts = new CancellationTokenSource();
                _receiveQueue = new ConcurrentQueue<VCI_CAN_OBJ>();
                
                _receiveThread = new Thread(ReceiveThreadProc)
                {
                    IsBackground = true,
                    Name = "CAN接收线程"
                };
                
                _isReceiveThreadRunning = true;
                _receiveThread.Start();
            }
        }

        /// <summary>
        /// 停止接收线程
        /// </summary>
        private void StopReceiveThread()
        {
            if (_isReceiveThreadRunning)
            {
                _cts.Cancel();
                if (_receiveThread != null && _receiveThread.IsAlive)
                {
                    _receiveThread.Join(1000); // 等待线程结束，最多1秒
                }
                _isReceiveThreadRunning = false;
                _receiveQueue.Clear();
            }
        }

        /// <summary>
        /// 接收线程处理函数
        /// </summary>
        private void ReceiveThreadProc()
        {
            try
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    if (!_isConnected)
                    {
                        Thread.Sleep(100);
                        continue;
                    }

                    VCI_CAN_OBJ[] receiveBuffer = new VCI_CAN_OBJ[100];
                    IntPtr receivePtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(VCI_CAN_OBJ)) * receiveBuffer.Length);

                    try
                    {
                        // 接收最多100帧报文，等待时间100ms
                        uint receiveResult = VCI_Receive(_deviceType, _deviceIndex, _channelIndex, receivePtr, 100, 100);

                        if (receiveResult > 0)
                        {
                            // 将接收到的报文加入队列
                            for (int i = 0; i < receiveResult; i++)
                            {
                                IntPtr framePtr = new IntPtr(receivePtr.ToInt64() + i * Marshal.SizeOf(typeof(VCI_CAN_OBJ)));
                                VCI_CAN_OBJ canFrame = Marshal.PtrToStructure<VCI_CAN_OBJ>(framePtr);
                                _receiveQueue.Enqueue(canFrame);
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // 忽略接收过程中的错误
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(receivePtr);
                    }

                    // 短暂休眠，避免CPU占用过高
                    Thread.Sleep(10);
                }
            }
            catch (Exception)
            {
                // 忽略线程中的异常
            }
            finally
            {
                _isReceiveThreadRunning = false;
            }
        }

        public async Task<string> ReceiveAsync(int timeout, uint? canId)
        {
            try
            {
                if (!_isConnected)
                {
                    throw new InvalidOperationException("CAN未连接");
                }

                // 清空队列，确保只处理新收到的报文
                while (_receiveQueue.TryDequeue(out _)) { }

                // 记录开始时间
                DateTime startTime = DateTime.Now;

                // 循环检查队列中是否有匹配的报文
                while ((DateTime.Now - startTime).TotalMilliseconds < timeout)
                {
                    // 检查队列中是否有报文
                    while (_receiveQueue.TryDequeue(out VCI_CAN_OBJ canFrame))
                    {
                        uint receivedCanId = canFrame.ID;

                        // 如果指定了CAN ID，检查是否匹配
                        if (!canId.HasValue || receivedCanId == canId.Value)
                        {
                            string hexData = BitConverter.ToString(canFrame.Data, 0, canFrame.DataLen).Replace("-", " ");
                            string receiveResultStr = $"[{_deviceModel}] 接收成功: ID=0x{receivedCanId:X3}, Data={hexData}, DLC={canFrame.DataLen}, Timestamp={canFrame.TimeStamp}us";
                            return receiveResultStr;
                        }
                    }

                    // 短暂休眠，避免CPU占用过高
                    await Task.Delay(10);
                }

                // 超时
                if (canId.HasValue)
                {
                    throw new TimeoutException($"在{timeout}ms内未收到指定CAN ID (0x{canId.Value:X3}) 的报文");
                }
                else
                {
                    throw new TimeoutException($"[{_deviceModel}] 接收超时，{timeout}ms内未收到任何报文");
                }
            }
            catch (TimeoutException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        /// <summary>
        /// 计算CAN波特率对应的timing0和timing1值
        /// </summary>
        private (byte timing0, byte timing1) CalculateCanTiming(int baudRate)
        {
            // 根据波特率计算对应的timing值
            // 这里使用简单的映射，实际应用中需要根据设备手册调整
            switch (baudRate)
            {
                case 1000000:
                    return (0x00, 0x14);
                case 500000:
                    return (0x00, 0x1C);
                case 250000:
                    return (0x01, 0x1C);
                case 125000:
                    return (0x03, 0x1C);
                case 100000:
                    return (0x04, 0x1C);
                case 50000:
                    return (0x09, 0x1C);
                case 20000:
                    return (0x13, 0x1C);
                case 10000:
                    return (0x27, 0x1C);
                default:
                    return (0x01, 0x1C); // 默认250K
            }
        }

        /// <summary>
        /// 从字符串解析CAN帧
        /// </summary>
        private VCI_CAN_OBJ ParseCanFrameFromString(string data)
        {
            // 默认值
            uint canId = 0x100;
            byte[] frameData = new byte[8];
            int dlc = 8;

            // 解析ID
            int idIndex = data.IndexOf("ID=", StringComparison.OrdinalIgnoreCase);
            if (idIndex >= 0)
            {
                int idEndIndex = data.IndexOf(',', idIndex);
                if (idEndIndex >= 0)
                {
                    string idStr = data.Substring(idIndex + 3, idEndIndex - idIndex - 3).Trim();
                    canId = Convert.ToUInt32(idStr, 16);
                }
            }

            // 解析DATA
            int dataIndex = data.IndexOf("DATA=", StringComparison.OrdinalIgnoreCase);
            if (dataIndex >= 0)
            {
                string dataStr = data.Substring(dataIndex + 5).Trim();
                string[] dataBytes = dataStr.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                dlc = Math.Min(dataBytes.Length, 8);
                frameData = new byte[8];
                for (int i = 0; i < dlc; i++)
                {
                    frameData[i] = Convert.ToByte(dataBytes[i], 16);
                }
                // 确保剩余字节用00补全
                for (int i = dlc; i < 8; i++)
                {
                    frameData[i] = 0;
                }
            }

            // 创建CAN帧
            return new VCI_CAN_OBJ
            {
                ID = canId,
                TimeStamp = 0,
                TimeFlag = 0,
                SendType = 0,
                RemoteFlag = 0,
                ExternFlag = 0,
                DataLen = 8,
                Data = frameData,
                Reserved = new byte[3]
            };
        }
    }
}