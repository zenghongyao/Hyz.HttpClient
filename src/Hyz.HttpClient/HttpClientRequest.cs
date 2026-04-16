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
        /// <returns>响应结果</returns>
        public async Task<T?> ExecuteAsync<T>(
            IBaseRequest<T> request,
            string? clientName = null,
            bool enableRetry = true) where T : class
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }
            
            try
            {
                var client = string.IsNullOrWhiteSpace(clientName)
                    ? _httpClientFactory.CreateClient()
                    : _httpClientFactory.CreateClient(clientName!);

                Func<CancellationToken, ValueTask<object>> executeRequest = async (CancellationToken token) => 
                    await ExecuteRequestCore(client, request, token);

                return enableRetry
                    ? (T)await HttpClientPolicy.GetApiPipelinePolicy.ExecuteAsync(executeRequest, CancellationToken.None)
                    : await ExecuteRequestCore(client, request, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"API请求失败: {request.Method} {request.GetRequestApi()}");
                throw;
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
            bool enableRetry = true) where T : class
        {
            request.Method = "GET";
            return await ExecuteAsync(request, clientName, enableRetry);
        }

        #endregion

        #region POST请求

        /// <summary>
        /// POST请求
        /// </summary>
        public async Task<T?> ExecutePostAsync<T>(
            IBaseRequest<T> request,
            string? clientName = null,
            bool enableRetry = true) where T : class
        {
            request.Method = "POST";
            return await ExecuteAsync(request, clientName, enableRetry);
        }

        #endregion

        #region PUT请求

        /// <summary>
        /// PUT请求
        /// </summary>
        public async Task<T?> ExecutePutAsync<T>(
            IBaseRequest<T> request,
            string? clientName = null,
            bool enableRetry = true) where T : class
        {
            request.Method = "PUT";
            return await ExecuteAsync(request, clientName, enableRetry);
        }

        #endregion

        #region DELETE请求

        /// <summary>
        /// DELETE请求
        /// </summary>
        public async Task<T?> ExecuteDeleteAsync<T>(
            IBaseRequest<T> request,
            string? clientName = null,
            bool enableRetry = true) where T : class
        {
            request.Method = "DELETE";
            return await ExecuteAsync(request, clientName, enableRetry);
        }

        #endregion

        #region PATCH请求

        /// <summary>
        /// PATCH请求
        /// </summary>
        public async Task<T?> ExecutePatchAsync<T>(
            IBaseRequest<T> request,
            string? clientName = null,
            bool enableRetry = true) where T : class
        {
            request.Method = "PATCH";
            return await ExecuteAsync(request, clientName, enableRetry);
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 执行请求核心方法
        /// </summary>
        private async Task<T> ExecuteRequestCore<T>(System.Net.Http.HttpClient client, IBaseRequest<T> request, CancellationToken token) where T : class
        {
            var httpRequest = CreateHttpRequestMessage(request);
            var startTime = DateTime.Now;
            HttpResponseMessage? resp = null;
            string? responseContent = null;
            Exception? exception = null;

            // 构建请求拦截上下文
            var requestContext = BuildRequestContext(request, httpRequest);

            // 调用请求前拦截器
            HttpClientPolicy.OnRequestSending?.Invoke(requestContext);

            try
            {
                resp = await client.SendAsync(httpRequest, token);
                resp.EnsureSuccessStatusCode();
                responseContent = await resp.Content.ReadAsStringAsync();
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
                // 调用请求后拦截器
                var responseContext = new ResponseInterceptionContext
                {
                    RequestContext = requestContext,
                    ResponseMessage = resp,
                    StatusCode = resp != null ? (int)resp.StatusCode : 0,
                    IsSuccess = resp?.IsSuccessStatusCode ?? false,
                    ResponseContent = responseContent,
                    ResponseTime = DateTime.Now,
                    Duration = DateTime.Now - startTime,
                    Exception = exception
                };
                HttpClientPolicy.OnRequestCompleted?.Invoke(responseContext);
            }

#pragma warning disable CS8603 // 可能返回 null 引用。
            return JsonSerializer.Deserialize<T>(responseContent, _jsonSerializerOptions);
#pragma warning restore CS8603 // 可能返回 null 引用。
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
                RequestTime = DateTime.Now
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
