using SIAT.ResourceManagement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace SIAT
{
    public partial class AddEditStepDialog : Window
    {
        public Step Step { get; private set; }
        private Device parentDevice;

        public AddEditStepDialog(Step? step, Device device)
        {
            InitializeComponent();
            parentDevice = device;

            if (step != null)
            {
                // 编辑模式
                Step = step;
                LoadStepData();
            }
            else
            {
                // 新增模式
                Step = new Step();
                Step.ResultVariables = new List<ResultVariable>();
                ResultVarListView.ItemsSource = Step.ResultVariables;
            }
        }

        private void LoadStepData()
        {
            StepNameTextBox.Text = Step.Name;

            // 设置步骤类型
            StepTypeComboBox.SelectedItem = StepTypeComboBox.Items.Cast<ComboBoxItem>()
                .FirstOrDefault(item => item.Tag.ToString() == Step.StepType.ToString());

            // 设置协议类型
            ProtocolTypeComboBox.SelectedItem = ProtocolTypeComboBox.Items.Cast<ComboBoxItem>()
                .FirstOrDefault(item => item.Tag.ToString() == Step.Protocol.Type.ToString());

            // 设置协议参数
            if (Step.Protocol == null)
            {
                Step.Protocol = new Protocol();
            }

            IsCanFdCheckBox.IsChecked = Step.Protocol.IsCanFd;
            CanIdTextBox.Text = Step.Protocol.Id;
            ProtocolContentTextBox.Text = Step.Protocol.Content;



            // 设置结果变量
            if (Step.ResultVariables == null)
            {
                Step.ResultVariables = new List<ResultVariable>();
            }
            ResultVarListView.ItemsSource = Step.ResultVariables;

            UpdateProtocolVisibility();
            UpdateResultVarListColumnsVisibility();
            UpdateProtocolContentVisibility();
        }

        private void ProtocolTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateProtocolVisibility();
            UpdateResultVarParamsVisibility();
            UpdateResultVarListColumnsVisibility();
        }

        private void StepTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateProtocolContentVisibility();
        }

        private void UpdateProtocolContentVisibility()
        {
            // 根据步骤类型显示/隐藏协议内容相关控件
            ComboBoxItem selectedItem = (ComboBoxItem)StepTypeComboBox.SelectedItem;
            if (selectedItem == null) return;

            string stepType = selectedItem.Tag.ToString();
            bool isReadOnly = stepType == "ReadOnly";

            // 如果是仅读取类型，禁用协议内容相关控件
            ProtocolContentTextBox.IsEnabled = !isReadOnly;
        }



        private void UpdateProtocolVisibility()
        {
            ComboBoxItem selectedItem = (ComboBoxItem)ProtocolTypeComboBox.SelectedItem;
            if (selectedItem == null) return;

            string protocolType = selectedItem.Tag?.ToString() ?? "CAN";

            CanFdStackPanel.Visibility = (protocolType == "CAN") ? Visibility.Visible : Visibility.Collapsed;
            CanIdStackPanel.Visibility = (protocolType == "CAN") ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateResultVarParamsVisibility()
        {
            ComboBoxItem selectedItem = (ComboBoxItem)ProtocolTypeComboBox.SelectedItem;
            if (selectedItem == null) return;

            string protocolType = selectedItem.Tag?.ToString() ?? "CAN";

            switch (protocolType)
            {
                case "HEX":
                    EndBitStackPanel.Visibility = Visibility.Visible;
                    LengthStackPanel.Visibility = Visibility.Collapsed;
                    EndianStackPanel.Visibility = Visibility.Collapsed;
                    break;
                case "ASCII":
                    EndBitStackPanel.Visibility = Visibility.Visible;
                    LengthStackPanel.Visibility = Visibility.Collapsed;
                    EndianStackPanel.Visibility = Visibility.Collapsed;
                    break;
                case "CAN":
                    EndBitStackPanel.Visibility = Visibility.Collapsed;
                    LengthStackPanel.Visibility = Visibility.Visible;
                    EndianStackPanel.Visibility = Visibility.Visible;
                    break;
            }
        }

        private void UpdateResultVarListColumnsVisibility()
        {
            ComboBoxItem selectedItem = (ComboBoxItem)ProtocolTypeComboBox.SelectedItem;
            if (selectedItem == null) return;

            string protocolType = selectedItem.Tag?.ToString() ?? "CAN";

            // 获取所有GridView列
            if (ResultVarListView.View == null) return;
            GridView? gridView = ResultVarListView.View as GridView;
            if (gridView == null) return;

            // 存储原始宽度的字典
            Dictionary<string, double> originalWidths = new Dictionary<string, double>();
            originalWidths["变量名称"] = 100;
            originalWidths["单位"] = 50;
            originalWidths["起始位"] = 60;
            originalWidths["末位"] = 60;
            originalWidths["长度"] = 60;
            originalWidths["分辨率"] = 80;
            originalWidths["偏移量"] = 80;
            originalWidths["大小端"] = 80;
            originalWidths["CAN ID"] = 100;

            // 默认显示基础列
            foreach (GridViewColumn column in gridView.Columns)
            {
                if (column.Header == null) continue;
                string header = column.Header.ToString()!;
                // 始终显示基本信息列
                if (header == "变量名称" || 
                    header == "单位" || 
                    header == "起始位" ||
                    header == "分辨率" || 
                    header == "偏移量")
                {
                    // 设置原始宽度
                    column.Width = originalWidths[header];
                }
                else
                {
                    // 隐藏其他列
                    column.Width = 0;
                }
            }

            // 根据协议类型显示特定列
            switch (protocolType)
            {
                case "HEX":
                case "ASCII":
                    // 对于HEX和ASCII协议，显示末位，隐藏长度、大小端和CAN ID
                    SetColumnWidth(gridView, "末位", originalWidths["末位"]);
                    SetColumnWidth(gridView, "长度", 0);
                    SetColumnWidth(gridView, "大小端", 0);
                    SetColumnWidth(gridView, "CAN ID", 0);
                    break;
                case "CAN":
                    // 对于CAN协议，显示长度、大小端和CAN ID，隐藏末位
                    SetColumnWidth(gridView, "末位", 0);
                    SetColumnWidth(gridView, "长度", originalWidths["长度"]);
                    SetColumnWidth(gridView, "大小端", originalWidths["大小端"]);
                    SetColumnWidth(gridView, "CAN ID", originalWidths["CAN ID"]);
                    break;
            }
        }

        private void SetColumnWidth(GridView? gridView, string headerText, double width)
        {
            if (gridView == null) return;
            GridViewColumn? column = gridView.Columns.FirstOrDefault(c => c.Header != null && c.Header.ToString() == headerText);
            if (column != null) column.Width = width;
        }

        private void ResultVarListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ResultVarListView.SelectedItem != null)
            {
                EditResultVarButton.IsEnabled = true;
                DeleteResultVarButton.IsEnabled = true;
                // 单击不显示变量参数面板，只启用按钮
            }
            else
            {
                EditResultVarButton.IsEnabled = false;
                DeleteResultVarButton.IsEnabled = false;
                ResultVarParamsGroupBox.Visibility = Visibility.Collapsed;
            }
        }

        private void LoadResultVarData(ResultVariable resultVar)
        {
            StartBitTextBox.Text = resultVar.StartBit.ToString();
            EndBitTextBox.Text = resultVar.EndBit.ToString();
            LengthTextBox.Text = resultVar.Length.ToString();
            ResolutionTextBox.Text = resultVar.Resolution.ToString();
            OffsetTextBox.Text = resultVar.Offset.ToString();
            EndianComboBox.SelectedItem = EndianComboBox.Items.Cast<ComboBoxItem>()
                .FirstOrDefault(item => item.Tag.ToString() == resultVar.Endian.ToString());
        }

        private void AddResultVarButton_Click(object sender, RoutedEventArgs e)
        {
            ComboBoxItem selectedProtocolItem = (ComboBoxItem)ProtocolTypeComboBox.SelectedItem;
            string protocolTypeStr = selectedProtocolItem?.Tag?.ToString() ?? "CAN";
            ResultVarEditDialog dialog = new ResultVarEditDialog(null, (ProtocolType)Enum.Parse(typeof(ProtocolType), protocolTypeStr));
            if (dialog.ShowDialog() == true)
            {
                Step.ResultVariables.Add(dialog.ResultVar);
                ResultVarListView.Items.Refresh();
            }
        }

        private void EditResultVarButton_Click(object sender, RoutedEventArgs e)
        {
            if (ResultVarListView.SelectedItem != null)
            {
                ResultVariable selectedVar = (ResultVariable)ResultVarListView.SelectedItem;
                ComboBoxItem selectedProtocolItem = (ComboBoxItem)ProtocolTypeComboBox.SelectedItem;
                string protocolTypeStr = selectedProtocolItem?.Tag?.ToString() ?? "CAN";
                ResultVarEditDialog dialog = new ResultVarEditDialog(selectedVar, (ProtocolType)Enum.Parse(typeof(ProtocolType), protocolTypeStr));
                if (dialog.ShowDialog() == true)
                {
                    ResultVarListView.Items.Refresh();
                }
            }
        }

        private void DeleteResultVarButton_Click(object sender, RoutedEventArgs e)
        {
            if (ResultVarListView.SelectedItem != null)
            {
                ResultVariable selectedVar = (ResultVariable)ResultVarListView.SelectedItem;
                MessageBoxResult result = MessageBox.Show($"确定要删除结果变量 '{selectedVar.Name}' 吗？", "删除确认", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    Step.ResultVariables.Remove(selectedVar);
                    ResultVarListView.Items.Refresh();
                    EditResultVarButton.IsEnabled = false;
                    DeleteResultVarButton.IsEnabled = false;
                    ResultVarParamsGroupBox.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(StepNameTextBox.Text))
            {
                MessageBox.Show("步骤名称不能为空", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Step.Name = StepNameTextBox.Text;

            // 保存步骤类型
            ComboBoxItem selectedStepTypeItem = (ComboBoxItem)StepTypeComboBox.SelectedItem;
            if (selectedStepTypeItem != null)
            {
                string stepTypeStr = selectedStepTypeItem.Tag?.ToString() ?? "SendAndReceive";
                Step.StepType = (StepType)Enum.Parse(typeof(StepType), stepTypeStr);
            }

            // 根据步骤类型设置WaitForResponse属性
            switch (Step.StepType)
            {
                case StepType.SendAndReceive:
                    Step.WaitForResponse = true;
                    break;
                case StepType.SendOnly:
                    Step.WaitForResponse = false;
                    break;
                case StepType.ReadOnly:
                    Step.WaitForResponse = true;
                    break;
            }


            // 保存协议参数
            if (Step.Protocol == null)
            {
                Step.Protocol = new Protocol();
            }

            ComboBoxItem selectedProtocolItem = (ComboBoxItem)ProtocolTypeComboBox.SelectedItem;
            if (selectedProtocolItem != null)
            {
                string protocolTypeStr = selectedProtocolItem.Tag?.ToString() ?? "CAN";
                Step.Protocol.Type = (ProtocolType)Enum.Parse(typeof(ProtocolType), protocolTypeStr);
            }

            Step.Protocol.IsCanFd = IsCanFdCheckBox.IsChecked ?? false;
            Step.Protocol.Id = CanIdTextBox.Text;
            Step.Protocol.Content = ProtocolContentTextBox.Text;

            this.DialogResult = true;
            this.Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void ResultVarListView_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // 双击修改变量
            EditResultVarButton_Click(sender, e);
        }
    }
}