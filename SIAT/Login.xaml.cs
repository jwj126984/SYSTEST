using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Security.Cryptography;

namespace SIAT
{
    public partial class Login : Window
    {
        // 用于跟踪密码是否可见
        private bool isPasswordVisible = false;

        // 配置文件路径
        private string configFilePath;

        // 用于简单的数据保护（实际项目中应该使用更安全的方法）
        private byte[] entropy = Encoding.UTF8.GetBytes("SIAT_Login_Config_Salt_2023");
        static Login()
        {
            // 确保用户数据已加载
            UserDataManager.LoadUsers();
        }
        public Login()
        {
            InitializeComponent();

            // 设置配置文件路径
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appFolder = Path.Combine(appDataPath, "SIAT");
            Directory.CreateDirectory(appFolder); // 确保目录存在
            configFilePath = Path.Combine(appFolder, "login.config");

            Loaded += Login_Loaded;

            // 启用窗口拖动功能
            this.MouseDown += Window_MouseDown;
        }

        private void Login_Loaded(object sender, RoutedEventArgs e)
        {
            // 加载上次登录的用户信息
            LoadLastLoginInfo();

            // 根据用户名和密码状态智能设置焦点
            SetFocusBasedOnInput();

            // 初始化显示密码按钮状态
            UpdateShowPasswordButton();
        }

        // 智能设置焦点
        private void SetFocusBasedOnInput()
        {
            bool hasUsername = !string.IsNullOrWhiteSpace(UsernameTextBox.Text);
            bool hasPassword = !string.IsNullOrWhiteSpace(
                isPasswordVisible ? PasswordTextBox.Text : PasswordBox.Password);

            // 根据输入状态设置焦点
            if (!hasUsername)
            {
                // 没有用户名时，焦点设置到用户名输入框
                UsernameTextBox.Focus();
                UsernameTextBox.SelectAll();
            }
            else if (!hasPassword)
            {
                // 有用户名但没有密码时，焦点设置到密码输入框
                if (isPasswordVisible)
                {
                    PasswordTextBox.Focus();
                    PasswordTextBox.SelectAll();
                }
                else
                {
                    PasswordBox.Focus();
                }
            }
            else
            {
                // 用户名和密码都有时，焦点设置到登录按钮
                LoginButton.Focus();
            }
        }

        // 加载上次登录信息
        private void LoadLastLoginInfo()
        {
            try
            {
                if (File.Exists(configFilePath))
                {
                    string[] lines = File.ReadAllLines(configFilePath);

                    if (lines.Length >= 1)
                    {
                        // 第一行是用户名（不加密）
                        string username = DecryptString(lines[0]);
                        if (!string.IsNullOrEmpty(username))
                        {
                            UsernameTextBox.Text = username;
                        }
                    }

                    if (lines.Length >= 2)
                    {
                        // 第二行是密码（加密存储）
                        string encryptedPassword = lines[1];
                        string password = DecryptString(encryptedPassword);

                        if (!string.IsNullOrEmpty(password))
                        {
                            PasswordBox.Password = password;
                            PasswordTextBox.Text = password;
                            RememberPasswordCheckBox.IsChecked = true;
                        }
                    }

                    // 第三行是"记住密码"状态（如果有）
                    if (lines.Length >= 3)
                    {
                        string rememberStatus = lines[2];
                        if (!string.IsNullOrEmpty(rememberStatus))
                        {
                            bool remember = bool.Parse(rememberStatus);
                            RememberPasswordCheckBox.IsChecked = remember;

                            // 如果未选中记住密码，清除密码
                            if (!remember)
                            {
                                PasswordBox.Password = "";
                                PasswordTextBox.Text = "";
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // 如果配置文件损坏，删除它
                try { File.Delete(configFilePath); } catch { }
            }
        }

        // 保存登录信息
        private void SaveLoginInfo()
        {
            try
            {
                bool rememberPassword = RememberPasswordCheckBox.IsChecked == true;
                string username = UsernameTextBox.Text;
                string password = rememberPassword ?
                    (isPasswordVisible ? PasswordTextBox.Text : PasswordBox.Password) :
                    "";

                // 准备要保存的内容
                string[] lines = new string[]
                {
                    EncryptString(username), // 用户名
                    rememberPassword ? EncryptString(password) : "", // 密码（如果需要记住）
                    rememberPassword.ToString() // 记住密码状态
                };

                // 写入文件
                File.WriteAllLines(configFilePath, lines);
            }
            catch (Exception)
            {
            }
        }

        // 清除保存的登录信息
        private void ClearSavedLoginInfo()
        {
            try
            {
                if (File.Exists(configFilePath))
                {
                    // 只清除密码，保留用户名
                    string[] lines = File.ReadAllLines(configFilePath);
                    if (lines.Length >= 1)
                    {
                        string username = DecryptString(lines[0]);
                        string[] newLines = new string[]
                        {
                            EncryptString(username), // 保留用户名
                            "", // 清空密码
                            "False" // 不记住密码
                        };
                        File.WriteAllLines(configFilePath, newLines);
                    }
                }
            }
            catch (Exception ex)
            {
            }
        }

        // 简单的字符串加密（实际项目中应使用更安全的加密方法）
        private string EncryptString(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return "";

            try
            {
                byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                byte[] encryptedBytes = ProtectedData.Protect(plainBytes, entropy, DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(encryptedBytes);
            }
            catch
            {
                return plainText; // 加密失败时返回原文（不推荐，仅作演示）
            }
        }

        // 简单的字符串解密
        private string DecryptString(string encryptedText)
        {
            if (string.IsNullOrEmpty(encryptedText)) return "";

            try
            {
                byte[] encryptedBytes = Convert.FromBase64String(encryptedText);
                byte[] plainBytes = ProtectedData.Unprotect(encryptedBytes, entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch
            {
                return ""; // 解密失败时返回空字符串
            }
        }

        // 窗口拖动功能
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        // 关闭按钮点击事件
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        // 最小化按钮点击事件
        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        // 最大化按钮点击事件
        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = this.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            // 当文本框获得焦点时，改变边框颜色
            if (sender is TextBox textBox)
            {
                textBox.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 120, 212));
            }
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            // 当文本框失去焦点时，恢复默认边框颜色
            if (sender is TextBox textBox)
            {
                textBox.BorderBrush = new SolidColorBrush(Color.FromRgb(171, 171, 171));
            }
        }

        private void PasswordBox_GotFocus(object sender, RoutedEventArgs e)
        {
            // 当密码框获得焦点时，改变边框颜色
            if (sender is PasswordBox passwordBox)
            {
                passwordBox.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 120, 212));
            }
        }

        private void PasswordBox_LostFocus(object sender, RoutedEventArgs e)
        {
            // 当密码框失去焦点时，恢复默认边框颜色
            if (sender is PasswordBox passwordBox)
            {
                passwordBox.BorderBrush = new SolidColorBrush(Color.FromRgb(171, 171, 171));
            }
        }

        // 用户名文本框文本变化事件
        private void UsernameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // 清除错误消息
            if (!string.IsNullOrWhiteSpace(UsernameTextBox.Text))
            {
                ClearErrorMessage();
            }

            // 自动保存用户名（不等待登录）
            if (!string.IsNullOrWhiteSpace(UsernameTextBox.Text))
            {
                SaveUsernameOnly();
            }

            // 根据输入状态重新设置焦点
            Dispatcher.BeginInvoke(new Action(() =>
            {
                SetFocusAfterInputChange();
            }));
        }

        // 密码框密码变化事件
        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            // 当密码框内容变化时，同步到文本框（如果文本框是可见的）
            if (isPasswordVisible)
            {
                PasswordTextBox.Text = PasswordBox.Password;
            }

            // 清除错误消息
            if (!string.IsNullOrWhiteSpace(PasswordBox.Password))
            {
                ClearErrorMessage();
            }

            // 根据输入状态重新设置焦点
            Dispatcher.BeginInvoke(new Action(() =>
            {
                SetFocusAfterInputChange();
            }));
        }

        // 密码文本框文本变化事件
        private void PasswordTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // 当文本框内容变化时，同步到密码框
            if (isPasswordVisible)
            {
                PasswordBox.Password = PasswordTextBox.Text;
            }

            // 清除错误消息
            if (!string.IsNullOrWhiteSpace(PasswordTextBox.Text))
            {
                ClearErrorMessage();
            }

            // 根据输入状态重新设置焦点
            Dispatcher.BeginInvoke(new Action(() =>
            {
                SetFocusAfterInputChange();
            }));
        }

        // 输入变化后智能设置焦点
        private void SetFocusAfterInputChange()
        {
            // 获取当前焦点元素
            IInputElement focusedElement = FocusManager.GetFocusedElement(this);

            // 如果当前焦点在关闭按钮或显示密码按钮上，不改变焦点
            if (focusedElement == CloseButton || focusedElement == ShowPasswordButton || focusedElement == RememberPasswordCheckBox)
            {
                return;
            }

            bool hasUsername = !string.IsNullOrWhiteSpace(UsernameTextBox.Text);
            bool hasPassword = !string.IsNullOrWhiteSpace(
                isPasswordVisible ? PasswordTextBox.Text : PasswordBox.Password);

            // 如果当前焦点在用户名输入框且用户正在输入，不改变焦点
            if (focusedElement == UsernameTextBox && Keyboard.FocusedElement == UsernameTextBox)
            {
                // 只有当用户完成输入（例如按Tab键）时才自动切换焦点
                return;
            }

            // 如果当前焦点在密码输入框且用户正在输入，不改变焦点
            if ((focusedElement == PasswordBox || focusedElement == PasswordTextBox) &&
                (Keyboard.FocusedElement == PasswordBox || Keyboard.FocusedElement == PasswordTextBox))
            {
                return;
            }

            // 根据输入状态设置焦点
            if (!hasUsername)
            {
                // 没有用户名时，如果焦点不在用户名输入框，则设置焦点
                if (focusedElement != UsernameTextBox)
                {
                    UsernameTextBox.Focus();
                }
            }
            else if (!hasPassword)
            {
                // 有用户名但没有密码时，如果焦点不在密码输入相关控件，则设置焦点
                if (isPasswordVisible && focusedElement != PasswordTextBox)
                {
                    PasswordTextBox.Focus();
                }
                else if (!isPasswordVisible && focusedElement != PasswordBox)
                {
                    PasswordBox.Focus();
                }
            }
            else
            {
                // 用户名和密码都有时，如果焦点不在登录按钮，则设置焦点
                if (focusedElement != LoginButton)
                {
                    LoginButton.Focus();
                }
            }
        }

        // 显示/隐藏密码按钮点击事件
        private void ShowPasswordButton_Click(object sender, RoutedEventArgs e)
        {
            isPasswordVisible = !isPasswordVisible;
            UpdateShowPasswordButton();

            if (isPasswordVisible)
            {
                // 切换到显示密码文本框
                PasswordTextBox.Text = PasswordBox.Password;
                PasswordTextBox.Visibility = Visibility.Visible;
                PasswordBox.Visibility = Visibility.Collapsed;

                // 设置焦点到密码文本框
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    PasswordTextBox.Focus();
                    PasswordTextBox.SelectAll();

                    // 检查是否需要将焦点设置到登录按钮
                    SetFocusAfterInputChange();
                }));
            }
            else
            {
                // 切换到密码框
                PasswordBox.Password = PasswordTextBox.Text;
                PasswordBox.Visibility = Visibility.Visible;
                PasswordTextBox.Visibility = Visibility.Collapsed;

                // 设置焦点到密码框
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    PasswordBox.Focus();

                    // 检查是否需要将焦点设置到登录按钮
                    SetFocusAfterInputChange();
                }));
            }
        }

        // 更新显示密码按钮状态
        private void UpdateShowPasswordButton()
        {
            if (isPasswordVisible)
            {
                ShowPasswordButton.Content = "隐藏密码";
            }
            else
            {
                ShowPasswordButton.Content = "显示密码";
            }
        }

        // 只保存用户名（不保存密码）
        private void SaveUsernameOnly()
        {
            try
            {
                string username = UsernameTextBox.Text;
                bool rememberPassword = RememberPasswordCheckBox.IsChecked == true;

                if (File.Exists(configFilePath))
                {
                    string[] lines = File.ReadAllLines(configFilePath);
                    string encryptedPassword = lines.Length >= 2 ? lines[1] : "";
                    string rememberStatus = lines.Length >= 3 ? lines[2] : "False";

                    // 只更新用户名，不更新密码
                    string[] newLines = new string[]
                    {
                EncryptString(username),
                rememberPassword ? encryptedPassword : "",
                rememberPassword.ToString()
                    };

                    File.WriteAllLines(configFilePath, newLines);
                }
                else
                {
                    // 创建新的配置文件
                    string[] lines = new string[]
                    {
                EncryptString(username),
                "",
                "False"
                    };

                    File.WriteAllLines(configFilePath, lines);
                }
            }
            catch (Exception ex)
            {
            }
        }

        // 记住密码复选框选中事件
        private void RememberPasswordCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            // 当选中"记住密码"时，立即保存当前密码（如果密码不为空）
            if (!string.IsNullOrEmpty(
                isPasswordVisible ? PasswordTextBox.Text : PasswordBox.Password))
            {
                SaveLoginInfo();
            }
        }

        // 记住密码复选框取消选中事件
        private void RememberPasswordCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            // 当取消选中"记住密码"时，清除保存的密码
            ClearSavedLoginInfo();
        }

        // 清除错误消息
        private void ClearErrorMessage()
        {
            if (MessageTextBlock.Text != "")
            {
                MessageTextBlock.Text = "";
                MessageTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(255, 0, 0));
            }
        }

        // 登录按钮点击事件
      
        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string username = UsernameTextBox.Text;
            string password = isPasswordVisible ? PasswordTextBox.Text : PasswordBox.Password;

            // 简单的验证逻辑
            if (string.IsNullOrWhiteSpace(username))
            {
                ShowErrorMessage("请输入用户名");
                UsernameTextBox.Focus();
                UsernameTextBox.SelectAll();
                return;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                ShowErrorMessage("请输入密码");
                if (isPasswordVisible)
                {
                    PasswordTextBox.Focus();
                    PasswordTextBox.SelectAll();
                }
                else
                {
                    PasswordBox.Focus();
                }
                return;
            }

            // 使用UserDataManager验证用户
            var (success, user, message) = UserDataManager.ValidateUser(username, password);

            // 将 MainWindow 构造函数调用改为先判空 user
            if (success && user != null)
            {
                ShowSuccessMessage("登录成功！正在跳转...");

                // 保存登录信息（根据是否记住密码）
                SaveLoginInfo();

                // 创建主窗口并传递用户信息
                MainWindow mainWindow = new MainWindow(user.Username, user.UserRole);
                mainWindow.Show();

                // 关闭登录窗口
                this.Close();
            }
            else
            {
                ShowErrorMessage(message);

                // 清空密码
                PasswordBox.Password = "";
                PasswordTextBox.Text = "";

                // 重新设置焦点到密码输入框
                if (isPasswordVisible)
                {
                    PasswordTextBox.Focus();
                }
                else
                {
                    PasswordBox.Focus();
                }
            }
        }


        // 显示错误消息
        private void ShowErrorMessage(string message)
        {
            MessageTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(255, 0, 0));
            MessageTextBlock.Text = message;
        }

        // 显示成功消息
        private void ShowSuccessMessage(string message)
        {
            MessageTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(0, 120, 0));
            MessageTextBlock.Text = message;
        }

        // 添加键盘快捷键支持
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            // Tab键切换焦点时，智能跳转到下一个合适的控件
            if (e.Key == Key.Tab)
            {
                e.Handled = true;
                HandleTabNavigation();
                return;
            }

            // Enter键触发登录
            if (e.Key == Key.Enter)
            {
                LoginButton_Click(this, new RoutedEventArgs());
            }
            // Esc键关闭窗口
            else if (e.Key == Key.Escape)
            {
                this.Close();
            }
        }

        // 处理Tab键导航
        private void HandleTabNavigation()
        {
            IInputElement focusedElement = FocusManager.GetFocusedElement(this);

            // 根据当前焦点决定下一个焦点
            if (focusedElement == UsernameTextBox)
            {
                // 从用户名输入框，根据是否有密码决定下一个焦点
                bool hasPassword = !string.IsNullOrWhiteSpace(
                    isPasswordVisible ? PasswordTextBox.Text : PasswordBox.Password);

                if (!hasPassword)
                {
                    // 没有密码，跳转到密码输入框
                    if (isPasswordVisible)
                    {
                        PasswordTextBox.Focus();
                    }
                    else
                    {
                        PasswordBox.Focus();
                    }
                }
                else
                {
                    // 有密码，跳转到登录按钮
                    LoginButton.Focus();
                }
            }
            else if (focusedElement == PasswordBox || focusedElement == PasswordTextBox)
            {
                // 从密码输入框，跳转到登录按钮
                LoginButton.Focus();
            }
            else if (focusedElement == LoginButton)
            {
                // 从登录按钮，跳转到用户名输入框
                UsernameTextBox.Focus();
                UsernameTextBox.SelectAll();
            }
            else if (focusedElement == ShowPasswordButton || focusedElement == RememberPasswordCheckBox)
            {
                // 从其他控件，跳转到合适的输入控件
                bool hasUsername = !string.IsNullOrWhiteSpace(UsernameTextBox.Text);
                bool hasPassword = !string.IsNullOrWhiteSpace(
                    isPasswordVisible ? PasswordTextBox.Text : PasswordBox.Password);

                if (!hasUsername)
                {
                    UsernameTextBox.Focus();
                }
                else if (!hasPassword)
                {
                    if (isPasswordVisible)
                    {
                        PasswordTextBox.Focus();
                    }
                    else
                    {
                        PasswordBox.Focus();
                    }
                }
                else
                {
                    LoginButton.Focus();
                }
            }
            else
            {
                // 默认情况：跳转到用户名输入框
                UsernameTextBox.Focus();
            }
        }
    }
}