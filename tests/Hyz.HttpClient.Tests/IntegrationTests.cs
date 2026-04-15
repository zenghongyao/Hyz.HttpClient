using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using HttpClientType = System.Net.Http.HttpClient;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Hyz.HttpClient.Tests
{
    /// <summary>
    /// 集成测试
    /// </summary>
    public class IntegrationTests
    {
        private class UserResponse
        {
            public int Code { get; set; }
            public bool Result => Code == 0;
            public List<User>? Users { get; set; }
        }

        private class UsersListResponse : List<User>
        {
        }

        private class SimpleResponse
        {
            public int Code { get; set; }
            public bool Result => Code == 0;
        }

        private class User
        {
            public int Id { get; set; }
            public string? Name { get; set; }
            public string? Email { get; set; }
        }

        private class LoginRequest : BaseRequest<SimpleResponse>
        {
            public LoginInfo? Login { get; set; }
        }

        private class LoginInfo
        {
            public string? Username { get; set; }
            public string? Password { get; set; }
        }

        [Fact]
        public async Task FullWorkflow_GetUsersWithPagination_ShouldWork()
        {
            // Skip this test in CI/CD without actual API
            // Uncomment to test with real API
            // return;

            // Arrange
            var request = new SimpleApiRequest<UsersListResponse>();
            request.SetRequestApi("https://jsonplaceholder.typicode.com/users");
            request.AddQueryParameter("_limit", "5");
            request.AddHeader("Accept", "application/json");

            var factory = new HttpClientFactory();
            var logger = LoggerFactory.Create(builder => builder.AddConsole())
                .CreateLogger<HttpClientRequest>();
            var jsonSerializerOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var service = new HttpClientRequest(logger, factory, jsonSerializerOptions);

            // Act
            var response = await service.ExecuteGetAsync<UsersListResponse>(request);

            // Assert
            Assert.NotNull(response);
            Assert.True(response.Count > 0);
        }

        [Fact]
        public async Task FullWorkflow_CreateUserWithPost_ShouldWork()
        {
            // Skip this test in CI/CD without actual API
            // Uncomment to test with real API
            // return;

            // Arrange
            var request = new SimpleApiRequest<SimpleResponse>();
            request.SetRequestApi("https://jsonplaceholder.typicode.com/users");
            request.SetBody(new
            {
                name = "Test User",
                email = "test@example.com"
            });
            request.AddHeader("Content-Type", "application/json");

            var factory = new HttpClientFactory();
            var logger = LoggerFactory.Create(builder => builder.AddConsole())
                .CreateLogger<HttpClientRequest>();
            var jsonSerializerOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var service = new HttpClientRequest(logger, factory, jsonSerializerOptions);

            // Act
            var response = await service.ExecutePostAsync<SimpleResponse>(request);

            // Assert
            Assert.NotNull(response);
            Assert.Equal(0, response.Code);
        }

        [Fact]
        public async Task FullWorkflow_LoginWithHeadersAndBody_ShouldWork()
        {
            // Arrange
            var request = new LoginRequest();
            request.SetRequestApi("/api/login");
            request.Login = new LoginInfo
            {
                Username = "testuser",
                Password = "testpass"
            };
            request.AddHeader("X-Request-ID", Guid.NewGuid().ToString());
            request.AddHeader("X-Client-Version", "1.0.0");

            // Note: This is a mock example, real API would be different
            // In production, you would test against your actual API
            var factory = new HttpClientFactory();
            var logger = LoggerFactory.Create(builder => builder.AddConsole())
                .CreateLogger<HttpClientRequest>();
            var jsonSerializerOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var service = new HttpClientRequest(logger, factory, jsonSerializerOptions);

            // Act & Assert
            // This would normally call a real login API
            // For now, we just verify the request is properly constructed
            Assert.Equal("/api/login", request.GetRequestApi());
            Assert.NotNull(request.GetHeaders());
            Assert.Equal(2, request.GetHeaders()!.Count);
            Assert.NotNull(request.GetBody());
        }

        [Fact]
        public void RequestConstruction_ComplexQueryParameters_ShouldEncodeCorrectly()
        {
            // Arrange
            var request = new SimpleApiRequest<UserResponse>();
            request.SetRequestApi("https://api.example.com/search");
            request.AddQueryParameter("q", "测试&搜索");
            request.AddQueryParameter("filter", "status:active&role:admin");

            // Act
            var url = request.GetQueryParametersUrl();

            // Assert
            Assert.Contains(Uri.EscapeDataString("测试&搜索"), url);
            Assert.Contains(Uri.EscapeDataString("status:active&role:admin"), url);
        }

        [Fact]
        public void RequestConstruction_MultipleHeaders_ShouldAllBeAdded()
        {
            // Arrange
            var request = new SimpleApiRequest<SimpleResponse>();
            request.SetRequestApi("/api/test");

            // Act
            request.AddHeader("Authorization", "Bearer token123");
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader("X-Request-ID", Guid.NewGuid().ToString());

            var headers = request.GetHeaders();

            // Assert
            Assert.NotNull(headers);
            Assert.Equal(3, headers.Count);
        }

        [Fact]
        public void RequestConstruction_UseSetHeaders_ShouldReplaceAllHeaders()
        {
            // Arrange
            var request = new SimpleApiRequest<SimpleResponse>();
            request.SetRequestApi("/api/test");
            request.AddHeader("OldHeader", "OldValue");

            // Act
            var newHeaders = new Dictionary<string, string>
            {
                { "NewHeader1", "Value1" },
                { "NewHeader2", "Value2" },
                { "NewHeader3", "Value3" }
            };
            request.SetHeaders(newHeaders);

            var headers = request.GetHeaders();

            // Assert
            Assert.NotNull(headers);
            Assert.Equal(3, headers.Count);
            Assert.DoesNotContain("OldHeader", headers.Keys);
        }

        [Fact]
        public void DirectBaseRequestUsage_ShouldWork()
        {
            // Arrange & Act
            var request = new BaseRequest<SimpleResponse>();
            request.Method= "GET";
            request.SetRequestApi("/api/direct");
            request.AddHeader("Accept", "application/json");
            request.AddQueryParameter("id", "123");

            // Assert
            Assert.Equal("/api/direct?id=123", request.GetRequestApi()+request.GetQueryParametersUrl());
            Assert.NotNull(request.GetHeaders());
            Assert.Equal(1, request.GetHeaders()?.Count);
            Assert.Equal("GET", request.Method);
        }

        [Fact]
        public void InheritedBaseRequest_PropertiesShouldMergeWithSetBody()
        {
            // Arrange
            var request = new TestRequest();
            request.SetRequestApi("/api/test");
            request.Username = "testuser";
            request.Email = "test@example.com";

            // Set body with additional properties
            request.SetBody(new { Age = 30, City = "Beijing" });

            // Act
            var body = request.GetBody();

            // Assert - 实体类属性默认使用小驼峰命名
            Assert.NotNull(body);
            // Verify that body is a dictionary containing both properties from the class and from SetBody
            var bodyDict = body as Dictionary<string, object>;
            Assert.NotNull(bodyDict);
            Assert.Contains("username", bodyDict.Keys);
            Assert.Contains("email", bodyDict.Keys);
            Assert.Contains("age", bodyDict.Keys);
            Assert.Contains("city", bodyDict.Keys);
            Assert.Equal("testuser", bodyDict["username"]);
            Assert.Equal("test@example.com", bodyDict["email"]);
            Assert.Equal(30, bodyDict["age"]);
            Assert.Equal("Beijing", bodyDict["city"]);
        }

        [Fact]
        public void InheritedBaseRequest_WithoutSetBody_ShouldReturnInstance()
        {
            // Arrange
            var request = new TestRequest();
            request.SetRequestApi("/api/test");
            request.Username = "testuser";
            request.Email = "test@example.com";

            // Act
            var body = request.GetBody();

            // Assert
            Assert.NotNull(body);
            // Verify that body is the request instance itself
            var bodyDict = Assert.IsType<Dictionary<string, object>>(body);

            //将字典转换回你的 TestRequest 对象
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var jsonString = JsonSerializer.Serialize(bodyDict);
            var testRequestBody = JsonSerializer.Deserialize<TestRequest>(jsonString, options);
            
            Assert.Equal("testuser", testRequestBody!.Username);
            Assert.Equal("test@example.com", testRequestBody!.Email);
        }

        [Fact]
        public void InheritedBaseRequest_PropertiesShouldBeAddedAsQueryParameters()
        {
            // Arrange
            var request = new TestRequest();
            request.SetRequestApi("/api/users");
            request.Username = "testuser";
            request.Email = "test@example.com";

            // Act
            var url = request.GetQueryParametersUrl();

            // Assert - 实体类属性默认使用小驼峰命名
            Assert.Contains("username=testuser", url);
            Assert.Contains("email=test%40example.com", url); // Email should be URL encoded
        }

        [Fact]
        public void InheritedBaseRequest_ExplicitQueryParametersShouldTakePriority()
        {
            // Arrange
            var request = new TestRequest();
            request.SetRequestApi("/api/users");
            request.Username = "testuser";
            request.Email = "test@example.com";
            // Add explicit query parameter with the same name as a property (using lowercase to match camelCase)
            request.AddQueryParameter("username", "overrideuser");

            // Act
            var url = request.GetQueryParametersUrl();

            // Assert - 实体类属性默认使用小驼峰命名
            Assert.Contains("username=overrideuser", url); // Explicit parameter should take priority
            Assert.Contains("email=test%40example.com", url);
        }

        #region Test Helpers

        private class SimpleApiRequest<T> : BaseRequest<T> where T : class
        {
        }

        private class TestRequest : BaseRequest<SimpleResponse>
        {
            public string? Username { get; set; }
            public string? Email { get; set; }
        }

        private class HttpClientFactory : IHttpClientFactory
        {
            private readonly Lazy<HttpClientType> _httpClient = new(() => new HttpClientType());

            public HttpClientType CreateClient(string? name = null)
            {
                return _httpClient.Value;
            }
        }

        #endregion
    }
}
