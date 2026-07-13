using System;
using System.Collections.Generic;
using System.Linq;
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
    /// 请求拦截器测试（补充覆盖）
    /// </summary>
    /// <remarks>
    /// 使用 "RequestInterception" 集合确保拦截器测试不并行执行，
    /// 避免全局静态拦截器状态污染。
    /// 现有基础测试见 <see cref="HttpClientRequestTests"/> 中的 RequestInterceptionTests。
    /// </remarks>
    [Collection("RequestInterception")]
    public class InterceptorTests
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

        public InterceptorTests()
        {
            _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            _mockLogger = new Mock<ILogger<HttpClientRequest>>();
            _httpClient = new HttpClientType(_mockHttpMessageHandler.Object)
            {
                BaseAddress = new Uri("https://api.example.com")
            };
        }

        /// <summary>
        /// 创建测试用的 HttpClientRequest 服务实例
        /// </summary>
        private HttpClientRequest CreateService(HttpClientType? httpClient = null)
        {
            var jsonSerializerOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return new HttpClientRequest(
                _mockLogger.Object,
                new TestHttpClientFactory(httpClient ?? _httpClient),
                jsonSerializerOptions);
        }

        /// <summary>
        /// 设置 mock 返回成功响应
        /// </summary>
        private void SetupSuccessResponse(string responseContent = "{\"Code\":0,\"Message\":\"Success\"}")
        {
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
        }

        #region ClearInterceptors

        [Fact]
        public void ClearInterceptors_ShouldNullOutBothInterceptors()
        {
            // Arrange
            var originalSending = HttpClientPolicy.OnRequestSending;
            var originalCompleted = HttpClientPolicy.OnRequestCompleted;

            HttpClientPolicy.OnRequestSending = _ => { };
            HttpClientPolicy.OnRequestCompleted = _ => { };
            Assert.NotNull(HttpClientPolicy.OnRequestSending);
            Assert.NotNull(HttpClientPolicy.OnRequestCompleted);

            try
            {
                // Act
                HttpClientPolicy.ClearInterceptors();

                // Assert
                Assert.Null(HttpClientPolicy.OnRequestSending);
                Assert.Null(HttpClientPolicy.OnRequestCompleted);
            }
            finally
            {
                HttpClientPolicy.OnRequestSending = originalSending;
                HttpClientPolicy.OnRequestCompleted = originalCompleted;
            }
        }

        [Fact]
        public void ClearInterceptors_CalledMultipleTimes_ShouldBeIdempotent()
        {
            // Arrange
            var originalSending = HttpClientPolicy.OnRequestSending;
            var originalCompleted = HttpClientPolicy.OnRequestCompleted;

            try
            {
                // Act - 多次调用不应抛异常
                HttpClientPolicy.ClearInterceptors();
                HttpClientPolicy.ClearInterceptors();
                HttpClientPolicy.ClearInterceptors();

                // Assert
                Assert.Null(HttpClientPolicy.OnRequestSending);
                Assert.Null(HttpClientPolicy.OnRequestCompleted);
            }
            finally
            {
                HttpClientPolicy.OnRequestSending = originalSending;
                HttpClientPolicy.OnRequestCompleted = originalCompleted;
            }
        }

        #endregion

        #region RequestId / TraceId 传递

        [Fact]
        public async Task OnRequestSending_ShouldPropagateRequestId_ToOnRequestCompleted()
        {
            // Arrange
            var originalSending = HttpClientPolicy.OnRequestSending;
            var originalCompleted = HttpClientPolicy.OnRequestCompleted;
            string? requestRequestId = null;
            string? responseRequestId = null;

            HttpClientPolicy.OnRequestSending = ctx =>
            {
                requestRequestId = ctx.RequestId;
            };
            HttpClientPolicy.OnRequestCompleted = ctx =>
            {
                responseRequestId = ctx.RequestId;
            };

            try
            {
                SetupSuccessResponse();
                var service = CreateService();
                var request = new TestRequest();
                request.SetRequestApi("/api/test");

                // Act
                await service.ExecuteGetAsync<TestResponse>(request, enableRetry: false);

                // Assert
                Assert.NotNull(requestRequestId);
                Assert.NotNull(responseRequestId);
                Assert.Equal(requestRequestId, responseRequestId);
            }
            finally
            {
                HttpClientPolicy.OnRequestSending = originalSending;
                HttpClientPolicy.OnRequestCompleted = originalCompleted;
            }
        }

        [Fact]
        public async Task OnRequestSending_ShouldPropagateTraceId_ToOnRequestCompleted()
        {
            // Arrange
            var originalSending = HttpClientPolicy.OnRequestSending;
            var originalCompleted = HttpClientPolicy.OnRequestCompleted;
            string? requestTraceId = null;
            string? responseTraceId = null;

            HttpClientPolicy.OnRequestSending = ctx =>
            {
                requestTraceId = ctx.TraceId;
            };
            HttpClientPolicy.OnRequestCompleted = ctx =>
            {
                responseTraceId = ctx.TraceId;
            };

            try
            {
                SetupSuccessResponse();
                var service = CreateService();
                var request = new TestRequest();
                request.SetRequestApi("/api/test");

                // Act
                await service.ExecuteGetAsync<TestResponse>(request, enableRetry: false);

                // Assert
                Assert.NotNull(requestTraceId);
                Assert.False(string.IsNullOrEmpty(requestTraceId));
                Assert.Equal(requestTraceId, responseTraceId);
            }
            finally
            {
                HttpClientPolicy.OnRequestSending = originalSending;
                HttpClientPolicy.OnRequestCompleted = originalCompleted;
            }
        }

        [Fact]
        public async Task OnRequestSending_ShouldAutoGenerateRequestId_AndTraceId()
        {
            // Arrange
            var originalSending = HttpClientPolicy.OnRequestSending;
            string? capturedRequestId = null;
            string? capturedTraceId = null;

            HttpClientPolicy.OnRequestSending = ctx =>
            {
                capturedRequestId = ctx.RequestId;
                capturedTraceId = ctx.TraceId;
            };

            try
            {
                SetupSuccessResponse();
                var service = CreateService();
                var request = new TestRequest();
                request.SetRequestApi("/api/test");

                // Act
                await service.ExecuteGetAsync<TestResponse>(request, enableRetry: false);

                // Assert
                Assert.NotNull(capturedRequestId);
                Assert.False(string.IsNullOrEmpty(capturedRequestId));
                Assert.NotNull(capturedTraceId);
                Assert.False(string.IsNullOrEmpty(capturedTraceId));
                // RequestId 和 TraceId 都应是非空字符串（GUID 的 N 格式，32 位）
                Assert.NotEqual(capturedRequestId, capturedTraceId);
            }
            finally
            {
                HttpClientPolicy.OnRequestSending = originalSending;
            }
        }

        #endregion

        #region Items 跨拦截器数据传递

        [Fact]
        public async Task Items_ShouldPassData_BetweenRequestAndResponseInterceptors()
        {
            // Arrange
            var originalSending = HttpClientPolicy.OnRequestSending;
            var originalCompleted = HttpClientPolicy.OnRequestCompleted;
            const string itemsKey = "CustomKey";
            const string itemsValue = "CustomValue";
            object? receivedValue = null;

            HttpClientPolicy.OnRequestSending = ctx =>
            {
                ctx.Items = new Dictionary<string, object>();
                ctx.Items[itemsKey] = itemsValue;
            };
            HttpClientPolicy.OnRequestCompleted = ctx =>
            {
                receivedValue = ctx.RequestContext.Items?.TryGetValue(itemsKey, out var v) == true ? v : null;
            };

            try
            {
                SetupSuccessResponse();
                var service = CreateService();
                var request = new TestRequest();
                request.SetRequestApi("/api/test");

                // Act
                await service.ExecuteGetAsync<TestResponse>(request, enableRetry: false);

                // Assert
                Assert.Equal(itemsValue, receivedValue);
            }
            finally
            {
                HttpClientPolicy.OnRequestSending = originalSending;
                HttpClientPolicy.OnRequestCompleted = originalCompleted;
            }
        }

        [Fact]
        public async Task Items_ShouldBeSameReference_InBothInterceptors()
        {
            // Arrange
            var originalSending = HttpClientPolicy.OnRequestSending;
            var originalCompleted = HttpClientPolicy.OnRequestCompleted;
            object? requestItemsRef = null;
            object? responseItemsRef = null;

            HttpClientPolicy.OnRequestSending = ctx =>
            {
                ctx.Items = new Dictionary<string, object>();
                requestItemsRef = ctx.Items;
            };
            HttpClientPolicy.OnRequestCompleted = ctx =>
            {
                responseItemsRef = ctx.RequestContext.Items;
            };

            try
            {
                SetupSuccessResponse();
                var service = CreateService();
                var request = new TestRequest();
                request.SetRequestApi("/api/test");

                // Act
                await service.ExecuteGetAsync<TestResponse>(request, enableRetry: false);

                // Assert - Items 是同一引用（通过共享 RequestContext）
                Assert.NotNull(requestItemsRef);
                Assert.Same(requestItemsRef, responseItemsRef);
            }
            finally
            {
                HttpClientPolicy.OnRequestSending = originalSending;
                HttpClientPolicy.OnRequestCompleted = originalCompleted;
            }
        }

        #endregion

        #region 拦截器异常隔离

        [Fact]
        public async Task OnRequestSending_Throws_ShouldNotBreakRequest()
        {
            // Arrange
            var originalSending = HttpClientPolicy.OnRequestSending;
            HttpClientPolicy.OnRequestSending = ctx =>
            {
                throw new InvalidOperationException("Interceptor failure");
            };

            try
            {
                SetupSuccessResponse();
                var service = CreateService();
                var request = new TestRequest();
                request.SetRequestApi("/api/test");

                // Act - 请求应该正常完成，拦截器异常被吞掉
                var response = await service.ExecuteGetAsync<TestResponse>(request, enableRetry: false);

                // Assert
                Assert.NotNull(response);
                Assert.True(response.Result);
            }
            finally
            {
                HttpClientPolicy.OnRequestSending = originalSending;
            }
        }

        [Fact]
        public async Task OnRequestCompleted_Throws_ShouldNotBreakResponse()
        {
            // Arrange
            var originalCompleted = HttpClientPolicy.OnRequestCompleted;
            HttpClientPolicy.OnRequestCompleted = ctx =>
            {
                throw new InvalidOperationException("Response interceptor failure");
            };

            try
            {
                SetupSuccessResponse();
                var service = CreateService();
                var request = new TestRequest();
                request.SetRequestApi("/api/test");

                // Act - 响应应该正常返回，拦截器异常被吞掉
                var response = await service.ExecuteGetAsync<TestResponse>(request, enableRetry: false);

                // Assert
                Assert.NotNull(response);
                Assert.True(response.Result);
            }
            finally
            {
                HttpClientPolicy.OnRequestCompleted = originalCompleted;
            }
        }

        [Fact]
        public async Task OnRequestSending_Throws_ShouldStillCallOnRequestCompleted()
        {
            // Arrange
            var originalSending = HttpClientPolicy.OnRequestSending;
            var originalCompleted = HttpClientPolicy.OnRequestCompleted;
            bool completedCalled = false;

            HttpClientPolicy.OnRequestSending = ctx =>
            {
                throw new InvalidOperationException("Sending interceptor failure");
            };
            HttpClientPolicy.OnRequestCompleted = ctx =>
            {
                completedCalled = true;
            };

            try
            {
                SetupSuccessResponse();
                var service = CreateService();
                var request = new TestRequest();
                request.SetRequestApi("/api/test");

                // Act
                await service.ExecuteGetAsync<TestResponse>(request, enableRetry: false);

                // Assert - 即使请求前拦截器抛异常，请求后拦截器仍应被调用
                Assert.True(completedCalled);
            }
            finally
            {
                HttpClientPolicy.OnRequestSending = originalSending;
                HttpClientPolicy.OnRequestCompleted = originalCompleted;
            }
        }

        #endregion

        #region HttpRequest 修改

        [Fact]
        public async Task OnRequestSending_CanModifyHttpRequest_Headers()
        {
            // Arrange
            var originalSending = HttpClientPolicy.OnRequestSending;
            HttpRequestMessage? capturedRequest = null;
            const string customHeader = "X-Trace-Id";
            const string customHeaderValue = "trace-12345";

            HttpClientPolicy.OnRequestSending = ctx =>
            {
                ctx.HttpRequest?.Headers.TryAddWithoutValidation(customHeader, customHeaderValue);
            };

            try
            {
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
                        Content = new StringContent("{\"Code\":0}", Encoding.UTF8, "application/json")
                    });

                var service = CreateService();
                var request = new TestRequest();
                request.SetRequestApi("/api/test");

                // Act
                await service.ExecuteGetAsync<TestResponse>(request, enableRetry: false);

                // Assert - 拦截器在 HttpRequest 上添加的头应该出现在实际请求中
                Assert.NotNull(capturedRequest);
                Assert.True(capturedRequest.Headers.Contains(customHeader));
                Assert.Equal(customHeaderValue, capturedRequest.Headers.GetValues(customHeader).First());
            }
            finally
            {
                HttpClientPolicy.OnRequestSending = originalSending;
            }
        }

        #endregion

        #region 响应信息捕获

        [Fact]
        public async Task OnRequestCompleted_ShouldCaptureResponseHeaders()
        {
            // Arrange
            var originalCompleted = HttpClientPolicy.OnRequestCompleted;
            System.Net.Http.Headers.HttpResponseHeaders? capturedHeaders = null;

            HttpClientPolicy.OnRequestCompleted = ctx =>
            {
                capturedHeaders = ctx.ResponseHeaders;
            };

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                ReasonPhrase = "OK",
                Content = new StringContent("{\"Code\":0}", Encoding.UTF8, "application/json")
            };
            responseMessage.Headers.Add("X-Custom-Response-Header", "response-value");

            try
            {
                _mockHttpMessageHandler
                    .Protected()
                    .Setup<Task<HttpResponseMessage>>(
                        "SendAsync",
                        ItExpr.IsAny<HttpRequestMessage>(),
                        ItExpr.IsAny<CancellationToken>())
                    .ReturnsAsync(responseMessage);

                var service = CreateService();
                var request = new TestRequest();
                request.SetRequestApi("/api/test");

                // Act
                await service.ExecuteGetAsync<TestResponse>(request, enableRetry: false);

                // Assert
                Assert.NotNull(capturedHeaders);
                Assert.True(capturedHeaders.Contains("X-Custom-Response-Header"));
                Assert.Equal("response-value", capturedHeaders.GetValues("X-Custom-Response-Header").First());
            }
            finally
            {
                HttpClientPolicy.OnRequestCompleted = originalCompleted;
            }
        }

        [Fact]
        public async Task OnRequestCompleted_ShouldCaptureReasonPhrase()
        {
            // Arrange
            var originalCompleted = HttpClientPolicy.OnRequestCompleted;
            string? capturedReasonPhrase = null;

            HttpClientPolicy.OnRequestCompleted = ctx =>
            {
                capturedReasonPhrase = ctx.ReasonPhrase;
            };

            try
            {
                _mockHttpMessageHandler
                    .Protected()
                    .Setup<Task<HttpResponseMessage>>(
                        "SendAsync",
                        ItExpr.IsAny<HttpRequestMessage>(),
                        ItExpr.IsAny<CancellationToken>())
                    .ReturnsAsync(new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        ReasonPhrase = "OK",
                        Content = new StringContent("{\"Code\":0}", Encoding.UTF8, "application/json")
                    });

                var service = CreateService();
                var request = new TestRequest();
                request.SetRequestApi("/api/test");

                // Act
                await service.ExecuteGetAsync<TestResponse>(request, enableRetry: false);

                // Assert
                Assert.Equal("OK", capturedReasonPhrase);
            }
            finally
            {
                HttpClientPolicy.OnRequestCompleted = originalCompleted;
            }
        }

        [Fact]
        public async Task OnRequestCompleted_ShouldCaptureDuration()
        {
            // Arrange
            var originalCompleted = HttpClientPolicy.OnRequestCompleted;
            TimeSpan capturedDuration = TimeSpan.MinValue;
            DateTime? capturedResponseTime = null;

            HttpClientPolicy.OnRequestCompleted = ctx =>
            {
                capturedDuration = ctx.Duration;
                capturedResponseTime = ctx.ResponseTime;
            };

            try
            {
                SetupSuccessResponse();
                var service = CreateService();
                var request = new TestRequest();
                request.SetRequestApi("/api/test");

                // Act
                await service.ExecuteGetAsync<TestResponse>(request, enableRetry: false);

                // Assert
                Assert.True(capturedDuration >= TimeSpan.Zero);
                Assert.NotNull(capturedResponseTime);
            }
            finally
            {
                HttpClientPolicy.OnRequestCompleted = originalCompleted;
            }
        }

        #endregion

        #region 失败场景

        [Fact]
        public async Task OnRequestCompleted_ShouldBeCalled_EvenWhenRequestFails()
        {
            // Arrange
            var originalCompleted = HttpClientPolicy.OnRequestCompleted;
            bool interceptorCalled = false;
            int? capturedStatusCode = null;
            bool? capturedIsSuccess = null;

            HttpClientPolicy.OnRequestCompleted = ctx =>
            {
                interceptorCalled = true;
                capturedStatusCode = ctx.StatusCode;
                capturedIsSuccess = ctx.IsSuccess;
            };

            var mockHandler = new Mock<HttpMessageHandler>();
            var httpClient = new HttpClientType(mockHandler.Object)
            {
                BaseAddress = new Uri("https://api.example.com")
            };

            mockHandler
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

            try
            {
                var service = CreateService(httpClient);
                var request = new TestRequest();
                request.SetRequestApi("/api/test");

                // Act & Assert
                await Assert.ThrowsAsync<HttpRequestException>(async () =>
                    await service.ExecuteGetAsync<TestResponse>(request, enableRetry: false));

                // Assert - 拦截器仍然被调用（finally 块）
                Assert.True(interceptorCalled);
                Assert.Equal(500, capturedStatusCode);
                Assert.False(capturedIsSuccess);
            }
            finally
            {
                HttpClientPolicy.OnRequestCompleted = originalCompleted;
            }
        }

        [Fact]
        public async Task OnRequestCompleted_ShouldCaptureException_WhenRequestFails()
        {
            // Arrange
            var originalCompleted = HttpClientPolicy.OnRequestCompleted;
            Exception? capturedException = null;

            HttpClientPolicy.OnRequestCompleted = ctx =>
            {
                capturedException = ctx.Exception;
            };

            var mockHandler = new Mock<HttpMessageHandler>();
            var httpClient = new HttpClientType(mockHandler.Object)
            {
                BaseAddress = new Uri("https://api.example.com")
            };

            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.NotFound,
                    Content = new StringContent("Not Found", Encoding.UTF8, "application/json")
                });

            try
            {
                var service = CreateService(httpClient);
                var request = new TestRequest();
                request.SetRequestApi("/api/test");

                // Act & Assert
                await Assert.ThrowsAsync<HttpRequestException>(async () =>
                    await service.ExecuteGetAsync<TestResponse>(request, enableRetry: false));

                // Assert
                Assert.NotNull(capturedException);
                Assert.IsType<HttpRequestException>(capturedException);
            }
            finally
            {
                HttpClientPolicy.OnRequestCompleted = originalCompleted;
            }
        }

        #endregion

        #region 无拦截器场景

        [Fact]
        public async Task NoInterceptors_ShouldWorkNormally()
        {
            // Arrange
            var originalSending = HttpClientPolicy.OnRequestSending;
            var originalCompleted = HttpClientPolicy.OnRequestCompleted;
            HttpClientPolicy.ClearInterceptors();

            try
            {
                SetupSuccessResponse();
                var service = CreateService();
                var request = new TestRequest();
                request.SetRequestApi("/api/test");

                // Act
                var response = await service.ExecuteGetAsync<TestResponse>(request, enableRetry: false);

                // Assert
                Assert.NotNull(response);
                Assert.True(response.Result);
            }
            finally
            {
                HttpClientPolicy.OnRequestSending = originalSending;
                HttpClientPolicy.OnRequestCompleted = originalCompleted;
            }
        }

        [Fact]
        public async Task BothInterceptors_ShouldBeCalled_ForSingleRequest()
        {
            // Arrange
            var originalSending = HttpClientPolicy.OnRequestSending;
            var originalCompleted = HttpClientPolicy.OnRequestCompleted;
            int sendingCallCount = 0;
            int completedCallCount = 0;

            HttpClientPolicy.OnRequestSending = ctx =>
            {
                sendingCallCount++;
            };
            HttpClientPolicy.OnRequestCompleted = ctx =>
            {
                completedCallCount++;
            };

            try
            {
                SetupSuccessResponse();
                var service = CreateService();
                var request = new TestRequest();
                request.SetRequestApi("/api/test");

                // Act
                await service.ExecuteGetAsync<TestResponse>(request, enableRetry: false);

                // Assert
                Assert.Equal(1, sendingCallCount);
                Assert.Equal(1, completedCallCount);
            }
            finally
            {
                HttpClientPolicy.OnRequestSending = originalSending;
                HttpClientPolicy.OnRequestCompleted = originalCompleted;
            }
        }

        #endregion

        #region 请求上下文完整性

        [Fact]
        public async Task OnRequestSending_ShouldCaptureAllRequestInfo()
        {
            // Arrange
            var originalSending = HttpClientPolicy.OnRequestSending;
            RequestInterceptionContext? capturedContext = null;

            HttpClientPolicy.OnRequestSending = ctx =>
            {
                capturedContext = ctx;
            };

            try
            {
                SetupSuccessResponse();
                var service = CreateService();
                var request = new TestRequest();
                request.SetRequestApi("/api/users");
                request.Username = "testuser";
                request.Age = 25;
                request.AddHeader("Authorization", "Bearer token");
                request.AddQueryParameter("page", "1");

                // Act
                await service.ExecuteGetAsync<TestResponse>(request, enableRetry: false);

                // Assert
                Assert.NotNull(capturedContext);
                Assert.Equal("/api/users", capturedContext.RequestApi);
                Assert.Equal("GET", capturedContext.HttpMethod);
                Assert.Contains("/api/users", capturedContext.FullUrl);
                Assert.Contains("page=1", capturedContext.FullUrl);
                Assert.NotNull(capturedContext.Headers);
                Assert.True(capturedContext.Headers.ContainsKey("Authorization"));
                Assert.NotNull(capturedContext.QueryParameters);
                Assert.True(capturedContext.QueryParameters.ContainsKey("page"));
                Assert.NotNull(capturedContext.HttpRequest);
                Assert.NotNull(capturedContext.RequestId);
                Assert.False(string.IsNullOrEmpty(capturedContext.RequestId));
                Assert.NotNull(capturedContext.TraceId);
                Assert.False(string.IsNullOrEmpty(capturedContext.TraceId));
            }
            finally
            {
                HttpClientPolicy.OnRequestSending = originalSending;
            }
        }

        [Fact]
        public async Task OnRequestCompleted_ShouldCaptureFullResponseInfo()
        {
            // Arrange
            var originalCompleted = HttpClientPolicy.OnRequestCompleted;
            ResponseInterceptionContext? capturedContext = null;

            HttpClientPolicy.OnRequestCompleted = ctx =>
            {
                capturedContext = ctx;
            };

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                ReasonPhrase = "OK",
                Content = new StringContent("{\"Code\":0,\"Message\":\"Success\"}", Encoding.UTF8, "application/json")
            };
            responseMessage.Headers.Add("X-Response-Id", "resp-001");

            try
            {
                _mockHttpMessageHandler
                    .Protected()
                    .Setup<Task<HttpResponseMessage>>(
                        "SendAsync",
                        ItExpr.IsAny<HttpRequestMessage>(),
                        ItExpr.IsAny<CancellationToken>())
                    .ReturnsAsync(responseMessage);

                var service = CreateService();
                var request = new TestRequest();
                request.SetRequestApi("/api/test");

                // Act
                await service.ExecuteGetAsync<TestResponse>(request, enableRetry: false);

                // Assert
                Assert.NotNull(capturedContext);
                Assert.Equal(200, capturedContext.StatusCode);
                Assert.True(capturedContext.IsSuccess);
                Assert.Equal("OK", capturedContext.ReasonPhrase);
                Assert.NotNull(capturedContext.ResponseContent);
                Assert.Contains("Success", capturedContext.ResponseContent);
                Assert.Null(capturedContext.Exception);
                Assert.NotNull(capturedContext.RequestContext);
                Assert.NotNull(capturedContext.ResponseHeaders);
                Assert.True(capturedContext.ResponseHeaders.Contains("X-Response-Id"));
                Assert.False(string.IsNullOrEmpty(capturedContext.RequestId));
                Assert.False(string.IsNullOrEmpty(capturedContext.TraceId));
            }
            finally
            {
                HttpClientPolicy.OnRequestCompleted = originalCompleted;
            }
        }

        [Fact]
        public async Task OnRequestSending_ShouldCaptureRequestTime()
        {
            // Arrange
            var originalSending = HttpClientPolicy.OnRequestSending;
            DateTime beforeRequest = DateTime.UtcNow.AddSeconds(-1);
            DateTime? capturedRequestTime = null;

            HttpClientPolicy.OnRequestSending = ctx =>
            {
                capturedRequestTime = ctx.RequestTime;
            };

            try
            {
                SetupSuccessResponse();
                var service = CreateService();
                var request = new TestRequest();
                request.SetRequestApi("/api/test");

                // Act
                await service.ExecuteGetAsync<TestResponse>(request, enableRetry: false);

                // Assert
                Assert.NotNull(capturedRequestTime);
                Assert.True(capturedRequestTime >= beforeRequest);
                Assert.True(capturedRequestTime <= DateTime.UtcNow.AddSeconds(1));
            }
            finally
            {
                HttpClientPolicy.OnRequestSending = originalSending;
            }
        }

        #endregion

        #region 不同 HTTP 方法

        [Theory]
        [InlineData("GET")]
        [InlineData("POST")]
        [InlineData("PUT")]
        [InlineData("DELETE")]
        [InlineData("PATCH")]
        public async Task Interceptors_ShouldFire_ForAllHttpMethods(string httpMethod)
        {
            // Arrange
            var originalSending = HttpClientPolicy.OnRequestSending;
            var originalCompleted = HttpClientPolicy.OnRequestCompleted;
            string? capturedMethod = null;
            bool completedCalled = false;

            HttpClientPolicy.OnRequestSending = ctx =>
            {
                capturedMethod = ctx.HttpMethod;
            };
            HttpClientPolicy.OnRequestCompleted = ctx =>
            {
                completedCalled = true;
            };

            try
            {
                SetupSuccessResponse();
                var service = CreateService();
                var request = new TestRequest();
                request.SetRequestApi("/api/test");

                // Act
                switch (httpMethod)
                {
                    case "GET":
                        await service.ExecuteGetAsync<TestResponse>(request, enableRetry: false);
                        break;
                    case "POST":
                        request.SetBody(new { Name = "Test" });
                        await service.ExecutePostAsync<TestResponse>(request, enableRetry: false);
                        break;
                    case "PUT":
                        request.SetBody(new { Name = "Test" });
                        await service.ExecutePutAsync<TestResponse>(request, enableRetry: false);
                        break;
                    case "DELETE":
                        await service.ExecuteDeleteAsync<TestResponse>(request, enableRetry: false);
                        break;
                    case "PATCH":
                        request.SetBody(new { Name = "Test" });
                        await service.ExecutePatchAsync<TestResponse>(request, enableRetry: false);
                        break;
                }

                // Assert
                Assert.Equal(httpMethod, capturedMethod);
                Assert.True(completedCalled);
            }
            finally
            {
                HttpClientPolicy.OnRequestSending = originalSending;
                HttpClientPolicy.OnRequestCompleted = originalCompleted;
            }
        }

        #endregion

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
}
