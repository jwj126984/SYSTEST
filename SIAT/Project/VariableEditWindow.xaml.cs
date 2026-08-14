using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SIAT
{
    /// <summary>
    /// VariableEditWindow.xaml 的交互逻辑
    /// </summary>
    public partial class VariableEditWindow : Window, INotifyPropertyChanged
    {
        public ProjectVariable Variable { get; set; }
        

        public List<string> VariableTypes { get; set; } = new List<string>();
        
        public event PropertyChangedEventHandler? PropertyChanged;
        
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public VariableEditWindow(ProjectVariable variable)
        {
            InitializeComponent();
            Variable = variable;
            DataContext = this;

            // 初始化变量类型列表
            VariableTypes = new List<string> { "String", "Int", "Double", "Bool", "DateTime" };
        }



        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(Variable.VariableName))
            {
                MessageBox.Show("变量名称不能为空！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

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