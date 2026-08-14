using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using SIAT.ResourceManagement;

namespace SIAT.CommunicationManagement
{
    public class NetworkCommunication : CommunicationBase
    {
        private TcpClient _tcpClient;
        private NetworkStream? _networkStream;
        
        public NetworkCommunication(CommunicationParams parameters) : base(parameters)
        {
            _tcpClient = new TcpClient();
        }
        
        public override async Task<bool> ConnectAsync(CommunicationParams parameters)
        {
            try
            {
                // 无论当前状态如何，先关闭并重新创建TcpClient，避免使用不可靠的Connected属性
                await DisconnectAsync();
                
                // 解析IP地址，确保格式正确
                IPAddress ipAddress = IPAddress.Parse(parameters.IPAddress);
                await _tcpClient.ConnectAsync(ipAddress, parameters.Port);
                _networkStream = _tcpClient.GetStream();
                _networkStream.ReadTimeout = 5000;
                _networkStream.WriteTimeout = 5000;
                
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
                // 不依赖Connected属性，直接尝试关闭资源
                try
                {
                    _networkStream?.Close();
                }
                catch (Exception)
                {
                    // 忽略关闭流时的异常
                }
                
                try
                {
                    _tcpClient.Close();
                }
                catch (Exception)
                {
                    // 忽略关闭客户端时的异常
                }
                
                // 无论如何都重新创建TcpClient，确保处于干净状态
                _tcpClient = new TcpClient();
                _networkStream = null;
                
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
                // 不依赖Connected属性，直接尝试发送数据，由异常处理实际连接状态
                byte[] buffer = Encoding.ASCII.GetBytes(data);
                await _networkStream.WriteAsync(buffer, 0, buffer.Length);
                return "发送成功";
            }
            catch (Exception ex)
            {
                // 发送失败时自动断开连接
                await DisconnectAsync();
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
                // 不依赖Connected属性，直接尝试发送数据，由异常处理实际连接状态
                await _networkStream.WriteAsync(data, 0, data.Length);
                return "发送成功";
            }
            catch (Exception ex)
            {
                // 发送失败时自动断开连接
                await DisconnectAsync();
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
                // 不依赖Connected属性，直接尝试接收数据
                _networkStream.ReadTimeout = timeout;
                byte[] buffer = new byte[1024];
                int bytesRead = await _networkStream.ReadAsync(buffer, 0, buffer.Length);
                
                if (bytesRead == 0)
                {
                    await DisconnectAsync();
                    throw new InvalidOperationException("连接已断开");
                }
                
                return Encoding.ASCII.GetString(buffer, 0, bytesRead);
            }
            catch (TimeoutException)
            {
                // 超时异常不自动断开连接，由调用方处理
                throw;
            }
            catch (Exception ex)
            {
                // 其他异常自动断开连接
                await DisconnectAsync();
                throw new Exception($"接收失败: {ex.Message}", ex);
            }
        }
        
        public override async Task<string> ReceiveAsync(int timeout, uint? canId, string protocolType)
        {
            return await ReceiveAsync(timeout, protocolType);
        }
    }
}