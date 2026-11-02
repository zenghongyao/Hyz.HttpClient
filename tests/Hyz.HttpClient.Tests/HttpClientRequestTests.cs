using System;
using System.Net;
using HttpClientType = System.Net.Http.HttpClient;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hyz.HttpClient;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace Hyz.HttpClient.Tests
{
    /// <summary>
    /// HttpClientRequest测试
    /// </summary>
    [Collection("RequestInterception")]
    public class HttpClientRequestTests
    {
        private class TestResponse
        {
            public int Code { get; set; }
            public bool Result => Code == 0;
            public string? Message { get; set; }
        }

        private class TestRequest : BaseRequest<TestResponse>
        {
        }

        private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
        private readonly Mock<ILogger<HttpClientRequest>> _mockLogger;
        private readonly HttpClientType _httpClient;

        public HttpClientRequestTests()
        {
            _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            _mockLogger = new Mock<ILogger<HttpClientRequest>>();
            _httpClient = new HttpClientType(_mockHttpMessageHandler.Object)
            {
                BaseAddress = new Uri("https://api.example.com")
            };
        }

        /// <summary>
        /// 验证普通请求初始默认关闭重试
        /// </summary>
        [Fact]
        public void EnableRetryByDefault_ShouldBeFalse()
        {
            Assert.False(HttpClientPolicy.EnableRetryByDefault);
        }

        /// <summary>
        /// 验证普通请求使用全局默认值且单次参数可覆盖全局配置
        /// </summary>
        /// <param name="method">请求入口名称</param>
        /// <param name="globalDefault">全局重试默认值</param>
        /// <param name="enableRetry">单次重试参数，null 时省略调用参数</param>
        /// <param name="expectedAttempts">预期发送次数</param>
        /// <returns>任务完成时已验证响应或异常以及实际发送次数</returns>
        [Theory]
        [InlineData("Execute", false, null, 1)]
        [InlineData("Execute", true, null, 2)]
        [InlineData("Execute", false, true, 2)]
        [InlineData("Execute", true, false, 1)]
        [InlineData("Execute", false, false, 1)]
        [InlineData("Execute", true, true, 2)]
        [InlineData("Get", false, null, 1)]
        [InlineData("Get", true, null, 2)]
        [InlineData("Get", false, true, 2)]
        [InlineData("Get", true, false, 1)]
        [InlineData("Get", false, false, 1)]
        [InlineData("Get", true, true, 2)]
        [InlineData("Post", false, null, 1)]
        [InlineData("Post", true, null, 2)]
        [InlineData("Post", false, true, 2)]
        [InlineData("Post", true, false, 1)]
        [InlineData("Post", false, false, 1)]
        [InlineData("Post", true, true, 2)]
        [InlineData("Put", false, null, 1)]
        [InlineData("Put", true, null, 2)]
        [InlineData("Put", false, true, 2)]
        [InlineData("Put", true, false, 1)]
        [InlineData("Put", false, false, 1)]
        [InlineData("Put", true, true, 2)]
        [InlineData("Delete", false, null, 1)]
        [InlineData("Delete", true, null, 2)]
        [InlineData("Delete", false, true, 2)]
        [InlineData("Delete", true, false, 1)]
        [InlineData("Delete", false, false, 1)]
        [InlineData("Delete", true, true, 2)]
        [InlineData("Patch", false, null, 1)]
        [InlineData("Patch", true, null, 2)]
        [InlineData("Patch", false, true, 2)]
        [InlineData("Patch", true, false, 1)]
        [InlineData("Patch", false, false, 1)]
        [InlineData("Patch", true, true, 2)]
        public async Task ExecuteAsync_ShouldRespectRetryConfiguration(
            string method, bool globalDefault, bool? enableRetry, int expectedAttempts)
        {
            var originalDefault = HttpClientPolicy.EnableRetryByDefault;
            var attempts = 0;
            try
            {
                HttpClientPolicy.EnableRetryByDefault = globalDefault;
                var request = new TestRequest();
                request.SetRequestApi("/api/retry/" + Guid.NewGuid().ToString("N"));
                _mockHttpMessageHandler.Protected()
                    .Setup<Task<HttpResponseMessage>>("SendAsync",
                        ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                    .Returns(() =>
                    {
                        attempts++;
                        if (attempts == 1)
                        {
                            return Task.FromException<HttpResponseMessage>(new HttpRequestException("模拟瞬时请求失败"));
                        }
                        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent("{\"code\":0}", Encoding.UTF8, "application/json")
                        });
                    });
                var service = new HttpClientRequest(_mockLogger.Object,
                    new TestHttpClientFactory(_httpClient),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                // null 分支省略可选参数，以验证各入口的真实默认行为
                Func<Task<TestResponse?>> executeRequest = () => method switch
                {
                    "Execute" => enableRetry.HasValue
                        ? service.ExecuteAsync(request, enableRetry: enableRetry.Value)
                        : service.ExecuteAsync(request),
                    "Get" => enableRetry.HasValue
                        ? service.ExecuteGetAsync(request, enableRetry: enableRetry.Value)
                        : service.ExecuteGetAsync(request),
                    "Post" => enableRetry.HasValue
                        ? service.ExecutePostAsync(request, enableRetry: enableRetry.Value)
                        : service.ExecutePostAsync(request),
                    "Put" => enableRetry.HasValue
                        ? service.ExecutePutAsync(request, enableRetry: enableRetry.Value)
                        : service.ExecutePutAsync(request),
                    "Delete" => enableRetry.HasValue
                        ? service.ExecuteDeleteAsync(request, enableRetry: enableRetry.Value)
                        : service.ExecuteDeleteAsync(request),
                    "Patch" => enableRetry.HasValue
                        ? service.ExecutePatchAsync(request, enableRetry: enableRetry.Value)
                        : service.ExecutePatchAsync(request),
                    _ => throw new ArgumentOutOfRangeException(nameof(method))
                };

                if (expectedAttempts == 1)
                {
                    await Assert.ThrowsAsync<HttpRequestException>(executeRequest);
                }
                else
                {
                    var response = await executeRequest();
                    Assert.NotNull(response);
                    Assert.True(response.Result);
                }
                Assert.Equal(expectedAttempts, attempts);
            }
            finally
            {
                HttpClientPolicy.EnableRetryByDefault = originalDefault;
                HttpClientPolicy.ClearAllPipelines();
            }
        }

        [Fact]
        public async Task ExecuteGetAsync_ShouldSendGetRequest()
        {
            // Arrange
            var request = new TestRequest();
            request.SetRequestApi("/api/test");

            var expectedResponse = new TestResponse { Code = 0, Message = "Success" };
            var responseContent = JsonSerializer.Serialize(expectedResponse);

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Get),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(responseContent, Encoding.UTF8, "application/json")
                });

            var jsonSerializerOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var service = new HttpClientRequest(_mockLogger.Object,
                new TestHttpClientFactory(_httpClient),
                jsonSerializerOptions);

            // Act
            var response = await service.ExecuteGetAsync<TestResponse>(request);

            // Assert
            Assert.NotNull(response);
            Assert.True(response.Result);
            Assert.Equal("Success", response.Message);
        }

        [Fact]
        public async Task ExecutePostAsync_ShouldSendPostRequest()
        {
            // Arrange
            var request = new TestRequest();
            request.SetRequestApi("/api/test");
            request.SetBody(new { Name = "Test" });

            var expectedResponse = new TestResponse { Code = 0, Message = "Success" };
            var responseContent = JsonSerializer.Serialize(expectedResponse);

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Post),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(responseContent, Encoding.UTF8, "application/json")
                });

            var jsonSerializerOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var service = new HttpClientRequest(_mockLogger.Object,
                new TestHttpClientFactory(_httpClient),
                jsonSerializerOptions);

            // Act
            var response = await service.ExecutePostAsync<TestResponse>(request);

            // Assert
            Assert.NotNull(response);
            Assert.True(response.Result);
        }

        [Fact]
        public async Task ExecutePutAsync_ShouldSendPutRequest()
        {
            // Arrange
            var request = new TestRequest();
            request.SetRequestApi("/api/test");
            request.SetBody(new { Name = "Updated" });

            var expectedResponse = new TestResponse { Code = 0 };
            var responseContent = JsonSerializer.Serialize(expectedResponse);

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Put),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(responseContent, Encoding.UTF8, "application/json")
                });

            var jsonSerializerOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var service = new HttpClientRequest(_mockLogger.Object,
                new TestHttpClientFactory(_httpClient),
                jsonSerializerOptions);

            // Act
            var response = await service.ExecutePutAsync<TestResponse>(request);

            // Assert
            Assert.NotNull(response);
            Assert.True(response.Result);
        }

        [Fact]
        public async Task ExecuteDeleteAsync_ShouldSendDeleteRequest()
        {
            // Arrange
            var request = new TestRequest();
            request.SetRequestApi("/api/test/123");

            var expectedResponse = new TestResponse { Code = 0 };
            var responseContent = JsonSerializer.Serialize(expectedResponse);

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Delete),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(responseContent, Encoding.UTF8, "application/json")
                });

            var jsonSerializerOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var service = new HttpClientRequest(_mockLogger.Object,
                new TestHttpClientFactory(_httpClient),
                jsonSerializerOptions);

            // Act
            var response = await service.ExecuteDeleteAsync<TestResponse>(request);

            // Assert
            Assert.NotNull(response);
            Assert.True(response.Result);
        }

        [Fact]
        public async Task ExecutePatchAsync_ShouldSendPatchRequest()
        {
            // Arrange
            var request = new TestRequest();
            request.SetRequestApi("/api/test/123");
            request.SetBody(new { Name = "Patched" });

            var expectedResponse = new TestResponse { Code = 0 };
            var responseContent = JsonSerializer.Serialize(expectedResponse);

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => r.Method.Method == "PATCH"),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(responseContent, Encoding.UTF8, "application/json")
                });

            var jsonSerializerOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var service = new HttpClientRequest(_mockLogger.Object,
                new TestHttpClientFactory(_httpClient),
                jsonSerializerOptions);

            // Act
            var response = await service.ExecutePatchAsync<TestResponse>(request);

            // Assert
            Assert.NotNull(response);
            Assert.True(response.Result);
        }

        [Fact]
        public async Task ExecuteAsync_ShouldIncludeHeaders()
        {
            // Arrange
            var request = new TestRequest();
            request.SetRequestApi("/api/test");
            request.AddHeader("Authorization", "Bearer test-token");
            request.AddHeader("X-Custom-Header", "custom-value");

            var expectedResponse = new TestResponse { Code = 0 };
            var responseContent = JsonSerializer.Serialize(expectedResponse);

            HttpRequestMessage? capturedRequest = null;

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((req, ct) =>
                {
                    capturedRequest = req;
                })
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(responseContent, Encoding.UTF8, "application/json")
                });

            var jsonSerializerOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var service = new HttpClientRequest(_mockLogger.Object,
                new TestHttpClientFactory(_httpClient),
                jsonSerializerOptions);

            // Act
            await service.ExecuteGetAsync<TestResponse>(request);

            // Assert
            Assert.NotNull(capturedRequest);
            Assert.True(capturedRequest.Headers.Contains("Authorization"));
            Assert.True(capturedRequest.Headers.Contains("X-Custom-Header"));
        }

        [Fact]
        public async Task ExecuteAsync_ShouldIncludeQueryParameters()
        {
            // Arrange
            var request = new TestRequest();
            request.SetRequestApi("/api/users");
            request.AddQueryParameter("page", "1");
            request.AddQueryParameter("pageSize", "20");

            var expectedResponse = new TestResponse { Code = 0 };
            var responseContent = JsonSerializer.Serialize(expectedResponse);

            HttpRequestMessage? capturedRequest = null;

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((req, ct) =>
                {
                    capturedRequest = req;
                })
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(responseContent, Encoding.UTF8, "application/json")
                });

            var jsonSerializerOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var service = new HttpClientRequest(_mockLogger.Object,
                new TestHttpClientFactory(_httpClient),
                jsonSerializerOptions);

            // Act
            await service.ExecuteGetAsync<TestResponse>(request);

            // Assert
            Assert.NotNull(capturedRequest);
            Assert.Contains("page=1", capturedRequest.RequestUri?.ToString() ?? string.Empty);
            Assert.Contains("pageSize=20", capturedRequest.RequestUri?.ToString() ?? string.Empty);
        }

        #region Test Helpers

        private class TestHttpClientFactory : IHttpClientFactory
        {
            private readonly HttpClientType _httpClient;

            public TestHttpClientFactory(HttpClientType httpClient)
            {
                _httpClient = httpClient;
            }

            public HttpClientType CreateClient(string? name = null)
            {
                return _httpClient;
            }
        }

        #endregion
    }

    /// <summary>
    /// 请求拦截器测试
    /// </summary>
    [Collection("RequestInterception")]
    public class RequestInterceptionTests
    {
        private class TestResponse
        {
            public int Code { get; set; }
            public bool Result => Code == 0;
            public string? Message { get; set; }
        }

        private class TestRequest : BaseRequest<TestResponse>
        {
            public string? Username { get; set; }
            public int Age { get; set; }
        }

        private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
        private readonly Mock<ILogger<HttpClientRequest>> _mockLogger;
        private readonly HttpClientType _httpClient;

        public RequestInterceptionTests()
        {
            _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            _mockLogger = new Mock<ILogger<HttpClientRequest>>();
            _httpClient = new HttpClientType(_mockHttpMessageHandler.Object)
            {
                BaseAddress = new Uri("https://api.example.com")
            };
        }

        [Fact]
        public async Task OnRequestSending_ShouldBeCalledBeforeRequest()
        {
            // Arrange
            var originalInterceptor = HttpClientPolicy.OnRequestSending;
            RequestInterceptionContext? capturedContext = null;

            HttpClientPolicy.OnRequestSending = context =>
            {
                capturedContext = context;
            };

            try
            {
                var request = new TestRequest();
                request.SetRequestApi("/api/test");
                request.Username = "testuser";
                request.Age = 25;
                request.AddHeader("Authorization", "Bearer token");
                request.AddQueryParameter("page", "1");

                var expectedResponse = new TestResponse { Code = 0, Message = "Success" };
                var responseContent = JsonSerializer.Serialize(expectedResponse);

                _mockHttpMessageHandler
                    .Protected()
                    .Setup<Task<HttpResponseMessage>>(
                        "SendAsync",
                        ItExpr.IsAny<HttpRequestMessage>(),
                        ItExpr.IsAny<CancellationToken>())
                    .ReturnsAsync(new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent(responseContent, Encoding.UTF8, "application/json")
                    });

                var jsonSerializerOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var service = new HttpClientRequest(_mockLogger.Object,
                    new TestHttpClientFactory(_httpClient),
                    jsonSerializerOptions);

                // Act
                await service.ExecuteGetAsync<TestResponse>(request);

                // Assert
                Assert.NotNull(capturedContext);
                Assert.Equal("/api/test", capturedContext.RequestApi);
                Assert.Equal("GET", capturedContext.HttpMethod);
                Assert.Contains("page=1", capturedContext.FullUrl);
                Assert.NotNull(capturedContext.Headers);
                Assert.True(capturedContext.Headers.ContainsKey("Authorization"));
                Assert.NotNull(capturedContext.QueryParameters);
                Assert.True(capturedContext.QueryParameters.ContainsKey("page"));
            }
            finally
            {
                HttpClientPolicy.OnRequestSending = originalInterceptor;
            }
        }

        [Fact]
        public async Task OnRequestCompleted_ShouldBeCalledAfterRequest()
        {
            // Arrange
            var originalInterceptor = HttpClientPolicy.OnRequestCompleted;
            ResponseInterceptionContext? capturedContext = null;

            HttpClientPolicy.OnRequestCompleted = context =>
            {
                capturedContext = context;
            };

            try
            {
                var request = new TestRequest();
                request.SetRequestApi("/api/test");
                request.Method = "POST";
                request.SetBody(new { Name = "Test" });

                var expectedResponse = new TestResponse { Code = 0, Message = "Success" };
                var responseContent = JsonSerializer.Serialize(expectedResponse);

                _mockHttpMessageHandler
                    .Protected()
                    .Setup<Task<HttpResponseMessage>>(
                        "SendAsync",
                        ItExpr.IsAny<HttpRequestMessage>(),
                        ItExpr.IsAny<CancellationToken>())
                    .ReturnsAsync(new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent(responseContent, Encoding.UTF8, "application/json")
                    });

                var jsonSerializerOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var service = new HttpClientRequest(_mockLogger.Object,
                    new TestHttpClientFactory(_httpClient),
                    jsonSerializerOptions);

                // Act
                await service.ExecutePostAsync<TestResponse>(request);

                // Assert
                Assert.NotNull(capturedContext);
                Assert.Equal(200, capturedContext.StatusCode);
                Assert.True(capturedContext.IsSuccess);
                Assert.NotNull(capturedContext.ResponseContent);
                Assert.Contains("Success", capturedContext.ResponseContent);
                Assert.True(capturedContext.Duration.TotalMilliseconds >= 0);
                Assert.Null(capturedContext.Exception);
            }
            finally
            {
                HttpClientPolicy.OnRequestCompleted = originalInterceptor;
            }
        }

        [Fact]
        public async Task OnRequestCompleted_ShouldCaptureException_WhenRequestFails()
        {
            // Arrange
            var originalInterceptor = HttpClientPolicy.OnRequestCompleted;
            ResponseInterceptionContext? capturedContext = null;

            HttpClientPolicy.OnRequestCompleted = context =>
            {
                capturedContext = context;
            };

            // 使用新的 mock 对象避免状态污染
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            var httpClient = new HttpClientType(mockHttpMessageHandler.Object)
            {
                BaseAddress = new Uri("https://api.example.com")
            };

            try
            {
                var request = new TestRequest();
                request.SetRequestApi("/api/test");

                mockHttpMessageHandler
                    .Protected()
                    .Setup<Task<HttpResponseMessage>>(
                        "SendAsync",
                        ItExpr.IsAny<HttpRequestMessage>(),
                        ItExpr.IsAny<CancellationToken>())
                    .ReturnsAsync(new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.InternalServerError,
                        Content = new StringContent("Error", Encoding.UTF8, "application/json")
                    });

                var jsonSerializerOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var service = new HttpClientRequest(_mockLogger.Object,
                    new TestHttpClientFactory(httpClient),
                    jsonSerializerOptions);

                // Act & Assert
                await Assert.ThrowsAsync<HttpRequestException>(async () =>
                    await service.ExecuteGetAsync<TestResponse>(request, enableRetry: false));

                Assert.NotNull(capturedContext);
                Assert.Equal(500, capturedContext.StatusCode);
                Assert.False(capturedContext.IsSuccess);
                Assert.NotNull(capturedContext.Exception);
            }
            finally
            {
                HttpClientPolicy.OnRequestCompleted = originalInterceptor;
            }
        }

        [Fact]
        public async Task OnRequestSending_ShouldCaptureBody_WhenBodyIsSet()
        {
            // Arrange
            var originalInterceptor = HttpClientPolicy.OnRequestSending;
            RequestInterceptionContext? capturedContext = null;

            HttpClientPolicy.OnRequestSending = context =>
            {
                capturedContext = context;
            };

            try
            {
                var request = new TestRequest();
                request.SetRequestApi("/api/test");
                request.Method = "POST";
                request.SetBody(new { UserName = "testuser", Age = 25 });

                var expectedResponse = new TestResponse { Code = 0 };
                var responseContent = JsonSerializer.Serialize(expectedResponse);

                _mockHttpMessageHandler
                    .Protected()
                    .Setup<Task<HttpResponseMessage>>(
                        "SendAsync",
                        ItExpr.IsAny<HttpRequestMessage>(),
                        ItExpr.IsAny<CancellationToken>())
                    .ReturnsAsync(new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent(responseContent, Encoding.UTF8, "application/json")
                    });

                var jsonSerializerOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var service = new HttpClientRequest(_mockLogger.Object,
                    new TestHttpClientFactory(_httpClient),
                    jsonSerializerOptions);

                // Act
                await service.ExecutePostAsync<TestResponse>(request);

                // Assert
                Assert.NotNull(capturedContext);
                Assert.NotNull(capturedContext.Body);
                Assert.NotNull(capturedContext.BodyJson);
                Assert.Contains("userName", capturedContext.BodyJson);
                Assert.Contains("testuser", capturedContext.BodyJson);
            }
            finally
            {
                HttpClientPolicy.OnRequestSending = originalInterceptor;
            }
        }

        [Fact]
        public async Task ExecuteGetAsync_ShouldTimeout_WhenTimeoutIsSet()
        {
            // Arrange
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            var httpClient = new HttpClientType(mockHttpMessageHandler.Object)
            {
                BaseAddress = new Uri("https://api.example.com")
            };

            var request = new TestRequest();
            request.SetRequestApi("/api/slow");

            mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Returns(async (HttpRequestMessage _, CancellationToken ct) =>
                {
                    await Task.Delay(500, ct);
                    return new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent("{}", Encoding.UTF8, "application/json")
                    };
                });

            var jsonSerializerOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var service = new HttpClientRequest(_mockLogger.Object,
                new TestHttpClientFactory(httpClient),
                jsonSerializerOptions);

            // Act & Assert - 100ms timeout should throw TimeoutException
            var ex = await Assert.ThrowsAsync<TimeoutException>(async () =>
                await service.ExecuteGetAsync<TestResponse>(request, enableRetry: false, timeout: TimeSpan.FromMilliseconds(100)));

            Assert.Contains("请求超时", ex.Message);
        }

        [Fact]
        public async Task ExecuteGetAsync_ShouldPropagateCancellation_WhenExternalTokenCancelled()
        {
            // Arrange
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            var httpClient = new HttpClientType(mockHttpMessageHandler.Object)
            {
                BaseAddress = new Uri("https://api.example.com")
            };

            var request = new TestRequest();
            request.SetRequestApi("/api/slow");

            mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Returns(async (HttpRequestMessage _, CancellationToken ct) =>
                {
                    await Task.Delay(5000, ct);
                    return new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent("{}", Encoding.UTF8, "application/json")
                    };
                });

            var jsonSerializerOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var service = new HttpClientRequest(_mockLogger.Object,
                new TestHttpClientFactory(httpClient),
                jsonSerializerOptions);

            using var cts = new CancellationTokenSource();
            cts.CancelAfter(100);

            // Act & Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                await service.ExecuteGetAsync<TestResponse>(request, enableRetry: false, cancellationToken: cts.Token));
        }

        private class TestHttpClientFactory : IHttpClientFactory
        {
            private readonly HttpClientType _httpClient;

            public TestHttpClientFactory(HttpClientType httpClient)
            {
                _httpClient = httpClient;
            }

            public HttpClientType CreateClient(string? name = null)
            {
                return _httpClient;
            }
        }
    }

    /// <summary>
    /// 用于确保拦截器测试顺序执行的集合定义
    /// </summary>
    [CollectionDefinition("RequestInterception", DisableParallelization = true)]
    public class RequestInterceptionCollection
    {
    }
}
