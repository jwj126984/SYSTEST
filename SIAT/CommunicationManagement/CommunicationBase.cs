using System;
using System.Threading.Tasks;
using SIAT.ResourceManagement;

namespace SIAT.CommunicationManagement
{
    public interface ICommunication
    {
        bool IsConnected { get; }
        string ConnectionStatus { get; }
        
        Task<bool> ConnectAsync(CommunicationParams parameters);
        Task<bool> DisconnectAsync();
        Task<string> SendAsync(string data);
        Task<string> SendAsync(byte[] data);
        Task<string> SendAsync(string data, string protocolType);
        Task<string> SendAsync(byte[] data, string protocolType);
        Task<string> ReceiveAsync();
        Task<string> ReceiveAsync(int timeout);
        Task<string> ReceiveAsync(int timeout, string protocolType);
        Task<string> ReceiveAsync(int timeout, uint? canId, string protocolType);
    }
    
    public abstract class CommunicationBase : ICommunication
    {
        public bool IsConnected { get; protected set; }
        public string ConnectionStatus { get; protected set; } = "未连接";
        
        protected CommunicationParams _parameters;
        
        public CommunicationBase(CommunicationParams parameters)
        {
            _parameters = parameters;
        }
        
        public abstract Task<bool> ConnectAsync(CommunicationParams parameters);
        public abstract Task<bool> DisconnectAsync();
        public abstract Task<string> SendAsync(string data);
        public abstract Task<string> SendAsync(byte[] data);
        public abstract Task<string> SendAsync(string data, string protocolType);
        public abstract Task<string> SendAsync(byte[] data, string protocolType);
        public abstract Task<string> ReceiveAsync();
        public abstract Task<string> ReceiveAsync(int timeout);
        public abstract Task<string> ReceiveAsync(int timeout, string protocolType);
        public abstract Task<string> ReceiveAsync(int timeout, uint? canId, string protocolType);
        
        protected void UpdateConnectionStatus(bool connected, string status)
        {
            IsConnected = connected;
            ConnectionStatus = status;
        }
    }
}