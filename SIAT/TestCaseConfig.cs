using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace SIAT
{
    /// <summary>
    /// 用例配置类
    /// </summary>
    [Serializable]
    public class TestCaseConfig
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime ModifiedDate { get; set; } = DateTime.Now;
        
        [XmlArray("Projects")]
        [XmlArrayItem("Project")]
        public List<TestCaseProject> Projects { get; set; } = new List<TestCaseProject>();

        public TestCaseConfig()
        {
        }

        public TestCaseConfig(string name, string description)
        {
            Name = name;
            Description = description;
            CreatedDate = DateTime.Now;
            ModifiedDate = DateTime.Now;
        }
    }

    /// <summary>
    /// 用例中的项目项
    /// </summary>
    [Serializable]
    public class TestCaseProject
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ProjectPath { get; set; } = string.Empty;
        public DateTime AddedDate { get; set; } = DateTime.Now;
        public int Order { get; set; } = 0;

        [XmlArray("Variables")]
        [XmlArrayItem("Variable")]
        public List<ProjectVariable> Variables { get; set; } = new List<ProjectVariable>();

        /// <summary>
        /// 模板源路径：从模板复制而来但尚未保存创建物理文件夹时，记录模板源
        /// </summary>
        [XmlIgnore]
        public string SourceTemplatePath { get; set; } = string.Empty;

        /// <summary>
        /// 是否待创建物理文件夹：添加后未保存时为true，保存后清除
        /// </summary>
        [XmlIgnore]
        public bool IsPendingCreate { get; set; } = false;

        /// <summary>
        /// 新建空白测试项的描述：用于保存时创建空白项目
        /// </summary>
        [XmlIgnore]
        public bool IsNewBlank { get; set; } = false;

        public TestCaseProject()
        {
        }

        public TestCaseProject(string name, string description, string projectPath)
        {
            Name = name;
            Description = description;
            ProjectPath = projectPath;
            AddedDate = DateTime.Now;
            Order = 0;
        }
    }
}