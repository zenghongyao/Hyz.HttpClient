using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Hyz.HttpClient
{
    /// <summary>
    /// 请求拦截上下文
    /// </summary>
    public class RequestInterceptionContext
    {
        /// <summary>
        /// 请求唯一标识符（自动生成）
        /// </summary>
        public string RequestId { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>
        /// 分布式追踪 ID（优先复用 Activity.Current.Id，否则自动生成）
        /// </summary>
        /// <remarks>
        /// 支持 W3C TraceContext 标准，外部链路追踪系统（如 Zipkin/Jaeger）可据此关联调用链
        /// </remarks>
        public string TraceId { get; set; } = Activity.Current?.Id ?? Guid.NewGuid().ToString("N");

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
        /// 请求时间（UTC）
        /// </summary>
        public DateTime RequestTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 自定义数据（可用于传递额外信息）
        /// </summary>
        public IDictionary<string, object>? Items { get; set; }
    }
}
