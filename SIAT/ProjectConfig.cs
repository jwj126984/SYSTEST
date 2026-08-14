using System;
using System.Xml.Serialization;

namespace SIAT
{
    [Serializable]
    public class ProjectConfig
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ProjectPath { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime ModifiedDate { get; set; } = DateTime.Now;

        public ProjectConfig()
        {
        }

        public ProjectConfig(string name, string description)
        {
            Name = name;
            Description = description;
            CreatedDate = DateTime.Now;
            ModifiedDate = DateTime.Now;
        }

        public ProjectConfig(string name, string description, string projectPath)
        {
            Name = name;
            Description = description;
            ProjectPath = projectPath;
            CreatedDate = DateTime.Now;
            ModifiedDate = DateTime.Now;
        }
    }
}