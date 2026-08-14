using System;
using System.IO;
using System.Xml.Serialization;
using System.Windows;

namespace SIAT.TSET
{
    [Serializable]
    public class TestStatistics
    {
        public int TotalTestCount { get; set; }
        public int TotalPassedCount { get; set; }
        public int TotalFailedCount { get; set; }
        public DateTime LastUpdateTime { get; set; }
        public string? Version { get; set; }

        public TestStatistics()
        {
            TotalTestCount = 0;
            TotalPassedCount = 0;
            TotalFailedCount = 0;
            LastUpdateTime = DateTime.Now;
            Version = "1.0.0";
        }

        public void SaveToFile(string filePath)
        {
            try
            {
                LastUpdateTime = DateTime.Now;
                XmlSerializer serializer = new XmlSerializer(typeof(TestStatistics));

                // 确保目录存在
                string? directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using (StreamWriter writer = new StreamWriter(filePath))
                {
                    serializer.Serialize(writer, this);
                }
            }
            catch (Exception ex)
            {
                // 记录错误但不影响程序运行
                System.Diagnostics.Debug.WriteLine($"保存统计信息失败: {ex.Message}");
            }
        }

        public static TestStatistics LoadFromFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(TestStatistics));
                    using (StreamReader reader = new StreamReader(filePath))
                    {
                        var result = serializer.Deserialize(reader) as TestStatistics;
                        return result ?? new TestStatistics();
                    }
                }
            }
            catch (Exception ex)
            {
                // 如果加载失败，记录错误并返回默认值
                System.Diagnostics.Debug.WriteLine($"加载统计信息失败: {ex.Message}");
                // 尝试修复或备份损坏的文件
                TryBackupCorruptedFile(filePath);
            }
            return new TestStatistics();
        }

        private static void TryBackupCorruptedFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    string backupPath = filePath + ".bak";
                    if (File.Exists(backupPath))
                    {
                        File.Delete(backupPath);
                    }
                    File.Move(filePath, backupPath);
                    System.Diagnostics.Debug.WriteLine($"已备份损坏的统计文件: {backupPath}");
                }
            }
            catch
            {
                // 忽略备份失败
            }
        }

        public double CalculatePassRate()
        {
            if (TotalTestCount == 0) return 0;
            return Math.Round((double)TotalPassedCount / TotalTestCount * 100, 2);
        }
    }
}