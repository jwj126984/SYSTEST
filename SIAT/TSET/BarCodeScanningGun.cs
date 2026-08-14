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

                int lenTemp = serialPort.BytesToRead;//获取可以读取的字节数
                if (lenTemp > 0)
                {
                    byte[] buff = new byte[lenTemp];//创建缓存数据数组
                    serialPort.Read(buff, 0, lenTemp);//把数据读取到buff数组
                }

                Task.Run(async () =>
                {
                    try
                    {
                        while (true)
                        {
                            await Task.Delay(100);
                            int len = serialPort.BytesToRead;//获取可以读取的字节数
                            if (len > 0)
                            {
                                byte[] buff = new byte[len];//创建缓存数据数组
                                serialPort.Read(buff, 0, len);//把数据读取到buff数组
                                Received?.Invoke(buff.ToList());
                            }

                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.ShowMsg(ex.ToString());
                    }

                });
            }
            catch (Exception err)
            {
                LogHelper.ShowMsg($"打开扫码枪失败" + err.ToString());
                throw new Exception(err.Message);
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