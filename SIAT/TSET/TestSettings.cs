using System;
using System.IO;
using System.Xml.Serialization;

namespace SIAT.TSET
{
    /// <summary>
    /// 启动方式枚举
    /// </summary>
    public enum StartMode
    {
        /// <summary>
        /// 软件启动（手动点击按钮）
        /// </summary>
        Software,
        
        /// <summary>
        /// 条码启动（扫描条码后自动启动）
        /// </summary>
        Barcode,
        
        /// <summary>
        /// 工装启动（工装板发送信号启动）
        /// </summary>
        Tooling
    }

    /// <summary>
    /// 测试设置类
    /// </summary>
    [Serializable]
    public class TestSettings
    {
        private static readonly string SettingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestSettings.xml");
        private static TestSettings _instance;
        
        /// <summary>
        /// 单例实例
        /// </summary>
        public static TestSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Load();
                }
                return _instance;
            }
        }
        
        /// <summary>
        /// 启动方式
        /// </summary>
        public StartMode StartMode { get; set; } = StartMode.Software;
        
        /// <summary>
        /// 条码长度
        /// </summary>
        public int BarcodeLength { get; set; } = 10;
        
        /// <summary>
        /// 工装板通讯端口
        /// </summary>
        public string ToolingPort { get; set; } = string.Empty;
        
        /// <summary>
        /// 工装板通讯波特率
        /// </summary>
        public int ToolingBaudRate { get; set; } = 38400;
        
        /// <summary>
        /// CAN设备名称（保留兼容旧版本）
        /// </summary>
        public string CanDeviceName { get; set; } = string.Empty;
        
        /// <summary>
        /// 治具卡通讯端口
        /// </summary>
        public string ToolingJigPort { get; set; } = string.Empty;
        
        /// <summary>
        /// 治具卡通讯波特率
        /// </summary>
        public int ToolingJigBaudRate { get; set; } = 38400;
        
        /// <summary>
        /// 加载测试设置
        /// </summary>
        /// <returns>测试设置实例</returns>
        public static TestSettings Load()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    using (var stream = new FileStream(SettingsFilePath, FileMode.Open))
                    {
                        var serializer = new XmlSerializer(typeof(TestSettings));
                        return (TestSettings)serializer.Deserialize(stream);
                    }
                }
            }
            catch (Exception ex)
            {
                // 加载失败，返回默认设置
            }
            
            // 返回默认设置
            return new TestSettings();
        }
        
        /// <summary>
        /// 保存测试设置
        /// </summary>
        public void Save()
        {
            try
            {
                using (var stream = new FileStream(SettingsFilePath, FileMode.Create))
                {
                    var serializer = new XmlSerializer(typeof(TestSettings));
                    serializer.Serialize(stream, this);
                }
                
                // 更新单例实例
                _instance = this;
            }
            catch (Exception ex)
            {
                throw new Exception($"保存测试设置失败: {ex.Message}", ex);
            }
        }
    }
}
