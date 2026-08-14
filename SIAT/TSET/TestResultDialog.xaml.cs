using System.Windows;
using System.Windows.Media;

namespace SIAT.TSET
{
    /// <summary>
    /// TestResultDialog.xaml 的交互逻辑
    /// </summary>
    public partial class TestResultDialog : Window
    {
        public TestResultDialog(bool isPassed)
        {
            InitializeComponent();
            
            // 设置窗口始终在最上层
            this.Topmost = true;
            
            // 设置窗口启动位置为屏幕中央
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            
            // 设置结果文本和颜色
            if (isPassed)
            {
                resultText.Text = "PASS";
                resultText.Foreground = Brushes.Green;
            }
            else
            {
                resultText.Text = "FAIL";
                resultText.Foreground = Brushes.Red;
            }
            
            // 设置窗口大小为屏幕的一半
            this.Width = SystemParameters.PrimaryScreenWidth / 2;
            this.Height = SystemParameters.PrimaryScreenHeight / 2;
            
            // 设置边框大小
            resultBorder.Width = this.Width * 0.8;
            resultBorder.Height = this.Height * 0.6;
            
            // 添加鼠标点击事件，点击窗口任意位置关闭
            this.MouseLeftButtonDown += (sender, e) => this.Close();
        }
    }
}