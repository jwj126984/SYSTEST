using System;
using System.IO;
using System.Xml.Serialization;

namespace SIAT
{
    public static class XmlHelper
    {
        public static bool SerializeToFile<T>(T obj, string filePath)
        {
            try
            {
                var settings = new System.Xml.XmlWriterSettings
                {
                    Encoding = System.Text.Encoding.UTF8,
                    Indent = true
                };
                
                using (var writer = System.Xml.XmlWriter.Create(filePath, settings))
                {
                    var serializer = new XmlSerializer(typeof(T));
                    serializer.Serialize(writer, obj);
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"序列化失败: {ex.Message}");
                return false;
            }
        }

        public static T DeserializeFromFile<T>(string filePath) where T : new()
        {
            try
            {
                if (!File.Exists(filePath))
                    return new T();

                var settings = new System.Xml.XmlReaderSettings
                {
                    IgnoreWhitespace = true
                };
                
                using (var reader = System.Xml.XmlReader.Create(filePath, settings))
                {
                    var serializer = new XmlSerializer(typeof(T));
                    var result = serializer.Deserialize(reader);
                    return result != null ? (T)result : new T();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"反序列化失败: {ex.Message}");
                return new T();
            }
        }
    }
}