using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Text.Json;

namespace Hyz.HttpClient
{
    /// <summary>
    /// 轻量级 HttpClientFactory 实现，用于不支持依赖注入的环境（如 .NET Framework）
    /// </summary>
    /// <remarks>
    /// 实现 IHttpClientFactory 和 IHttpMessageHandlerFactory 接口，内部使用 ConcurrentDictionary 
    /// 管理命名客户端实例池和 HttpMessageHandler 实例池，确保线程安全和 HttpClient 复用，避免 Socket 泄露。
    /// 
    /// 设计要点：
    /// 1. 使用 Lazy<T> 实现延迟初始化，确保线程安全的单例创建
    /// 2. disposeHandler 设置为 false，避免多次释放同一个 HttpMessageHandler
    /// 3. 支持默认配置回调，所有客户端共享基础配置
    /// 4. 支持证书配置，可自定义证书验证逻辑
    /// </remarks>
    public class SimpleHttpClientFactory : IHttpClientFactory, IHttpMessageHandlerFactory
    {
        /// <summary>
        /// 命名客户端实例池，key 为客户端名称，value 为延迟初始化的 HttpClient 实例
        /// </summary>
        private readonly ConcurrentDictionary<string, Lazy<System.Net.Http.HttpClient>> _httpClients = new();

        /// <summary>
        /// HttpMessageHandler 实例池，key 为处理器名称，value 为延迟初始化的 HttpMessageHandler 实例
        /// </summary>
        private readonly ConcurrentDictionary<string, Lazy<HttpMessageHandler>> _httpMessageHandlers = new();

        /// <summary>
        /// 默认的客户端配置回调，所有新创建的客户端都会执行此配置
        /// </summary>
        private readonly Action<System.Net.Http.HttpClient>? _defaultConfigureClient;

        /// <summary>
        /// 证书配置选项，用于配置 HTTPS 证书验证
        /// </summary>
        private readonly CertificateOptions? _certificateOptions;

        /// <summary>
        /// 初始化 SimpleHttpClientFactory
        /// </summary>
        /// <param name="configureClient">默认 HttpClient 配置回调，所有客户端创建时都会执行</param>
        /// <param name="certificateOptions">证书配置选项，用于 HTTPS 请求的证书验证</param>
        public SimpleHttpClientFactory(Action<System.Net.Http.HttpClient>? configureClient = null, CertificateOptions? certificateOptions = null)
        {
            _defaultConfigureClient = configureClient;
            _certificateOptions = certificateOptions;
        }

        /// <summary>
        /// 创建默认 HttpClient 实例（使用空字符串作为客户端名称）
        /// </summary>
        /// <returns>HttpClient 实例</returns>
        public System.Net.Http.HttpClient CreateClient()
        {
            return CreateClient(string.Empty);
        }

        /// <summary>
        /// 创建指定名称的 HttpClient 实例
        /// </summary>
        /// <remarks>
        /// 相同名称的客户端会返回同一个实例，实现客户端复用，避免频繁创建和销毁 HttpClient
        /// 导致的 Socket 资源泄露问题。
        /// </remarks>
        /// <param name="name">客户端名称，用于区分不同的客户端实例</param>
        /// <returns>HttpClient 实例</returns>
        public System.Net.Http.HttpClient CreateClient(string name)
        {
            var key = name ?? string.Empty;
            return _httpClients.GetOrAdd(key, _ => new Lazy<System.Net.Http.HttpClient>(() =>
            {
                var handler = CreateHandler(key);
                var client = new System.Net.Http.HttpClient(handler, disposeHandler: false);
                _defaultConfigureClient?.Invoke(client);
                return client;
            }, System.Threading.LazyThreadSafetyMode.ExecutionAndPublication)).Value;
        }

        /// <summary>
        /// 创建 HttpMessageHandler 实例
        /// </summary>
        /// <remarks>
        /// 相同名称的处理器会返回同一个实例，实现处理器复用。
        /// 如果没有提供证书配置，会使用全局的 HttpClientPolicy.IgnoreCertificateErrors 配置。
        /// </remarks>
        /// <param name="name">处理器名称，用于区分不同的处理器实例</param>
        /// <returns>HttpMessageHandler 实例</returns>
        public HttpMessageHandler CreateHandler(string name)
        {
            var key = name ?? string.Empty;
            return _httpMessageHandlers.GetOrAdd(key, _ => new Lazy<HttpMessageHandler>(() =>
            {
                var options = _certificateOptions ?? new CertificateOptions
                {
                    IgnoreCertificateErrors = HttpClientPolicy.IgnoreCertificateErrors
                };
                return HttpClientExtensions.CreateHttpMessageHandler(options);
            }, System.Threading.LazyThreadSafetyMode.ExecutionAndPublication)).Value;
        }

        /// <summary>
        /// 为指定名称的客户端配置自定义回调
        /// </summary>
        /// <remarks>
        /// 此方法会先移除已存在的客户端实例，然后重新创建带有新配置的客户端。
        /// 注意：新配置会与默认配置合并（先执行默认配置，再执行自定义配置）。
        /// </remarks>
        /// <param name="name">客户端名称</param>
        /// <param name="configureClient">自定义客户端配置回调</param>
        public void ConfigureClient(string name, Action<System.Net.Http.HttpClient> configureClient)
        {
            var key = name ?? string.Empty;
            _httpClients.TryRemove(key, out _);
            _httpClients.GetOrAdd(key, _ => new Lazy<System.Net.Http.HttpClient>(() =>
            {
                var handler = CreateHandler(key);
                var client = new System.Net.Http.HttpClient(handler, disposeHandler: false);
                _defaultConfigureClient?.Invoke(client);
                configureClient.Invoke(client);
                return client;
            }, System.Threading.LazyThreadSafetyMode.ExecutionAndPublication));
        }

        /// <summary>
        /// 清除所有已创建的客户端和处理器实例
        /// </summary>
        /// <remarks>
        /// 调用此方法后，下次创建客户端时会重新初始化。
        /// 注意：此方法不会释放已创建的 HttpClient 实例，因为它们可能正在被使用。
        /// </remarks>
        public void Clear()
        {
            _httpClients.Clear();
            _httpMessageHandlers.Clear();
        }
    }

    /// <summary>
    /// Hyz.HttpClient 工厂类，专门为无依赖注入环境（如 .NET Framework）设计
    /// </summary>
    /// <remarks>
    /// 兼容环境：.NET Framework 4.7.2+、.NET Core 2.0+、.NET 5+
    /// 无需任何 DI 容器即可开箱即用，支持静态方法快速调用和实例化配置两种模式。
    /// 
    /// 设计要点：
    /// 1. 单例模式：通过 Instance 属性获取全局唯一实例
    /// 2. 全局配置：支持全局默认 BaseAddress、Timeout、证书配置等
    /// 3. 线程安全：全局配置属性使用 lock 保护，确保多线程环境下的安全性
    /// 4. 客户端池化：默认情况下复用内部的 SimpleHttpClientFactory 实例，实现命名客户端池化
    /// 5. 灵活配置：支持创建时覆盖全局配置，也支持自定义配置回调
    /// </remarks>
    public class HyzHttpClientFactory
    {
        /// <summary>
        /// 全局单例实例，使用 Lazy<T> 实现延迟初始化和线程安全
        /// </summary>
        private static readonly Lazy<HyzHttpClientFactory> _instance = new(() => new HyzHttpClientFactory(), System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);

        /// <summary>
        /// 全局配置锁，用于保护静态配置属性的线程安全访问
        /// </summary>
        private static readonly object _globalLock = new object();

        /// <summary>
        /// 内部的 SimpleHttpClientFactory 实例，用于创建和管理 HttpClient 实例池
        /// </summary>
        private readonly SimpleHttpClientFactory _simpleHttpClientFactory;

        /// <summary>
        /// 默认的 JSON 序列化选项
        /// </summary>
        private readonly JsonSerializerOptions _defaultJsonOptions;

        /// <summary>
        /// 获取全局单例实例
        /// </summary>
        /// <value>HyzHttpClientFactory 的全局唯一实例</value>
        public static HyzHttpClientFactory Instance => _instance.Value;

        /// <summary>
        /// 全局默认 BaseAddress，所有通过工厂创建的 HttpClient 都会使用此地址（除非在创建时覆盖）
        /// </summary>
        /// <remarks>
        /// 使用线程安全的 getter/setter，确保多线程环境下的安全性
        /// </remarks>
        private static Uri? _globalBaseAddress;
        public static Uri? GlobalBaseAddress
        {
            get { lock (_globalLock) return _globalBaseAddress; }
            set { lock (_globalLock) _globalBaseAddress = value; }
        }

        /// <summary>
        /// 全局默认超时时间，所有通过工厂创建的 HttpClient 都会使用此超时时间（除非在创建时覆盖）
        /// </summary>
        /// <remarks>
        /// 使用线程安全的 getter/setter，确保多线程环境下的安全性
        /// </remarks>
        private static TimeSpan? _globalTimeout;
        public static TimeSpan? GlobalTimeout
        {
            get { lock (_globalLock) return _globalTimeout; }
            set { lock (_globalLock) _globalTimeout = value; }
        }

        /// <summary>
        /// 全局默认证书配置，所有通过工厂创建的 HttpClient 都会使用此配置（除非在创建时覆盖）
        /// </summary>
        /// <remarks>
        /// 使用线程安全的 getter/setter，确保多线程环境下的安全性
        /// </remarks>
        private static CertificateOptions? _globalCertificateOptions;
        public static CertificateOptions? GlobalCertificateOptions
        {
            get { lock (_globalLock) return _globalCertificateOptions; }
            set { lock (_globalLock) _globalCertificateOptions = value; }
        }

        /// <summary>
        /// 全局默认 JSON 序列化配置，所有通过工厂创建的 HttpClientRequest 都会使用此配置
        /// </summary>
        /// <remarks>
        /// 使用线程安全的 getter/setter，确保多线程环境下的安全性
        /// </remarks>
        private static JsonSerializerOptions? _globalJsonOptions;
        public static JsonSerializerOptions? GlobalJsonOptions
        {
            get { lock (_globalLock) return _globalJsonOptions; }
            set { lock (_globalLock) _globalJsonOptions = value; }
        }

        /// <summary>
        /// 使用默认配置初始化 HyzHttpClientFactory
        /// </summary>
        /// <remarks>
        /// 默认使用 HttpClientPolicy.DefaultJsonOptions 作为 JSON 序列化配置，
        /// 使用全局证书配置（如果已设置）。
        /// </remarks>
        public HyzHttpClientFactory()
        {
            _defaultJsonOptions = new JsonSerializerOptions(HttpClientPolicy.DefaultJsonOptions);
            _simpleHttpClientFactory = new SimpleHttpClientFactory(ConfigureDefaultClient, GlobalCertificateOptions);
        }

        /// <summary>
        /// 使用指定配置初始化 HyzHttpClientFactory
        /// </summary>
        /// <remarks>
        /// 此构造函数会同时设置全局配置属性，因此会影响后续所有使用静态方法创建的实例。
        /// 如果需要独立配置，建议使用默认构造函数后调用 Create 方法时传入参数。
        /// </remarks>
        /// <param name="baseAddress">默认 BaseAddress，会设置为全局配置</param>
        /// <param name="timeout">默认超时时间，会设置为全局配置</param>
        /// <param name="certificateOptions">证书配置，会设置为全局配置</param>
        /// <param name="jsonOptions">JSON 序列化配置，仅用于此实例</param>
        public HyzHttpClientFactory(Uri? baseAddress = null, TimeSpan? timeout = null, CertificateOptions? certificateOptions = null, JsonSerializerOptions? jsonOptions = null)
        {
            GlobalBaseAddress = baseAddress;
            GlobalTimeout = timeout;
            GlobalCertificateOptions = certificateOptions;

            _defaultJsonOptions = jsonOptions != null 
                ? new JsonSerializerOptions(jsonOptions) 
                : new JsonSerializerOptions(HttpClientPolicy.DefaultJsonOptions);

            _simpleHttpClientFactory = new SimpleHttpClientFactory(ConfigureDefaultClient, certificateOptions);
        }

        /// <summary>
        /// 配置默认 HttpClient 的方法，应用全局配置
        /// </summary>
        /// <param name="client">待配置的 HttpClient 实例</param>
        private void ConfigureDefaultClient(System.Net.Http.HttpClient client)
        {
            var baseAddress = GlobalBaseAddress;
            var timeout = GlobalTimeout;

            if (baseAddress != null)
            {
                client.BaseAddress = baseAddress;
            }
            if (timeout.HasValue)
            {
                client.Timeout = timeout.Value;
            }
        }

        /// <summary>
        /// 创建 HttpClientRequest 实例（使用全局配置）
        /// </summary>
        /// <returns>HttpClientRequest 实例</returns>
        public HttpClientRequest Create()
        {
            return Create(null, null, null, null);
        }

        /// <summary>
        /// 创建 HttpClientRequest 实例（指定日志记录器）
        /// </summary>
        /// <param name="logger">日志记录器，默认为 NullLogger（不输出日志）</param>
        /// <returns>HttpClientRequest 实例</returns>
        public HttpClientRequest Create(ILogger<HttpClientRequest>? logger)
        {
            return Create(logger, null, null, null);
        }

        /// <summary>
        /// 创建 HttpClientRequest 实例（完整配置）
        /// </summary>
        /// <remarks>
        /// 优先级规则：
        /// 1. 如果传入了 baseAddress 或 timeout，会创建新的 SimpleHttpClientFactory 实例，使用传入的配置
        /// 2. 如果没有传入自定义配置，会复用内部的 _simpleHttpClientFactory 实例，实现客户端池化
        /// 3. 局部配置（参数传入）优先于全局配置
        /// </remarks>
        /// <param name="logger">日志记录器，默认为 NullLogger（不输出日志）</param>
        /// <param name="baseAddress">BaseAddress，覆盖全局配置</param>
        /// <param name="timeout">超时时间，覆盖全局配置</param>
        /// <param name="jsonOptions">JSON 序列化配置，覆盖默认配置</param>
        /// <returns>HttpClientRequest 实例</returns>
        public HttpClientRequest Create(ILogger<HttpClientRequest>? logger = null, Uri? baseAddress = null, TimeSpan? timeout = null, JsonSerializerOptions? jsonOptions = null)
        {
            if (baseAddress != null || timeout.HasValue)
            {
                var factory = new SimpleHttpClientFactory(client =>
                {
                    if (baseAddress != null)
                    {
                        client.BaseAddress = baseAddress;
                    }
                    else if (GlobalBaseAddress != null)
                    {
                        client.BaseAddress = GlobalBaseAddress;
                    }

                    if (timeout.HasValue)
                    {
                        client.Timeout = timeout.Value;
                    }
                    else if (GlobalTimeout.HasValue)
                    {
                        client.Timeout = GlobalTimeout.Value;
                    }
                }, GlobalCertificateOptions);

                var effectiveJsonOptions = jsonOptions != null 
                    ? new JsonSerializerOptions(jsonOptions) 
                    : _defaultJsonOptions;

                return new HttpClientRequest(logger ?? NullLogger<HttpClientRequest>.Instance, factory, effectiveJsonOptions);
            }

            var effectiveJson = jsonOptions != null 
                ? new JsonSerializerOptions(jsonOptions) 
                : _defaultJsonOptions;

            return new HttpClientRequest(logger ?? NullLogger<HttpClientRequest>.Instance, _simpleHttpClientFactory, effectiveJson);
        }

        /// <summary>
        /// 创建命名 HttpClientRequest 实例
        /// </summary>
        /// <remarks>
        /// 优先级规则：
        /// 1. 如果传入了 configureClient，会创建新的 SimpleHttpClientFactory 实例，使用传入的配置
        /// 2. 如果没有传入自定义配置，会复用内部的 _simpleHttpClientFactory 实例，实现客户端池化
        /// </remarks>
        /// <param name="clientName">客户端名称，用于区分不同的客户端实例</param>
        /// <param name="configureClient">客户端配置回调，用于自定义客户端配置</param>
        /// <param name="logger">日志记录器，默认为 NullLogger（不输出日志）</param>
        /// <param name="jsonOptions">JSON 序列化配置，覆盖默认配置</param>
        /// <returns>HttpClientRequest 实例</returns>
        public HttpClientRequest CreateNamedClient(string clientName, Action<System.Net.Http.HttpClient>? configureClient = null, ILogger<HttpClientRequest>? logger = null, JsonSerializerOptions? jsonOptions = null)
        {
            if (configureClient != null)
            {
                var factory = new SimpleHttpClientFactory(configureClient, GlobalCertificateOptions);
                var effectiveJsonOptions = jsonOptions != null 
                    ? new JsonSerializerOptions(jsonOptions) 
                    : _defaultJsonOptions;

                return new HttpClientRequest(logger ?? NullLogger<HttpClientRequest>.Instance, factory, effectiveJsonOptions);
            }

            var effectiveJson = jsonOptions != null 
                ? new JsonSerializerOptions(jsonOptions) 
                : _defaultJsonOptions;

            return new HttpClientRequest(logger ?? NullLogger<HttpClientRequest>.Instance, _simpleHttpClientFactory, effectiveJson);
        }

        /// <summary>
        /// 静态方法：创建 HttpClientRequest 实例（使用全局配置）
        /// </summary>
        /// <remarks>
        /// 等价于 HyzHttpClientFactory.Instance.Create()
        /// </remarks>
        /// <returns>HttpClientRequest 实例</returns>
        public static HttpClientRequest CreateInstance()
        {
            return Instance.Create();
        }

        /// <summary>
        /// 静态方法：创建 HttpClientRequest 实例（指定日志记录器）
        /// </summary>
        /// <remarks>
        /// 等价于 HyzHttpClientFactory.Instance.Create(logger)
        /// </remarks>
        /// <param name="logger">日志记录器，默认为 NullLogger（不输出日志）</param>
        /// <returns>HttpClientRequest 实例</returns>
        public static HttpClientRequest CreateInstance(ILogger<HttpClientRequest>? logger)
        {
            return Instance.Create(logger);
        }

        /// <summary>
        /// 静态方法：创建 HttpClientRequest 实例（完整配置）
        /// </summary>
        /// <remarks>
        /// 等价于 HyzHttpClientFactory.Instance.Create(logger, baseAddress, timeout, jsonOptions)
        /// </remarks>
        /// <param name="logger">日志记录器，默认为 NullLogger（不输出日志）</param>
        /// <param name="baseAddress">BaseAddress，覆盖全局配置</param>
        /// <param name="timeout">超时时间，覆盖全局配置</param>
        /// <param name="jsonOptions">JSON 序列化配置，覆盖默认配置</param>
        /// <returns>HttpClientRequest 实例</returns>
        public static HttpClientRequest CreateInstance(ILogger<HttpClientRequest>? logger = null, Uri? baseAddress = null, TimeSpan? timeout = null, JsonSerializerOptions? jsonOptions = null)
        {
            return Instance.Create(logger, baseAddress, timeout, jsonOptions);
        }

        /// <summary>
        /// 静态方法：创建命名 HttpClientRequest 实例
        /// </summary>
        /// <remarks>
        /// 等价于 HyzHttpClientFactory.Instance.CreateNamedClient(clientName, configureClient, logger, jsonOptions)
        /// </remarks>
        /// <param name="clientName">客户端名称，用于区分不同的客户端实例</param>
        /// <param name="configureClient">客户端配置回调，用于自定义客户端配置</param>
        /// <param name="logger">日志记录器，默认为 NullLogger（不输出日志）</param>
        /// <param name="jsonOptions">JSON 序列化配置，覆盖默认配置</param>
        /// <returns>HttpClientRequest 实例</returns>
        public static HttpClientRequest CreateNamedInstance(string clientName, Action<System.Net.Http.HttpClient>? configureClient = null, ILogger<HttpClientRequest>? logger = null, JsonSerializerOptions? jsonOptions = null)
        {
            return Instance.CreateNamedClient(clientName, configureClient, logger, jsonOptions);
        }

        /// <summary>
        /// 静态方法：初始化全局配置
        /// </summary>
        /// <remarks>
        /// 建议在应用启动时（如 Program.cs 或 Startup.cs）调用此方法配置全局参数。
        /// 配置后，所有通过静态方法或实例方法创建的 HttpClientRequest 都会使用这些默认配置。
        /// </remarks>
        /// <param name="baseAddress">默认 BaseAddress</param>
        /// <param name="timeout">默认超时时间</param>
        /// <param name="certificateOptions">证书配置</param>
        /// <param name="jsonOptions">JSON 序列化配置</param>
        public static void Initialize(Uri? baseAddress = null, TimeSpan? timeout = null, CertificateOptions? certificateOptions = null, JsonSerializerOptions? jsonOptions = null)
        {
            GlobalBaseAddress = baseAddress;
            GlobalTimeout = timeout;
            GlobalCertificateOptions = certificateOptions;
            GlobalJsonOptions = jsonOptions;
        }

        /// <summary>
        /// 静态方法：重置全局配置为默认值（null）
        /// </summary>
        /// <remarks>
        /// 主要用于单元测试清理，确保测试之间的隔离。
        /// 在生产环境中通常不需要调用此方法。
        /// </remarks>
        public static void ResetGlobalConfiguration()
        {
            GlobalBaseAddress = null;
            GlobalTimeout = null;
            GlobalCertificateOptions = null;
            GlobalJsonOptions = null;
        }
    }
}