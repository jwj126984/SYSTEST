using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using SIAT.ResourceManagement;

namespace SIAT
{
    /// <summary>
    /// 值类型枚举
    /// </summary>
    public enum BindingValueType
    {
        DirectValue,
        VariableBinding
    }

    /// <summary>
    /// 值类型选项（用于UI显示）
    /// </summary>
    public class ValueTypeOption
    {
        public string Text { get; set; } = string.Empty;
        public BindingValueType Value { get; set; }
    }

    /// <summary>
    /// 输入绑定项
    /// </summary>
    [Serializable]
    public class InputBindingItem : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        private string _inputDescription = string.Empty;
        public string InputDescription
        {
            get => _inputDescription;
            set { _inputDescription = value; OnPropertyChanged(); }
        }

        private BindingValueType _valueType = BindingValueType.VariableBinding;
        public BindingValueType ValueType
        {
            get => _valueType;
            set { _valueType = value; OnPropertyChanged(); }
        }

        private string _directValue = string.Empty;
        public string DirectValue
        {
            get => _directValue;
            set { _directValue = value; OnPropertyChanged(); }
        }

        private ProjectVariable _inputVariable = new ProjectVariable();
        public ProjectVariable InputVariable
        {
            get => _inputVariable;
            set { _inputVariable = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// 输出绑定项
    /// </summary>
    [Serializable]
    public class OutputBindingItem : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        private string _outputDescription = string.Empty;
        public string OutputDescription
        {
            get => _outputDescription;
            set { _outputDescription = value; OnPropertyChanged(); }
        }

        private BindingValueType _valueType = BindingValueType.VariableBinding;
        public BindingValueType ValueType
        {
            get => _valueType;
            set { _valueType = value; OnPropertyChanged(); }
        }

        private string _directValue = string.Empty;
        public string DirectValue
        {
            get => _directValue;
            set { _directValue = value; OnPropertyChanged(); }
        }

        private ProjectVariable _outputVariable = new ProjectVariable();
        public ProjectVariable OutputVariable
        {
            get => _outputVariable;
            set { _outputVariable = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// StepConfigWindow.xaml 的交互逻辑
    /// </summary>
    public partial class StepConfigWindow : Window
    {
        public Step Step { get; set; }
        public List<InputBindingItem> StepInputBindings { get; set; } = new List<InputBindingItem>();
        public List<OutputBindingItem> StepOutputBindings { get; set; } = new List<OutputBindingItem>();
        public List<ProjectVariable> ProjectVariables { get; set; } = new List<ProjectVariable>();

        private static readonly List<ValueTypeOption> ValueTypeOptions = new List<ValueTypeOption>
        {
            new ValueTypeOption { Text = "直接填值", Value = BindingValueType.DirectValue },
            new ValueTypeOption { Text = "绑定变量", Value = BindingValueType.VariableBinding }
        };

        public StepConfigWindow(Step step, List<ProjectVariable> projectVariables, 
            List<InputBindingItem>? existingInputBindings = null, 
            List<OutputBindingItem>? existingOutputBindings = null)
        {
            InitializeComponent();
            DataContext = this;

            Step = step;
            ProjectVariables = projectVariables;

            // 初始化输入绑定列表
            StepInputBindings = new List<InputBindingItem>();
            if (existingInputBindings != null && existingInputBindings.Count > 0)
            {
                foreach (var existingBinding in existingInputBindings)
                {
                    var newItem = new InputBindingItem
                    {
                        Name = existingBinding.Name,
                        InputDescription = existingBinding.InputDescription,
                        ValueType = existingBinding.ValueType,
                        DirectValue = existingBinding.DirectValue ?? string.Empty,
                        InputVariable = existingBinding.ValueType == BindingValueType.VariableBinding
                            ? projectVariables.FirstOrDefault(v => v.VariableName == existingBinding.InputVariable?.VariableName)
                            : new ProjectVariable()
                    };
                    StepInputBindings.Add(newItem);
                }
            }

            // 初始化输出绑定列表
            StepOutputBindings = new List<OutputBindingItem>();
            if (existingOutputBindings != null && existingOutputBindings.Count > 0)
            {
                foreach (var existingBinding in existingOutputBindings)
                {
                    var newItem = new OutputBindingItem
                    {
                        Name = existingBinding.Name,
                        OutputDescription = existingBinding.OutputDescription,
                        ValueType = existingBinding.ValueType,
                        DirectValue = existingBinding.DirectValue ?? string.Empty,
                        OutputVariable = existingBinding.ValueType == BindingValueType.VariableBinding
                            ? projectVariables.FirstOrDefault(v => v.VariableName == existingBinding.OutputVariable?.VariableName)
                            : new ProjectVariable()
                    };
                    StepOutputBindings.Add(newItem);
                }
            }
            else
            {
                // 没有现有绑定，为设备步骤自动创建输出绑定
                if (Step.ResultVariables != null && Step.ResultVariables.Count > 0)
                {
                    foreach (var variable in Step.ResultVariables)
                    {
                        StepOutputBindings.Add(new OutputBindingItem
                        {
                            Name = variable.Name ?? string.Empty,
                            OutputDescription = variable.Name ?? string.Empty,
                            ValueType = BindingValueType.VariableBinding,
                            OutputVariable = null
                        });
                    }
                }
            }

            // 初始化界面控件
            StepNameText.Text = Step.Name;
            InputBindingsList.ItemsSource = StepInputBindings;
            OutputBindingsList.ItemsSource = StepOutputBindings;
        }

        private void ValueTypeComboBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is ComboBox comboBox)
            {
                comboBox.ItemsSource = ValueTypeOptions;
            }
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            if (IsLoaded)
            {
                DialogResult = true;
            }
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (IsLoaded)
            {
                DialogResult = false;
            }
            Close();
        }


    }
}