using SIAT.Devices;
using SIAT.ResourceManagement;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SIAT
{
    public partial class ResourceManagementWindow : Window
    {
        private readonly List<Device> devices;
        private Device? selectedDevice;
        private Step? selectedStep;
        private readonly string devicesFolderPath;

        public ResourceManagementWindow()
        {
            InitializeComponent();
            devices = [];
            selectedDevice = null;
            selectedStep = null;
            devicesFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Devices");
            LoadDevices();
        }

        /// <summary>
        /// 从代码中识别设备和步骤，并合并XML中保存的启用状态和通讯参数
        /// </summary>
        private void LoadDevices()
        {
            devices.Clear();

            // 1. 从代码中扫描所有设备类
            var discoveredDevices = DeviceBase.DiscoverDevices();

            // 2. 确保Devices目录存在
            if (!Directory.Exists(devicesFolderPath))
            {
                Directory.CreateDirectory(devicesFolderPath);
            }

            // 3. 为每个代码中识别到的设备创建Device对象，并尝试从XML加载启用状态和通讯参数
            foreach (var (type, definition) in discoveredDevices)
            {
                // 从代码中获取步骤定义
                var codeSteps = DeviceBase.GetSteps(type);

                // 创建Device对象
                Device device = new Device
                {
                    Name = definition.Name,
                    DeviceType = definition.DeviceType,
                    CommunicationType = definition.CommunicationType,
                    SourceClassName = type.FullName ?? type.Name,
                    Status = "未连接",
                    IsEnabled = true
                };

                // 尝试从XML加载已保存的启用状态和通讯参数
                string xmlPath = Path.Combine(devicesFolderPath, $"{device.Name}.xml");
                if (File.Exists(xmlPath))
                {
                    try
                    {
                        Device? savedDevice = XmlHelper.DeserializeFromFile<Device>(xmlPath);
                        if (savedDevice != null && !string.IsNullOrEmpty(savedDevice.Name))
                        {
                            // 合并：使用XML中的启用状态和通讯参数
                            device.IsEnabled = savedDevice.IsEnabled;
                            device.DeviceIndex = savedDevice.DeviceIndex;
                            if (savedDevice.Params != null)
                            {
                                device.Params = savedDevice.Params;
                            }

                            // 合并步骤的启用状态：以代码定义为准，但保留XML中的启用状态
                            if (savedDevice.Steps != null)
                            {
                                foreach (var codeStep in codeSteps)
                                {
                                    var savedStep = savedDevice.Steps.FirstOrDefault(s => s.Name == codeStep.Name);
                                    if (savedStep != null)
                                    {
                                        codeStep.IsEnabled = savedStep.IsEnabled;
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"加载设备配置 {Path.GetFileName(xmlPath)} 失败: {ex.Message}", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }

                device.Steps = codeSteps;
                devices.Add(device);
            }

            DeviceListView.ItemsSource = devices;
        }

        /// <summary>
        /// 保存设备的启用状态和通讯参数到XML
        /// </summary>
        private void SaveDevices()
        {
            foreach (Device device in devices)
            {
                string filePath = Path.Combine(devicesFolderPath, $"{device.Name}.xml");
                XmlHelper.SerializeToFile(device, filePath);
            }
        }

        private void DeviceListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DeviceListView.SelectedItem != null)
            {
                selectedDevice = (Device)DeviceListView.SelectedItem;
                selectedStep = null;
                StepListView.ItemsSource = selectedDevice.Steps;
                StepListView.SelectedItem = null;

                // 显示设备详细信息，隐藏步骤详细信息
                DeviceDetailPanel.Visibility = Visibility.Visible;
                StepDetailPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void DeviceListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // 双击设备打开通讯参数配置界面
            if (selectedDevice != null)
            {
                // 记住代码定义的设备名称和类型，防止被修改
                string originalName = selectedDevice.Name;
                var originalDeviceType = selectedDevice.DeviceType;

                var dialog = new AddEditDeviceDialog(selectedDevice);

                // 设备名称和类型由代码定义，设置为只读
                dialog.DeviceNameTextBox.IsReadOnly = true;
                dialog.DeviceTypeComboBox.IsEnabled = false;

                if (dialog.ShowDialog() == true)
                {
                    // 确保代码定义的名称和类型不被修改
                    selectedDevice.Name = originalName;
                    selectedDevice.DeviceType = originalDeviceType;

                    // 刷新显示
                    DeviceListView.Items.Refresh();
                    DeviceDetailPanel.DataContext = selectedDevice;
                }

                DeviceDetailPanel.Visibility = Visibility.Visible;
                StepDetailPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void DeviceListView_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // 当点击已经选中的设备时，确保显示设备详细信息
            if (selectedDevice != null && DeviceListView.SelectedItem == selectedDevice)
            {
                DeviceDetailPanel.Visibility = Visibility.Visible;
                StepDetailPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void StepListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (StepListView.SelectedItem != null)
            {
                selectedStep = (Step)StepListView.SelectedItem;

                // 显示步骤详细信息，隐藏设备详细信息
                StepDetailPanel.DataContext = selectedStep;
                StepDetailPanel.Visibility = Visibility.Visible;
                DeviceDetailPanel.Visibility = Visibility.Collapsed;

                // 更新变量列表列的可见性
                UpdateResultVarListColumnsVisibility();
            }
        }

        private void UpdateResultVarListColumnsVisibility()
        {
            if (selectedStep == null || selectedStep.Protocol == null)
                return;

            // 获取变量列表的ListView
            if (FindName("ResultVariablesListView") is not ListView resultVarListView || resultVarListView.View == null)
                return;

            if (resultVarListView.View is not GridView gridView)
                return;

            // 存储原始宽度的字典
            var originalWidths = new Dictionary<string, double>
            {
                { "变量名称", 120 },
                { "单位", 60 },
                { "起始位", 100 },
                { "结束位", 100 },
                { "长度", 100 },
                { "分辨率", 100 },
                { "偏移量", 100 },
                { "字节序", 100 },
                { "CAN ID", 100 }
            };

            ProtocolType protocolType = selectedStep.Protocol.Type;

            // 设置列宽
            foreach (GridViewColumn column in gridView.Columns)
            {
                if (column.Header == null)
                    continue;

                string header = column.Header.ToString()!;

                // 始终显示基本信息列
                if (header == "变量名称" ||
                    header == "单位" ||
                    header == "起始位" ||
                    header == "分辨率" ||
                    header == "偏移量")
                {
                    column.Width = originalWidths[header];
                }
                // 根据协议类型显示特定列
                else if (header == "结束位")
                {
                    column.Width = (protocolType == ProtocolType.HEX || protocolType == ProtocolType.ASCII) ? originalWidths[header] : 0;
                }
                else if (header == "长度")
                {
                    column.Width = (protocolType == ProtocolType.CAN) ? originalWidths[header] : 0;
                }
                else if (header == "字节序")
                {
                    column.Width = (protocolType == ProtocolType.CAN) ? originalWidths[header] : 0;
                }
                else if (header == "CAN ID")
                {
                    column.Width = (protocolType == ProtocolType.CAN) ? originalWidths[header] : 0;
                }
            }
        }

        private void StepListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // 步骤由代码定义，不再支持双击编辑
            if (selectedStep != null)
            {
                StepDetailPanel.DataContext = selectedStep;
                StepDetailPanel.Visibility = Visibility.Visible;
                DeviceDetailPanel.Visibility = Visibility.Collapsed;
                UpdateResultVarListColumnsVisibility();
            }
        }

        private void StepListView_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // 当点击已经选中的步骤时，确保显示步骤详细信息
            if (selectedStep != null && StepListView.SelectedItem == selectedStep)
            {
                StepDetailPanel.DataContext = selectedStep;
                StepDetailPanel.Visibility = Visibility.Visible;
                DeviceDetailPanel.Visibility = Visibility.Collapsed;
                UpdateResultVarListColumnsVisibility();
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            SaveDevices();
            MessageBox.Show("设备启用状态和通讯参数已保存", "保存成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
