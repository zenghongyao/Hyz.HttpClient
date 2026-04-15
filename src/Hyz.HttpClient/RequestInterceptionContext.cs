using System;
using System.Collections.Generic;

namespace Hyz.HttpClient
{
    /// <summary>
    /// 请求拦截上下文
    /// </summary>
    public class RequestInterceptionContext
    {
        /// <summary>
        /// 请求API地址
        /// </summary>
        public string RequestApi { get; set; } = string.Empty;

        /// <summary>
        /// 完整请求URL（包含查询参数）
        /// </summary>
        public string FullUrl { get; set; } = string.Empty;

        /// <summary>
        /// HTTP方法
        /// </summary>
        public string HttpMethod { get; set; } = string.Empty;

        /// <summary>
        /// 请求头
        /// </summary>
        public IDictionary<string, string>? Headers { get; set; }

        /// <summary>
        /// 查询参数
        /// </summary>
        public IDictionary<string, string>? QueryParameters { get; set; }

        /// <summary>
        /// 请求体（JSON字符串）
        /// </summary>
        public string? BodyJson { get; set; }

        /// <summary>
        /// 请求体对象
        /// </summary>
        public object? Body { get; set; }

        /// <summary>
        /// 请求时间
        /// </summary>
        public DateTime RequestTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 自定义数据（可用于传递额外信息）
        /// </summary>
        public IDictionary<string, object>? Items { get; set; }
    }
}
