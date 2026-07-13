using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Polly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
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

        #region 文件上传

        /// <summary>
        /// 上传文件（单个文件）
        /// </summary>
        /// <typeparam name="T">响应类型</typeparam>
        /// <param name="api">API地址</param>
        /// <param name="file">文件参数</param>
        /// <param name="clientName">HttpClient名称</param>
        /// <param name="enableRetry">是否启用重试（默认false，大文件重试代价高）</param>
        /// <param name="timeout">请求超时时间</param>
        /// <param name="progressCallback">上传进度回调</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>响应结果</returns>
        public async Task<T?> ExecuteUploadAsync<T>(
            string api,
            FileParameter file,
            string? clientName = null,
            bool enableRetry = false,
            TimeSpan? timeout = null,
            Action<UploadProgress>? progressCallback = null,
            CancellationToken cancellationToken = default) where T : class
        {
            return await ExecuteUploadAsync<T>(api, new[] { file }, null, clientName, enableRetry, timeout, progressCallback, cancellationToken);
        }

        /// <summary>
        /// 上传文件（多个文件）
        /// </summary>
        /// <typeparam name="T">响应类型</typeparam>
        /// <param name="api">API地址</param>
        /// <param name="files">文件参数列表</param>
        /// <param name="clientName">HttpClient名称</param>
        /// <param name="enableRetry">是否启用重试（默认false，大文件重试代价高）</param>
        /// <param name="timeout">请求超时时间</param>
        /// <param name="progressCallback">上传进度回调</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>响应结果</returns>
        public async Task<T?> ExecuteUploadAsync<T>(
            string api,
            IEnumerable<FileParameter> files,
            string? clientName = null,
            bool enableRetry = false,
            TimeSpan? timeout = null,
            Action<UploadProgress>? progressCallback = null,
            CancellationToken cancellationToken = default) where T : class
        {
            return await ExecuteUploadAsync<T>(api, files, null, clientName, enableRetry, timeout, progressCallback, cancellationToken);
        }

        /// <summary>
        /// 上传文件（带表单数据）
        /// </summary>
        /// <typeparam name="T">响应类型</typeparam>
        /// <param name="api">API地址</param>
        /// <param name="files">文件参数列表</param>
        /// <param name="formData">表单数据字典</param>
        /// <param name="clientName">HttpClient名称</param>
        /// <param name="enableRetry">是否启用重试（默认false，大文件重试代价高）</param>
        /// <param name="timeout">请求超时时间</param>
        /// <param name="progressCallback">上传进度回调</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>响应结果</returns>
        public async Task<T?> ExecuteUploadAsync<T>(
            string api,
            IEnumerable<FileParameter>? files,
            Dictionary<string, string>? formData,
            string? clientName = null,
            bool enableRetry = false,
            TimeSpan? timeout = null,
            Action<UploadProgress>? progressCallback = null,
            CancellationToken cancellationToken = default) where T : class
        {
            if (string.IsNullOrWhiteSpace(api))
            {
                throw new ArgumentNullException(nameof(api));
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

                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, api);
                using var content = new MultipartFormDataContent();

                long totalSize = 0;

                if (files != null)
                {
                    foreach (var file in files)
                    {
                        var streamContent = new ProgressableStreamContent(file.Stream, progressCallback);
                        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
                        content.Add(streamContent, file.FieldName, file.FileName);
                        totalSize += file.ContentLength;
                    }
                }

                if (formData != null)
                {
                    foreach (var kvp in formData)
                    {
                        content.Add(new StringContent(kvp.Value, Encoding.UTF8), kvp.Key);
                    }
                }

                httpRequest.Content = content;

                using var resp = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseContentRead, externalToken);
                resp.EnsureSuccessStatusCode();

                var responseContent = await resp.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<T>(responseContent, _jsonSerializerOptions);
            }
            catch (TaskCanceledException ex) when (ex.CancellationToken != cancellationToken)
            {
                if (timeoutCts?.IsCancellationRequested == true)
                {
                    throw new TimeoutException($"上传超时: {timeout!.Value.TotalSeconds:F1}秒内未完成上传。", ex);
                }
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"文件上传失败: POST {api}");
                throw;
            }
            finally
            {
                timeoutCts?.Dispose();
            }
        }

        /// <summary>
        /// 断点续传上传（分片上传）
        /// </summary>
        /// <typeparam name="T">响应类型</typeparam>
        /// <param name="context">上传上下文</param>
        /// <param name="provider">分片上传提供者</param>
        /// <param name="clientName">HttpClient名称</param>
        /// <param name="timeout">请求超时时间</param>
        /// <param name="progressCallback">上传进度回调</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>响应结果</returns>
        public async Task<T?> ExecuteResumableUploadAsync<T>(
            UploadContext context,
            IResumableUploadProvider provider,
            string? clientName = null,
            TimeSpan? timeout = null,
            Action<UploadProgress>? progressCallback = null,
            CancellationToken cancellationToken = default) where T : class
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));

            CancellationTokenSource? timeoutCts = null;
            CancellationTokenSource? linkedCts = null;

            try
            {
                var localCts = context.GetCancellationToken();
                linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, localCts);
                var externalToken = linkedCts.Token;

                if (timeout.HasValue && timeout.Value != Timeout.InfiniteTimeSpan)
                {
                    timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
                    timeoutCts.CancelAfter(timeout.Value);
                    externalToken = timeoutCts.Token;
                }

                context.Status = UploadStatus.Uploading;

                if (string.IsNullOrWhiteSpace(context.ServerUploadId))
                {
                    context.ServerUploadId = await provider.CreateUploadSessionAsync(
                        context.FileName ?? Path.GetFileName(context.FilePath),
                        context.FileSize,
                        null,
                        externalToken);
                }

                var uploadedParts = await provider.ListUploadedPartsAsync(context.ServerUploadId!, externalToken);
                foreach (var part in uploadedParts)
                {
                    context.MarkPartUploaded(part.PartNumber);
                    context.BytesUploaded += part.Size;
                }

                var allParts = new List<UploadPartInfo>();
                allParts.AddRange(uploadedParts);

                var totalParts = context.TotalParts;
                var chunkSize = context.ChunkSize;

                for (int partNumber = 1; partNumber <= totalParts; partNumber++)
                {
                    externalToken.ThrowIfCancellationRequested();

                    if (context.IsPartUploaded(partNumber))
                        continue;

                    long offset = (partNumber - 1) * chunkSize;
                    int bytesToRead = partNumber == totalParts ?
                        (int)(context.FileSize - offset) : chunkSize;

                    byte[] chunkData;

                    if (context.Stream != null)
                    {
                        context.Stream.Seek(offset, SeekOrigin.Begin);
                        chunkData = new byte[bytesToRead];
                        int bytesRead = await context.Stream.ReadAsync(chunkData, 0, bytesToRead, externalToken);
                        if (bytesRead < bytesToRead)
                        {
                            Array.Resize(ref chunkData, bytesRead);
                        }
                    }
                    else if (!string.IsNullOrWhiteSpace(context.FilePath))
                    {
                        using var fileStream = new FileStream(context.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                        fileStream.Seek(offset, SeekOrigin.Begin);
                        chunkData = new byte[bytesToRead];
                        int bytesRead = await fileStream.ReadAsync(chunkData, 0, bytesToRead, externalToken);
                        if (bytesRead < bytesToRead)
                        {
                            Array.Resize(ref chunkData, bytesRead);
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException("上传上下文必须包含文件路径或流");
                    }

                    var result = await provider.UploadPartAsync(
                        context.ServerUploadId!,
                        partNumber,
                        chunkData,
                        offset,
                        context.FileSize,
                        externalToken);

                    if (!result.Success)
                    {
                        throw new IOException($"上传分片 {partNumber} 失败: {result.ErrorMessage}");
                    }

                    context.MarkPartUploaded(partNumber);
                    context.BytesUploaded += chunkData.Length;

                    allParts.Add(new UploadPartInfo
                    {
                        PartNumber = partNumber,
                        ETag = result.ETag,
                        Size = chunkData.Length
                    });

                    progressCallback?.Invoke(new UploadProgress
                    {
                        BytesSent = context.BytesUploaded,
                        TotalBytes = context.FileSize,
                        BytesPerSecond = 0,
                        Status = UploadStatus.Uploading
                    });
                }

                context.Status = UploadStatus.Completed;

                progressCallback?.Invoke(new UploadProgress
                {
                    BytesSent = context.FileSize,
                    TotalBytes = context.FileSize,
                    BytesPerSecond = 0,
                    Status = UploadStatus.Completed
                });

                return await provider.CompleteUploadAsync<T>(context.ServerUploadId!, allParts, externalToken);
            }
            catch (TaskCanceledException ex)
            {
                if (context.Status == UploadStatus.Paused)
                {
                    throw new TaskCanceledException("上传已暂停", ex);
                }
                if (context.Status == UploadStatus.Cancelled)
                {
                    throw new TaskCanceledException("上传已取消", ex);
                }
                throw;
            }
            catch (Exception ex)
            {
                context.Status = UploadStatus.Failed;
                context.Exception = ex;
                _logger.LogError(ex, $"分片上传失败: {context.FileName}");
                throw;
            }
            finally
            {
                timeoutCts?.Dispose();
                linkedCts?.Dispose();
            }
        }

        #endregion

        #region 文件下载

        /// <summary>
        /// 下载文件到指定路径
        /// </summary>
        /// <param name="api">API地址</param>
        /// <param name="filePath">本地文件保存路径</param>
        /// <param name="clientName">HttpClient名称</param>
        /// <param name="enableRetry">是否启用重试（默认false，大文件重试代价高）</param>
        /// <param name="timeout">请求超时时间</param>
        /// <param name="progressCallback">下载进度回调</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>下载的字节数</returns>
        public async Task<long> ExecuteDownloadAsync(
            string api,
            string filePath,
            string? clientName = null,
            bool enableRetry = false,
            TimeSpan? timeout = null,
            Action<DownloadProgress>? progressCallback = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(api))
            {
                throw new ArgumentNullException(nameof(api));
            }
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
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

                using var httpRequest = new HttpRequestMessage(HttpMethod.Get, api);
                using var resp = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, externalToken);
                resp.EnsureSuccessStatusCode();

                long totalBytes = resp.Content.Headers.ContentLength ?? 0;
                long bytesRead = 0;

                using var stream = await resp.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);

                var buffer = new byte[8192];
                int bytes;

                var lastReportTime = DateTime.UtcNow;
                var lastBytesRead = 0L;

                while ((bytes = await stream.ReadAsync(buffer, 0, buffer.Length, externalToken)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytes, externalToken);
                    bytesRead += bytes;

                    if (progressCallback != null)
                    {
                        var now = DateTime.UtcNow;
                        var elapsedMs = (now - lastReportTime).TotalMilliseconds;

                        if (elapsedMs >= 100)
                        {
                            var speed = (long)((bytesRead - lastBytesRead) / (elapsedMs / 1000.0));

                            progressCallback?.Invoke(new DownloadProgress
                            {
                                BytesReceived = bytesRead,
                                TotalBytes = totalBytes,
                                BytesPerSecond = speed,
                                Status = DownloadStatus.Downloading
                            });

                            lastReportTime = now;
                            lastBytesRead = bytesRead;
                        }
                    }
                }

                if (progressCallback != null)
                {
                    progressCallback?.Invoke(new DownloadProgress
                    {
                        BytesReceived = bytesRead,
                        TotalBytes = totalBytes,
                        Status = DownloadStatus.Completed
                    });
                }

                return bytesRead;
            }
            catch (TaskCanceledException ex) when (ex.CancellationToken != cancellationToken)
            {
                if (timeoutCts?.IsCancellationRequested == true)
                {
                    throw new TimeoutException($"下载超时: {timeout!.Value.TotalSeconds:F1}秒内未完成下载。", ex);
                }
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"文件下载失败: GET {api}");
                throw;
            }
            finally
            {
                timeoutCts?.Dispose();
            }
        }

        /// <summary>
        /// 下载文件并返回Stream
        /// </summary>
        /// <param name="api">API地址</param>
        /// <param name="clientName">HttpClient名称</param>
        /// <param name="enableRetry">是否启用重试（默认false，大文件重试代价高）</param>
        /// <param name="timeout">请求超时时间</param>
        /// <param name="progressCallback">下载进度回调</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>响应流（调用方负责释放）</returns>
        public async Task<Stream> ExecuteDownloadStreamAsync(
            string api,
            string? clientName = null,
            bool enableRetry = false,
            TimeSpan? timeout = null,
            Action<DownloadProgress>? progressCallback = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(api))
            {
                throw new ArgumentNullException(nameof(api));
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

                using var httpRequest = new HttpRequestMessage(HttpMethod.Get, api);
                using var resp = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, externalToken);
                resp.EnsureSuccessStatusCode();

                long totalBytes = resp.Content.Headers.ContentLength ?? 0;
                var stream = await resp.Content.ReadAsStreamAsync();

                if (progressCallback == null)
                {
                    return stream;
                }

                var progressStream = new ProgressReportingStream(stream, totalBytes, progressCallback);
                return progressStream;
            }
            catch (TaskCanceledException ex) when (ex.CancellationToken != cancellationToken)
            {
                if (timeoutCts?.IsCancellationRequested == true)
                {
                    throw new TimeoutException($"下载超时: {timeout!.Value.TotalSeconds:F1}秒内未完成下载。", ex);
                }
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"文件下载失败: GET {api}");
                throw;
            }
            finally
            {
                timeoutCts?.Dispose();
            }
        }

        /// <summary>
        /// 断点续传下载
        /// </summary>
        /// <param name="context">下载上下文</param>
        /// <param name="clientName">HttpClient名称</param>
        /// <param name="enableRetry">是否启用重试（默认false，大文件重试代价高）</param>
        /// <param name="timeout">请求超时时间</param>
        /// <param name="progressCallback">下载进度回调</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>下载的字节数</returns>
        public async Task<long> ExecuteResumableDownloadAsync(
            DownloadContext context,
            string? clientName = null,
            bool enableRetry = false,
            TimeSpan? timeout = null,
            Action<DownloadProgress>? progressCallback = null,
            CancellationToken cancellationToken = default)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var directory = Path.GetDirectoryName(context.LocalFilePath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            CancellationTokenSource? timeoutCts = null;
            CancellationTokenSource? linkedCts = null;
            try
            {
                var localCts = context.GetCancellationToken();
                linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, localCts);
                var externalToken = linkedCts.Token;

                if (timeout.HasValue && timeout.Value != Timeout.InfiniteTimeSpan)
                {
                    timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
                    timeoutCts.CancelAfter(timeout.Value);
                    externalToken = timeoutCts.Token;
                }

                var client = string.IsNullOrWhiteSpace(clientName)
                    ? _httpClientFactory.CreateClient()
                    : _httpClientFactory.CreateClient(clientName!);

                long bytesDownloaded = 0;
                long totalBytes = 0;
                bool supportsResume = false;

                using (var headRequest = new HttpRequestMessage(HttpMethod.Head, context.Url))
                {
                    using (var headResp = await client.SendAsync(headRequest, HttpCompletionOption.ResponseHeadersRead, externalToken))
                    {
                        headResp.EnsureSuccessStatusCode();

                        totalBytes = headResp.Content.Headers.ContentLength ?? 0;
                        context.TotalBytes = totalBytes;

                        if (headResp.Headers.TryGetValues("ETag", out var etags))
                        {
                            context.ETag = etags.ToString();
                        }

                        if (headResp.Content.Headers.ContentRange != null ||
                            headResp.Headers.TryGetValues("Accept-Ranges", out var acceptRanges) &&
                            acceptRanges.Any(r => string.Equals(r, "bytes", StringComparison.OrdinalIgnoreCase)))
                        {
                            supportsResume = true;
                        }

                        context.SupportsResume = supportsResume;
                    }
                }

                long startByte = 0;
                if (supportsResume && context.HasPartialDownload())
                {
                    startByte = context.GetLocalFileSize();
                    if (startByte >= totalBytes)
                    {
                        if (progressCallback != null)
                        {
                            progressCallback?.Invoke(new DownloadProgress
                            {
                                BytesReceived = totalBytes,
                                TotalBytes = totalBytes,
                                SupportsResume = true,
                                Status = DownloadStatus.Completed
                            });
                        }
                        return 0;
                    }
                }

                context.Status = DownloadStatus.Downloading;
                context.BytesDownloaded = startByte;

                using (var httpRequest = new HttpRequestMessage(HttpMethod.Get, context.Url))
                {
                    if (startByte > 0)
                    {
                        httpRequest.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(startByte, null);
                    }

                    using (var resp = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, externalToken))
                    {
                        if (startByte > 0 && resp.StatusCode != HttpStatusCode.PartialContent)
                        {
                            throw new InvalidOperationException("服务器不支持断点续传，将重新下载");
                        }

                        resp.EnsureSuccessStatusCode();

                        using (var stream = await resp.Content.ReadAsStreamAsync())
                        {
                            using (var fileStream = new FileStream(context.LocalFilePath, startByte > 0 ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None))
                            {
                                var buffer = new byte[8192];
                                int bytes;

                                var lastReportTime = DateTime.UtcNow;
                                var lastBytesRead = startByte;

                                while ((bytes = await stream.ReadAsync(buffer, 0, buffer.Length, externalToken)) > 0)
                                {
                                    await fileStream.WriteAsync(buffer, 0, bytes, externalToken);
                                    bytesDownloaded += bytes;

                                    var totalReceived = startByte + bytesDownloaded;

                                    if (progressCallback != null)
                                    {
                                        var now = DateTime.UtcNow;
                                        var elapsedMs = (now - lastReportTime).TotalMilliseconds;

                                        if (elapsedMs >= 100)
                                        {
                                            var speed = (long)((totalReceived - lastBytesRead) / (elapsedMs / 1000.0));

                                            progressCallback?.Invoke(new DownloadProgress
                                            {
                                                BytesReceived = totalReceived,
                                                TotalBytes = totalBytes,
                                                BytesPerSecond = speed,
                                                SupportsResume = supportsResume,
                                                Status = DownloadStatus.Downloading
                                            });

                                            lastReportTime = now;
                                            lastBytesRead = totalReceived;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                context.Status = DownloadStatus.Completed;
                context.BytesDownloaded = startByte + bytesDownloaded;

                if (progressCallback != null)
                {
                    progressCallback?.Invoke(new DownloadProgress
                    {
                        BytesReceived = context.BytesDownloaded,
                        TotalBytes = totalBytes,
                        SupportsResume = supportsResume,
                        Status = DownloadStatus.Completed
                    });
                }

                return bytesDownloaded;
            }
            catch (TaskCanceledException ex) when (ex.CancellationToken != cancellationToken)
            {
                if (timeoutCts?.IsCancellationRequested == true)
                {
                    throw new TimeoutException($"下载超时: {timeout!.Value.TotalSeconds:F1}秒内未完成下载。", ex);
                }
                throw;
            }
            catch (OperationCanceledException ex)
            {
                if (context.Status == DownloadStatus.Paused)
                {
                    throw new TaskCanceledException("下载已暂停", ex);
                }
                if (context.Status == DownloadStatus.Cancelled)
                {
                    throw new TaskCanceledException("下载已取消", ex);
                }
                throw;
            }
            catch (Exception ex)
            {
                context.Status = DownloadStatus.Failed;
                context.Exception = ex;
                _logger.LogError(ex, $"文件下载失败: GET {context.Url}");
                throw;
            }
            finally
            {
                timeoutCts?.Dispose();
                linkedCts?.Dispose();
            }
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
                HttpRequest = httpRequest,
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
