using System.Windows;

namespace SIAT
{
    public partial class TestCaseOperationDialog : Window
    {
        public enum OperationType
        {
            None,
            NewTestCase,
            OpenTestCase
        }

        public OperationType SelectedOperation { get; private set; }

        public TestCaseOperationDialog()
        {
            InitializeComponent();
            SelectedOperation = OperationType.None;
            
            // 绑定按钮点击事件
            NewTestCaseButton.Click += NewTestCaseButton_Click;
            OpenTestCaseButton.Click += OpenTestCaseButton_Click;
        }

        private void NewTestCaseButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedOperation = OperationType.NewTestCase;
            DialogResult = true;
            Close();
        }

        private void OpenTestCaseButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedOperation = OperationType.OpenTestCase;
            DialogResult = true;
            Close();
        }
    }
}