using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;

namespace SIAT.ResourceManagement{
    public enum CommunicationType
    {
        Serial,
        Network,
        CAN,
        USB
    }

    public enum DeviceType
    {
        Generic,
        ZLG_USBCAN_I,
        ZLG_USBCAN_II,
        ZLG_USBCAN_II_Pro,
        ZLG_USBCAN_E_U,
        ZLG_USBCAN_2E_U,
        ZLG_USBCAN_200U,
        ZLG_USBCANFD_200U,
        ZLG_CANET_UDP,
        ZLG_CANET_TCP,
        ZLG_CANFDU,
        ZLG_CANFDU_Pro,
        ZLG_CANBridge,
        ZLG_CANHub,
        CXKJ_CANALYST_II
    }

    public enum ProtocolType
    {
        HEX,
        ASCII,
        CAN
    }
    
    public enum StepType
    {
        SendAndReceive,
        SendOnly,
        ReadOnly
    }

    public enum EndianType
    {
        BigEndian,
        LittleEndian
    }

    public enum CanChannelType
    {
        CAN0,
        CAN1,
        CAN2,
        CAN3
    }

    public enum CanWorkMode
    {
        Normal,
        ListenOnly,
        SelfTest
    }

    public enum CanFilterMode
    {
        DualFilter,
        SingleFilter
    }

    public class CommunicationParams : INotifyPropertyChanged
    {
        private string _serialPort = "COM1";
        public string SerialPort
        {
            get => _serialPort;
            set { _serialPort = value; OnPropertyChanged(); }
        }

        private int _baudRate = 9600;
        public int BaudRate
        {
            get => _baudRate;
            set { _baudRate = value; OnPropertyChanged(); }
        }

        private int _dataBits = 8;
        public int DataBits
        {
            get => _dataBits;
            set { _dataBits = value; OnPropertyChanged(); }
        }

        private string _parity = "None";
        public string Parity
        {
            get => _parity;
            set { _parity = value; OnPropertyChanged(); }
        }

        private int _stopBits = 1;
        public int StopBits
        {
            get => _stopBits;
            set { _stopBits = value; OnPropertyChanged(); }
        }

        private string _ipAddress = "192.168.1.1";
        public string IPAddress
        {
            get => _ipAddress;
            set { _ipAddress = value; OnPropertyChanged(); }
        }

        private int _port = 8080;
        public int Port
        {
            get => _port;
            set { _port = value; OnPropertyChanged(); }
        }

        // CAN通讯参数 - 按照周立功规范设计
        private CanChannelType _canChannel = CanChannelType.CAN0;
        public CanChannelType CanChannel
        {
            get => _canChannel;
            set { _canChannel = value; OnPropertyChanged(); }
        }

        private int _deviceIndex = 0;
        public int DeviceIndex
        {
            get => _deviceIndex;
            set { _deviceIndex = value; OnPropertyChanged(); }
        }

        private int _canBaudRate = 500000;
        public int CanBaudRate
        {
            get => _canBaudRate;
            set { _canBaudRate = value; OnPropertyChanged(); }
        }

        private CanWorkMode _canWorkMode = CanWorkMode.Normal;
        public CanWorkMode CanWorkMode
        {
            get => _canWorkMode;
            set { _canWorkMode = value; OnPropertyChanged(); }
        }

        private bool _isCanFd = false;
        public bool IsCanFd
        {
            get => _isCanFd;
            set { _isCanFd = value; OnPropertyChanged(); }
        }

        private int _canFdDataBaudRate = 2000000;
        public int CanFdDataBaudRate
        {
            get => _canFdDataBaudRate;
            set { _canFdDataBaudRate = value; OnPropertyChanged(); }
        }

        private int _arbitrationSamplePoint = 80;
        public int ArbitrationSamplePoint
        {
            get => _arbitrationSamplePoint;
            set { _arbitrationSamplePoint = value; OnPropertyChanged(); }
        }

        private int _dataSamplePoint = 80;
        public int DataSamplePoint
        {
            get => _dataSamplePoint;
            set { _dataSamplePoint = value; OnPropertyChanged(); }
        }

        private CanFilterMode _canFilterMode = CanFilterMode.DualFilter;
        public CanFilterMode CanFilterMode
        {
            get => _canFilterMode;
            set { _canFilterMode = value; OnPropertyChanged(); }
        }

        private uint _acceptanceCode = 0x00000000;
        public uint AcceptanceCode
        {
            get => _acceptanceCode;
            set { _acceptanceCode = value; OnPropertyChanged(); }
        }

        private uint _acceptanceMask = 0xFFFFFFFF;
        public uint AcceptanceMask
        {
            get => _acceptanceMask;
            set { _acceptanceMask = value; OnPropertyChanged(); }
        }

        private bool _isTerminalResistorEnabled = false;
        public bool IsTerminalResistorEnabled
        {
            get => _isTerminalResistorEnabled;
            set { _isTerminalResistorEnabled = value; OnPropertyChanged(); }
        }

        private bool _enableErrorFrame = false;
        public bool EnableErrorFrame
        {
            get => _enableErrorFrame;
            set { _enableErrorFrame = value; OnPropertyChanged(); }
        }

        private uint _filterCanId = 0;
        public uint FilterCanId
        {
            get => _filterCanId;
            set { _filterCanId = value; OnPropertyChanged(); }
        }

        private string _usbDeviceId = "VID_1234&PID_5678";
        public string UsbDeviceId
        {
            get => _usbDeviceId;
            set { _usbDeviceId = value; OnPropertyChanged(); }
        }

        public CommunicationParams()
        {
            // 设置默认值
            _serialPort = "COM1";
            _baudRate = 9600;
            _dataBits = 8;
            _parity = "None";
            _stopBits = 1;
            _ipAddress = "192.168.1.1";
            _port = 8080;
            
            // CAN通讯参数默认值 - 按照周立功规范
            _canChannel = CanChannelType.CAN0;
            _deviceIndex = 0;
            _canBaudRate = 500000;
            _canWorkMode = CanWorkMode.Normal;
            _isCanFd = false;
            _canFdDataBaudRate = 2000000;
            _arbitrationSamplePoint = 80;
            _dataSamplePoint = 80;
            _canFilterMode = CanFilterMode.DualFilter;
            _acceptanceCode = 0x00000000;
            _acceptanceMask = 0xFFFFFFFF;
            _isTerminalResistorEnabled = false;
            _enableErrorFrame = false;
            _usbDeviceId = "VID_1234&PID_5678";
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class Protocol : INotifyPropertyChanged
    {
        private ProtocolType _type = ProtocolType.HEX;
        public ProtocolType Type
        {
            get => _type;
            set { _type = value; OnPropertyChanged(); }
        }

        private bool _isCanFd = false;
        public bool IsCanFd
        {
            get => _isCanFd;
            set { _isCanFd = value; OnPropertyChanged(); }
        }

        private string _id = "0x123";
        public string Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        private string _content = "";
        public string Content
        {
            get => _content;
            set { _content = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class ResultVariable : INotifyPropertyChanged
    {
        private string _name = "";
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        private string _unit = "";
        public string Unit
        {
            get => _unit;
            set { _unit = value; OnPropertyChanged(); }
        }

        private int _startBit = 0;
        public int StartBit
        {
            get => _startBit;
            set { _startBit = value; OnPropertyChanged(); }
        }

        private int _endBit = 7;
        public int EndBit
        {
            get => _endBit;
            set { _endBit = value; OnPropertyChanged(); }
        }

        private int _length = 8;
        public int Length
        {
            get => _length;
            set { _length = value; OnPropertyChanged(); }
        }

        private double _resolution = 1.0;
        public double Resolution
        {
            get => _resolution;
            set { _resolution = value; OnPropertyChanged(); }
        }

        private double _offset = 0.0;
        public double Offset
        {
            get => _offset;
            set { _offset = value; OnPropertyChanged(); }
        }

        private EndianType _endian = EndianType.LittleEndian;
        public EndianType Endian
        {
            get => _endian;
            set { _endian = value; OnPropertyChanged(); }
        }

        private string _canId = "";
        public string CanId
        {
            get => _canId;
            set { _canId = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class Step : INotifyPropertyChanged
    {
        private string _name = "";
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        private StepType _stepType = StepType.SendAndReceive;
        public StepType StepType
        {
            get => _stepType;
            set { _stepType = value; OnPropertyChanged(); }
        }

        private Protocol _protocol = new Protocol();
        public Protocol Protocol
        {
            get => _protocol;
            set { _protocol = value; OnPropertyChanged(); }
        }

        private List<ResultVariable> _resultVariables = new List<ResultVariable>();
        public List<ResultVariable> ResultVariables
        {
            get => _resultVariables;
            set { _resultVariables = value; OnPropertyChanged(); }
        }

        private bool _isEnabled = true;
        public bool IsEnabled
        {
            get => _isEnabled;
            set { _isEnabled = value; OnPropertyChanged(); }
        }

        private bool _waitForResponse = true;
        public bool WaitForResponse
        {
            get => _waitForResponse;
            set { _waitForResponse = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 输入绑定列表（从代码中识别）
        /// </summary>
        private List<StepBindingInfo> _inputBindings = new List<StepBindingInfo>();
        [XmlArray("InputBindings")]
        [XmlArrayItem("Binding")]
        public List<StepBindingInfo> InputBindings
        {
            get => _inputBindings;
            set { _inputBindings = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 输出绑定列表（从代码中识别）
        /// </summary>
        private List<StepBindingInfo> _outputBindings = new List<StepBindingInfo>();
        [XmlArray("OutputBindings")]
        [XmlArrayItem("Binding")]
        public List<StepBindingInfo> OutputBindings
        {
            get => _outputBindings;
            set { _outputBindings = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// 步骤绑定信息（输入或输出）
    /// </summary>
    [Serializable]
    public class StepBindingInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsInput { get; set; }
    }

    [XmlRoot("Device")]
    public class Device : INotifyPropertyChanged
    {
        private string _name = "";
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        private DeviceType _deviceType = DeviceType.Generic;
        public DeviceType DeviceType
        {
            get => _deviceType;
            set { _deviceType = value; OnPropertyChanged(); }
        }

        private int _deviceIndex = 0;
        public int DeviceIndex
        {
            get => _deviceIndex;
            set { _deviceIndex = value; OnPropertyChanged(); }
        }

        private CommunicationType _communicationType = CommunicationType.Serial;
        public CommunicationType CommunicationType
        {
            get => _communicationType;
            set { _communicationType = value; OnPropertyChanged(); }
        }

        private CommunicationParams _params = new CommunicationParams();
        public CommunicationParams Params
        {
            get => _params;
            set { _params = value; OnPropertyChanged(); }
        }

        private bool _isEnabled = true;
        public bool IsEnabled
        {
            get => _isEnabled;
            set { _isEnabled = value; OnPropertyChanged(); }
        }

        private string _status = "未连接";
        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 代码中设备类的全名，用于关联代码定义的设备
        /// </summary>
        private string _sourceClassName = "";
        [XmlElement("SourceClassName")]
        public string SourceClassName
        {
            get => _sourceClassName;
            set { _sourceClassName = value; OnPropertyChanged(); }
        }

        private List<Step> _steps = new List<Step>();
        [XmlArray("Steps")]
        [XmlArrayItem("Step")]
        public List<Step> Steps
        {
            get => _steps;
            set { _steps = value; OnPropertyChanged(); }
        }

        public Device()
        {
            // 设置默认值
            _name = "";
            _deviceType = DeviceType.Generic;
            _deviceIndex = 0;
            _communicationType = CommunicationType.Serial;
            _params = new CommunicationParams();
            _isEnabled = true;
            _status = "未连接";
            _sourceClassName = "";
            _steps = new List<Step>();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}