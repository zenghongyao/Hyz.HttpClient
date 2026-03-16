using System;

namespace Hyz.HttpClient
{
    /// <summary>
    /// 请求参数别名特性，用于指定请求参数的别名
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class RequestParameterAliasAttribute : Attribute
    {
        /// <summary>
        /// 参数别名
        /// </summary>
        public string Alias { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="alias">参数别名</param>
        public RequestParameterAliasAttribute(string alias)
        {
            Alias = alias;
        }
    }
}