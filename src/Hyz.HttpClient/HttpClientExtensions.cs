using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace Hyz.HttpClient
{
    /// <summary>
    /// Hyz.HttpClient服务集合扩展
    /// </summary>
    public static class HttpClientExtensions
    {
        /// <summary>
        /// 添加Hyz.HttpClient服务
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="httpClientName">HttpClient名称（可选）</param>
        /// <param name="configureClient">配置HttpClient（可选）</param>
        /// <param name="configureJsonSerializer">配置JsonSerializerOptions（可选）</param>
        /// <returns>服务集合</returns>
        public static IServiceCollection AddHyzHttpClient(
            this IServiceCollection services,
            string? httpClientName = null,
            Action<System.Net.Http.HttpClient>? configureClient = null,
            Action<JsonSerializerOptions>? configureJsonSerializer = null)
        {
            var certificateOptions = new CertificateOptions
            {
                IgnoreCertificateErrors = HttpClientPolicy.IgnoreCertificateErrors
            };

            // 使用Microsoft的AddHttpClient方法注册HttpClientFactory
            if (string.IsNullOrEmpty(httpClientName))
            {
                services.AddHttpClient();
                services.ConfigureAll<HttpClientFactoryOptions>(options =>
                {
                    options.HttpMessageHandlerBuilderActions.Add(builder =>
                    {
                        builder.PrimaryHandler = CreateHttpMessageHandler(certificateOptions);
                    });
                });
            }
            else
            {
                services.AddHttpClient(httpClientName!, configureClient)
                    .ConfigurePrimaryHttpMessageHandler(() => CreateHttpMessageHandler(certificateOptions));
            }

            // 创建JsonSerializerOptions实例
            var jsonSerializerOptions = HttpClientPolicy.DefaultJsonOptions;

            // 应用用户配置的JsonSerializerOptions
            configureJsonSerializer?.Invoke(jsonSerializerOptions);

            // 注册HttpClientRequest服务
            services.AddSingleton<HttpClientRequest>(sp =>
            {
                var factory = sp.GetRequiredService<IHttpClientFactory>();
                var logger = sp.GetRequiredService<ILogger<HttpClientRequest>>();
                return new HttpClientRequest(logger, factory, jsonSerializerOptions);
            });

            return services;
        }

        /// <summary>
        /// 添加Hyz.HttpClient服务（支持证书配置）
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="httpClientName">HttpClient名称</param>
        /// <param name="configureClient">配置HttpClient（可选）</param>
        /// <param name="configureJsonSerializer">配置JsonSerializerOptions（可选）</param>
        /// <param name="configureCertificate">配置证书选项（可选）</param>
        /// <returns>服务集合</returns>
        public static IServiceCollection AddHyzHttpClient(
            this IServiceCollection services,
            string httpClientName,
            Action<System.Net.Http.HttpClient>? configureClient,
            Action<JsonSerializerOptions>? configureJsonSerializer,
            Action<CertificateOptions>? configureCertificate)
        {
            CertificateOptions certificateOptions;
            
            if (configureCertificate != null)
            {
                certificateOptions = new CertificateOptions
                {
                    IgnoreCertificateErrors = false
                };
                configureCertificate.Invoke(certificateOptions);
            }
            else
            {
                certificateOptions = new CertificateOptions
                {
                    IgnoreCertificateErrors = HttpClientPolicy.IgnoreCertificateErrors
                };
            }

            services.AddHttpClient(httpClientName, configureClient)
                .ConfigurePrimaryHttpMessageHandler(() => CreateHttpMessageHandler(certificateOptions));

            var jsonSerializerOptions = HttpClientPolicy.DefaultJsonOptions;

            configureJsonSerializer?.Invoke(jsonSerializerOptions);

            services.AddSingleton<HttpClientRequest>(sp =>
            {
                var factory = sp.GetRequiredService<IHttpClientFactory>();
                var logger = sp.GetRequiredService<ILogger<HttpClientRequest>>();
                return new HttpClientRequest(logger, factory, jsonSerializerOptions);
            });

            return services;
        }

        /// <summary>
        /// 创建HttpMessageHandler
        /// </summary>
        private static HttpMessageHandler CreateHttpMessageHandler(CertificateOptions options)
        {
            var handler = new HttpClientHandler();

            if (options.ServerCertificateValidationCallback != null)
            {
                handler.ServerCertificateCustomValidationCallback = options.ServerCertificateValidationCallback;
            }
            else if (options.IgnoreCertificateErrors)
            {
                handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
            }

            if (options.ClientCertificates?.Count > 0)
            {
                handler.ClientCertificates.AddRange(options.ClientCertificates);
            }

            if (options.SslProtocols.HasValue)
            {
                handler.SslProtocols = options.SslProtocols.Value;
            }

            return handler;
        }
    }

    /// <summary>
    /// 证书配置选项
    /// </summary>
    public class CertificateOptions
    {
        /// <summary>
        /// 是否忽略证书错误（默认使用 HttpClientPolicy.IgnoreCertificateErrors）
        /// </summary>
        public bool IgnoreCertificateErrors { get; set; }

        /// <summary>
        /// 服务器证书验证回调
        /// </summary>
        public Func<HttpRequestMessage, X509Certificate2?, X509Chain?, SslPolicyErrors, bool>? ServerCertificateValidationCallback { get; set; }

        /// <summary>
        /// 客户端证书集合
        /// </summary>
        public X509CertificateCollection? ClientCertificates { get; set; }

        /// <summary>
        /// SSL协议版本
        /// </summary>
        public SslProtocols? SslProtocols { get; set; }

        /// <summary>
        /// 添加客户端证书（从文件）
        /// </summary>
        /// <param name="certificatePath">证书文件路径</param>
        /// <param name="password">证书密码（可选）</param>
        public void AddClientCertificate(string certificatePath, string? password = null)
        {
            ClientCertificates ??= new X509CertificateCollection();
            
            var certificate = string.IsNullOrEmpty(password)
                ? new X509Certificate2(certificatePath)
                : new X509Certificate2(certificatePath, password);
            
            ClientCertificates.Add(certificate);
        }

        /// <summary>
        /// 添加客户端证书（从字节数组）
        /// </summary>
        /// <param name="rawData">证书原始数据</param>
        /// <param name="password">证书密码（可选）</param>
        public void AddClientCertificate(byte[] rawData, string? password = null)
        {
            ClientCertificates ??= new X509CertificateCollection();
            
            var certificate = string.IsNullOrEmpty(password)
                ? new X509Certificate2(rawData)
                : new X509Certificate2(rawData, password);
            
            ClientCertificates.Add(certificate);
        }
    }
}
