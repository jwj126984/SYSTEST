using SIAT.ResourceManagement;
using System;
using System.Windows;
using System.Windows.Controls;

namespace SIAT
{
    public partial class ResultVarEditDialog : Window
    {
        public ResultVariable ResultVar { get; private set; }

        private ProtocolType protocolType;

        public ResultVarEditDialog(ResultVariable? resultVar, ProtocolType type)
        {
            InitializeComponent();
            protocolType = type;

            if (resultVar != null)
            {
                // 编辑模式
                ResultVar = resultVar;
                LoadResultVarData();
            }
            else
            {
                // 新增模式
                ResultVar = new ResultVariable();
            }

            UpdateVisibility();
        }

        private void LoadResultVarData()
        {
            VarNameTextBox.Text = ResultVar.Name;
            VarUnitTextBox.Text = ResultVar.Unit;
            StartBitTextBox.Text = ResultVar.StartBit.ToString();
            EndBitTextBox.Text = ResultVar.EndBit.ToString();
            CanIdTextBox.Text = ResultVar.CanId;
            LengthTextBox.Text = ResultVar.Length.ToString();
            ResolutionTextBox.Text = ResultVar.Resolution.ToString();
            OffsetTextBox.Text = ResultVar.Offset.ToString();

            // 设置大小端
            EndianComboBox.SelectedItem = EndianComboBox.Items.Cast<ComboBoxItem>()
                .FirstOrDefault(item => item.Tag.ToString() == ResultVar.Endian.ToString());
        }

        private void UpdateVisibility()
        {
            switch (protocolType)
            {
                case ProtocolType.HEX:
                    StartBitStackPanel.Visibility = Visibility.Visible;
                    EndBitStackPanel.Visibility = Visibility.Visible;
                    CanIdStackPanel.Visibility = Visibility.Collapsed;
                    LengthStackPanel.Visibility = Visibility.Collapsed;
                    EndianStackPanel.Visibility = Visibility.Collapsed;
                    // HEX协议显示分辨率和偏移量
                    ResolutionTextBox.Visibility = Visibility.Visible;
                    OffsetTextBox.Visibility = Visibility.Visible;
                    break;
                case ProtocolType.ASCII:
                    StartBitStackPanel.Visibility = Visibility.Visible;
                    EndBitStackPanel.Visibility = Visibility.Visible;
                    CanIdStackPanel.Visibility = Visibility.Collapsed;
                    LengthStackPanel.Visibility = Visibility.Collapsed;
                    EndianStackPanel.Visibility = Visibility.Collapsed;
                    // ASCII协议隐藏分辨率和偏移量
                    ResolutionTextBox.Visibility = Visibility.Collapsed;
                    OffsetTextBox.Visibility = Visibility.Collapsed;
                    break;
                case ProtocolType.CAN:
                    StartBitStackPanel.Visibility = Visibility.Visible;
                    EndBitStackPanel.Visibility = Visibility.Collapsed;
                    CanIdStackPanel.Visibility = Visibility.Visible;
                    LengthStackPanel.Visibility = Visibility.Visible;
                    EndianStackPanel.Visibility = Visibility.Visible;
                    // CAN协议显示分辨率和偏移量
                    ResolutionTextBox.Visibility = Visibility.Visible;
                    OffsetTextBox.Visibility = Visibility.Visible;
                    break;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(VarNameTextBox.Text))
            {
                MessageBox.Show("变量名称不能为空", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            ResultVar.Name = VarNameTextBox.Text;
            ResultVar.Unit = VarUnitTextBox.Text;

            // 根据协议类型保存不同的参数
            switch (protocolType)
            {
                case ProtocolType.HEX:
                    int.TryParse(StartBitTextBox.Text, out int startBit);
                    ResultVar.StartBit = startBit;
                    int.TryParse(EndBitTextBox.Text, out int endBit);
                    ResultVar.EndBit = endBit;
                    // HEX协议保存分辨率和偏移量
                    double.TryParse(ResolutionTextBox.Text, out double hexResolution);
                    ResultVar.Resolution = hexResolution;
                    double.TryParse(OffsetTextBox.Text, out double hexOffset);
                    ResultVar.Offset = hexOffset;
                    break;
                case ProtocolType.ASCII:
                    int.TryParse(StartBitTextBox.Text, out int asciiStartBit);
                    ResultVar.StartBit = asciiStartBit;
                    int.TryParse(EndBitTextBox.Text, out int asciiEndBit);
                    ResultVar.EndBit = asciiEndBit;
                    // ASCII协议不需要保存分辨率和偏移量
                    ResultVar.Resolution = 1.0;
                    ResultVar.Offset = 0.0;
                    break;
                case ProtocolType.CAN:
                    ResultVar.CanId = CanIdTextBox.Text;
                    int.TryParse(StartBitTextBox.Text, out int canStartBit);
                    ResultVar.StartBit = canStartBit;
                    int.TryParse(LengthTextBox.Text, out int length);
                    ResultVar.Length = length;
                    if (EndianComboBox.SelectedItem != null)
                    {
                        ComboBoxItem selectedEndianItem = (ComboBoxItem)EndianComboBox.SelectedItem;
                string endianTypeStr = selectedEndianItem?.Tag?.ToString() ?? "LittleEndian";
                ResultVar.Endian = (EndianType)Enum.Parse(typeof(EndianType), endianTypeStr);
                    }
                    // CAN协议保存分辨率和偏移量
                    double.TryParse(ResolutionTextBox.Text, out double canResolution);
                    ResultVar.Resolution = canResolution;
                    double.TryParse(OffsetTextBox.Text, out double canOffset);
                    ResultVar.Offset = canOffset;
                    break;
            }

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