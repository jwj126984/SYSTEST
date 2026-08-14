using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace SIAT
{
    public partial class MainWindow : Window
    {
      
        private DispatcherTimer? timeTimer;
        private string currentUser = "管理员";
        private string userType = "管理人员"; // 默认用户类型，应从登录窗口传递

        public MainWindow()
        {
            InitializeComponent();

            // 启用窗口拖动功能
            MouseDown += Window_MouseDown;

            // 启动时间更新计时器
            InitializeTimeTimer();

            // 设置初始用户显示
            CurrentUserText.Text = currentUser;

            // 应用用户权限
            ApplyUserPermissions();
        }

        // 带参数的重载构造函数，可以从登录窗口传递用户信息
        public MainWindow(string username, string userRole)
        {
            InitializeComponent();

            // 设置用户信息
            currentUser = username;
            userType = userRole;

            // 启用窗口拖动功能
            MouseDown += Window_MouseDown;

            // 启动时间更新计时器
            InitializeTimeTimer();

            // 设置用户显示
            CurrentUserText.Text = currentUser;

            // 应用用户权限
            ApplyUserPermissions();
        }

        private void InitializeTimeTimer()
        {
            // 创建计时器每秒更新时间
            timeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timeTimer.Tick += TimeTimer_Tick;
            timeTimer.Start();

            // 立即更新时间显示
            UpdateTimeDisplay();
        }

        
        private void TimeTimer_Tick(object? sender, EventArgs e)
        {
            UpdateTimeDisplay();
        }

        private void UpdateTimeDisplay()
        {
            DateTime now = DateTime.Now;

            // 更新日期显示
            CurrentDateText.Text = now.ToString("yyyy-MM-dd");

            // 更新时间显示
            CurrentTimeText.Text = now.ToString("HH:mm:ss");
        }

        // 设置当前用户和用户类型
        public void SetCurrentUser(string username, string role)
        {
            currentUser = username;
            userType = role;

            if (CurrentUserText != null)
            {
                CurrentUserText.Text = currentUser;
            }

            // 重新应用权限
            ApplyUserPermissions();
        }

        // 应用用户权限
        public void ApplyUserPermissions()
        {
            // 根据用户类型设置按钮可用性
            bool isManager = userType == "管理人员";

            // 设置权限
            if (TestInterfaceButton != null)
            {
                // 测试界面按钮：所有人都可以使用
                TestInterfaceButton.IsEnabled = true;
            }

            if (InstanceManagementButton != null)
            {
                // 实例管理按钮：只有管理人员可以使用
                InstanceManagementButton.IsEnabled = isManager;

                // 如果禁用，添加提示
                if (!isManager)
                {
                    InstanceManagementButton.ToolTip = "只有管理人员可以使用此功能";
                    // 添加视觉提示（禁用状态样式）
                    InstanceManagementButton.Opacity = 0.6;
                }
                else
                {
                    InstanceManagementButton.Opacity = 1.0;
                }
            }

            if (ResourceManagementButton != null)
            {
                // 资源管理按钮：只有管理人员可以使用
                ResourceManagementButton.IsEnabled = isManager;

                if (!isManager)
                {
                    ResourceManagementButton.ToolTip = "只有管理人员可以使用此功能";
                    ResourceManagementButton.Opacity = 0.6;
                }
                else
                {
                    ResourceManagementButton.Opacity = 1.0;
                }
            }

            if (SystemSettingsButton != null)
            {
                // 系统设置按钮：只有管理人员可以使用
                SystemSettingsButton.IsEnabled = isManager;

                if (!isManager)
                {
                    SystemSettingsButton.ToolTip = "只有管理人员可以使用此功能";
                    SystemSettingsButton.Opacity = 0.6;
                }
                else
                {
                    SystemSettingsButton.Opacity = 1.0;
                }
            }

            if (ProjectManagementButton != null)
            {
                // 系统设置按钮：只有管理人员可以使用
                ProjectManagementButton.IsEnabled = isManager;

                if (!isManager)
                {
                    ProjectManagementButton.ToolTip = "只有管理人员可以使用此功能";
                    ProjectManagementButton.Opacity = 0.6;
                }
                else
                {
                    ProjectManagementButton.Opacity = 1.0;
                }
                // 项目管理按钮：所有人都可以使用
                ProjectManagementButton.IsEnabled = true;
                ProjectManagementButton.Opacity = 1.0;
            }

            if (DevelopmentButton != null)
            {
                // 待开发按钮：所有人都可以查看
                DevelopmentButton.IsEnabled = true;
                DevelopmentButton.Opacity = 1.0;
            }

            // 更新状态栏显示
            if (CurrentUserText != null)
            {
                CurrentUserText.Text = $"{currentUser} ({userType})";
            }
        }

        // 窗口拖动功能
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && e.ClickCount == 1)
            {
                DragMove();
            }
        }

        // 最小化按钮点击事件
        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        // 最大化/还原按钮点击事件
        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        // 关闭按钮点击事件
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
                
                Close();
      
        }

        // 测试界面按钮点击事件
        
        private void TestInterfaceButton_Click(object sender, RoutedEventArgs e)
        {
            if (TestInterfaceButton.IsEnabled)
            {
                try
                {
                    // 隐藏MainWindow
                    this.Hide();
                    
                    // 打开测试界面窗口
                    var testWindow = new TestInterfaceWindow(currentUser, userType)
                    {
                        Owner = this,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner
                    };

                    testWindow.ShowDialog();
                    
                    // 测试界面关闭后显示MainWindow
                    this.Show();
                }
                catch (Exception ex)
                {
                    // 发生异常时确保MainWindow显示
                    this.Show();
                    
                    MessageBox.Show($"打开测试界面失败: {ex.Message}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // 实例管理按钮点击事件
        private void InstanceManagementButton_Click(object sender, RoutedEventArgs e)
        {
            if (InstanceManagementButton.IsEnabled)
            {
                try
                {
                    // 隐藏MainWindow
                    this.Hide();
                    
                    // 打开用例操作对话框
                    var operationDialog = new TestCaseOperationDialog
                    {
                        Owner = this
                    };
                    
                    if (operationDialog.ShowDialog() == true)
                    {
                        // 根据用户选择的操作执行相应的功能
                        switch (operationDialog.SelectedOperation)
                        {
                            case TestCaseOperationDialog.OperationType.NewTestCase:
                                // 打开新建用例窗口
                                var newTestCaseWindow = new NewTestCaseWindow
                                {
                                    Owner = this
                                };
                                
                                if (newTestCaseWindow.ShowDialog() == true)
                                {
                                    // 创建用例文件并打开用例编辑窗口
                                    string testCasesFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestCases");
                                    string testCaseFileName = newTestCaseWindow.TestCaseName + ".testcase";
                                    string testCasePath = Path.Combine(testCasesFolder, testCaseFileName);
                                    
                                    // 创建空的用例配置
                                    var testCaseConfig = new TestCaseConfig
                                    {
                                        Name = newTestCaseWindow.TestCaseName,
                                        Description = newTestCaseWindow.TestCaseDescription,
                                        CreatedDate = DateTime.Now,
                                        ModifiedDate = DateTime.Now
                                    };
                                    
                                    // 保存用例文件
                                    XmlHelper.SerializeToFile(testCaseConfig, testCasePath);
                                    
                                    // 打开用例编辑窗口
                                    var testCaseEditWindow = new TestCaseEditWindow(testCasePath)
                                    {
                                        Owner = this
                                    };
                                    testCaseEditWindow.ShowDialog();
                                }
                                break;
                                
                            case TestCaseOperationDialog.OperationType.OpenTestCase:
                                // 打开打开用例窗口
                                var openTestCaseWindow = new OpenTestCaseWindow
                                {
                                    Owner = this
                                };
                                
                                if (openTestCaseWindow.ShowDialog() == true && openTestCaseWindow.SelectedTestCase != null)
                                {
                                    // 打开用例编辑窗口
                                    string testCasesFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestCases");
                                    string testCaseFileName = openTestCaseWindow.SelectedTestCase.Name + ".testcase";
                                    string testCasePath = Path.Combine(testCasesFolder, testCaseFileName);
                                    
                                    var testCaseEditWindow = new TestCaseEditWindow(testCasePath)
                                    {
                                        Owner = this
                                    };
                                    testCaseEditWindow.ShowDialog();
                                }
                                break;
                        }
                    }
                    
                    // 所有窗口操作完成后显示MainWindow
                    this.Show();
                }
                catch (Exception ex)
                {
                    // 发生异常时确保MainWindow显示
                    this.Show();
                    
                    MessageBox.Show($"打开用例管理失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("您没有权限访问此功能", "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // 资源管理按钮点击事件
        private void ResourceManagementButton_Click(object sender, RoutedEventArgs e)
        {
            if (ResourceManagementButton.IsEnabled)
            {
                try
                {
                    // 隐藏MainWindow
                    this.Hide();
                    
                    // 打开资源管理界面窗口
                    var resourceWindow = new ResourceManagementWindow
                    {
                        Owner = this,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner
                    };

                    resourceWindow.ShowDialog();
                    
                    // 资源管理界面关闭后显示MainWindow
                    this.Show();
                }
                catch (Exception ex)
                {
                    // 发生异常时确保MainWindow显示
                    this.Show();
                    
                    MessageBox.Show($"打开资源管理界面失败: {ex.Message}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("您没有权限访问此功能", "权限不足",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // 项目管理按钮点击事件
        private void ProjectManagementButton_Click(object sender, RoutedEventArgs e)
        {
            if (ProjectManagementButton.IsEnabled)
            {
                try
                {
                    // 隐藏MainWindow
                    this.Hide();
                    
                    // 打开项目操作对话框
                    var operationDialog = new ProjectOperationDialog
                    {
                        Owner = this
                    };
                    
                    if (operationDialog.ShowDialog() == true)
                    {
                        // 根据用户选择的操作执行相应的功能
                        switch (operationDialog.SelectedOperation)
                        {
                            case ProjectOperationDialog.ProjectOperation.NewProject:
                                // 打开新建项目窗口
                                var newProjectWindow = new NewProjectWindow
                                {
                                    Owner = this
                                };
                                newProjectWindow.ShowDialog();
                                break;
                                
                            case ProjectOperationDialog.ProjectOperation.OpenProject:
                                // 打开打开项目窗口
                                var openProjectWindow = new OpenProjectWindow
                                {
                                    Owner = this
                                };
                                openProjectWindow.ShowDialog();
                                break;
                        }
                    }
                    
                    // 所有窗口操作完成后显示MainWindow
                    this.Show();
                }
                catch (Exception ex)
                {
                    // 发生异常时确保MainWindow显示
                    this.Show();
                    
                    MessageBox.Show($"打开项目管理失败: {ex.Message}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // 待开发按钮点击事件
        private void DevelopmentButton_Click(object sender, RoutedEventArgs e)
        {
            if (DevelopmentButton.IsEnabled)
            {
                try
                {
                    // 这里可以添加打开待开发功能的代码
                    MessageBox.Show("此功能正在开发中，敬请期待", "功能提示",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"操作失败: {ex.Message}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // 系统设置按钮点击事件 - 添加实际代码
        // 在SystemSettingsButton_Click方法中，修改为打开系统设置窗口
        private void SystemSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            if (SystemSettingsButton.IsEnabled)
            {
                // 检查当前用户是否为管理员
                if (userType != "管理人员")
                {
                    MessageBox.Show("只有管理人员可以使用此功能", "权限不足",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                try
                {
                    // 隐藏MainWindow
                    this.Hide();
                    
                    // 打开系统设置窗口
                    var settingsWindow = new SystemSettingsWindow
                    {
                        Owner = this,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner
                    };

                      
                    // 确保窗口正确显示
                    settingsWindow.ShowDialog();
                    
                    // 系统设置窗口关闭后显示MainWindow
                    this.Show();

                    // 如果修改了当前用户的信息，可能需要重新加载权限
                    ApplyUserPermissions();
                    
                }
                catch (Exception ex)
                {
                    // 发生异常时确保MainWindow显示
                    this.Show();
                    
                    MessageBox.Show($"打开系统设置失败: {ex.Message}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("您没有权限访问此功能", "权限不足",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // 显示消息（保留原有方法）
        private static void ShowMessage(string message, string title)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // 添加键盘快捷键支持
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            // Esc键关闭窗口（需要确认）
            if (e.Key == Key.Escape)
            {
                CloseButton_Click(this, new RoutedEventArgs());
            }
            // F1键打开帮助
            else if (e.Key == Key.F1)
            {
                ShowMessage("远聪自动测试系统\n版本: 1.0.0\n\n功能模块:\n1. 测试界面 - 执行自动化测试\n2. 实例管理 - 管理测试用例\n3. 资源管理 - 管理测试资源\n4. 系统设置 - 配置系统参数\n5. 项目管理 - 管理测试项目\n6. 待开发 - 新功能预览\n\n快捷键:\nEsc - 退出系统\nF1 - 显示帮助\nCtrl+1~6 - 快速访问功能",
                            "系统帮助");
            }
            // Ctrl+数字键快速访问功能
            else if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (e.Key == Key.D1 || e.Key == Key.NumPad1)
                {
                    TestInterfaceButton_Click(this, new RoutedEventArgs());
                }
                else if (e.Key == Key.D2 || e.Key == Key.NumPad2)
                {
                    InstanceManagementButton_Click(this, new RoutedEventArgs());
                }
                else if (e.Key == Key.D3 || e.Key == Key.NumPad3)
                {
                    ResourceManagementButton_Click(this, new RoutedEventArgs());
                }
                else if (e.Key == Key.D4 || e.Key == Key.NumPad4)
                {
                    SystemSettingsButton_Click(this, new RoutedEventArgs());
                }
                else if (e.Key == Key.D5 || e.Key == Key.NumPad5)
                {
                    ProjectManagementButton_Click(this, new RoutedEventArgs());
                }
                else if (e.Key == Key.D6 || e.Key == Key.NumPad6)
                {
                    DevelopmentButton_Click(this, new RoutedEventArgs());
                }
            }
        }

        // 窗口关闭时清理资源
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            if (timeTimer != null)
            {
                timeTimer.Stop();
                timeTimer = null;
            }
        }


    }
}