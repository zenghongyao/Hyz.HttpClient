using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace Hyz.HttpClient
{
    /// <summary>
    /// HttpClient策略配置
    /// </summary>
    public static class HttpClientPolicy
    {
        private static readonly object _lock = new object();
        private static readonly object _cleanupLock = new object();

        /// <summary>
        /// 断路器管道最大空闲时间（超过此时间未使用则自动清理）
        /// </summary>
        /// <remarks>默认值: 5 分钟</remarks>
        public static TimeSpan PipelineCleanupMaxIdleTime { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// 断路器管道清理检查间隔
        /// </summary>
        /// <remarks>默认值: 1 分钟</remarks>
        public static TimeSpan PipelineCleanupInterval { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// 是否保持字典参数的原始命名（不转换为小驼峰）
        /// </summary>
        /// <remarks>
        /// false: 字典方式设置的参数转换为小驼峰命名（默认）<br/>
        /// true: 字典方式设置的参数保持原始 key 名称<br/>
        /// 注意：此配置仅影响字典方式设置的参数，实体类属性始终默认使用小驼峰命名
        /// </remarks>
        public static bool PreserveDictionaryKeyNaming { get; set; } = false;

        /// <summary>
        /// 是否忽略证书验证错误（全局配置）
        /// </summary>
        /// <remarks>
        /// true: 忽略所有SSL证书验证错误（便于开发调试，生产环境不推荐）<br/>
        /// false: 启用严格的证书验证（生产环境推荐）<br/>
        /// 警告：生产环境应设置为 false 并配置正确的证书验证
        /// </remarks>
        public static bool IgnoreCertificateErrors { get; set; } = false;

        /// <summary>
        /// DateTime 序列化格式（用于 DateTimeConverter 和 NullableDateTimeConverter）
        /// </summary>
        /// <remarks>
        /// 默认值: "yyyy-MM-dd HH:mm:ss.FFFFFFFK"（ISO 8601 扩展格式）<br/>
        /// 常用格式示例:<br/>
        /// - "o" 或 "yyyy-MM-ddTHH:mm:ss.fffffffK" (ISO 8601 Round-Trip)<br/>
        /// - "s" (Sortable DateTime 格式)<br/>
        /// - "u" (Universal Sortable DateTime 格式)<br/>
        /// 完整格式说明请参考: https://docs.microsoft.com/zh-cn/dotnet/standard/base-types/custom-date-and-time-format-strings
        /// </remarks>
        public static string DateTimeFormat { get; set; } = "yyyy-MM-dd HH:mm:ss.FFFFFFFK";

        /// <summary>
        /// 请求前拦截器（全局）
        /// </summary>
        /// <remarks>
        /// 在请求发送前调用，可用于日志记录、请求验证等场景
        /// </remarks>
        public static Action<RequestInterceptionContext>? OnRequestSending { get; set; }

        /// <summary>
        /// 请求后拦截器（全局）
        /// </summary>
        /// <remarks>
        /// 在请求完成后调用，可用于日志记录、性能监控等场景
        /// </remarks>
        public static Action<ResponseInterceptionContext>? OnRequestCompleted { get; set; }

        /// <summary>
        /// 清除所有已注册的拦截器
        /// </summary>
        /// <remarks>
        /// 用于清理静态事件引用，防止内存泄漏。
        /// 建议在应用退出或测试 tearDown 时调用。
        /// </remarks>
        public static void ClearInterceptors()
        {
            OnRequestSending = null;
            OnRequestCompleted = null;
        }

        /// <summary>
        /// 默认JSON序列化配置
        /// </summary>

        public static readonly JsonSerializerOptions DefaultJsonOptions = new()
        {
            //大小写不敏感
            PropertyNameCaseInsensitive = true,
            //驼峰命名策略
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            //忽略JSON注释	
            ReadCommentHandling = JsonCommentHandling.Skip,
            //序列化时忽略 readonly字段
            IgnoreReadOnlyFields = true,
            // 序列化时忽略只有 get 没有 set 的属性
            IgnoreReadOnlyProperties = true,
            //忽略尾随逗号
            AllowTrailingCommas = true,
            //反序列化带引号的数字
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            //处理循环引用
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            //中文字符转义
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            //格式化输出
            WriteIndented = true,
            //元数据处理器
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
            Converters =
            {
                new FlexibleEnumConverter(),
                new DateTimeConverter(),
                new NullableDateTimeConverter()
            }
        };

        /// <summary>
        /// 重试配置选项
        /// </summary>
        public class RetryOptions
        {
            /// <summary>
            /// 最大重试次数
            /// </summary>
            public int MaxRetryAttempts { get; set; } = 3;

            /// <summary>
            /// 退避策略类型
            /// </summary>
            public DelayBackoffType BackoffType { get; set; } = DelayBackoffType.Exponential;

            /// <summary>
            /// 初始延迟
            /// </summary>
            public TimeSpan InitialDelay { get; set; } = TimeSpan.FromMilliseconds(200);

            /// <summary>
            /// 重试事件
            /// </summary>
            public Action<OnRetryArguments<object>>? OnRetry { get; set; }
        }

        /// <summary>
        /// 熔断配置选项
        /// </summary>
        public class CircuitBreakerOptions
        {
            /// <summary>
            /// 失败率阈值（0.0-1.0），达到此比例时熔断器打开
            /// </summary>
            public double FailureRatio { get; set; } = 0.5;

            /// <summary>
            /// 采样时间窗口
            /// </summary>
            public TimeSpan SamplingDuration { get; set; } = TimeSpan.FromSeconds(2);

            /// <summary>
            /// 最小吞吐量
            /// </summary>
            public int MinimumThroughput { get; set; } = 4;

            /// <summary>
            /// 熔断持续时间
            /// </summary>
            public TimeSpan BreakDuration { get; set; } = TimeSpan.FromSeconds(3);

            /// <summary>
            /// 熔断打开事件
            /// </summary>
            public Action<OnCircuitOpenedArguments<object>>? OnOpened { get; set; }

            /// <summary>
            /// 熔断关闭事件
            /// </summary>
            public Action<OnCircuitClosedArguments<object>>? OnClosed { get; set; }

            /// <summary>
            /// 熔断半开事件
            /// </summary>
            public Action<OnCircuitHalfOpenedArguments>? OnHalfOpened { get; set; }
        }

        /// <summary>
        /// 断路器管道条目，记录管道及其最后使用时间
        /// </summary>
        private class PipelineEntry
        {
            public ResiliencePipeline<object> Pipeline { get; }
            public DateTime LastAccessTime { get; set; }

            public PipelineEntry(ResiliencePipeline<object> pipeline)
            {
                Pipeline = pipeline;
                LastAccessTime = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// 按 Key 隔离的断路器管道字典
        /// </summary>
        private static readonly ConcurrentDictionary<string, Lazy<PipelineEntry>> _pipelines = new();

        /// <summary>
        /// 自动清理后台定时器
        /// </summary>
        private static Timer? _cleanupTimer;

        private static RetryOptions _retryOptions = new RetryOptions();
        private static CircuitBreakerOptions _circuitBreakerOptions = new CircuitBreakerOptions();

        /// <summary>
        /// 配置重试选项
        /// </summary>
        /// <param name="options">重试选项</param>
        public static void ConfigureRetry(RetryOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }
            
            lock (_lock)
            {
                _retryOptions = options;
                _pipelines.Clear();
            }
        }

        /// <summary>
        /// 配置熔断选项
        /// </summary>
        /// <param name="options">熔断选项</param>
        public static void ConfigureCircuitBreaker(CircuitBreakerOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }
            
            lock (_lock)
            {
                _circuitBreakerOptions = options;
                _pipelines.Clear();
            }
        }

        /// <summary>
        /// 根据 Key 获取或创建隔离的断路器管道
        /// </summary>
        /// <param name="clientName">HttpClient 名称</param>
        /// <param name="method">HTTP 方法</param>
        /// <param name="requestApiPath">请求 API 路径</param>
        /// <returns>对应 Key 的断路器管道</returns>
        public static ResiliencePipeline<object> GetPipeline(string clientName, string method, string requestApiPath)
        {
            var key = BuildPipelineKey(clientName, method, requestApiPath);

            var lazyEntry = _pipelines.GetOrAdd(key, _ => new Lazy<PipelineEntry>(() =>
            {
                var pipeline = BuildPipeline();
                return new PipelineEntry(pipeline);
            }, LazyThreadSafetyMode.ExecutionAndPublication));

            var entry = lazyEntry.Value;
            entry.LastAccessTime = DateTime.UtcNow;
            return entry.Pipeline;
        }

        /// <summary>
        /// 构建管道 Key
        /// </summary>
        /// <remarks>
        /// Key 格式: {ClientName}:{HTTPMethod}:{BasePath}
        /// BasePath 会去除查询参数，确保同类请求共享断路器
        /// </remarks>
        public static string BuildPipelineKey(string clientName, string method, string requestApiPath)
        {
            var basePath = ExtractBasePath(requestApiPath);
            return $"{clientName}:{method.ToUpperInvariant()}:{basePath}";
        }

        /// <summary>
        /// 从请求 API 路径中提取基础路径（去除查询参数和协议域名）
        /// </summary>
        public static string ExtractBasePath(string requestApiPath)
        {
            if (string.IsNullOrEmpty(requestApiPath))
            {
                return string.Empty;
            }

            string pathOnly;

            // 尝试作为完整 URI 解析
            if (Uri.TryCreate(requestApiPath, UriKind.Absolute, out var uri))
            {
                pathOnly = uri.AbsolutePath;
            }
            else
            {
                // 相对路径，手动去除查询参数
                var queryIndex = requestApiPath.IndexOf('?');
                pathOnly = queryIndex >= 0 ? requestApiPath.Substring(0, queryIndex) : requestApiPath;
            }

            // 确保以 / 开头
            if (!pathOnly.StartsWith("/"))
            {
                pathOnly = "/" + pathOnly;
            }

            return pathOnly;
        }

        /// <summary>
        /// 构建新的断路器管道
        /// </summary>
        private static ResiliencePipeline<object> BuildPipeline()
        {
            var pipelineBuilder = new ResiliencePipelineBuilder<object>();

            // 添加重试策略
            RetryStrategyOptions<object> retryStrategyOptions = new RetryStrategyOptions<object>
            {
                ShouldHandle = new PredicateBuilder<object>()
                    .Handle<Exception>(ex => ex is not BrokenCircuitException),
                MaxRetryAttempts = _retryOptions.MaxRetryAttempts,
                BackoffType = _retryOptions.BackoffType,
                Delay = _retryOptions.InitialDelay,
                OnRetry = args =>
                {
                    try
                    {
                        _retryOptions.OnRetry?.Invoke(args);
                    }
                    catch
                    {
                        // 回调抛出的异常不应破坏 Polly 管道
                    }
                    return new ValueTask();
                }
            };
            pipelineBuilder.AddRetry(retryStrategyOptions);

            // 添加熔断策略
            CircuitBreakerStrategyOptions<object> circuitBreakerStrategyOptions = new CircuitBreakerStrategyOptions<object>
            {
                ShouldHandle = new PredicateBuilder<object>()
                    .Handle<HttpRequestException>(),
                FailureRatio = _circuitBreakerOptions.FailureRatio,
                SamplingDuration = _circuitBreakerOptions.SamplingDuration,
                MinimumThroughput = _circuitBreakerOptions.MinimumThroughput,
                BreakDuration = _circuitBreakerOptions.BreakDuration,
                OnOpened = args =>
                {
                    try
                    {
                        _circuitBreakerOptions.OnOpened?.Invoke(args);
                    }
                    catch
                    {
                        // 回调抛出的异常不应破坏 Polly 管道
                    }
                    return new ValueTask();
                },
                OnClosed = args =>
                {
                    try
                    {
                        _circuitBreakerOptions.OnClosed?.Invoke(args);
                    }
                    catch
                    {
                        // 回调抛出的异常不应破坏 Polly 管道
                    }
                    return new ValueTask();
                },
                OnHalfOpened = args =>
                {
                    try
                    {
                        _circuitBreakerOptions.OnHalfOpened?.Invoke(args);
                    }
                    catch
                    {
                        // 回调抛出的异常不应破坏 Polly 管道
                    }
                    return new ValueTask();
                }
            };
            pipelineBuilder.AddCircuitBreaker(circuitBreakerStrategyOptions);

            return pipelineBuilder.Build();
        }

        /// <summary>
        /// 清理超过指定时间未使用的断路器管道
        /// </summary>
        /// <param name="maxIdleTime">最大空闲时间</param>
        /// <returns>清理的管道数量</returns>
        public static int CleanupUnusedPipelines(TimeSpan maxIdleTime)
        {
            var now = DateTime.UtcNow;
            var keysToRemove = new System.Collections.Generic.List<string>();

            foreach (var kvp in _pipelines)
            {
                if (kvp.Value.IsValueCreated)
                {
                    var entry = kvp.Value.Value;
                    if (now - entry.LastAccessTime > maxIdleTime)
                    {
                        keysToRemove.Add(kvp.Key);
                    }
                }
            }

            foreach (var key in keysToRemove)
            {
                _pipelines.TryRemove(key, out _);
            }

            return keysToRemove.Count;
        }

        /// <summary>
        /// 启动自动清理过期断路器管道的后台任务（幂等，重复调用安全）
        /// </summary>
        /// <remarks>
        /// 每 PipelineCleanupInterval 检查一次，清理超过 PipelineCleanupMaxIdleTime 未使用的管道。
        /// 每次 GetPipeline() 调用会自动续期 LastAccessTime，活跃的管道不会被清理。
        /// </remarks>
        public static void StartPipelineCleanup()
        {
            lock (_cleanupLock)
            {
                if (_cleanupTimer != null) return;

                _cleanupTimer = new Timer(_ =>
                {
                    try
                    {
                        CleanupUnusedPipelines(PipelineCleanupMaxIdleTime);
                    }
                    catch
                    {
                        // 清理回调中的异常不应导致进程崩溃
                    }
                }, null, PipelineCleanupInterval, PipelineCleanupInterval);
            }
        }

        /// <summary>
        /// 停止自动清理后台任务
        /// </summary>
        public static void StopPipelineCleanup()
        {
            lock (_cleanupLock)
            {
                if (_cleanupTimer == null) return;

                _cleanupTimer.Change(Timeout.Infinite, Timeout.Infinite);
                _cleanupTimer.Dispose();
                _cleanupTimer = null;
            }
        }

        /// <summary>
        /// 获取当前管道数量
        /// </summary>
        public static int PipelineCount => _pipelines.Count;

        /// <summary>
        /// 清除所有管道（用于测试重置）
        /// </summary>
        public static void ClearAllPipelines()
        {
            StopPipelineCleanup();
            _pipelines.Clear();
        }
    }
}