using SIAT.ResourceManagement;
using SIAT.TSET;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SIAT.Devices
{
    /// <summary>
    /// CAN通信设备
    /// 支持常规CAN收发与UDS(ISO 14229)诊断服务，传输层遵循ISO 15765-2(ISO-TP)。
    /// 通讯层使用 DeviceBase 的字符串收发：发送格式 "ID=0x...,DATA=AA BB..."，
    /// 接收为格式化字符串 "[模型] 接收成功: ID=0x..., Data=AA BB..., DLC=..., Timestamp=...us"。
    /// </summary>
    [DeviceDefinition("CAN通信", Description = "CAN总线通信及UDS诊断设备", CommunicationType = CommunicationType.CAN)]
    public class CANDevice : DeviceBase
    {
        private const uint DEFAULT_REQUEST_ID = 0x7E0;
        private const uint DEFAULT_RESPONSE_ID = 0x7E8;
        private const int DEFAULT_TIMEOUT = 2000;
        private const int TESTER_PRESENT_INTERVAL = 2000; // 会话维持发送间隔(ms)

        // 会话维持(测试仪在线)后台任务
        private CancellationTokenSource? _testerPresentCts;
        private Task? _testerPresentTask;

        #region 常规CAN收发

        /// <summary>
        /// CAN发送
        /// 发送一帧原始CAN报文(经典CAN,8字节)
        /// </summary>
        [StepDefinition("CAN发送", Description = "发送一帧原始CAN报文", StepType = StepType.SendOnly)]
        [InputBinding("CanId", "CAN标识符(十六进制,如7E0)")]
        [InputBinding("Data", "数据(十六进制,空格分隔,如22 F1 90)")]
        [InputBinding("FrameType", "帧类型(Standard=标准帧,Extended=扩展帧,默认Standard)")]
        [InputBinding("FrameFormat", "帧格式(CAN=经典CAN,CANFD=CANFD,默认CAN)")]
        public async Task<TestStepResult> CAN发送Step()
        {
            uint canId = ParseHexUint(GetInputValue("CanId", "100"));
            byte[] data = ParseHexSpaced(GetInputValue("Data", ""));
            bool isExtended = ParseFrameType(GetInputValue("FrameType", "Standard"));
            bool useCanFd = ParseFrameFormat(GetInputValue("FrameFormat", "CAN"));

            if (!IsDeviceConnected())
                return FailResult("CAN发送", "设备未连接");

            try
            {
                await SendCanFrameAsync(canId, data, isExtended, useCanFd);
    
                return OkResult("CAN发送", $"ID=0x{canId:X}, Data={ToHexSpaced(data)}, {(isExtended ? "扩展帧" : "标准帧")}, {(useCanFd ? "CANFD" : "CAN")}");
            }
            catch (Exception ex)
            {
                return FailResult("CAN发送", ex.Message);
            }
        }

        /// <summary>
        /// CAN接收
        /// 接收一帧CAN报文，可按ID过滤
        /// </summary>
        [StepDefinition("CAN接收", Description = "接收一帧CAN报文", StepType = StepType.ReadOnly)]
       
        [InputBinding("ExpectedId", "期望CAN ID(十六进制,可选)")]
        [OutputBinding("Receiveddata", "接收到的报文")]
        public async Task<TestStepResult> CAN接收Step()
        {
          
            string expectedIdStr = GetInputValue("ExpectedId", "");
            uint? expectedId = string.IsNullOrWhiteSpace(expectedIdStr) ? (uint?)null : ParseHexUint(expectedIdStr);

            if (!IsDeviceConnected())
                return FailResult("CAN接收", "设备未连接");

            try
            {
                CanFrame? frame = await ReceiveCanFrameAsync(expectedId, 2000);
                if (frame == null)
                    return FailResult("CAN接收", $"在{2000}ms内未收到{(expectedId.HasValue ? $"ID=0x{expectedId.Value:X}的" : "")}报文");

                var f = frame.Value;
              
                SetOutputValue("Receiveddata", $"{ToHexSpaced(f.Data)}");

                return OkResult("CAN接收", $"ID=0x{f.CanId:X}, Data={ToHexSpaced(f.Data)}, DLC={f.Data.Length}");
            }
            catch (TimeoutException)
            {
                return FailResult("CAN接收", $"在{2000}ms内未接收到报文");
            }
            catch (Exception ex)
            {
                return FailResult("CAN接收", ex.Message);
            }
        }

        #endregion

        #region UDS诊断服务

        /// <summary>
        /// UDS请求
        /// 通用UDS服务请求(支持ISO-TP多帧)，发送服务ID+数据，接收响应
        /// </summary>
        [StepDefinition("UDS请求", Description = "通用UDS服务请求(支持ISO-TP多帧)", StepType = StepType.SendAndReceive)]
        [InputBinding("Service", "服务ID(十六进制,如22)")]
        [InputBinding("RequestData", "请求数据(十六进制,空格分隔,不含服务ID)")]
        [InputBinding("RequestId", "请求CAN ID(十六进制,默认7E0)")]
        [InputBinding("ResponseId", "响应CAN ID(十六进制,默认7E8)")]
        [InputBinding("Timeout", "超时(ms,默认2000)")]
        [InputBinding("FrameType", "帧类型(Standard=标准帧,Extended=扩展帧,默认Standard)")]
        [InputBinding("FrameFormat", "帧格式(CAN=经典CAN,CANFD=CANFD,默认CAN)")]
        [OutputBinding("Response", "响应数据(十六进制)")]
        [OutputBinding("IsPositive", "是否肯定响应")]
        [OutputBinding("Nrc", "否定响应码(否定时)")]
        [OutputBinding("Result", "执行结果")]
        public async Task<TestStepResult> UDS请求Step()
        {
            byte service = ParseHexByte(GetInputValue("Service", "3E"));
            byte[] requestData = ParseHexSpaced(GetInputValue("RequestData", ""));
            uint reqId = ParseHexUint(GetInputValue("RequestId", DEFAULT_REQUEST_ID.ToString("X")));
            uint respId = ParseHexUint(GetInputValue("ResponseId", DEFAULT_RESPONSE_ID.ToString("X")));
            int timeout = int.Parse(GetInputValue("Timeout", DEFAULT_TIMEOUT.ToString()));
            bool isExtended = ParseFrameType(GetInputValue("FrameType", "Standard"));
            bool useCanFd = ParseFrameFormat(GetInputValue("FrameFormat", "CAN"));

            if (!IsDeviceConnected())
                return FailResult("UDS请求", "设备未连接");

            try
            {
                byte[] request = new byte[1 + requestData.Length];
                request[0] = service;
                Buffer.BlockCopy(requestData, 0, request, 1, requestData.Length);

                byte[] response = await UdsRequestAsync(reqId, respId, request, timeout, isExtended, useCanFd);
                bool isPositive = IsPositiveResponse(response, service);
                string respHex = ToHexSpaced(response);
                SetOutputValue("Response", respHex);
                SetOutputValue("IsPositive", isPositive);

                if (!isPositive)
                {
                    byte nrc = GetNegativeResponseCode(response);
                    SetOutputValue("Nrc", $"{nrc:X2}");
                    return new TestStepResult
                    {
                        StepName = "UDS请求",
                        IsSuccess = false,
                        ErrorMessage = $"否定响应 NRC=0x{nrc:X2}",
                        ActualValue = respHex
                    };
                }

                SetOutputValue("Result", "成功");
                return OkResult("UDS请求", respHex);
            }
            catch (TimeoutException)
            {
                return FailResult("UDS请求", $"在{timeout}ms内未收到响应");
            }
            catch (Exception ex)
            {
                return FailResult("UDS请求", ex.Message);
            }
        }

        /// <summary>
        /// 诊断会话控制
        /// UDS服务0x10，切换诊断会话模式
        /// </summary>
        [StepDefinition("诊断会话控制", Description = "切换诊断会话(0x10)", StepType = StepType.SendAndReceive)]
        [InputBinding("SessionType", "会话类型(01=默认,02=扩展,03=安全)")]
        [InputBinding("RequestId", "请求CAN ID(默认7E0)")]
        [InputBinding("ResponseId", "响应CAN ID(默认7E8)")]
        [InputBinding("Timeout", "超时(ms,默认2000)")]
        [InputBinding("FrameType", "帧类型(Standard=标准帧,Extended=扩展帧,默认Standard)")]
        [InputBinding("FrameFormat", "帧格式(CAN=经典CAN,CANFD=CANFD,默认CAN)")]
        [OutputBinding("Response", "响应数据")]
        [OutputBinding("Nrc", "否定响应码(否定时)")]
        [OutputBinding("Result", "执行结果")]
        public async Task<TestStepResult> 诊断会话控制Step()
        {
            byte sessionType = ParseHexByte(GetInputValue("SessionType", "01"));
            uint reqId = ParseHexUint(GetInputValue("RequestId", DEFAULT_REQUEST_ID.ToString("X")));
            uint respId = ParseHexUint(GetInputValue("ResponseId", DEFAULT_RESPONSE_ID.ToString("X")));
            int timeout = int.Parse(GetInputValue("Timeout", DEFAULT_TIMEOUT.ToString()));
            bool isExtended = ParseFrameType(GetInputValue("FrameType", "Standard"));
            bool useCanFd = ParseFrameFormat(GetInputValue("FrameFormat", "CAN"));

            if (!IsDeviceConnected())
                return FailResult("诊断会话控制", "设备未连接");

            try
            {
                byte[] response = await UdsRequestAsync(reqId, respId, new byte[] { 0x10, sessionType }, timeout, isExtended, useCanFd);
                var result = BuildServiceResult("诊断会话控制", response, 0x10);
                SetOutputValue("Response", ToHexSpaced(response));
                return result;
            }
            catch (TimeoutException)
            {
                return FailResult("诊断会话控制", $"在{timeout}ms内未收到响应");
            }
            catch (Exception ex)
            {
                return FailResult("诊断会话控制", ex.Message);
            }
        }

        /// <summary>
        /// 请求安全种子
        /// UDS服务0x27子功能01，请求安全访问种子(解锁第一步)
        /// </summary>
        [StepDefinition("请求安全种子", Description = "请求安全访问种子(0x27 01,解锁第一步)", StepType = StepType.ReadOnly)]
        [InputBinding("SubFunction", "子功能(默认01)")]
        [InputBinding("RequestId", "请求CAN ID(默认7E0)")]
        [InputBinding("ResponseId", "响应CAN ID(默认7E8)")]
        [InputBinding("Timeout", "超时(ms,默认2000)")]
        [InputBinding("FrameType", "帧类型(Standard=标准帧,Extended=扩展帧,默认Standard)")]
        [InputBinding("FrameFormat", "帧格式(CAN=经典CAN,CANFD=CANFD,默认CAN)")]
        [OutputBinding("Response", "响应数据")]
        [OutputBinding("Nrc", "否定响应码(否定时)")]
        [OutputBinding("Result", "执行结果")]
        public async Task<TestStepResult> 请求安全种子Step()
        {
            byte subFunc = ParseHexByte(GetInputValue("SubFunction", "01"));
            uint reqId = ParseHexUint(GetInputValue("RequestId", DEFAULT_REQUEST_ID.ToString("X")));
            uint respId = ParseHexUint(GetInputValue("ResponseId", DEFAULT_RESPONSE_ID.ToString("X")));
            int timeout = int.Parse(GetInputValue("Timeout", DEFAULT_TIMEOUT.ToString()));
            bool isExtended = ParseFrameType(GetInputValue("FrameType", "Standard"));
            bool useCanFd = ParseFrameFormat(GetInputValue("FrameFormat", "CAN"));

            if (!IsDeviceConnected())
                return FailResult("请求安全种子", "设备未连接");

            try
            {
                byte[] response = await UdsRequestAsync(reqId, respId, new byte[] { 0x27, subFunc }, timeout, isExtended, useCanFd);
                if (response == null || response.Length < 2)
                    return FailResult("请求安全种子", "响应数据无效");

                SetOutputValue("Response", ToHexSpaced(response));

                if (!IsPositiveResponse(response, 0x27))
                {
                    byte nrc = GetNegativeResponseCode(response);
                    SetOutputValue("Nrc", $"{nrc:X2}");
                    return FailResult("请求安全种子", $"请求种子失败 NRC=0x{nrc:X2}");
                }

                byte[] seed = response.Skip(2).ToArray();
                string seedHex = ToHexSpaced(seed);
                SetOutputValue("Seed", seedHex);
                SetOutputValue("Result", "成功");
                return OkResult("请求安全种子", $"Seed={seedHex}");
            }
            catch (TimeoutException)
            {
                return FailResult("请求安全种子", $"在{timeout}ms内未收到响应");
            }
            catch (Exception ex)
            {
                return FailResult("请求安全种子", ex.Message);
            }
        }

        /// <summary>
        /// 发送安全密钥
        /// UDS服务0x27子功能02，发送安全访问密钥(解锁第二步)
        /// </summary>
        [StepDefinition("发送安全密钥", Description = "发送安全访问密钥(0x27 02,解锁第二步)", StepType = StepType.SendAndReceive)]
        [InputBinding("Key", "密钥(十六进制,空格分隔)")]
        [InputBinding("SubFunction", "子功能(默认02,须为种子子功能+1)")]
        [InputBinding("RequestId", "请求CAN ID(默认7E0)")]
        [InputBinding("ResponseId", "响应CAN ID(默认7E8)")]
        [InputBinding("Timeout", "超时(ms,默认2000)")]
        [InputBinding("FrameType", "帧类型(Standard=标准帧,Extended=扩展帧,默认Standard)")]
        [InputBinding("FrameFormat", "帧格式(CAN=经典CAN,CANFD=CANFD,默认CAN)")]
        [OutputBinding("Response", "响应数据")]
        [OutputBinding("Nrc", "否定响应码(否定时)")]
        [OutputBinding("Result", "执行结果")]
        public async Task<TestStepResult> 发送安全密钥Step()
        {
            byte subFunc = ParseHexByte(GetInputValue("SubFunction", "02"));
            byte[] key = ParseHexSpaced(GetInputValue("Key", ""));
            uint reqId = ParseHexUint(GetInputValue("RequestId", DEFAULT_REQUEST_ID.ToString("X")));
            uint respId = ParseHexUint(GetInputValue("ResponseId", DEFAULT_RESPONSE_ID.ToString("X")));
            int timeout = int.Parse(GetInputValue("Timeout", DEFAULT_TIMEOUT.ToString()));
            bool isExtended = ParseFrameType(GetInputValue("FrameType", "Standard"));
            bool useCanFd = ParseFrameFormat(GetInputValue("FrameFormat", "CAN"));

            if (key.Length == 0)
                return FailResult("发送安全密钥", "密钥不能为空");

            if (!IsDeviceConnected())
                return FailResult("发送安全密钥", "设备未连接");

            try
            {
                byte[] request = new byte[2 + key.Length];
                request[0] = 0x27;
                request[1] = subFunc;
                Buffer.BlockCopy(key, 0, request, 2, key.Length);

                byte[] response = await UdsRequestAsync(reqId, respId, request, timeout, isExtended, useCanFd);
                var result = BuildServiceResult("发送安全密钥", response, 0x27);
                SetOutputValue("Response", ToHexSpaced(response));
                return result;
            }
            catch (TimeoutException)
            {
                return FailResult("发送安全密钥", $"在{timeout}ms内未收到响应");
            }
            catch (Exception ex)
            {
                return FailResult("发送安全密钥", ex.Message);
            }
        }

        /// <summary>
        /// 开启会话维持
        /// 启动后台任务，每2秒发送一次测试仪在线(0x3E,抑制肯定响应)以保持当前诊断会话
        /// </summary>
        [StepDefinition("开启会话维持", Description = "启动测试仪在线定时发送(0x3E,每2秒)", StepType = StepType.SendOnly)]
        [InputBinding("RequestId", "请求CAN ID(十六进制,默认7E0)")]
        [InputBinding("ResponseId", "响应CAN ID(十六进制,默认7E8,仅记录)")]
        [InputBinding("FrameType", "帧类型(Standard=标准帧,Extended=扩展帧,默认Standard)")]
        [InputBinding("FrameFormat", "帧格式(CAN=经典CAN,CANFD=CANFD,默认CAN)")]
        [OutputBinding("Result", "执行结果")]
        public async Task<TestStepResult> 开启会话维持Step()
        {
            uint reqId = ParseHexUint(GetInputValue("RequestId", DEFAULT_REQUEST_ID.ToString("X")));
            uint respId = ParseHexUint(GetInputValue("ResponseId", DEFAULT_RESPONSE_ID.ToString("X")));
            bool isExtended = ParseFrameType(GetInputValue("FrameType", "Standard"));
            bool useCanFd = ParseFrameFormat(GetInputValue("FrameFormat", "CAN"));

            if (!IsDeviceConnected())
                return FailResult("开启会话维持", "设备未连接");

            try
            {
                // 已在运行则先停止再重启
                if (_testerPresentTask != null && !_testerPresentTask.IsCompleted)
                {
                    await StopTesterPresentAsync();
                }

                _testerPresentCts = new CancellationTokenSource();
                var token = _testerPresentCts.Token;
                _testerPresentTask = Task.Run(() => TesterPresentLoopAsync(reqId, respId, token, isExtended, useCanFd));

                SetOutputValue("Result", "成功");
                return OkResult("开启会话维持", $"已启动,每{TESTER_PRESENT_INTERVAL}ms发送一次0x3E(ID=0x{reqId:X})");
            }
            catch (Exception ex)
            {
                return FailResult("开启会话维持", ex.Message);
            }
        }

        /// <summary>
        /// 关闭会话维持
        /// 停止后台测试仪在线定时发送
        /// </summary>
        [StepDefinition("关闭会话维持", Description = "停止测试仪在线定时发送", StepType = StepType.SendOnly)]
        [OutputBinding("Result", "执行结果")]
        public async Task<TestStepResult> 关闭会话维持Step()
        {
            try
            {
                bool wasRunning = _testerPresentTask != null && !_testerPresentTask.IsCompleted;
                await StopTesterPresentAsync();

                SetOutputValue("Result", "成功");
                return OkResult("关闭会话维持", wasRunning ? "已停止" : "原未运行");
            }
            catch (Exception ex)
            {
                return FailResult("关闭会话维持", ex.Message);
            }
        }

        /// <summary>
        /// ECU复位
        /// UDS服务0x11，复位ECU
        /// </summary>
        [StepDefinition("ECU复位", Description = "复位ECU(0x11)", StepType = StepType.SendAndReceive)]
        [InputBinding("ResetType", "复位类型(01=硬复位,02=关键复位,03=软复位)")]
        [InputBinding("RequestId", "请求CAN ID(默认7E0)")]
        [InputBinding("ResponseId", "响应CAN ID(默认7E8)")]
        [InputBinding("Timeout", "超时(ms,默认2000)")]
        [InputBinding("FrameType", "帧类型(Standard=标准帧,Extended=扩展帧,默认Standard)")]
        [InputBinding("FrameFormat", "帧格式(CAN=经典CAN,CANFD=CANFD,默认CAN)")]
        [OutputBinding("Response", "响应数据")]
        [OutputBinding("Nrc", "否定响应码(否定时)")]
        [OutputBinding("Result", "执行结果")]
        public async Task<TestStepResult> ECU复位Step()
        {
            byte resetType = ParseHexByte(GetInputValue("ResetType", "01"));
            uint reqId = ParseHexUint(GetInputValue("RequestId", DEFAULT_REQUEST_ID.ToString("X")));
            uint respId = ParseHexUint(GetInputValue("ResponseId", DEFAULT_RESPONSE_ID.ToString("X")));
            int timeout = int.Parse(GetInputValue("Timeout", DEFAULT_TIMEOUT.ToString()));
            bool isExtended = ParseFrameType(GetInputValue("FrameType", "Standard"));
            bool useCanFd = ParseFrameFormat(GetInputValue("FrameFormat", "CAN"));

            if (!IsDeviceConnected())
                return FailResult("ECU复位", "设备未连接");

            try
            {
                byte[] response = await UdsRequestAsync(reqId, respId, new byte[] { 0x11, resetType }, timeout, isExtended, useCanFd);
                var result = BuildServiceResult("ECU复位", response, 0x11);
                SetOutputValue("Response", ToHexSpaced(response));
                return result;
            }
            catch (TimeoutException)
            {
                return FailResult("ECU复位", $"在{timeout}ms内未收到响应");
            }
            catch (Exception ex)
            {
                return FailResult("ECU复位", ex.Message);
            }
        }

        #endregion

        #region 会话维持后台任务

        /// <summary>
        /// 测试仪在线后台循环：每2秒发送0x3E(抑制肯定响应)以保持会话
        /// </summary>
        private async Task TesterPresentLoopAsync(uint reqId, uint respId, CancellationToken token, bool isExtended = false, bool useCanFd = false)
        {
            byte[] request = new byte[] { 0x3E, 0x80 }; // 抑制肯定响应
            try
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        if (IsDeviceConnected())
                            await SendIsoTpAsync(reqId, respId, request, 1000, waitForFlowControl: false, isExtended, useCanFd);
                    }
                    catch
                    {
                        // 忽略单次发送失败，保持循环
                    }

                    await Task.Delay(TESTER_PRESENT_INTERVAL, token);
                }
            }
            catch (TaskCanceledException)
            {
                // 正常退出
            }
        }

        /// <summary>
        /// 停止会话维持后台任务
        /// </summary>
        private async Task StopTesterPresentAsync()
        {
            if (_testerPresentCts != null)
            {
                try { _testerPresentCts.Cancel(); } catch { }
            }
            if (_testerPresentTask != null)
            {
                try { await _testerPresentTask; } catch { }
            }
            _testerPresentCts?.Dispose();
            _testerPresentCts = null;
            _testerPresentTask = null;
        }

        #endregion

        #region ISO-TP传输层(ISO 15765-2)

        /// <summary>
        /// UDS请求：发送请求报文并接收响应(均经ISO-TP分段)
        /// </summary>
        private async Task<byte[]> UdsRequestAsync(uint reqId, uint respId, byte[] request, int timeout, bool isExtended = false, bool useCanFd = false)
        {
            await SendIsoTpAsync(reqId, respId, request, timeout, waitForFlowControl: true, isExtended, useCanFd);
            return await ReceiveIsoTpAsync(respId, timeout, isExtended, useCanFd);
        }

        /// <summary>
        /// ISO-TP发送：单帧或首帧+连续帧(需ECU回送流控制)
        /// </summary>
        private async Task SendIsoTpAsync(uint reqId, uint respId, byte[] data, int timeout, bool waitForFlowControl, bool isExtended = false, bool useCanFd = false)
        {
            if (data.Length <= 7)
            {
                // 单帧 SF: PCI=0x0N, N为数据长度
                byte[] frame = new byte[8];
                frame[0] = (byte)data.Length;
                Buffer.BlockCopy(data, 0, frame, 1, data.Length);
                await SendCanFrameAsync(reqId, frame, isExtended, useCanFd);
                return;
            }

            // 首帧 FF: PCI=0x1N NN, 后跟前6字节数据
            int total = data.Length;
            byte[] ff = new byte[8];
            ff[0] = (byte)(0x10 | ((total >> 8) & 0x0F));
            ff[1] = (byte)(total & 0xFF);
            Buffer.BlockCopy(data, 0, ff, 2, 6);
            await SendCanFrameAsync(reqId, ff, isExtended, useCanFd);

            if (!waitForFlowControl)
                return;

            // 等待流控制 FC: PCI=0x30 FS BS STmin
            CanFrame? fcFrame = await ReceiveCanFrameAsync(respId, timeout);
            if (fcFrame == null || fcFrame.Value.Data.Length < 3 || (fcFrame.Value.Data[0] & 0xF0) != 0x30)
                throw new InvalidOperationException("未收到流控制帧(FC)");
            int stMin = fcFrame.Value.Data.Length > 3 ? fcFrame.Value.Data[3] : 0;

            // 连续帧 CF: PCI=0x2N, 后跟7字节数据
            int sn = 1;
            int offset = 6;
            while (offset < total)
            {
                byte[] cf = new byte[8];
                cf[0] = (byte)(0x20 | (sn & 0x0F));
                int chunk = Math.Min(7, total - offset);
                Buffer.BlockCopy(data, offset, cf, 1, chunk);
                await SendCanFrameAsync(reqId, cf, isExtended, useCanFd);

                sn = (sn + 1) & 0x0F;
                offset += 7;

                if (stMin > 0)
                    await Task.Delay(stMin);
            }
        }

        /// <summary>
        /// ISO-TP接收：单帧或首帧+连续帧(本端回送流控制)
        /// </summary>
        private async Task<byte[]> ReceiveIsoTpAsync(uint respId, int timeout, bool isExtended = false, bool useCanFd = false)
        {
            CanFrame? firstFrame = await ReceiveCanFrameAsync(respId, timeout);
            if (firstFrame == null)
                return Array.Empty<byte>();

            byte[] ff = firstFrame.Value.Data;
            byte pci = (byte)(ff[0] & 0xF0);

            if (pci == 0x00)
            {
                // 单帧
                int len = ff[0] & 0x0F;
                if (len == 0) return Array.Empty<byte>();
                byte[] result = new byte[len];
                Buffer.BlockCopy(ff, 1, result, 0, Math.Min(len, ff.Length - 1));
                return result;
            }

            if (pci != 0x10)
                throw new InvalidOperationException($"非预期的PCI类型: 0x{pci:X2}");

            // 首帧
            int total = ((ff[0] & 0x0F) << 8) | ff[1];
            List<byte> payload = new List<byte>(total);
            payload.AddRange(ff.Skip(2).Take(6));

            // 回送流控制 FC: 继续发送, BS=0, STmin=0
            byte[] fc = new byte[8];
            fc[0] = 0x30;
            fc[1] = 0x00;
            fc[2] = 0x00;
            await SendCanFrameAsync(respId, fc, isExtended, useCanFd);

            // 接收连续帧
            DateTime start = DateTime.Now;
            while (payload.Count < total && (DateTime.Now - start).TotalMilliseconds < timeout)
            {
                CanFrame? cf;
                try
                {
                    cf = await ReceiveCanFrameAsync(respId, Math.Max(timeout, DEFAULT_TIMEOUT));
                }
                catch (TimeoutException)
                {
                    break;
                }

                if (cf == null) break;

                byte[] cfData = cf.Value.Data;
                if (cfData.Length == 0 || (cfData[0] & 0xF0) != 0x20)
                    continue;

                int need = total - payload.Count;
                int take = Math.Min(7, need);
                payload.AddRange(cfData.Skip(1).Take(take));
            }

            if (payload.Count < total)
                throw new TimeoutException("ISO-TP多帧接收不完整");

            return payload.ToArray();
        }

        #endregion

        #region CAN帧收发(基于DeviceBase字符串收发)

        /// <summary>
        /// 发送CAN帧：使用"ID=0x..,DATA=..[,EXT]"字符串格式(数据补齐8字节)
        /// </summary>
        /// <param name="canId">CAN标识符</param>
        /// <param name="data">数据</param>
        /// <param name="isExtended">true=扩展帧(设置EFF标志位)；false=标准帧(默认)</param>
        /// <param name="useCanFd">true=CANFD发送；false=经典CAN发送(默认)</param>
        private async Task SendCanFrameAsync(uint canId, byte[] data, bool isExtended = false, bool useCanFd = false)
        {
            int len = Math.Min(data.Length, 8);
            byte[] frame = new byte[8];
            Buffer.BlockCopy(data, 0, frame, 0, len);

            // 扩展帧追加 EXT 标记，由 CANCommunication.ParseCanFrameFromString 解析并设置 EFF 位
            string extMark = isExtended ? ",EXT" : "";
            string protocolType = useCanFd ? "CANFD" : "CAN";
            await SendDataAsync($"ID=0x{canId:X},DATA={ToHexSpaced(frame, 8)}{extMark}", protocolType);
        }

        /// <summary>
        /// 接收CAN帧：按ID过滤并解析为帧结构(无匹配则返回null)
        /// 注意：本方法不再调用无参 ReceiveDataAsync()(它会清空接收队列且超时即退)
        /// 而是使用带短超时的重载，在整体 timeout 内多次尝试，避免单次超时即放弃
        /// </summary>
        private async Task<CanFrame?> ReceiveCanFrameAsync(uint? expectedId, int timeout)
        {
            // 掩除 EFF 标志位(0x80000000)后的纯净 ID，用于和解析出的 ID 比较
            uint? expectedPureId = expectedId.HasValue ? (expectedId.Value & 0x1FFFFFFFu) : (uint?)null;

            DateTime start = DateTime.Now;
            while ((DateTime.Now - start).TotalMilliseconds < timeout)
            {
                string response;
                try
                {
                    // 单次接收用 200ms 短超时，便于在整体 timeout 内多次重试
                    int remaining = (int)(timeout - (DateTime.Now - start).TotalMilliseconds);
                    if (remaining <= 0) break;
                    response = await ReceiveDataAsync(Math.Min(200, remaining));
                }
                catch (TimeoutException)
                {
                    // 单次短超时不算失败，继续重试直到整体 timeout
                    continue;
                }

                if (!TryParseCanFrame(response, out uint canId, out byte[] data))
                    continue;

                // 去除 EFF 标志位后再比较
                uint pureId = canId & 0x1FFFFFFFu;
                if (!expectedPureId.HasValue || pureId == expectedPureId.Value)
                    return new CanFrame { CanId = pureId, Data = data };
            }
            return null;
        }

        private struct CanFrame
        {
            public uint CanId;
            public byte[] Data;
        }

        #endregion

        #region 解析与转换辅助

        /// <summary>
        /// 解析CAN接收字符串，提取ID与数据
        /// 格式: "[模型] 接收成功: ID=0x123, Data=AA BB CC, DLC=8, Timestamp=...us"
        /// </summary>
        private static bool TryParseCanFrame(string response, out uint canId, out byte[] data)
        {
            canId = 0;
            data = null;
            if (string.IsNullOrWhiteSpace(response))
                return false;

            int idIdx = response.IndexOf("ID=0x", StringComparison.OrdinalIgnoreCase);
            if (idIdx < 0) return false;
            int idStart = idIdx + 5;
            int idEnd = response.IndexOf(',', idStart);
            if (idEnd < 0) idEnd = response.Length;
            string idStr = response.Substring(idStart, idEnd - idStart).Trim();
            if (!uint.TryParse(idStr, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out canId))
                return false;

            int dataIdx = response.IndexOf("Data=", StringComparison.OrdinalIgnoreCase);
            if (dataIdx < 0) return false;
            int dataStart = dataIdx + 5;
            int dataEnd = response.IndexOf(',', dataStart);
            if (dataEnd < 0) dataEnd = response.Length;
            string dataStr = response.Substring(dataStart, dataEnd - dataStart).Trim();

            var bytes = new List<byte>();
            foreach (var part in dataStr.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (byte.TryParse(part, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
                    bytes.Add(b);
            }
            data = bytes.ToArray();
            return true;
        }

        private static string ToHexSpaced(byte[] data)
        {
            return data == null ? string.Empty : ToHexSpaced(data, data.Length);
        }

        private static string ToHexSpaced(byte[] data, int length)
        {
            if (data == null || length <= 0) return string.Empty;
            var sb = new System.Text.StringBuilder(length * 3);
            for (int i = 0; i < length; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(data[i].ToString("X2"));
            }
            return sb.ToString();
        }

        private static byte[] ParseHexSpaced(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return Array.Empty<byte>();
            var bytes = new List<byte>();
            foreach (var part in hex.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (byte.TryParse(part, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
                    bytes.Add(b);
            }
            return bytes.ToArray();
        }

        private static uint ParseHexUint(string s)
        {
            s = s.Trim();
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase) || s.StartsWith("0X"))
                s = s.Substring(2);
            return uint.Parse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        private static byte ParseHexByte(string s)
        {
            s = s.Trim();
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase) || s.StartsWith("0X"))
                s = s.Substring(2);
            return byte.Parse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 解析帧类型: Standard/标准=标准帧(false), Extended/扩展=扩展帧(true)
        /// </summary>
        private static bool ParseFrameType(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            s = s.Trim();
            if (s.Equals("Extended", StringComparison.OrdinalIgnoreCase) ||
                s.Equals("扩展", StringComparison.OrdinalIgnoreCase) ||
                s.Equals("扩展帧", StringComparison.OrdinalIgnoreCase))
                return true;
            return false;
        }

        /// <summary>
        /// 解析帧格式: CAN=经典CAN(false), CANFD=CANFD(true)
        /// </summary>
        private static bool ParseFrameFormat(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            s = s.Trim();
            if (s.Equals("CANFD", StringComparison.OrdinalIgnoreCase) ||
                s.Equals("CAN FD", StringComparison.OrdinalIgnoreCase) ||
                s.Equals("CAN-FD", StringComparison.OrdinalIgnoreCase))
                return true;
            return false;
        }

        #endregion

        #region UDS响应辅助

        /// <summary>
        /// 是否肯定响应: 响应SID = 请求SID | 0x40
        /// </summary>
        private static bool IsPositiveResponse(byte[] response, byte requestSid)
        {
            return response != null && response.Length >= 1 && response[0] == (requestSid | 0x40);
        }

        /// <summary>
        /// 获取否定响应码(NRC), 否定响应格式: 0x7F [SID] [NRC]
        /// </summary>
        private static byte GetNegativeResponseCode(byte[] response)
        {
            if (response != null && response.Length >= 3 && response[0] == 0x7F)
                return response[2];
            return 0xFF;
        }

        /// <summary>
        /// 构建服务步骤结果(处理正/负响应)
        /// </summary>
        private TestStepResult BuildServiceResult(string stepName, byte[] response, byte requestSid)
        {
            if (response == null || response.Length == 0)
                return FailResult(stepName, "未收到响应");

            if (IsPositiveResponse(response, requestSid))
            {
                SetOutputValue("Result", "成功");
                return OkResult(stepName, ToHexSpaced(response));
            }

            byte nrc = GetNegativeResponseCode(response);
            SetOutputValue("Nrc", $"{nrc:X2}");
            SetOutputValue("Result", "失败");
            return new TestStepResult
            {
                StepName = stepName,
                IsSuccess = false,
                ErrorMessage = $"否定响应 NRC=0x{nrc:X2}",
                ActualValue = ToHexSpaced(response)
            };
        }

        #endregion

        #region 结果辅助

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
