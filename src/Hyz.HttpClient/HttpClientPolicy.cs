using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using System;
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
        /// true: 忽略所有SSL证书验证错误（默认，便于开发调试）<br/>
        /// false: 启用严格的证书验证（生产环境推荐）<br/>
        /// 警告：生产环境建议设置为 false 并配置正确的证书验证
        /// </remarks>
        public static bool IgnoreCertificateErrors { get; set; } = true;

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
            /// 失败率阈值
            /// </summary>
            public double FailureRatio { get; set; } = 1.0;

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

        private static ResiliencePipeline<object>? _cachedPipeline;
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
                _cachedPipeline = null;
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
                _cachedPipeline = null;
            }
        }

        /// <summary>
        /// 获取API管道策略（重试+熔断）
        /// </summary>
        public static ResiliencePipeline<object> GetApiPipelinePolicy
        {
            get
            {
                lock (_lock)
                {
                    if (_cachedPipeline != null)
                    {
                        return _cachedPipeline;
                    }

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
                            _retryOptions.OnRetry?.Invoke(args);
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
                            _circuitBreakerOptions.OnOpened?.Invoke(args);
                            return new ValueTask();
                        },
                        OnClosed = args =>
                        {
                            _circuitBreakerOptions.OnClosed?.Invoke(args);
                            return new ValueTask();
                        },
                        OnHalfOpened = args =>
                        {
                            _circuitBreakerOptions.OnHalfOpened?.Invoke(args);
                            return new ValueTask();
                        }
                    };
                    pipelineBuilder.AddCircuitBreaker(circuitBreakerStrategyOptions);

                    _cachedPipeline = pipelineBuilder.Build();
                    return _cachedPipeline;
                }
            }
        }
    }
}