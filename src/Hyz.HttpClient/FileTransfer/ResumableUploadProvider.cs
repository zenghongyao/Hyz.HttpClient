using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Hyz.HttpClient
{
    /// <summary>
    /// 默认的分片上传提供者实现，基于 HTTP REST API 进行分片上传
    /// </summary>
    public class ResumableUploadProvider : IResumableUploadProvider
    {
        private readonly System.Net.Http.HttpClient _httpClient;
        private readonly ILogger? _logger;
        private readonly ResumableUploadOptions _options;
        private readonly JsonSerializerOptions _jsonOptions;

        /// <summary>
        /// 初始化分片上传提供者
        /// </summary>
        /// <param name="httpClient">HTTP 客户端</param>
        /// <param name="options">上传选项（可选）</param>
        /// <param name="logger">日志记录器（可选）</param>
        /// <exception cref="ArgumentNullException">当 httpClient 为 null 时抛出</exception>
        public ResumableUploadProvider(System.Net.Http.HttpClient httpClient, ResumableUploadOptions? options = null, ILogger? logger = null)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _options = options ?? new ResumableUploadOptions();
            _logger = logger;
            _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        }

        /// <summary>
        /// 创建上传会话
        /// </summary>
        /// <param name="fileName">文件名</param>
        /// <param name="fileSize">文件大小（字节）</param>
        /// <param name="contentType">内容类型（可选）</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>服务器返回的上传会话ID</returns>
        /// <exception cref="InvalidOperationException">当服务器未返回上传会话ID时抛出</exception>
        public async Task<string> CreateUploadSessionAsync(string fileName, long fileSize, string? contentType = null, CancellationToken cancellationToken = default)
        {
            var createUrl = string.Format(_options.CreateSessionUrlFormat, fileName);

            var request = new HttpRequestMessage(HttpMethod.Post, createUrl);
            
            var body = new Dictionary<string, object>
            {
                { "fileName", fileName },
                { "fileSize", fileSize }
            };
            
            if (!string.IsNullOrWhiteSpace(contentType))
            {
                body["contentType"] = contentType!;
            }

            request.Content = new StringContent(JsonSerializer.Serialize(body, _jsonOptions), Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<CreateSessionResponse>(responseContent, _jsonOptions);

            return result?.UploadId ?? throw new InvalidOperationException("服务器未返回上传会话ID");
        }

        /// <summary>
        /// 上传单个分片
        /// </summary>
        /// <param name="uploadId">上传会话ID</param>
        /// <param name="partNumber">分片编号（从1开始）</param>
        /// <param name="data">分片数据</param>
        /// <param name="offset">分片在文件中的偏移量</param>
        /// <param name="totalSize">文件总大小</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>上传结果，包含分片编号、ETag 和是否成功</returns>
        public async Task<UploadPartResult> UploadPartAsync(string uploadId, int partNumber, byte[] data, long offset, long totalSize, CancellationToken cancellationToken = default)
        {
            var uploadUrl = string.Format(_options.UploadPartUrlFormat, uploadId, partNumber);

            var request = new HttpRequestMessage(HttpMethod.Put, uploadUrl);
            
            request.Content = new ByteArrayContent(data);
            
            request.Content.Headers.ContentRange = new ContentRangeHeaderValue(offset, offset + data.Length - 1, totalSize);

            request.Content.Headers.ContentType = new MediaTypeHeaderValue(_options.PartContentType);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                return new UploadPartResult
                {
                    PartNumber = partNumber,
                    Success = false,
                    ErrorMessage = errorContent
                };
            }

            string? etag = null;
            if (response.Headers.TryGetValues("ETag", out var etags))
            {
                etag = etags.FirstOrDefault();
            }

            return new UploadPartResult
            {
                PartNumber = partNumber,
                ETag = etag,
                Success = true
            };
        }

        /// <summary>
        /// 列出已上传的分片
        /// </summary>
        /// <param name="uploadId">上传会话ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>已上传的分片信息列表</returns>
        public async Task<IList<UploadPartInfo>> ListUploadedPartsAsync(string uploadId, CancellationToken cancellationToken = default)
        {
            var listUrl = string.Format(_options.ListPartsUrlFormat, uploadId);

            using var request = new HttpRequestMessage(HttpMethod.Get, listUrl);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ListPartsResponse>(responseContent, _jsonOptions);

            return result?.Parts ?? new List<UploadPartInfo>();
        }

        /// <summary>
        /// 完成上传（合并分片）
        /// </summary>
        /// <typeparam name="T">响应类型</typeparam>
        /// <param name="uploadId">上传会话ID</param>
        /// <param name="parts">所有已上传的分片信息</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>服务器返回的响应结果</returns>
        public async Task<T?> CompleteUploadAsync<T>(string uploadId, IList<UploadPartInfo> parts, CancellationToken cancellationToken = default) where T : class
        {
            var completeUrl = string.Format(_options.CompleteUploadUrlFormat, uploadId);

            var request = new HttpRequestMessage(HttpMethod.Post, completeUrl);

            var body = new
            {
                parts = parts.Select(p => new { partNumber = p.PartNumber, eTag = p.ETag }).ToList()
            };

            request.Content = new StringContent(JsonSerializer.Serialize(body, _jsonOptions), Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(responseContent, _jsonOptions);
        }

        /// <summary>
        /// 取消上传，清理服务器上的临时分片
        /// </summary>
        /// <param name="uploadId">上传会话ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>任务</returns>
        public async Task AbortUploadAsync(string uploadId, CancellationToken cancellationToken = default)
        {
            var abortUrl = string.Format(_options.AbortUploadUrlFormat, uploadId);

            using var request = new HttpRequestMessage(HttpMethod.Delete, abortUrl);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
        }

        /// <summary>
        /// 创建会话响应模型
        /// </summary>
        private class CreateSessionResponse
        {
            /// <summary>
            /// 服务器返回的上传会话ID
            /// </summary>
            public string? UploadId { get; set; }
        }

        /// <summary>
        /// 列出分片响应模型
        /// </summary>
        private class ListPartsResponse
        {
            /// <summary>
            /// 已上传的分片列表
            /// </summary>
            public List<UploadPartInfo>? Parts { get; set; }
        }
    }

    /// <summary>
    /// 分片上传选项
    /// </summary>
    public class ResumableUploadOptions
    {
        /// <summary>
        /// 创建上传会话的 URL 格式，{0} 会被替换为文件名
        /// </summary>
        public string CreateSessionUrlFormat { get; set; } = "/api/upload/create";

        /// <summary>
        /// 上传分片的 URL 格式，{0} 会被替换为 uploadId，{1} 会被替换为 partNumber
        /// </summary>
        public string UploadPartUrlFormat { get; set; } = "/api/upload/{0}/parts/{1}";

        /// <summary>
        /// 列出已上传分片的 URL 格式，{0} 会被替换为 uploadId
        /// </summary>
        public string ListPartsUrlFormat { get; set; } = "/api/upload/{0}/parts";

        /// <summary>
        /// 完成上传的 URL 格式，{0} 会被替换为 uploadId
        /// </summary>
        public string CompleteUploadUrlFormat { get; set; } = "/api/upload/{0}/complete";

        /// <summary>
        /// 取消上传的 URL 格式，{0} 会被替换为 uploadId
        /// </summary>
        public string AbortUploadUrlFormat { get; set; } = "/api/upload/{0}/abort";

        /// <summary>
        /// 分片内容类型，默认为 application/octet-stream
        /// </summary>
        public string PartContentType { get; set; } = "application/octet-stream";

        /// <summary>
        /// 最大重试次数，默认为 3
        /// </summary>
        public int MaxRetryAttempts { get; set; } = 3;

        /// <summary>
        /// 重试延迟时间，默认为 1 秒
        /// </summary>
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);
    }
}