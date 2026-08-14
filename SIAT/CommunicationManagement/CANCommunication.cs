using System;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using SIAT.ResourceManagement;
using ZLGAPI;

namespace SIAT.CommunicationManagement
{
    public class CANCommunication : CommunicationBase
    {
        private bool _isCanFd;
        private bool _isConnected;
        private IntPtr _deviceHandle;
        private IntPtr _channelHandle;
        private string _deviceModel;
        private uint _deviceType;
        private uint _channelIndex;
        private bool _isMerge = false; // 合并接收标志
        
        // 线程接收相关字段
        private ConcurrentQueue<ZLGCAN.ZCAN_Receive_Data> _receiveQueue;
        private ConcurrentQueue<ZLGCAN.ZCAN_ReceiveFD_Data> _receiveFdQueue;
        private CancellationTokenSource _cts;
        private Thread? _receiveThread;
        private bool _isReceiveThreadRunning;
        
        public CANCommunication(CommunicationParams parameters, DeviceType deviceType = DeviceType.ZLG_USBCAN_I) : base(parameters)
        {
            _isCanFd = parameters.IsCanFd;
            _channelIndex = parameters.CanChannel == CanChannelType.CAN0 ? 0u : 1u;
            
            // 根据设备类型设置不同的设备型号和设备类型
            switch (deviceType)
            {
                case DeviceType.ZLG_USBCAN_I:
                    _deviceModel = "USBCAN-Ⅰ"; // 周立功设备型号
                    _deviceType = ZLGCAN.ZCAN_USBCAN1; // USBCAN-Ⅰ设备类型
                    break;
                case DeviceType.ZLG_USBCAN_II:
                    _deviceModel = "USBCAN-II"; // 周立功设备型号
                    _deviceType = ZLGCAN.ZCAN_USBCAN2; // USBCAN-II设备类型
                    break;
                case DeviceType.ZLG_CANFDU:
                case DeviceType.ZLG_CANFDU_Pro:
                    _deviceModel = "USBCANFDU"; // 周立功设备型号
                    _deviceType = ZLGCAN.ZCAN_USBCANFD_200U; // USBCANFDU设备类型
                    break;
                default:
                    _deviceModel = "USBCANFD-200U"; // 周立功设备型号
                    _deviceType = ZLGCAN.ZCAN_USBCANFD_200U; // USBCANFD-200U设备类型
                    break;
            }
            
            // 初始化线程接收相关字段
            _receiveQueue = new ConcurrentQueue<ZLGCAN.ZCAN_Receive_Data>();
            _receiveFdQueue = new ConcurrentQueue<ZLGCAN.ZCAN_ReceiveFD_Data>();
            _cts = new CancellationTokenSource();
            _isReceiveThreadRunning = false;
        }
        
        /// <summary>
        /// 设置合并接收模式
        /// </summary>
        /// <param name="isMerge">是否启用合并接收</param>
        public void SetMergeReceive(bool isMerge)
        {
            _isMerge = isMerge;
        }
        
        public override async Task<bool> ConnectAsync(CommunicationParams parameters)
        {
            try
            {
                _isCanFd = parameters.IsCanFd;
                _channelIndex = parameters.CanChannel == CanChannelType.CAN0 ? 0u : 1u;
                
                // 1. 打开CAN设备
                _deviceHandle = ZLGCAN.ZCAN_OpenDevice(_deviceType, (uint)parameters.DeviceIndex, 0);
                if (_deviceHandle == IntPtr.Zero)
                {
                    throw new Exception("打开CAN设备失败");
                }

                // 2. 根据设备类型和是否为CANFD设备选择不同的初始化逻辑
                if (_isCanFd)
                {
                    // 初始化CANFD设备
                    InitializeZlgCanFdDevice(parameters);
                }
                else
                {
                    // 初始化CAN设备
                    InitializeZlgCanDevice(parameters);
                }

                // 3. 启动CAN通道
                uint startResult = ZLGCAN.ZCAN_StartCAN(_channelHandle);
                if (startResult != 1)
                {
                    ZLGCAN.ZCAN_CloseDevice(_deviceHandle);
                    throw new Exception($"启动CAN通道失败，错误码: {startResult}");
                }
                
                _isConnected = true;
                
                // 启动接收线程
                StartReceiveThread();
                
                UpdateConnectionStatus(true, $"已连接到周立功{_deviceModel}设备");
                return true;
            }
            catch (Exception ex)
            {
                _isConnected = false;
                if (_deviceHandle != IntPtr.Zero)
                {
                    ZLGCAN.ZCAN_CloseDevice(_deviceHandle);
                    _deviceHandle = IntPtr.Zero;
                }
                _channelHandle = IntPtr.Zero;
                UpdateConnectionStatus(false, $"连接失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 初始化周立功CAN设备
        /// </summary>
        private void InitializeZlgCanDevice(CommunicationParams parameters)
        {
            // 1. 设置终端电阻
            if (parameters.IsTerminalResistorEnabled)
            {
                string resistancePath = String.Format("{0}/initenal_resistance", _channelIndex);
                uint ret = ZLGCAN.ZCAN_SetValue(_deviceHandle, resistancePath, "1");
                if (ret != 1)
                {
                    throw new Exception("设置终端电阻失败");
                }
            }

            // 2. 初始化CAN通道
            ZLGCAN.ZCAN_CHANNEL_INIT_CONFIG initConfig = new ZLGCAN.ZCAN_CHANNEL_INIT_CONFIG();
            
            // CAN模式配置
            initConfig.can_type = 0; // 0 - CAN
            
            // 设置验收码和屏蔽码，默认接收所有报文
            initConfig.config.can.acc_code = 0x00000000;
            initConfig.config.can.acc_mask = 0xFFFFFFFF;
            initConfig.config.can.reserved = 0;
            
            // 设置波特率
            (byte timing0, byte timing1) = CalculateCanTiming(parameters.CanBaudRate);
            initConfig.config.can.timing0 = timing0;
            initConfig.config.can.timing1 = timing1;
            
            // 设置过滤器模式和工作模式
            initConfig.config.can.filter = (byte)parameters.CanFilterMode;
            initConfig.config.can.mode = (byte)parameters.CanWorkMode;
            
            IntPtr initConfigPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(ZLGCAN.ZCAN_CHANNEL_INIT_CONFIG)));
            Marshal.StructureToPtr(initConfig, initConfigPtr, false);
            
            _channelHandle = ZLGCAN.ZCAN_InitCAN(_deviceHandle, _channelIndex, initConfigPtr);
            Marshal.FreeHGlobal(initConfigPtr);
            
            if (_channelHandle == IntPtr.Zero)
            {
                ZLGCAN.ZCAN_CloseDevice(_deviceHandle);
                throw new Exception("初始化CAN通道失败");
            }
        }

        /// <summary>
        /// 初始化周立功CANFD设备
        /// </summary>
        private void InitializeZlgCanFdDevice(CommunicationParams parameters)
        {
            // 1. 设置仲裁域波特率
            string abitPath = String.Format("{0}/canfd_abit_baud_rate", _channelIndex);
            uint ret = ZLGCAN.ZCAN_SetValue(_deviceHandle, abitPath, parameters.CanBaudRate.ToString());
            if (ret != 1)
            {
                throw new Exception("设置仲裁域波特率失败");
            }

            // 2. 设置数据域波特率
            string dbitPath = String.Format("{0}/canfd_dbit_baud_rate", _channelIndex);
            ret = ZLGCAN.ZCAN_SetValue(_deviceHandle, dbitPath, parameters.CanFdDataBaudRate.ToString());
            if (ret != 1)
            {
                throw new Exception("设置数据域波特率失败");
            }

            // 3. 设置终端电阻
            if (parameters.IsTerminalResistorEnabled)
            {
                string resistancePath = String.Format("{0}/initenal_resistance", _channelIndex);
                ret = ZLGCAN.ZCAN_SetValue(_deviceHandle, resistancePath, "1");
                if (ret != 1)
                {
                    throw new Exception("设置终端电阻失败");
                }
            }

            // 4. 初始化CAN通道
            ZLGCAN.ZCAN_CHANNEL_INIT_CONFIG initConfig = new ZLGCAN.ZCAN_CHANNEL_INIT_CONFIG();
            
            // CAN FD模式配置
            initConfig.can_type = 1; // 1 - CANFD
            initConfig.config.canfd.mode = (byte)parameters.CanWorkMode; // 0-正常模式，1-只听模式
            
            IntPtr initConfigPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(ZLGCAN.ZCAN_CHANNEL_INIT_CONFIG)));
            Marshal.StructureToPtr(initConfig, initConfigPtr, false);
            
            _channelHandle = ZLGCAN.ZCAN_InitCAN(_deviceHandle, _channelIndex, initConfigPtr);
            Marshal.FreeHGlobal(initConfigPtr);
            
            if (_channelHandle == IntPtr.Zero)
            {
                ZLGCAN.ZCAN_CloseDevice(_deviceHandle);
                throw new Exception("初始化CAN通道失败");
            }

            // 5. 设置合并接收
            if (_isMerge)
            {
                // 设置合并接收
                uint mergeResult = ZLGCAN.ZCAN_SetValue(_deviceHandle, "0/set_device_recv_merge", "1");
                if (mergeResult != 1)
                {
                    throw new Exception("设置合并接收失败");
                }
            }
            else
            {
                // 关闭合并接收
                uint mergeResult = ZLGCAN.ZCAN_SetValue(_deviceHandle, "0/set_device_recv_merge", "0");
                if (mergeResult != 1)
                {
                    throw new Exception("关闭合并接收失败");
                }
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
                
                // 停止接收线程
                StopReceiveThread();
                
                // 重置CAN通道
                if (_channelHandle != IntPtr.Zero)
                {
                    ZLGCAN.ZCAN_ResetCAN(_channelHandle);
                }
                
                // 关闭设备
                uint closeResult = ZLGCAN.ZCAN_CloseDevice(_deviceHandle);
                if (closeResult != 1)
                {
                    throw new Exception($"关闭设备失败，错误码: {closeResult}");
                }
                
                _deviceHandle = IntPtr.Zero;
                _channelHandle = IntPtr.Zero;
                _isConnected = false;
                UpdateConnectionStatus(false, "已断开连接");

                return true;
            }
            catch (Exception ex)
            {
                _isConnected = false;
                _deviceHandle = IntPtr.Zero;
                _channelHandle = IntPtr.Zero;
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
                ZLGCAN.ZCAN_ClearBuffer(_channelHandle);
                
                // 解析字符串数据为CAN帧
                // 格式示例: "ID=0x123,DATA=11 22 33 44 55 66 77 88"
                var frame = ParseCanFrameFromString(data);
                
                // 判断是否为CANFD协议
                bool isCanFdProtocol = protocolType.Equals("CANFD", StringComparison.OrdinalIgnoreCase);
                
                if (isCanFdProtocol)
                {
                    // 发送CAN FD帧
                    ZLGCAN.ZCAN_TransmitFD_Data transmitData = new ZLGCAN.ZCAN_TransmitFD_Data();
                    transmitData.frame = frame.Item2;
                    transmitData.transmit_type = 0; // 0-正常发送
                    
                    IntPtr transmitPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(ZLGCAN.ZCAN_TransmitFD_Data)));
                    Marshal.StructureToPtr(transmitData, transmitPtr, false);
                    
                    uint sendResult = ZLGCAN.ZCAN_TransmitFD(_channelHandle, transmitPtr, 1);
                    Marshal.FreeHGlobal(transmitPtr);
                    
                    if (sendResult != 1)
                    {
                        throw new Exception($"发送CAN FD帧失败，错误码: {sendResult}");
                    }
                }
                else
                {
                    // 发送CAN帧
                    ZLGCAN.ZCAN_Transmit_Data transmitData = new ZLGCAN.ZCAN_Transmit_Data();
                    transmitData.frame = frame.Item1;
                    transmitData.transmit_type = 0; // 0-正常发送
                    
                    IntPtr transmitPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(ZLGCAN.ZCAN_Transmit_Data)));
                    Marshal.StructureToPtr(transmitData, transmitPtr, false);
                    
                    uint sendResult = ZLGCAN.ZCAN_Transmit(_channelHandle, transmitPtr, 1);
                    Marshal.FreeHGlobal(transmitPtr);
                    
                    if (sendResult != 1)
                    {
                        throw new Exception($"发送CAN帧失败，错误码: {sendResult}");
                    }
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
                ZLGCAN.ZCAN_ClearBuffer(_channelHandle);
                
                string hexData = BitConverter.ToString(data).Replace("-", " ");

                
                // 创建默认CAN帧
                bool isCanFdProtocol = protocolType.Equals("CANFD", StringComparison.OrdinalIgnoreCase);
                
                if (isCanFdProtocol)
                {
                    // 发送CAN FD帧
                    ZLGCAN.canfd_frame frame = new ZLGCAN.canfd_frame();
                    frame.can_id = 0x100;
                    frame.len = 64;
                    frame.flags = 0;
                    frame.data = new byte[64];
                    // 复制数据，不足64字节时自动补0
                    int copyLength = Math.Min(data.Length, 64);
                    Array.Copy(data, frame.data, copyLength);
                    // 确保剩余字节用00补全
                    for (int i = copyLength; i < 64; i++)
                    {
                        frame.data[i] = 0;
                    }
                    
                    ZLGCAN.ZCAN_TransmitFD_Data transmitData = new ZLGCAN.ZCAN_TransmitFD_Data();
                    transmitData.frame = frame;
                    transmitData.transmit_type = 0; // 0-正常发送
                    
                    IntPtr transmitPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(ZLGCAN.ZCAN_TransmitFD_Data)));
                    Marshal.StructureToPtr(transmitData, transmitPtr, false);
                    
                    uint sendResult = ZLGCAN.ZCAN_TransmitFD(_channelHandle, transmitPtr, 1);
                    Marshal.FreeHGlobal(transmitPtr);
                    
                    if (sendResult != 1)
                    {
                        throw new Exception($"发送CAN FD帧失败，错误码: {sendResult}");
                    }
                }
                else
                {
                    // 发送CAN帧
                    ZLGCAN.can_frame frame = new ZLGCAN.can_frame();
                    frame.can_id = 0x100;
                    frame.can_dlc = 8;
                    frame.data = new byte[8];
                    // 复制数据，不足8字节时自动补0
                    int copyLength = Math.Min(data.Length, 8);
                    Array.Copy(data, frame.data, copyLength);
                    // 确保剩余字节用00补全
                    for (int i = copyLength; i < 8; i++)
                    {
                        frame.data[i] = 0;
                    }
                    
                    ZLGCAN.ZCAN_Transmit_Data transmitData = new ZLGCAN.ZCAN_Transmit_Data();
                    transmitData.frame = frame;
                    transmitData.transmit_type = 0; // 0-正常发送
                    
                    IntPtr transmitPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(ZLGCAN.ZCAN_Transmit_Data)));
                    Marshal.StructureToPtr(transmitData, transmitPtr, false);
                    
                    uint sendResult = ZLGCAN.ZCAN_Transmit(_channelHandle, transmitPtr, 1);
                    Marshal.FreeHGlobal(transmitPtr);
                    
                    if (sendResult != 1)
                    {
                        throw new Exception($"发送CAN帧失败，错误码: {sendResult}");
                    }
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
            return await ReceiveAsync(5000, string.Empty);
        }
        
        public override async Task<string> ReceiveAsync(int timeout)
        {
            return await ReceiveAsync(timeout, null, string.Empty);
        }
        
        public override async Task<string> ReceiveAsync(int timeout, string protocolType)
        {
            return await ReceiveAsync(timeout, null, protocolType);
        }
        
        public override async Task<string> ReceiveAsync(int timeout, uint? canId, string protocolType)
        {
            try
            {
                if (!_isConnected)
                {
                    throw new InvalidOperationException("CAN未连接");
                }

                // 清空队列，确保只处理新收到的报文
                while (_receiveQueue.TryDequeue(out _)) { }
                while (_receiveFdQueue.TryDequeue(out _)) { }

                // 记录开始时间
                DateTime startTime = DateTime.Now;

                // 判断是否为CANFD协议
                bool isCanFdProtocol = protocolType.Equals("CANFD", StringComparison.OrdinalIgnoreCase);
                bool checkBothQueues = string.IsNullOrEmpty(protocolType);

                // 循环检查队列中是否有匹配的报文
                while ((DateTime.Now - startTime).TotalMilliseconds < timeout)
                {
                    // 检查CAN FD队列（如果需要）
                    if (isCanFdProtocol || checkBothQueues)
                    {
                        while (_receiveFdQueue.TryDequeue(out ZLGCAN.ZCAN_ReceiveFD_Data receiveData))
                        {
                            ZLGCAN.canfd_frame frame = receiveData.frame;
                            uint receivedCanId = frame.can_id;

                            // 如果指定了CAN ID，检查是否匹配
                            if (!canId.HasValue || receivedCanId == canId.Value)
                            {
                                string hexData = BitConverter.ToString(frame.data, 0, frame.len).Replace("-", " ");
                                string receiveResultStr = $"[{_deviceModel}] 接收成功: ID=0x{receivedCanId:X3}, Data={hexData}, DLC={frame.len}, Timestamp={receiveData.timestamp}us";
                                return receiveResultStr;
                            }
                        }
                    }

                    // 检查CAN队列（如果需要）
                    if (!isCanFdProtocol || checkBothQueues)
                    {
                        while (_receiveQueue.TryDequeue(out ZLGCAN.ZCAN_Receive_Data receiveData))
                        {
                            ZLGCAN.can_frame frame = receiveData.frame;
                            uint receivedCanId = frame.can_id;

                            // 如果指定了CAN ID，检查是否匹配
                            if (!canId.HasValue || receivedCanId == canId.Value)
                            {
                                string hexData = BitConverter.ToString(frame.data, 0, frame.can_dlc).Replace("-", " ");
                                string receiveResultStr = $"[{_deviceModel}] 接收成功: ID=0x{receivedCanId:X3}, Data={hexData}, DLC={frame.can_dlc}, Timestamp={receiveData.timestamp}us";
                                Console.WriteLine(receiveResultStr);
                                return receiveResultStr;
                            }
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
        /// 启动接收线程
        /// </summary>
        private void StartReceiveThread()
        {
            if (!_isReceiveThreadRunning)
            {
                _cts = new CancellationTokenSource();
                _receiveQueue = new ConcurrentQueue<ZLGCAN.ZCAN_Receive_Data>();
                _receiveFdQueue = new ConcurrentQueue<ZLGCAN.ZCAN_ReceiveFD_Data>();
                
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
                _receiveFdQueue.Clear();
            }
        }

        /// <summary>
        /// 接收线程处理函数
        /// </summary>
        private void ReceiveThreadProc()
        {
            try
            {
                // 预计算结构体大小
                int dataObjSize = Marshal.SizeOf(typeof(ZLGCAN.ZCANDataObj));
                int canfdSize = Marshal.SizeOf(typeof(ZLGCAN.ZCANCANFDData));

                // 分配非托管内存（循环外分配，复用避免频繁申请释放）
                IntPtr pDataObjs = Marshal.AllocHGlobal(dataObjSize * 100); // 写死100个最大
                IntPtr pCanfdBuffer = Marshal.AllocHGlobal(canfdSize);

                try
                {
                    while (!_cts.Token.IsCancellationRequested)
                    {
                        if (!_isConnected || _deviceHandle == IntPtr.Zero)
                        {
                            Thread.Sleep(100);
                            continue;
                        }

                        try
                        {
                            if (_isMerge)
                            {
                                // 合并接收模式
                                ReceiveMergedData(pDataObjs, pCanfdBuffer, dataObjSize, canfdSize);
                            }
                            else
                            {
                                // 普通接收模式
                                if (_isCanFd)
                                {
                                    // 接收CAN FD数据
                                    ReceiveCanFdData();
                                }
                                else
                                {
                                    // 接收CAN数据
                                    ReceiveCanData();
                                }
                            }
                        }
                        catch (Exception)
                        {
                            // 忽略接收过程中的错误
                        }

                        // 短暂休眠，避免CPU占用过高
                        Thread.Sleep(10);
                    }
                }
                finally
                {
                    // 确保非托管内存必释放
                    Marshal.FreeHGlobal(pCanfdBuffer);
                    Marshal.FreeHGlobal(pDataObjs);
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

        /// <summary>
        /// 接收合并模式下的数据
        /// </summary>
        private void ReceiveMergedData(IntPtr pDataObjs, IntPtr pCanfdBuffer, int dataObjSize, int canfdSize)
        {
            // 获取待接收数据量，无数据则休眠后继续
            uint recvNum = ZLGCAN.ZCAN_GetReceiveNum(_deviceHandle, 2);
            if (recvNum == 0)
            {
                Thread.Sleep(10);
                return;
            }

            // 接收数据
            uint actualRecv = ZLGCAN.ZCAN_ReceiveData(_deviceHandle, pDataObjs, 100, 10);
            if (actualRecv == 0)
            {
                return;
            }

            // 遍历处理每个接收的数据
            for (int i = 0; i < actualRecv; i++)
            {
                // 计算当前数据对象指针
                IntPtr pCurrentData = (IntPtr)(pDataObjs.ToInt64() + i * dataObjSize);
                ZLGCAN.ZCANDataObj dataObj = Marshal.PtrToStructure<ZLGCAN.ZCANDataObj>(pCurrentData);

                // 根据数据类型处理
                switch (dataObj.dataType)
                {
                    case 1: // CAN/CANFD
                        // 复制data到非托管缓冲区
                        Marshal.Copy(dataObj.data, 0, pCanfdBuffer, canfdSize);
                        ZLGCAN.ZCANCANFDData canfdData = Marshal.PtrToStructure<ZLGCAN.ZCANCANFDData>(pCanfdBuffer);

                        if ((canfdData.flag & 1) == 0)
                        {
                            // 处理CAN帧
                            ProcessCanFrame(canfdData);
                        }
                        else
                        {
                            // 处理CANFD帧
                            ProcessCanFdFrame(canfdData);
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// 接收CAN数据
        /// </summary>
        private void ReceiveCanData()
        {
            try
            {
                // 检查通道句柄是否有效
                if (_channelHandle == IntPtr.Zero)
                {
                    return;
                }

                // 预计算结构体大小
                int receiveDataSize = Marshal.SizeOf(typeof(ZLGCAN.ZCAN_Receive_Data));
                int bufferSize = receiveDataSize * 100; // 100帧缓冲区

                // 分配非托管内存
                IntPtr receivePtr = Marshal.AllocHGlobal(bufferSize);
                if (receivePtr == IntPtr.Zero)
                {
                    return;
                }

                try
                {
                    // 接收最多100帧报文，等待时间100ms
                    uint receiveResult = ZLGCAN.ZCAN_Receive(_channelHandle, receivePtr, 100, 100);

                    if (receiveResult > 0)
                    {
                        // 将接收到的报文加入队列
                        for (int i = 0; i < receiveResult; i++)
                        {
                            IntPtr framePtr = new IntPtr(receivePtr.ToInt64() + i * receiveDataSize);
                            ZLGCAN.ZCAN_Receive_Data receiveData = Marshal.PtrToStructure<ZLGCAN.ZCAN_Receive_Data>(framePtr);
                            _receiveQueue.Enqueue(receiveData);
                        }
                    }
                }
                finally
                {
                    // 确保释放非托管内存
                    Marshal.FreeHGlobal(receivePtr);
                }
            }
            catch (Exception)
            {
                // 忽略接收过程中的错误
            }
        }

        /// <summary>
        /// 接收CANFD数据
        /// </summary>
        private void ReceiveCanFdData()
        {
            try
            {
                // 检查通道句柄是否有效
                if (_channelHandle == IntPtr.Zero)
                {
                    return;
                }

                // 预计算结构体大小
                int receiveDataSize = Marshal.SizeOf(typeof(ZLGCAN.ZCAN_ReceiveFD_Data));
                int bufferSize = receiveDataSize * 100; // 100帧缓冲区

                // 分配非托管内存
                IntPtr receivePtr = Marshal.AllocHGlobal(bufferSize);
                if (receivePtr == IntPtr.Zero)
                {
                    return;
                }

                try
                {
                    // 接收最多100帧报文，等待时间100ms
                    uint receiveResult = ZLGCAN.ZCAN_ReceiveFD(_channelHandle, receivePtr, 100, 100);

                    if (receiveResult > 0)
                    {
                        // 将接收到的报文加入队列
                        for (int i = 0; i < receiveResult; i++)
                        {
                            IntPtr framePtr = new IntPtr(receivePtr.ToInt64() + i * receiveDataSize);
                            ZLGCAN.ZCAN_ReceiveFD_Data receiveData = Marshal.PtrToStructure<ZLGCAN.ZCAN_ReceiveFD_Data>(framePtr);
                            _receiveFdQueue.Enqueue(receiveData);
                        }
                    }
                }
                finally
                {
                    // 确保释放非托管内存
                    Marshal.FreeHGlobal(receivePtr);
                }
            }
            catch (Exception)
            {
                // 忽略接收过程中的错误
            }
        }

        /// <summary>
        /// 处理CAN帧
        /// </summary>
        private void ProcessCanFrame(ZLGCAN.ZCANCANFDData canfdData)
        {
            ZLGCAN.ZCAN_Receive_Data receiveData = new ZLGCAN.ZCAN_Receive_Data
            {
                frame = new ZLGCAN.can_frame
                {
                    can_id = canfdData.frame.can_id,
                    can_dlc = (byte)canfdData.frame.len,
                    data = new byte[8]
                },
                timestamp = canfdData.timeStamp
            };
            // 复制数据
            Array.Copy(canfdData.frame.data, receiveData.frame.data, Math.Min(8, (int)canfdData.frame.len));
            _receiveQueue.Enqueue(receiveData);
        }

        /// <summary>
        /// 处理CANFD帧
        /// </summary>
        private void ProcessCanFdFrame(ZLGCAN.ZCANCANFDData canfdData)
        {
            ZLGCAN.ZCAN_ReceiveFD_Data receiveFdData = new ZLGCAN.ZCAN_ReceiveFD_Data
            {
                frame = canfdData.frame,
                timestamp = canfdData.timeStamp
            };
            _receiveFdQueue.Enqueue(receiveFdData);
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
        /// 计算CAN FD波特率对应的位定时值
        /// </summary>
        private uint CalculateBitTiming(int baudRate, int samplePoint)
        {
            // 简化实现，实际应用中需要根据设备手册调整
            // 位定时格式：保留8位 + 采样点8位 + 时间段2 8位 + 时间段1 8位
            uint samplePointValue = (uint)(samplePoint & 0xFF);
            uint timeSegment2 = 2u;
            uint timeSegment1 = 3u;
            return (0u << 24) | (samplePointValue << 16) | (timeSegment2 << 8) | timeSegment1;
        }
        
        /// <summary>
        /// 从字符串解析CAN帧
        /// </summary>
        private Tuple<ZLGCAN.can_frame, ZLGCAN.canfd_frame> ParseCanFrameFromString(string data)
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
            ZLGCAN.can_frame canFrame = new ZLGCAN.can_frame();
            canFrame.can_id = canId;
            canFrame.can_dlc = 8;
            canFrame.data = frameData;
            
            // 创建CAN FD帧
            ZLGCAN.canfd_frame canFdFrame = new ZLGCAN.canfd_frame();
            canFdFrame.can_id = canId;
            canFdFrame.len = 64;
            canFdFrame.flags = 0;
            canFdFrame.data = new byte[64];
            // 复制CAN帧数据（最多8字节）
            Array.Copy(frameData, canFdFrame.data, 8);
            // 确保剩余字节用00补全到64字节
            for (int i = 8; i < 64; i++)
            {
                canFdFrame.data[i] = 0;
            }
            
            return Tuple.Create(canFrame, canFdFrame);
        }
    }
}