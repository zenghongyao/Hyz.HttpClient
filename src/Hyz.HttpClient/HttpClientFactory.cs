using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Text.Json;

namespace Hyz.HttpClient
{
    public class SimpleHttpClientFactory : IHttpClientFactory, IHttpMessageHandlerFactory
    {
        private readonly ConcurrentDictionary<string, Lazy<System.Net.Http.HttpClient>> _httpClients = new();
        private readonly ConcurrentDictionary<string, Lazy<HttpMessageHandler>> _httpMessageHandlers = new();
        private readonly Action<System.Net.Http.HttpClient>? _defaultConfigureClient;
        private readonly CertificateOptions? _certificateOptions;

        public SimpleHttpClientFactory(Action<System.Net.Http.HttpClient>? configureClient = null, CertificateOptions? certificateOptions = null)
        {
            _defaultConfigureClient = configureClient;
            _certificateOptions = certificateOptions;
        }

        public System.Net.Http.HttpClient CreateClient()
        {
            return CreateClient(string.Empty);
        }

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

        public void Clear()
        {
            _httpClients.Clear();
            _httpMessageHandlers.Clear();
        }
    }

    public class HyzHttpClientFactory
    {
        private static readonly Lazy<HyzHttpClientFactory> _instance = new(() => new HyzHttpClientFactory(), System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
        private static readonly object _globalLock = new object();

        private readonly SimpleHttpClientFactory _simpleHttpClientFactory;
        private readonly JsonSerializerOptions _defaultJsonOptions;

        public static HyzHttpClientFactory Instance => _instance.Value;

        private static Uri? _globalBaseAddress;
        public static Uri? GlobalBaseAddress
        {
            get { lock (_globalLock) return _globalBaseAddress; }
            set { lock (_globalLock) _globalBaseAddress = value; }
        }

        private static TimeSpan? _globalTimeout;
        public static TimeSpan? GlobalTimeout
        {
            get { lock (_globalLock) return _globalTimeout; }
            set { lock (_globalLock) _globalTimeout = value; }
        }

        private static CertificateOptions? _globalCertificateOptions;
        public static CertificateOptions? GlobalCertificateOptions
        {
            get { lock (_globalLock) return _globalCertificateOptions; }
            set { lock (_globalLock) _globalCertificateOptions = value; }
        }

        private static JsonSerializerOptions? _globalJsonOptions;
        public static JsonSerializerOptions? GlobalJsonOptions
        {
            get { lock (_globalLock) return _globalJsonOptions; }
            set { lock (_globalLock) _globalJsonOptions = value; }
        }

        public HyzHttpClientFactory()
        {
            _defaultJsonOptions = new JsonSerializerOptions(HttpClientPolicy.DefaultJsonOptions);
            _simpleHttpClientFactory = new SimpleHttpClientFactory(ConfigureDefaultClient, GlobalCertificateOptions);
        }

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

        public HttpClientRequest Create()
        {
            return Create(null, null, null, null);
        }

        public HttpClientRequest Create(ILogger<HttpClientRequest>? logger)
        {
            return Create(logger, null, null, null);
        }

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

        public static HttpClientRequest CreateInstance()
        {
            return Instance.Create();
        }

        public static HttpClientRequest CreateInstance(ILogger<HttpClientRequest>? logger)
        {
            return Instance.Create(logger);
        }

        public static HttpClientRequest CreateInstance(ILogger<HttpClientRequest>? logger = null, Uri? baseAddress = null, TimeSpan? timeout = null, JsonSerializerOptions? jsonOptions = null)
        {
            return Instance.Create(logger, baseAddress, timeout, jsonOptions);
        }

        public static HttpClientRequest CreateNamedInstance(string clientName, Action<System.Net.Http.HttpClient>? configureClient = null, ILogger<HttpClientRequest>? logger = null, JsonSerializerOptions? jsonOptions = null)
        {
            return Instance.CreateNamedClient(clientName, configureClient, logger, jsonOptions);
        }

        public static void Initialize(Uri? baseAddress = null, TimeSpan? timeout = null, CertificateOptions? certificateOptions = null, JsonSerializerOptions? jsonOptions = null)
        {
            GlobalBaseAddress = baseAddress;
            GlobalTimeout = timeout;
            GlobalCertificateOptions = certificateOptions;
            GlobalJsonOptions = jsonOptions;
        }

        public static void ResetGlobalConfiguration()
        {
            GlobalBaseAddress = null;
            GlobalTimeout = null;
            GlobalCertificateOptions = null;
            GlobalJsonOptions = null;
        }
    }
}