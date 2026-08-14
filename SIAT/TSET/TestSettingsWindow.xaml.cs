using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using SIAT.ResourceManagement;

namespace SIAT.TSET
{
    public partial class TestSettingsWindow : Window
    {
        public TestSettings Settings { get; private set; }
        private DispatcherTimer _portScanTimer;

        public TestSettingsWindow(TestSettings currentSettings)
        {
            // 先初始化Settings对象，避免InitializeComponent()中触发事件时出现空引用
            Settings = currentSettings ?? new TestSettings();
            
            InitializeComponent();
            
            // 初始化界面
            InitializeUI();
            
            // 初始化端口扫描定时器
            _portScanTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _portScanTimer.Tick += PortScanTimer_Tick;
            _portScanTimer.Start();
            
            // 初始扫描端口
            UpdatePortList();
        }

        private void InitializeUI()
        {
            // 设置启动方式
            switch (Settings.StartMode)
            {
                case StartMode.Software:
                    SoftwareStartRadio.IsChecked = true;
                    break;
                case StartMode.Barcode:
                    BarcodeStartRadio.IsChecked = true;
                    break;
                case StartMode.Tooling:
                    ToolingStartRadio.IsChecked = true;
                    break;
            }
            
            // 设置条码长度
            BarcodeLengthTextBox.Text = Settings.BarcodeLength.ToString();
            
            // 设置扫码枪参数（原工装板参数改为扫码枪）
            ToolingPortComboBox.Text = Settings.ToolingPort;
            if (!string.IsNullOrEmpty(Settings.ToolingPort))
            {
                ToolingPortComboBox.SelectedItem = Settings.ToolingPort;
            }
            ToolingBaudRateComboBox.Text = Settings.ToolingBaudRate.ToString();
            
            // 设置治具卡串口参数
            JigPortComboBox.Text = Settings.ToolingJigPort;
            if (!string.IsNullOrEmpty(Settings.ToolingJigPort))
            {
                JigPortComboBox.SelectedItem = Settings.ToolingJigPort;
            }
            JigBaudRateComboBox.Text = Settings.ToolingJigBaudRate.ToString();
            
            // 更新治具卡串口设置面板的可见性
            UpdateJigPortSettingsVisibility();
        }

        private void StartModeChanged(object sender, RoutedEventArgs e)
        {
            // 根据选择的启动方式更新设置
            if (SoftwareStartRadio.IsChecked == true)
            {
                Settings.StartMode = StartMode.Software;
            }
            else if (BarcodeStartRadio.IsChecked == true)
            {
                Settings.StartMode = StartMode.Barcode;
            }
            else if (ToolingStartRadio.IsChecked == true)
            {
                Settings.StartMode = StartMode.Tooling;
            }
            
            // 更新治具卡串口设置面板的可见性
            UpdateJigPortSettingsVisibility();
        }
        
        /// <summary>
        /// 更新治具卡串口设置面板的可见性
        /// </summary>
        private void UpdateJigPortSettingsVisibility()
        {
            // 只在选择工装启动时显示治具卡串口设置
            JigPortSettingsPanel.Visibility = ToolingStartRadio.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        private void PortScanTimer_Tick(object sender, EventArgs e)
        {
            UpdatePortList();
        }

        private void UpdatePortList()
        {
            try
            {
                string[] ports = SerialPort.GetPortNames();
                
                // 更新扫码枪端口列表
                UpdateComboBoxPortList(ToolingPortComboBox, Settings.ToolingPort);
                
                // 更新治具卡端口列表
                UpdateComboBoxPortList(JigPortComboBox, Settings.ToolingJigPort);
            }
            catch (Exception ex)
            {
                // 端口扫描失败，忽略
            }
        }
        
        private void UpdateComboBoxPortList(ComboBox comboBox, string savedPort)
        {
            string[] ports = SerialPort.GetPortNames();
            
            bool hasChanges = false;
            if (comboBox.Items.Count != ports.Length)
            {
                hasChanges = true;
            }
            else
            {
                for (int i = 0; i < ports.Length; i++)
                {
                    if (comboBox.Items[i].ToString() != ports[i])
                    {
                        hasChanges = true;
                        break;
                    }
                }
            }
            
            if (hasChanges)
            {
                comboBox.Items.Clear();
                foreach (string port in ports)
                {
                    comboBox.Items.Add(port);
                }
                
                if (!string.IsNullOrEmpty(savedPort) && Array.Exists(ports, p => p == savedPort))
                {
                    comboBox.SelectedItem = savedPort;
                    comboBox.Text = savedPort;
                }
                else if (ports.Length > 0)
                {
                    comboBox.SelectedIndex = 0;
                }
                else
                {
                    comboBox.SelectedItem = null;
                    comboBox.Text = "";
                }
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 先根据界面选择更新启动方式
                if (SoftwareStartRadio.IsChecked == true)
                {
                    Settings.StartMode = StartMode.Software;
                }
                else if (BarcodeStartRadio.IsChecked == true)
                {
                    Settings.StartMode = StartMode.Barcode;
                }
                else if (ToolingStartRadio.IsChecked == true)
                {
                    Settings.StartMode = StartMode.Tooling;
                }
                
                // 保存条码长度
                if (int.TryParse(BarcodeLengthTextBox.Text, out int barcodeLength) && barcodeLength > 0)
                {
                    Settings.BarcodeLength = barcodeLength;
                }
                else
                {
                    MessageBox.Show("请输入有效的条码长度", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                // 保存扫码枪参数（原工装板参数改为扫码枪）
                Settings.ToolingPort = ToolingPortComboBox.SelectedItem?.ToString() ?? string.Empty;
                
                if (int.TryParse(ToolingBaudRateComboBox.Text, out int toolingBaudRate))
                {
                    Settings.ToolingBaudRate = toolingBaudRate;
                }
                
                // 保存治具卡串口参数
                Settings.ToolingJigPort = JigPortComboBox.SelectedItem?.ToString() ?? string.Empty;
                
                if (int.TryParse(JigBaudRateComboBox.Text, out int jigBaudRate))
                {
                    Settings.ToolingJigBaudRate = jigBaudRate;
                }
                
                // 保存设置
                Settings.Save();
                
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存设置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            
            // 停止端口扫描定时器
            _portScanTimer.Stop();
        }
    }
}
