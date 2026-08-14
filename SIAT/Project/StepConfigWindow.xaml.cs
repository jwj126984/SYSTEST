using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using SIAT.ResourceManagement;

namespace SIAT
{
    /// <summary>
    /// 输入绑定项
    /// </summary>
    [Serializable]
    public class InputBindingItem
    {
        public string Name { get; set; } = string.Empty;
        public string InputDescription { get; set; } = string.Empty;
        public ProjectVariable InputVariable { get; set; } = new ProjectVariable();
    }

    /// <summary>
    /// 输出绑定项
    /// </summary>
    [Serializable]
    public class OutputBindingItem
    {
        public string Name { get; set; } = string.Empty;
        public string OutputDescription { get; set; } = string.Empty;
        public ProjectVariable OutputVariable { get; set; } = new ProjectVariable();
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
                        InputVariable = projectVariables.FirstOrDefault(v => v.VariableName == existingBinding.InputVariable?.VariableName)
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
                        OutputVariable = projectVariables.FirstOrDefault(v => v.VariableName == existingBinding.OutputVariable?.VariableName)
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