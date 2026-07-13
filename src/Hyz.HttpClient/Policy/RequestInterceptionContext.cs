using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;

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
        /// 当前 HTTP 请求消息实例（请求发送前可修改，用于注入 traceparent 等头）
        /// </summary>
        /// <remarks>
        /// 在 <c>OnRequestSending</c> 拦截器中可用，允许拦截器直接操作实际请求头。
        /// 此引用在请求发送前有效，请求发送后不应再修改。
        /// </remarks>
        public HttpRequestMessage? HttpRequest { get; set; }

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
