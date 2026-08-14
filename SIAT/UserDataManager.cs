using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;

namespace SIAT
{
    public static class UserDataManager
    {
        private static readonly string AppDataPath = System.IO.Directory.GetCurrentDirectory();

        private static readonly string UsersFilePath = Path.Combine(AppDataPath, "users.json");

        // 用户列表（使用静态构造函数确保初始化）
        public static ObservableCollection<User> Users { get; private set; }

        // 静态构造函数
        static UserDataManager()
        {
            Users = new ObservableCollection<User>();
            LoadUsers();
        }

        // 初始化默认用户
        private static void InitializeDefaultUsers()
        {
            Users.Clear();

            // 添加默认管理员用户
            Users.Add(new User
            {
                Username = "YC",
                Password = "123456",
                UserRole = "管理人员",
                IsActive = true
            });

            // 添加默认生产人员用户
            Users.Add(new User
            {
                Username = "USER",
                Password = "123",
                UserRole = "生产人员",
                IsActive = true
            });

            // 添加默认测试用户
            Users.Add(new User
            {
                Username = "TEST",
                Password = "123",
                UserRole = "生产人员",
                IsActive = true
            });

            SaveUsers(); // 保存默认用户
        }

        // 加载用户数据
        public static void LoadUsers()
        {
            try
            {
                // 确保应用数据目录存在
                if (!Directory.Exists(AppDataPath))
                {
                    Directory.CreateDirectory(AppDataPath);
                }

                // 检查用户数据文件是否存在
                if (File.Exists(UsersFilePath))
                {
                    string json = File.ReadAllText(UsersFilePath);

                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        var options = new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true,
                            WriteIndented = true
                        };

                        var loadedUsers = JsonSerializer.Deserialize<User[]>(json, options);

                        if (loadedUsers != null && loadedUsers.Length > 0)
                        {
                            Users.Clear();
                            foreach (var user in loadedUsers)
                            {
                                Users.Add(user);
                            }
                            return;
                        }
                    }
                }

                // 如果文件不存在或内容为空，初始化默认用户
                InitializeDefaultUsers();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载用户数据失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                InitializeDefaultUsers(); // 出错时初始化默认用户
            }
        }

        // 保存用户数据
        public static void SaveUsers()
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                string json = JsonSerializer.Serialize(Users, options);
                File.WriteAllText(UsersFilePath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存用户数据失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 验证用户登录
        public static (bool success, User? user, string message) ValidateUser(string username, string password)
        {
            var user = Users.FirstOrDefault(u =>
                u.Username.Equals(username, StringComparison.OrdinalIgnoreCase) &&
                u.IsActive);

            if (user == null)
            {
                return (false, null, "用户不存在或已被禁用");
            }

            if (user.Password == password)
            {
                // 更新最后登录时间
                user.UpdateLastLoginTime();
                SaveUsers();
                return (true, user, "登录成功");
            }

            return (false, null, "密码错误");
        }

        // 添加新用户
        public static bool AddUser(User newUser)
        {
            if (Users.Any(u => u.Username.Equals(newUser.Username, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            Users.Add(newUser);
            SaveUsers();
            return true;
        }

        // 更新用户信息
        public static bool UpdateUser(User updatedUser)
        {
            var existingUser = Users.FirstOrDefault(u =>
                u.Username.Equals(updatedUser.Username, StringComparison.OrdinalIgnoreCase));

            if (existingUser == null)
            {
                return false;
            }

            // 保留原始创建时间和最后登录时间
            updatedUser.CreateTime = existingUser.CreateTime;
            updatedUser.LastLoginTime = existingUser.LastLoginTime;

            // 确保密码被正确传递
            if (string.IsNullOrEmpty(updatedUser.Password))
            {
                // 如果密码为空，保留原密码
                updatedUser.Password = existingUser.Password;
            }

            // 更新用户信息
            Users.Remove(existingUser);
            Users.Add(updatedUser);
            SaveUsers();
            return true;
        }

        // 删除用户
        public static bool DeleteUser(string username)
        {
            var user = Users.FirstOrDefault(u =>
                u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));

            if (user == null)
            {
                return false;
            }

            // 防止删除最后一个管理员
            if (user.UserRole == "管理人员" &&
                Users.Count(u => u.UserRole == "管理人员" && u.IsActive) <= 1)
            {
                MessageBox.Show("不能删除最后一个管理员账户", "操作失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            Users.Remove(user);
            SaveUsers();
            return true;
        }

        // 获取用户角色列表
        public static string[] GetUserRoles()
        {
            return new string[] { "管理人员", "生产人员" };
        }

        // 检查是否为管理员
        public static bool IsAdmin(User user)
        {
            return user != null && user.UserRole == "管理人员";
        }

        // 获取所有用户（返回副本）
        public static ObservableCollection<User> GetAllUsers()
        {
            return new ObservableCollection<User>(Users.Select(u => new User(u)));
        }

        // 检查用户名是否存在
        public static bool UserExists(string username)
        {
            return Users.Any(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
        }
    }
}