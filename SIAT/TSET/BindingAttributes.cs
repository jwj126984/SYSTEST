using System;

namespace SIAT.TSET
{
    /// <summary>
    /// 输入绑定特性，用于定义插件步骤的输入参数
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class InputBindingAttribute : Attribute
    {
        /// <summary>
        /// 输入变量名称
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 输入变量描述
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="name">输入变量名称</param>
        /// <param name="description">输入变量描述</param>
        public InputBindingAttribute(string name, string description)
        {
            Name = name;
            Description = description;
        }
    }

    /// <summary>
    /// 输出绑定特性，用于定义插件步骤的输出参数
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class OutputBindingAttribute : Attribute
    {
        /// <summary>
        /// 输出变量名称
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 输出变量描述
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="name">输出变量名称</param>
        /// <param name="description">输出变量描述</param>
        public OutputBindingAttribute(string name, string description)
        {
            Name = name;
            Description = description;
        }
    }
}