using System;
using System.Net.Http.Headers;

namespace Hyz.HttpClient
{
    /// <summary>
    /// 响应拦截上下文
    /// </summary>
    public class ResponseInterceptionContext
    {
        /// <summary>
        /// 请求唯一标识符
        /// </summary>
        public string RequestId { get; set; } = string.Empty;

        /// <summary>
        /// 分布式追踪 ID
        /// </summary>
        public string TraceId { get; set; } = string.Empty;

        /// <summary>
        /// 请求上下文
        /// </summary>
        public RequestInterceptionContext RequestContext { get; set; }

        /// <summary>
        /// 响应头
        /// </summary>
        public HttpResponseHeaders? ResponseHeaders { get; set; }

        /// <summary>
        /// 响应状态描述
        /// </summary>
        public string? ReasonPhrase { get; set; }

        /// <summary>
        /// 响应状态码
        /// </summary>
        public int StatusCode { get; set; }

        /// <summary>
        /// 是否成功
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// 响应内容（JSON字符串）
        /// </summary>
        public string? ResponseContent { get; set; }

        /// <summary>
        /// 响应时间
        /// </summary>
        public DateTime ResponseTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 请求耗时
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// 异常信息（如果请求失败）
        /// </summary>
        public Exception? Exception { get; set; }

        public ResponseInterceptionContext()
        {
            RequestContext = new RequestInterceptionContext();
        }
    }
}
