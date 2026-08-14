using SIAT.ResourceManagement;
using System;
using System.IO.Ports;
using System.Management;
using System.Windows;
using System.Windows.Controls;

namespace SIAT
{
    public partial class AddEditDeviceDialog : Window
    {
        public Device Device { get; private set; }


        public AddEditDeviceDialog(Device? device)
        {
            InitializeComponent();
            InitializeComboBoxes();

            if (device != null)
            {
                // 编辑模式
                Device = device;
                LoadDeviceData();
            }
            else
            {
                // 新增模式
                Device = new Device();
                // 设置默认值
                DeviceTypeComboBox.SelectedIndex = 0; // 默认通用设备
                DeviceIndexComboBox.SelectedIndex = 0; // 默认设备0
                CommunicationTypeComboBox.SelectedIndex = 0; // 默认Serial
                SerialPortTextBox.SelectedIndex = 0; // 默认第一个串口号
                BaudRateTextBox.SelectedIndex = 4; // 默认9600
                DataBitsTextBox.Text = "8"; // 默认数据位
                ParityComboBox.SelectedIndex = 0; // 默认None
                StopBitsTextBox.Text = "1"; // 默认停止位
                IPAddressTextBox.Text = "192.168.1.1"; // 默认IP地址
                PortTextBox.Text = "8080"; // 默认端口
                
                // CAN通讯参数默认值 - 按照周立功规范
                DeviceIndexComboBox.SelectedIndex = 0; // 默认设备0
                CanChannelComboBox.SelectedIndex = 0; // 默认CAN0
                CanWorkModeComboBox.SelectedIndex = 0; // 默认正常模式
                CanBaudRateComboBox.SelectedIndex = 5; // 默认500 kbps
                CanFdModeComboBox.SelectedIndex = 0; // 默认CANFD禁用
                CanFdDataBaudRateComboBox.SelectedIndex = 1; // 默认2 Mbps
                ArbitrationSamplePointTextBox.Text = "80"; // 默认仲裁采样点80%
                DataSamplePointTextBox.Text = "80"; // 默认数据采样点80%
                CanFilterModeComboBox.SelectedIndex = 0; // 默认双滤波
                AcceptanceCodeTextBox.Text = "0x00000000"; // 默认验收码
                AcceptanceMaskTextBox.Text = "0xFFFFFFFF"; // 默认验收屏蔽码
                IsTerminalResistorCheckBox.IsChecked = false; // 默认终端电阻禁用
                EnableErrorFrameCheckBox.IsChecked = false; // 默认错误帧处理禁用
                
                UsbDeviceIdTextBox.SelectedIndex = 0; // 默认第一个USB设备ID
            }
        }

        private void InitializeComboBoxes()
        {
            // 初始化串口号选项 - 自动识别
            SerialPortTextBox.Items.Clear();
            try
            {
                string[] ports = SerialPort.GetPortNames();
                Array.Sort(ports);
                foreach (string port in ports)
                {
                    SerialPortTextBox.Items.Add(port);
                }
                
                // 如果没有检测到串口号，添加一些默认值
                if (SerialPortTextBox.Items.Count == 0)
                {
                    for (int i = 1; i <= 8; i++)
                    {
                        SerialPortTextBox.Items.Add($"COM{i}");
                    }
                }
            }
            catch (Exception)
            {
                // 如果获取失败，添加默认值
                for (int i = 1; i <= 8; i++)
                {
                    SerialPortTextBox.Items.Add($"COM{i}");
                }
            }

            // 初始化波特率选项
            int[] baudRates = [9600, 19200, 38400, 57600, 115200, 230400, 460800, 921600];
            foreach (int baudRate in baudRates)
            {
                BaudRateTextBox.Items.Add(baudRate.ToString());
            }

            // 初始化USB设备ID选项
            // 注意：由于System.Management依赖问题，这里使用简化版本
            // 在实际应用中，可以考虑使用更专业的USB设备检测库
            UsbDeviceIdTextBox.Items.Clear();

            try
            {
                // 使用WMI获取USB设备信息
                ManagementObjectSearcher searcher = new("SELECT * FROM Win32_USBHub");
                foreach (ManagementObject device in searcher.Get().Cast<ManagementObject>())
                {
                    string deviceId = device["DeviceID"]?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(deviceId))
                    {
                        // 提取VID和PID
                        string[] parts = deviceId.Split('\\');
                        if (parts.Length > 1)
                        {
                            string[] ids = parts[1].Split('&');
                            if (ids.Length >= 2)
                            {
                                string vid = ids[0].Trim();
                                string pid = ids[1].Trim();
                                string usbId = $"{vid}&{pid}";
                                if (!UsbDeviceIdTextBox.Items.Contains(usbId))
                                {
                                    UsbDeviceIdTextBox.Items.Add(usbId);
                                }
                            }
                        }
                    }
                }

                // 如果没有检测到USB设备，添加一些默认值
                if (UsbDeviceIdTextBox.Items.Count == 0)
                {
                    UsbDeviceIdTextBox.Items.Add("VID_1234&PID_5678");
                    UsbDeviceIdTextBox.Items.Add("VID_ABCD&PID_EFGH");
                    UsbDeviceIdTextBox.Items.Add("VID_1111&PID_2222");
                }
            }
            catch (Exception)
            {
                // 如果获取失败，添加默认值
                UsbDeviceIdTextBox.Items.Add("VID_1234&PID_5678");
                UsbDeviceIdTextBox.Items.Add("VID_ABCD&PID_EFGH");
                UsbDeviceIdTextBox.Items.Add("VID_1111&PID_2222");
            }
        }

        private void LoadDeviceData()
        {
            DeviceNameTextBox.Text = Device.Name;

            // 设置设备类型
            var deviceTypeItem = DeviceTypeComboBox.Items.Cast<ComboBoxItem>()
                .FirstOrDefault(item => item.Tag.ToString() == Device.DeviceType.ToString());
            
            // 如果找不到对应的设备类型项（比如Generic），则使用第一个有效的设备类型
            if (deviceTypeItem != null)
            {
                DeviceTypeComboBox.SelectedItem = deviceTypeItem;
            }
            else
            {
                // 如果设备类型是Generic，将其映射到第一个有效的设备类型
                DeviceTypeComboBox.SelectedIndex = 0;
                // 更新设备的设备类型，避免下次加载时再次出现问题
                Device.DeviceType = (DeviceType)Enum.Parse(typeof(DeviceType), ((ComboBoxItem)DeviceTypeComboBox.SelectedItem).Tag.ToString());
            }

            // 设置设备索引
            DeviceIndexComboBox.SelectedIndex = Device.DeviceIndex;

            // 设置通讯方式
            CommunicationTypeComboBox.SelectedItem = CommunicationTypeComboBox.Items.Cast<ComboBoxItem>()
                .FirstOrDefault(item => item.Tag.ToString() == Device.CommunicationType.ToString());

            // 设置通讯参数
            if (Device.Params == null)
            {
                Device.Params = new CommunicationParams();
            }

            SerialPortTextBox.SelectedItem = Device.Params.SerialPort;
            BaudRateTextBox.SelectedItem = Device.Params.BaudRate.ToString();
            DataBitsTextBox.Text = Device.Params.DataBits.ToString();
            ParityComboBox.SelectedItem = ParityComboBox.Items.Cast<ComboBoxItem>()
                .FirstOrDefault(item => item.Tag.ToString() == Device.Params.Parity);
            StopBitsTextBox.Text = Device.Params.StopBits.ToString();
            IPAddressTextBox.Text = Device.Params.IPAddress;
            PortTextBox.Text = Device.Params.Port.ToString();
            
            // CAN通讯参数设置
            DeviceIndexComboBox.SelectedIndex = Device.Params.DeviceIndex;
            CanChannelComboBox.SelectedItem = CanChannelComboBox.Items.Cast<ComboBoxItem>()
                .FirstOrDefault(item => item.Tag.ToString() == Device.Params.CanChannel.ToString());
            CanWorkModeComboBox.SelectedItem = CanWorkModeComboBox.Items.Cast<ComboBoxItem>()
                .FirstOrDefault(item => item.Tag.ToString() == Device.Params.CanWorkMode.ToString());
            
            // 设置CAN波特率
            var canBaudRateItem = CanBaudRateComboBox.Items.Cast<ComboBoxItem>()
                .FirstOrDefault(item => item.Tag.ToString() == Device.Params.CanBaudRate.ToString());
            if (canBaudRateItem != null)
                CanBaudRateComboBox.SelectedItem = canBaudRateItem;
            else
                CanBaudRateComboBox.SelectedIndex = 5; // 默认500 kbps
            
            CanFdModeComboBox.SelectedIndex = Device.Params.IsCanFd ? 1 : 0;
            
            // 设置CAN FD数据段波特率
            var canFdDataBaudRateItem = CanFdDataBaudRateComboBox.Items.Cast<ComboBoxItem>()
                .FirstOrDefault(item => item.Tag.ToString() == Device.Params.CanFdDataBaudRate.ToString());
            if (canFdDataBaudRateItem != null)
                CanFdDataBaudRateComboBox.SelectedItem = canFdDataBaudRateItem;
            else
                CanFdDataBaudRateComboBox.SelectedIndex = 1; // 默认2 Mbps
            
            ArbitrationSamplePointTextBox.Text = Device.Params.ArbitrationSamplePoint.ToString();
            DataSamplePointTextBox.Text = Device.Params.DataSamplePoint.ToString();
            
            CanFilterModeComboBox.SelectedItem = CanFilterModeComboBox.Items.Cast<ComboBoxItem>()
                .FirstOrDefault(item => item.Tag.ToString() == Device.Params.CanFilterMode.ToString());
            
            AcceptanceCodeTextBox.Text = "0x" + Device.Params.AcceptanceCode.ToString("X8");
            AcceptanceMaskTextBox.Text = "0x" + Device.Params.AcceptanceMask.ToString("X8");
            
            IsTerminalResistorCheckBox.IsChecked = Device.Params.IsTerminalResistorEnabled;
            EnableErrorFrameCheckBox.IsChecked = Device.Params.EnableErrorFrame;
            
            UsbDeviceIdTextBox.SelectedItem = Device.Params.UsbDeviceId;

            UpdateParamsVisibility();
        }

        private void CommunicationTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateParamsVisibility();
        }

        private void UpdateParamsVisibility()
        {
            SerialParamsGrid.Visibility = Visibility.Collapsed;
            NetworkParamsGrid.Visibility = Visibility.Collapsed;
            CanParamsGrid.Visibility = Visibility.Collapsed;
            UsbParamsGrid.Visibility = Visibility.Collapsed;

            ComboBoxItem selectedItem = (ComboBoxItem)CommunicationTypeComboBox.SelectedItem;
            if (selectedItem != null)
            {
                string communicationType = selectedItem.Tag?.ToString() ?? "Serial";
                switch (communicationType)
                {
                    case "Serial":
                        SerialParamsGrid.Visibility = Visibility.Visible;
                        break;
                    case "Network":
                        NetworkParamsGrid.Visibility = Visibility.Visible;
                        break;
                    case "CAN":
                        CanParamsGrid.Visibility = Visibility.Visible;
                        UpdateCanFdParamsVisibility();
                        break;
                    case "USB":
                        UsbParamsGrid.Visibility = Visibility.Visible;
                        break;
                }
            }
        }

        private void UpdateCanFdParamsVisibility()
        {
            bool isCanFdEnabled = CanFdModeComboBox.SelectedIndex == 1;
            
            // 获取CAN FD相关参数的行索引
            var canFdDataBaudRatePanel = CanParamsGrid.Children.OfType<StackPanel>().FirstOrDefault(p => p.Name == "CanFdDataBaudRatePanel");
            var dataSamplePointPanel = CanParamsGrid.Children.OfType<StackPanel>().FirstOrDefault(p => p.Name == "DataSamplePointPanel");
            
            // 如果没有找到具体的面板，使用Grid.Row来定位
            foreach (var child in CanParamsGrid.Children)
            {
                if (child is StackPanel panel)
                {
                    int row = Grid.GetRow(panel);
                    // CAN FD数据段波特率在第4行，数据采样点在第8行
                    if (row == 4 || row == 8)
                    {
                        panel.Visibility = isCanFdEnabled ? Visibility.Visible : Visibility.Collapsed;
                    }
                }
            }
        }

        private void CanFdModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateCanFdParamsVisibility();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(DeviceNameTextBox.Text))
            {
                MessageBox.Show("设备名称不能为空", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Device.Name = DeviceNameTextBox.Text;

            // 保存设备类型
            if (DeviceTypeComboBox.SelectedItem != null)
            {
                ComboBoxItem selectedDeviceTypeItem = (ComboBoxItem)DeviceTypeComboBox.SelectedItem;
                Device.DeviceType = (DeviceType)Enum.Parse(typeof(DeviceType), selectedDeviceTypeItem.Tag.ToString());
            }

            // 保存设备索引
            Device.DeviceIndex = DeviceIndexComboBox.SelectedIndex;

            ComboBoxItem selectedItem = (ComboBoxItem)CommunicationTypeComboBox.SelectedItem;
            if (selectedItem != null)
            {
                string communicationTypeStr = selectedItem.Tag?.ToString() ?? "Serial";
                Device.CommunicationType = (CommunicationType)Enum.Parse(typeof(CommunicationType), communicationTypeStr);
            }

            if (Device.Params == null)
            {
                Device.Params = new CommunicationParams();
            }

            // 保存通讯参数
            Device.Params.SerialPort = SerialPortTextBox.SelectedItem?.ToString() ?? Device.Params.SerialPort;
            
            // 显式检查波特率转换结果
            if (int.TryParse(BaudRateTextBox.SelectedItem?.ToString() ?? "9600", out int baudRate))
            {
                Device.Params.BaudRate = baudRate;
            }
            
            // 显式检查数据位转换结果
            if (int.TryParse(DataBitsTextBox.Text, out int dataBits))
            {
                Device.Params.DataBits = dataBits;
            }
            
            if (ParityComboBox.SelectedItem != null)
            {
                ComboBoxItem selectedParityItem = (ComboBoxItem)ParityComboBox.SelectedItem;
                Device.Params.Parity = selectedParityItem?.Tag?.ToString() ?? "None";
            }
            
            // 显式检查停止位转换结果
            if (int.TryParse(StopBitsTextBox.Text, out int stopBits))
            {
                Device.Params.StopBits = stopBits;
            }
            
            Device.Params.IPAddress = IPAddressTextBox.Text;
            
            // 显式检查端口转换结果
            if (int.TryParse(PortTextBox.Text, out int port))
            {
                Device.Params.Port = port;
            }
            
            // 保存CAN通讯参数 - 按照周立功规范
            Device.Params.DeviceIndex = DeviceIndexComboBox.SelectedIndex;
            
            if (CanChannelComboBox.SelectedItem != null)
            {
                ComboBoxItem selectedCanChannelItem = (ComboBoxItem)CanChannelComboBox.SelectedItem;
                Device.Params.CanChannel = (CanChannelType)Enum.Parse(typeof(CanChannelType), selectedCanChannelItem.Tag.ToString());
            }
            
            if (CanWorkModeComboBox.SelectedItem != null)
            {
                ComboBoxItem selectedCanWorkModeItem = (ComboBoxItem)CanWorkModeComboBox.SelectedItem;
                Device.Params.CanWorkMode = (CanWorkMode)Enum.Parse(typeof(CanWorkMode), selectedCanWorkModeItem.Tag.ToString());
            }
            
            // 保存CAN波特率
            if (CanBaudRateComboBox.SelectedItem != null)
            {
                ComboBoxItem selectedCanBaudRateItem = (ComboBoxItem)CanBaudRateComboBox.SelectedItem;
                if (int.TryParse(selectedCanBaudRateItem.Tag.ToString(), out int canBaudRate))
                {
                    Device.Params.CanBaudRate = canBaudRate;
                }
            }
            
            Device.Params.IsCanFd = CanFdModeComboBox.SelectedIndex == 1;
            
            // 保存CAN FD数据段波特率
            if (CanFdDataBaudRateComboBox.SelectedItem != null)
            {
                ComboBoxItem selectedCanFdDataBaudRateItem = (ComboBoxItem)CanFdDataBaudRateComboBox.SelectedItem;
                if (int.TryParse(selectedCanFdDataBaudRateItem.Tag.ToString(), out int canFdDataBaudRate))
                {
                    Device.Params.CanFdDataBaudRate = canFdDataBaudRate;
                }
            }
            
            // 保存采样点参数
            if (int.TryParse(ArbitrationSamplePointTextBox.Text, out int arbitrationSamplePoint))
            {
                Device.Params.ArbitrationSamplePoint = arbitrationSamplePoint;
            }
            
            if (int.TryParse(DataSamplePointTextBox.Text, out int dataSamplePoint))
            {
                Device.Params.DataSamplePoint = dataSamplePoint;
            }
            
            // 保存滤波参数
            if (CanFilterModeComboBox.SelectedItem != null)
            {
                ComboBoxItem selectedCanFilterModeItem = (ComboBoxItem)CanFilterModeComboBox.SelectedItem;
                Device.Params.CanFilterMode = (CanFilterMode)Enum.Parse(typeof(CanFilterMode), selectedCanFilterModeItem.Tag.ToString());
            }
            
            // 保存验收码和验收屏蔽码
            string acceptanceCodeText = AcceptanceCodeTextBox.Text.Replace("0x", "").Replace("0X", "");
            if (uint.TryParse(acceptanceCodeText, System.Globalization.NumberStyles.HexNumber, null, out uint acceptanceCode))
            {
                Device.Params.AcceptanceCode = acceptanceCode;
            }
            
            string acceptanceMaskText = AcceptanceMaskTextBox.Text.Replace("0x", "").Replace("0X", "");
            if (uint.TryParse(acceptanceMaskText, System.Globalization.NumberStyles.HexNumber, null, out uint acceptanceMask))
            {
                Device.Params.AcceptanceMask = acceptanceMask;
            }
            
            Device.Params.IsTerminalResistorEnabled = IsTerminalResistorCheckBox.IsChecked ?? false;
            Device.Params.EnableErrorFrame = EnableErrorFrameCheckBox.IsChecked ?? false;
            Device.Params.UsbDeviceId = UsbDeviceIdTextBox.SelectedItem?.ToString() ?? Device.Params.UsbDeviceId;

            this.DialogResult = true;
            this.Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}