using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Polly;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace Hyz.HttpClient
{
    /// <summary>
    /// Hyz.HttpClient服务
    /// </summary>
    public class HttpClientRequest
    {
        private readonly ILogger<HttpClientRequest> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly JsonSerializerOptions _jsonSerializerOptions;
        private readonly JsonSerializerOptions _requestSerializerOptions;

        /// <summary>
        /// 构造函数
        /// </summary>
        public HttpClientRequest(
            ILogger<HttpClientRequest> logger,
            IHttpClientFactory httpClientFactory,
            JsonSerializerOptions jsonSerializerOptions)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _jsonSerializerOptions = jsonSerializerOptions ?? HttpClientPolicy.DefaultJsonOptions;            
            // 为请求体序列化创建单独的选项，确保不使用属性命名策略，保持自定义特性设置的属性名
            _requestSerializerOptions = new JsonSerializerOptions(_jsonSerializerOptions)
            {
                PropertyNamingPolicy = null
            };
        }

        #region 通用请求方法

        /// <summary>
        /// 通用请求方法
        /// </summary>
        /// <typeparam name="T">响应类型</typeparam>
        /// <param name="request">请求参数</param>
        /// <param name="clientName">HttpClient名称</param>
        /// <param name="enableRetry">是否启用重试</param>
        /// <param name="completionOption">响应完成选项</param>
        /// <param name="timeout">请求超时时间（默认30秒，null表示使用HttpClient默认超时）</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>响应结果</returns>
        public async Task<T?> ExecuteAsync<T>(
            IBaseRequest<T> request,
            string? clientName = null,
            bool enableRetry = true,
            HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default) where T : class
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }
            
            CancellationTokenSource? timeoutCts = null;
            try
            {
                CancellationToken externalToken = cancellationToken;

                if (timeout.HasValue && timeout.Value != Timeout.InfiniteTimeSpan)
                {
                    timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
                    timeoutCts.CancelAfter(timeout.Value);
                    externalToken = timeoutCts.Token;
                }

                var client = string.IsNullOrWhiteSpace(clientName)
                    ? _httpClientFactory.CreateClient()
                    : _httpClientFactory.CreateClient(clientName!);

                Func<CancellationToken, ValueTask<object>> executeRequest = async (CancellationToken token) => 
                    await ExecuteRequestCore(client, request, completionOption, token);

                object result;
                if (enableRetry)
                {
                    var resolvedClientName = clientName ?? "default";
                    var pipeline = HttpClientPolicy.GetPipeline(resolvedClientName, request.Method, request.GetRequestApi());
                    result = await pipeline.ExecuteAsync(executeRequest, externalToken);
                }
                else
                {
                    result = await ExecuteRequestCore(client, request, completionOption, externalToken);
                }
                return (T)result;
            }
            catch (TaskCanceledException ex) when (ex.CancellationToken != cancellationToken)
            {
                if (timeoutCts?.IsCancellationRequested == true)
                {
                    throw new TimeoutException($"请求超时: {timeout!.Value.TotalSeconds:F1}秒内未收到响应。", ex);
                }
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"API请求失败: {request.Method} {request.GetRequestApi()}");
                throw;
            }
            finally
            {
                timeoutCts?.Dispose();
            }
        }

        #endregion

        #region GET请求

        /// <summary>
        /// GET请求
        /// </summary>
        public async Task<T?> ExecuteGetAsync<T>(
            IBaseRequest<T> request,
            string? clientName = null,
            bool enableRetry = true,
            HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default) where T : class
        {
            request.Method = "GET";
            return await ExecuteAsync(request, clientName, enableRetry, completionOption, timeout, cancellationToken);
        }

        #endregion

        #region POST请求

        /// <summary>
        /// POST请求
        /// </summary>
        public async Task<T?> ExecutePostAsync<T>(
            IBaseRequest<T> request,
            string? clientName = null,
            bool enableRetry = true,
            HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default) where T : class
        {
            request.Method = "POST";
            return await ExecuteAsync(request, clientName, enableRetry, completionOption, timeout, cancellationToken);
        }

        #endregion

        #region PUT请求

        /// <summary>
        /// PUT请求
        /// </summary>
        public async Task<T?> ExecutePutAsync<T>(
            IBaseRequest<T> request,
            string? clientName = null,
            bool enableRetry = true,
            HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default) where T : class
        {
            request.Method = "PUT";
            return await ExecuteAsync(request, clientName, enableRetry, completionOption, timeout, cancellationToken);
        }

        #endregion

        #region DELETE请求

        /// <summary>
        /// DELETE请求
        /// </summary>
        public async Task<T?> ExecuteDeleteAsync<T>(
            IBaseRequest<T> request,
            string? clientName = null,
            bool enableRetry = true,
            HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default) where T : class
        {
            request.Method = "DELETE";
            return await ExecuteAsync(request, clientName, enableRetry, completionOption, timeout, cancellationToken);
        }

        #endregion

        #region PATCH请求

        /// <summary>
        /// PATCH请求
        /// </summary>
        public async Task<T?> ExecutePatchAsync<T>(
            IBaseRequest<T> request,
            string? clientName = null,
            bool enableRetry = true,
            HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default) where T : class
        {
            request.Method = "PATCH";
            return await ExecuteAsync(request, clientName, enableRetry, completionOption, timeout, cancellationToken);
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 执行请求核心方法
        /// </summary>
        private async Task<T> ExecuteRequestCore<T>(System.Net.Http.HttpClient client, IBaseRequest<T> request, HttpCompletionOption completionOption, CancellationToken token) where T : class
        {
            var httpRequest = CreateHttpRequestMessage(request);
            var startTime = DateTime.UtcNow;
            string? responseContent = null;
            Exception? exception = null;
            int statusCode = 0;
            bool isSuccess = false;
            string? reasonPhrase = null;
            System.Net.Http.Headers.HttpResponseHeaders? responseHeaders = null;

            // 构建请求拦截上下文
            var requestContext = BuildRequestContext(request, httpRequest);

            // 调用请求前拦截器（捕获异常避免阻塞正常请求流程）
            try
            {
                HttpClientPolicy.OnRequestSending?.Invoke(requestContext);
            }
            catch (Exception interceptorEx)
            {
                _logger.LogWarning(interceptorEx, $"请求前拦截器执行失败: {request.GetRequestApi()}");
            }

            try
            {
                // 显式释放 HttpResponseMessage 以确保连接及时归还连接池
                using (var resp = await client.SendAsync(httpRequest, completionOption, token))
                {
                    // 在读取响应内容前先捕获响应头（读取后响应头可能被消耗）
                    responseHeaders = resp.Headers;
                    statusCode = (int)resp.StatusCode;
                    isSuccess = resp.IsSuccessStatusCode;
                    reasonPhrase = resp.ReasonPhrase;
                    responseContent = await resp.Content.ReadAsStringAsync();
                    resp.EnsureSuccessStatusCode();
                }

                return JsonSerializer.Deserialize<T>(responseContent, _jsonSerializerOptions)!;
            }
            catch (HttpRequestException ex)
            {
                exception = ex;
                _logger.LogWarning(ex, $"API请求客户端错误: {request.Method} {request.GetRequestApi()}");
                throw;
            }
            catch (Exception ex)
            {
                exception = ex;
                throw;
            }
            finally
            {
                // 提取响应头（在 using 块内已获取，仅传递引用）
                string? capturedReasonPhrase = reasonPhrase;

                // 调用请求后拦截器
                var responseContext = new ResponseInterceptionContext
                {
                    RequestId = requestContext.RequestId,
                    TraceId = requestContext.TraceId,
                    RequestContext = requestContext,
                    StatusCode = statusCode,
                    ReasonPhrase = capturedReasonPhrase,
                    ResponseHeaders = responseHeaders,
                    IsSuccess = isSuccess,
                    ResponseContent = responseContent,
                    ResponseTime = DateTime.UtcNow,
                    Duration = DateTime.UtcNow - startTime,
                    Exception = exception
                };
                try
                {
                    HttpClientPolicy.OnRequestCompleted?.Invoke(responseContext);
                }
                catch (Exception interceptorEx)
                {
                    _logger.LogWarning(interceptorEx, $"请求后拦截器执行失败: {request.GetRequestApi()}");
                }
            }
        }

        /// <summary>
        /// 构建请求拦截上下文
        /// </summary>
        private RequestInterceptionContext BuildRequestContext<T>(IBaseRequest<T> request, HttpRequestMessage httpRequest) where T : class
        {
            var context = new RequestInterceptionContext
            {
                RequestApi = request.GetRequestApi(),
                FullUrl = httpRequest.RequestUri?.ToString() ?? string.Empty,
                HttpMethod = request.Method,
                Headers = request.GetHeaders(),
                QueryParameters = request.GetQueryParameters(),
                Body = request.GetBody(),
                RequestTime = DateTime.UtcNow
            };

            // 序列化请求体为JSON字符串
            if (context.Body != null)
            {
                context.BodyJson = JsonSerializer.Serialize(context.Body, _requestSerializerOptions);
            }

            return context;
        }

        /// <summary>
        /// 创建HttpRequestMessage
        /// </summary>
        private HttpRequestMessage CreateHttpRequestMessage<T>(IBaseRequest<T> request) where T : class
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }
            
            var method = request.Method.ToUpperInvariant() switch
            {
                "GET" => HttpMethod.Get,
                "POST" => HttpMethod.Post,
                "PUT" => HttpMethod.Put,
                "DELETE" => HttpMethod.Delete,
                "PATCH" => new HttpMethod("PATCH"),
                _ => throw new NotSupportedException($"不支持的HTTP方法: {request.Method}")
            };
            string api = method != HttpMethod.Get && method != HttpMethod.Delete ? request.GetRequestApi() : $"{request.GetRequestApi()}{request.GetQueryParametersUrl() ?? string.Empty}";
            var httpRequest = new HttpRequestMessage(method, api);

            // 添加请求头
            var headers = request.GetHeaders();
            if (headers != null && headers.Count > 0)
            {
                foreach (var header in headers)
                {
                    httpRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            // 添加请求体（仅对非GET/DELETE请求）
            if (method != HttpMethod.Get && method != HttpMethod.Delete)
            {
                var body = request.GetBody();
                if (body != null)
                {
                    var json = JsonSerializer.Serialize(body, _requestSerializerOptions);
                    httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");
                }
            }

            return httpRequest;
        }

        #endregion
    }
}
