using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SIAT
{
    public partial class SystemSettingsWindow : Window, INotifyPropertyChanged
    {
        private User _selectedUser = new User();
        private ObservableCollection<User> _users = new ObservableCollection<User>();
        private string _password = string.Empty;
        private bool _isNewUser = false;

        public User SelectedUser
        {
            get => _selectedUser;
            set
            {
                _selectedUser = value;
                OnPropertyChanged();
                UpdateButtonStates();
                UpdateStatusText();
            }
        }

        public ObservableCollection<User> Users
        {
            get => _users;
            set
            {
                _users = value;
                OnPropertyChanged();
                UpdateTotalUsersText();
            }
        }

        public string Password
        {
            get => _password;
            set
            {
                _password = value;
                OnPropertyChanged();
                UpdateSaveButtonState();
            }
        }

        public string[] UserRoles => UserDataManager.GetUserRoles();

        public SystemSettingsWindow()
        {
            InitializeComponent();
            DataContext = this;

            // 初始化UI状态
            InitializeUI();
        }

        private void InitializeUI()
        {
            // 加载用户角色
            UserRoleComboBox.ItemsSource = UserRoles;

            // 加载用户列表
            LoadUsers();

            // 初始状态
            UpdateButtonStates();
            UpdateStatusText();

            // 启用窗口拖动功能
            this.MouseDown += Window_MouseDown;
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
        private void WindowCloseButton_Click(object sender, RoutedEventArgs e)
        {
            // 如果有未保存的修改，提示用户
            if (_isNewUser && (!string.IsNullOrWhiteSpace(SelectedUser?.Username) || !string.IsNullOrWhiteSpace(Password)))
            {
                var result = MessageBox.Show("有未保存的用户信息，确定要关闭吗？", "确认关闭",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.No)
                {
                    return;
                }
            }

            // 关闭登录窗口
            this.Close();

        }

        // 最小化按钮点击事件
        private void WindowMinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        // 最大化按钮点击事件
        private void WindowMaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
                this.WindowState = WindowState.Normal;
            else
                this.WindowState = WindowState.Maximized;
        }

        private void LoadUsers()
        {
            try
            {
                // 获取用户数据
                var userList = UserDataManager.GetAllUsers();

                // 更新Users集合
                Users = new ObservableCollection<User>(userList);

                UpdateTotalUsersText();

                // 设置初始选择
                if (Users.Count > 0)
                {
                    SelectUser(Users[0]);
                }
                else
                {
                    SelectedUser = new User();
                    Password = "";
                }

                StatusTextBlock.Text = "数据加载完成";
                StatusTextBlock.Foreground = Brushes.Green;
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"加载数据失败: {ex.Message}";
                StatusTextBlock.Foreground = Brushes.Red;
                MessageBox.Show($"加载用户数据失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SelectUser(User user)
        {
            SelectedUser = new User(user);
            Password = user.Password; // 显示用户的实际密码
            _isNewUser = false;

            // 在DataGrid中选中对应的行
            if (UsersDataGrid.Items != null)
            {
                for (int i = 0; i < UsersDataGrid.Items.Count; i++)
                {
                    if (UsersDataGrid.Items[i] is User u && u.Username == user.Username)
                    {
                        UsersDataGrid.SelectedIndex = i;
                        break;
                    }
                }
            }

            UpdateButtonStates();
            UpdateStatusText();
        }

        private void UpdateButtonStates()
        {
            bool hasSelection = SelectedUser != null;
            bool isValidUser = hasSelection && !string.IsNullOrWhiteSpace(SelectedUser?.Username);

            // 保存按钮状态
            SaveButton.IsEnabled = isValidUser;

            // 删除按钮状态
            if (isValidUser && !_isNewUser)
            {
                bool isLastAdmin = SelectedUser?.UserRole == "管理人员" &&
                                  Users.Count(u => u.UserRole == "管理人员" && u.IsActive) <= 1;
                DeleteButton.IsEnabled = !isLastAdmin;
            }
            else
            {
                DeleteButton.IsEnabled = false;
            }

            // 用户名文本框状态：只有新增时可以编辑
            UsernameTextBox.IsEnabled = _isNewUser;
        }

        private void UpdateSaveButtonState()
        {
            if (_isNewUser)
            {
                // 对于新用户，用户名和密码都不能为空
                bool hasUsername = !string.IsNullOrWhiteSpace(SelectedUser?.Username);
                bool hasPassword = !string.IsNullOrWhiteSpace(Password);
                SaveButton.IsEnabled = hasUsername && hasPassword;
            }
            else
            {
                // 对于现有用户，只需要用户名不为空
                bool hasUsername = !string.IsNullOrWhiteSpace(SelectedUser?.Username);
                SaveButton.IsEnabled = hasUsername;
            }
        }

        private void UpdateStatusText()
        {
            if (_isNewUser)
            {
                StatusTextBlock.Text = "正在创建新用户";
                StatusTextBlock.Foreground = Brushes.Blue;
            }
            else if (SelectedUser != null && !string.IsNullOrWhiteSpace(SelectedUser.Username))
            {
                StatusTextBlock.Text = $"编辑用户: {SelectedUser.Username}";
                StatusTextBlock.Foreground = Brushes.Green;
            }
            else
            {
                StatusTextBlock.Text = "请选择或创建用户";
                StatusTextBlock.Foreground = Brushes.Gray;
            }
        }

        private void UpdateTotalUsersText()
        {
            if (Users != null)
            {
                int total = Users.Count;
                int active = Users.Count(u => u.IsActive);
                int admins = Users.Count(u => u.UserRole == "管理人员");

                TotalUsersText.Text = $"共 {total} 个用户，{active} 个启用，{admins} 个管理员";
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LoadUsers();
                StatusTextBlock.Text = "数据已刷新";
                StatusTextBlock.Foreground = Brushes.Green;
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"刷新失败: {ex.Message}";
                StatusTextBlock.Foreground = Brushes.Red;
            }
        }

        private void AddUserButton_Click(object sender, RoutedEventArgs e)
        {
            // 创建新用户对象
            SelectedUser = new User
            {
                Username = "",
                Password = "",
                UserRole = UserRoles.FirstOrDefault() ?? "生产人员",
                IsActive = true
            };

            // 清空密码框
            Password = "";

            // 设置标志
            _isNewUser = true;

            // 更新UI状态
            UpdateButtonStates();
            UpdateStatusText();

            // 清除DataGrid选择
            UsersDataGrid.SelectedIndex = -1;

            // 设置焦点到用户名输入框
            Dispatcher.BeginInvoke(new Action(() =>
            {
                UsernameTextBox.Focus();
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 验证用户名
                if (string.IsNullOrWhiteSpace(SelectedUser.Username))
                {
                    MessageBox.Show("请输入用户名", "验证错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    UsernameTextBox.Focus();
                    return;
                }

                // 验证新用户的密码
                if (_isNewUser && string.IsNullOrWhiteSpace(Password))
                {
                    MessageBox.Show("请输入密码", "验证错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    PasswordTextBox.Focus();
                    return;
                }

                // 更新用户密码
                if (!string.IsNullOrWhiteSpace(Password))
                {
                    SelectedUser.Password = Password;
                }
                else if (_isNewUser)
                {
                    // 新用户必须设置密码
                    MessageBox.Show("请输入密码", "验证错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    PasswordTextBox.Focus();
                    return;
                }

                bool result;
                string message;

                if (_isNewUser)
                {
                    // 检查用户名是否已存在
                    if (UserDataManager.UserExists(SelectedUser.Username))
                    {
                        MessageBox.Show($"用户名 '{SelectedUser.Username}' 已存在", "添加失败",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // 添加新用户
                    result = UserDataManager.AddUser(new User(SelectedUser));
                    message = result ? $"用户 '{SelectedUser.Username}' 添加成功" : "添加用户失败";
                }
                else
                {
                    // 更新现有用户
                    result = UserDataManager.UpdateUser(new User(SelectedUser));
                    message = result ? $"用户 '{SelectedUser.Username}' 更新成功" : "更新用户失败";
                }

                if (result)
                {
                    // 刷新列表
                    LoadUsers();

                    // 重置状态
                    _isNewUser = false;

                    // 更新状态
                    StatusTextBlock.Text = message;
                    StatusTextBlock.Foreground = Brushes.Green;

                    MessageBox.Show(message, "操作成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    StatusTextBlock.Text = message;
                    StatusTextBlock.Foreground = Brushes.Red;
                    MessageBox.Show(message, "操作失败", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                string errorMessage = $"保存用户失败: {ex.Message}";
                StatusTextBlock.Text = errorMessage;
                StatusTextBlock.Foreground = Brushes.Red;
                MessageBox.Show(errorMessage, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedUser == null || string.IsNullOrWhiteSpace(SelectedUser.Username))
            {
                MessageBox.Show("请先选择要删除的用户", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (_isNewUser)
            {
                MessageBox.Show("请先保存新用户或取消创建", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 确认删除
            var result = MessageBox.Show($"确定要删除用户 '{SelectedUser.Username}' 吗？此操作无法撤销。",
                "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    bool success = UserDataManager.DeleteUser(SelectedUser.Username);
                    if (success)
                    {
                        // 刷新列表
                        LoadUsers();

                        // 更新状态
                        StatusTextBlock.Text = "用户删除成功";
                        StatusTextBlock.Foreground = Brushes.Green;

                        MessageBox.Show("用户删除成功", "操作成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    string errorMessage = $"删除用户失败: {ex.Message}";
                    StatusTextBlock.Text = errorMessage;
                    StatusTextBlock.Foreground = Brushes.Red;
                    MessageBox.Show(errorMessage, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void UsersDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (UsersDataGrid.SelectedItem is User selectedUser)
            {
                SelectUser(selectedUser);
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.Key == Key.F5)
            {
                // 传递当前对象作为 sender，EventArgs.Empty 作为 e 参数
                RefreshButton_Click(this, new RoutedEventArgs());
            }
            else if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (SaveButton.IsEnabled)
                {
                    SaveButton_Click(this, new RoutedEventArgs());
                }
            }
            else if (e.Key == Key.Delete)
            {
                if (DeleteButton.IsEnabled)
                {
                    DeleteButton_Click(this, new RoutedEventArgs());
                }
            }
            else if (e.Key == Key.Escape)
            {
                // Esc键关闭窗口
                WindowCloseButton_Click(this, new RoutedEventArgs());
            }
        }

        private void PasswordTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateSaveButtonState();
        }

        private void UsernameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateSaveButtonState();
            UpdateStatusText();
        }

        public event PropertyChangedEventHandler ?PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}