using System;
using SIAT.ResourceManagement;

namespace SIAT.CommunicationManagement
{
    public static class CommunicationManager
    {
        public static ICommunication CreateCommunication(CommunicationType communicationType, CommunicationParams parameters, DeviceType deviceType = DeviceType.Generic)
        {
            switch (communicationType)
            {
                case CommunicationType.Serial:
                    return new SerialCommunication(parameters);
                case CommunicationType.Network:
                    return new NetworkCommunication(parameters);
                case CommunicationType.CAN:
                    // 根据设备类型选择不同的CAN通信实现
                    if (deviceType == DeviceType.CXKJ_CANALYST_II)
                    {
                        return new CXKJCanCommunication(parameters);
                    }
                    else
                    {
                        return new CANCommunication(parameters, deviceType);
                    }
                case CommunicationType.USB:
                    return new USBCommunication(parameters);
                default:
                    throw new ArgumentException($"不支持的通讯类型: {communicationType}");
            }
        }
        
        public static async Task<ICommunication> CreateAndConnectAsync(CommunicationType communicationType, CommunicationParams parameters, DeviceType deviceType = DeviceType.Generic)
        {
            var communication = CreateCommunication(communicationType, parameters, deviceType);
            await communication.ConnectAsync(parameters);
            return communication;
        }
    }
}