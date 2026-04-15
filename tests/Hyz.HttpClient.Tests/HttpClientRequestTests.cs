using System;
using System.Net;
using HttpClientType = System.Net.Http.HttpClient;
using System.Net.Http;
using System.Text;
using System.Text.Json;
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
