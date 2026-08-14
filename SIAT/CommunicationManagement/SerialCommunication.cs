using System;
using System.IO.Ports;
using System.Text;
using System.Threading.Tasks;
using SIAT.ResourceManagement;

namespace SIAT.CommunicationManagement
{
    public class SerialCommunication : CommunicationBase
    {
        private SerialPort _serialPort;
        
        public SerialCommunication(CommunicationParams parameters) : base(parameters)
        {
            _serialPort = new SerialPort();
        }
        
        public override async Task<bool> ConnectAsync(CommunicationParams parameters)
        {
            try
            {
                if (_serialPort.IsOpen)
                {
                    await DisconnectAsync();
                }
                
                _serialPort.PortName = parameters.SerialPort;
                _serialPort.BaudRate = parameters.BaudRate;
                _serialPort.DataBits = parameters.DataBits;
                _serialPort.Parity = Enum.Parse<Parity>(parameters.Parity);
                _serialPort.StopBits = (StopBits)parameters.StopBits;
                _serialPort.ReadTimeout = 5000;
                _serialPort.WriteTimeout = 5000;
                
                await Task.Run(() => _serialPort.Open());
                UpdateConnectionStatus(true, "已连接");
                return true;
            }
            catch (Exception ex)
            {
                UpdateConnectionStatus(false, $"连接失败: {ex.Message}");
                return false;
            }
        }
        
        public override async Task<bool> DisconnectAsync()
        {
            try
            {
                if (_serialPort.IsOpen)
                {
                    await Task.Run(() => _serialPort.Close());
                }
                UpdateConnectionStatus(false, "已断开");
                return true;
            }
            catch (Exception ex)
            {
                UpdateConnectionStatus(false, $"断开失败: {ex.Message}");
                return false;
            }
        }
        
        public override async Task<string> SendAsync(string data)
        {
            // 调用带协议类型的重载方法，默认使用空协议类型
            return await SendAsync(data, string.Empty);
        }
        
        public override async Task<string> SendAsync(string data, string protocolType)
        {
            try
            {
                if (!_serialPort.IsOpen)
                {
                    throw new InvalidOperationException("串口未连接");
                }
                
                await Task.Run(() => _serialPort.Write(data));
                return "发送成功";
            }
            catch (Exception ex)
            {
                return $"发送失败: {ex.Message}";
            }
        }
        
        public override async Task<string> SendAsync(byte[] data)
        {
            // 调用带协议类型的重载方法，默认使用空协议类型
            return await SendAsync(data, string.Empty);
        }
        
        public override async Task<string> SendAsync(byte[] data, string protocolType)
        {
            try
            {
                if (!_serialPort.IsOpen)
                {
                    throw new InvalidOperationException("串口未连接");
                }
                
                await Task.Run(() => _serialPort.Write(data, 0, data.Length));
                return "发送成功";
            }
            catch (Exception ex)
            {
                return $"发送失败: {ex.Message}";
            }
        }
        
        public override async Task<string> ReceiveAsync()
        {
            return await ReceiveAsync(5000, string.Empty);
        }
        
        public override async Task<string> ReceiveAsync(int timeout)
        {
            return await ReceiveAsync(timeout, string.Empty);
        }
        
        public override async Task<string> ReceiveAsync(int timeout, string protocolType)
        {
            try
            {
                if (!_serialPort.IsOpen)
                {
                    throw new InvalidOperationException("串口未连接");
                }
                
                _serialPort.ReadTimeout = timeout;
                
                // 第一次接收时，增加一个小延迟，确保设备有足够时间响应
                await Task.Delay(50);
                
                // 检查是否有数据可读
                bool hasData = await Task.Run(() => _serialPort.BytesToRead > 0);
                
                if (hasData)
                {
                    // 循环读取，确保所有数据都被读取
                    StringBuilder sb = new StringBuilder();
                    int totalBytesRead = 0;
                    int maxAttempts = 10; // 最多尝试10次
                    int attempts = 0;
                    
                    while (attempts < maxAttempts)
                    {
                        int bytesAvailable = await Task.Run(() => _serialPort.BytesToRead);
                        if (bytesAvailable > 0)
                        {
                            var data = await Task.Run(() => _serialPort.ReadExisting());
                            sb.Append(data);
                            totalBytesRead += data.Length;
                            attempts = 0; // 重置尝试次数
                            // 短暂延迟，确保更多数据到达
                            await Task.Delay(10);
                        }
                        else
                        {
                            attempts++;
                            // 没有更多数据，等待一小段时间后再次检查
                            await Task.Delay(20);
                        }
                    }
                    
                    return sb.ToString();
                }
                else
                {
                    // 等待直到有数据可读或超时
                    var buffer = new byte[1];
                    int bytesRead = await Task.Run(() => {
                        try
                        {
                            return _serialPort.Read(buffer, 0, 1);
                        }
                        catch (TimeoutException)
                        {
                            throw;
                        }
                    });
                    
                    if (bytesRead > 0)
                    {
                        // 读取剩余数据，使用循环确保所有数据都被读取
                        StringBuilder sb = new StringBuilder(Encoding.ASCII.GetString(buffer));
                        int maxAttempts = 10;
                        int attempts = 0;
                        
                        while (attempts < maxAttempts)
                        {
                            int bytesAvailable = await Task.Run(() => _serialPort.BytesToRead);
                            if (bytesAvailable > 0)
                            {
                                var remainingData = await Task.Run(() => _serialPort.ReadExisting());
                                sb.Append(remainingData);
                                attempts = 0;
                                await Task.Delay(10);
                            }
                            else
                            {
                                attempts++;
                                await Task.Delay(20);
                            }
                        }
                        
                        return sb.ToString();
                    }
                    
                    return string.Empty;
                }
            }
            catch (TimeoutException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new Exception($"接收失败: {ex.Message}", ex);
            }
        }
        
        public override async Task<string> ReceiveAsync(int timeout, uint? canId, string protocolType)
        {
            return await ReceiveAsync(timeout, protocolType);
        }
    }
}