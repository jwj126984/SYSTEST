using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SIAT
{
    public class User : INotifyPropertyChanged
    {
        private string _username = string.Empty;
        private string _password = string.Empty;
        private string _userRole = string.Empty;
        private bool _isActive;
        private DateTime _createTime;
        private DateTime _lastLoginTime;

        public string Username
        {
            get => _username;
            set
            {
                _username = value;
                OnPropertyChanged();
            }
        }

        public string Password
        {
            get => _password;
            set
            {
                _password = value;
                OnPropertyChanged();
            }
        }

        public string UserRole
        {
            get => _userRole;
            set
            {
                _userRole = value;
                OnPropertyChanged();
            }
        }

        public bool IsActive
        {
            get => _isActive;
            set
            {
                _isActive = value;
                OnPropertyChanged();
            }
        }

        public DateTime CreateTime
        {
            get => _createTime;
            set
            {
                _createTime = value;
                OnPropertyChanged();
            }
        }

        public DateTime LastLoginTime
        {
            get => _lastLoginTime;
            set
            {
                _lastLoginTime = value;
                OnPropertyChanged();
            }
        }

        // 构造函数
        public User()
        {
            CreateTime = DateTime.Now;
            LastLoginTime = DateTime.MinValue;
            IsActive = true;
        }

        public User(string username, string password, string userRole, bool isActive = true)
        {
            Username = username;
            Password = password;
            UserRole = userRole;
            IsActive = isActive;
            CreateTime = DateTime.Now;
            LastLoginTime = DateTime.MinValue;
        }

        // 复制构造函数
        // 复制构造函数 - 确保所有属性都被正确复制
        public User(User other)
        {
            if (other == null) return;

            Username = other.Username;
            Password = other.Password; // 确保密码被复制
            UserRole = other.UserRole;
            IsActive = other.IsActive;
            CreateTime = other.CreateTime;
            LastLoginTime = other.LastLoginTime;
        }

        // 更新最后登录时间
        public void UpdateLastLoginTime()
        {
            LastLoginTime = DateTime.Now;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}