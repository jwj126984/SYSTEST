using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;
using System.Threading.Tasks;

namespace SIAT.TSET
{
    public class BarCodeScanningGun
    {
        private SerialPort serialPort;
        
        public event Action<List<byte>> Received;
        
        public BarCodeScanningGun(string portName, int baud)
        {
            serialPort = new SerialPort();
            
            try
            {
                serialPort.PortName = portName;
                serialPort.BaudRate = baud;//波特率
                serialPort.Encoding = Encoding.Default;
                serialPort.Open();

                // 清空接收缓冲区中的残留数据
                int lenTemp = serialPort.BytesToRead;
                if (lenTemp > 0)
                {
                    byte[] buff = new byte[lenTemp];
                    serialPort.Read(buff, 0, lenTemp);
                }
            }
            catch (Exception err)
            {
                LogHelper.ShowMsg($"打开扫码枪失败" + err.ToString());
                throw new Exception(err.Message);
            }
        }
        
        /// <summary>
        /// 触发扫码枪读取，发送 04 E4 04 00 FF 14 指令，并读取返回数据
        /// </summary>
        public void TriggerRead()
        {
            try
            {
                if (serialPort == null || !serialPort.IsOpen)
                {
                    LogHelper.ShowMsg("触发扫码枪读取失败：串口未打开");
                    return;
                }

                // 发送触发指令
                byte[] triggerCmd = new byte[] { 0x04, 0xE4, 0x04, 0x00, 0xFF, 0x14 };
                serialPort.Write(triggerCmd, 0, triggerCmd.Length);

                // 启动读取任务，等待扫码枪返回数据
                Task.Run(async () =>
                {
                    try
                    {
                        List<byte> result = new List<byte>();
                        int retry = 0;
                        const int maxRetry = 50; // 最长等待约 5 秒
                        const int interval = 100;

                        while (retry < maxRetry)
                        {
                            await Task.Delay(interval);
                            int len = serialPort.BytesToRead;
                            if (len > 0)
                            {
                                byte[] buff = new byte[len];
                                serialPort.Read(buff, 0, len);
                                result.AddRange(buff);
                                // 连续两次无新增数据视为一帧接收完成
                                int noChange = 0;
                                while (noChange < 2)
                                {
                                    await Task.Delay(interval);
                                    int len2 = serialPort.BytesToRead;
                                    if (len2 > 0)
                                    {
                                        byte[] buff2 = new byte[len2];
                                        serialPort.Read(buff2, 0, len2);
                                        result.AddRange(buff2);
                                        noChange = 0;
                                    }
                                    else
                                    {
                                        noChange++;
                                    }
                                }
                                break;
                            }
                            retry++;
                        }

                        if (result.Count > 0)
                        {
                            Received?.Invoke(result);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.ShowMsg("读取扫码枪返回数据失败: " + ex.ToString());
                    }
                });
            }
            catch (Exception ex)
            {
                LogHelper.ShowMsg($"触发扫码枪读取失败: {ex.Message}");
            }
        }

        public void Close()
        {
            try
            {
                if (serialPort != null && serialPort.IsOpen)
                {
                    serialPort.Close();
                }
            }
            catch (Exception ex)
            {
                LogHelper.ShowMsg($"关闭扫码枪失败: {ex.Message}");
            }
        }
    }
    
    public class LogHelper
    {
        public static void ShowMsg(string message)
        {
            // 简单的日志显示实现
            Console.WriteLine(message);
        }
    }
}