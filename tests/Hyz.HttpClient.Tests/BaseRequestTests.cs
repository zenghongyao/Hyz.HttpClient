using System.Collections.Generic;
using Hyz.HttpClient;
using Xunit;

namespace Hyz.HttpClient.Tests
{
    /// <summary>
    /// BaseRequest测试
    /// </summary>
    public class BaseRequestTests
    {
        private class TestRequest : BaseRequest<object>
        {
            
        }

        private class TestRequestWithProperties : BaseRequest<object>
        {
            public string? Username { get; set; }
            public int Age { get; set; }
            public string? City { get; set; }
        }

        [Fact]
        public void SetRequestApi_ShouldSetUrl()
        {
            // Arrange
            var request = new TestRequest();

            // Act
            request.SetRequestApi("/api/test");

            // Assert
            Assert.Equal("/api/test", request.GetRequestApi());
        }

        [Fact]
        public void AddHeader_ShouldAddHeader()
        {
            // Arrange
            var request = new TestRequest();

            // Act
            request.AddHeader("Authorization", "Bearer token");
            request.AddHeader("Content-Type", "application/json");

            // Assert
            var headers = request.GetHeaders();
            Assert.NotNull(headers);
            Assert.Equal(2, headers.Count);
            Assert.Equal("Bearer token", headers["Authorization"]);
            Assert.Equal("application/json", headers["Content-Type"]);
        }

        [Fact]
        public void SetHeaders_ShouldReplaceHeaders()
        {
            // Arrange
            var request = new TestRequest();
            request.AddHeader("OldHeader", "OldValue");

            // Act
            var newHeaders = new Dictionary<string, string>
            {
                { "NewHeader1", "Value1" },
                { "NewHeader2", "Value2" }
            };
            request.SetHeaders(newHeaders);

            // Assert
            var headers = request.GetHeaders();
            Assert.NotNull(headers);
            Assert.Equal(2, headers.Count);
            Assert.Equal("Value1", headers["NewHeader1"]);
            Assert.Equal("Value2", headers["NewHeader2"]);
        }

        [Fact]
        public void AddQueryParameter_ShouldAddParameter()
        {
            // Arrange
            var request = new TestRequest();
            request.SetRequestApi("/api/users");

            // Act
            request.AddQueryParameter("page", "1");
            request.AddQueryParameter("pageSize", "20");

            // Assert
            var url = request.GetQueryParametersUrl();
            Assert.Contains("page=1", url);
            Assert.Contains("pageSize=20", url);
        }

        [Fact]
        public void SetQueryParameters_ShouldMergeParameters()
        {
            // Arrange
            var request = new TestRequest();
            request.SetRequestApi("/api/users");
            request.AddQueryParameter("old", "value");

            // Act
            var newParams = new Dictionary<string, string>
            {
                { "page", "2" },
                { "pageSize", "30" }
            };
            request.SetQueryParameters(newParams);

            // Assert
            var url = request.GetQueryParametersUrl();
            Assert.Contains("page=2", url);
            Assert.Contains("pageSize=30", url);
            Assert.Contains("old=value", url); // Should still be present
        }

        [Fact]
        public void GetRequestApi_ShouldAutoEncodeQueryParameters()
        {
            // Arrange
            var request = new TestRequest();
            request.SetRequestApi("/api/search");
            request.AddQueryParameter("keyword", "测试&搜索");

            // Act
            var url = request.GetQueryParametersUrl();

            // Assert
            Assert.Contains(Uri.EscapeDataString("测试&搜索"), url);
        }

        [Fact]
        public void GetRequestApi_ShouldHandleExistingQueryParameters()
        {
            // Arrange
            var request = new TestRequest();
            request.SetRequestApi("/api/users?status=active");
            request.AddQueryParameter("page", "1");

            // Act
            var url = request.GetQueryParametersUrl();
            var api = request.GetRequestApi();
            // Assert
            Assert.Contains("status=active", api);
            Assert.Contains("&page=1", url);
            Assert.DoesNotContain("?page=1", url);
        }

        [Fact]
        public void SetBody_ShouldSetBody()
        {
            // Arrange
            var request = new TestRequest();
            var body = new { Id = 123, Name = "Test" };

            // Act
            request.SetBody(body);

            // Assert
            var retrievedBody = request.GetBody();
            Assert.NotNull(retrievedBody);
            Assert.Equal(body, retrievedBody);
        }

        [Fact]
        public void Method_ShouldDefaultToPost()
        {
            // Arrange & Act
            var request = new TestRequest();

            // Assert
            Assert.Equal("POST", request.Method);
        }

        [Fact]
        public void Method_ShouldBeSettable()
        {
            // Arrange
            var request = new TestRequest();

            // Act
            request.Method = "GET";

            // Assert
            Assert.Equal("GET", request.Method);
        }

        [Fact]
        public void GetRequestApi_ShouldAddPropertiesAsQueryParameters()
        {
            // Arrange
            var request = new TestRequestWithProperties();
            request.SetRequestApi("/api/users");
            request.Username = "testuser";
            request.Age = 30;
            request.City = "Beijing";

            // Act
            var url = request.GetQueryParametersUrl();

            // Assert
            Assert.Contains("Username=testuser", url);
            Assert.Contains("Age=30", url);
            Assert.Contains("City=Beijing", url);
        }

        [Fact]
        public void GetRequestApi_ExplicitQueryParametersShouldOverrideProperties()
        {
            // Arrange
            var request = new TestRequestWithProperties();
            request.Method = "GET";
            request.SetRequestApi("/api/users");
            request.Username = "testuser";
            request.Age = 30;
            request.City = "Beijing";
            // Add explicit query parameter with the same name as a property
            request.AddQueryParameter("Username", "overrideuser");
            request.AddQueryParameter("Age", "40");

            // Act
            var url = request.GetQueryParametersUrl();

            // Assert
            Assert.Contains("Username=overrideuser", url); // Explicit parameter should take priority
            Assert.Contains("Age=40", url); // Explicit parameter should take priority
            Assert.Contains("City=Beijing", url); // No explicit parameter, so use property value
        }

        [Fact]
        public void GetQueryParameters_ShouldReturnMergedParameters()
        {
            // Arrange
            var request = new TestRequestWithProperties();
            request.Username = "testuser";
            request.Age = 30;
            request.City = "Beijing";
            // Add explicit query parameter with the same name as a property
            request.AddQueryParameter("Username", "overrideuser");
            request.AddQueryParameter("Page", "1");

            // Act
            var queryParameters = request.GetQueryParameters();

            // Assert
            Assert.NotNull(queryParameters);
            Assert.Equal(4, queryParameters.Count);
            Assert.Equal("overrideuser", queryParameters["Username"]); // Explicit parameter should take priority
            Assert.Equal("30", queryParameters["Age"]); // Property value
            Assert.Equal("Beijing", queryParameters["City"]); // Property value
            Assert.Equal("1", queryParameters["Page"]); // Explicit parameter
        }

        [Fact]
        public void GetQueryParameters_WithNoParameters_ShouldReturnNull()
        {
            // Arrange
            var request = new TestRequest();

            // Act
            var queryParameters = request.GetQueryParameters();

            // Assert
            Assert.Null(queryParameters);
        }

        [Fact]
        public void SetQueryParameters_ShouldMergeWithExistingParameters()
        {
            // Arrange
            var request = new TestRequest();
            // Add some parameters first
            request.AddQueryParameter("page", "1");
            request.AddQueryParameter("pageSize", "20");
            // Then set new parameters
            var newParameters = new Dictionary<string, string>
            {
                { "sort", "name" },
                { "order", "asc" }
            };
            request.SetQueryParameters(newParameters);

            // Act
            var queryParameters = request.GetQueryParameters();

            // Assert
            Assert.NotNull(queryParameters);
            Assert.Equal(4, queryParameters.Count);
            Assert.Equal("1", queryParameters["page"]);
            Assert.Equal("20", queryParameters["pageSize"]);
            Assert.Equal("name", queryParameters["sort"]);
            Assert.Equal("asc", queryParameters["order"]);
        }

        [Fact]
        public void SetQueryParameters_ShouldOverrideExistingParameters()
        {
            // Arrange
            var request = new TestRequest();
            // Add some parameters first
            request.AddQueryParameter("page", "1");
            request.AddQueryParameter("pageSize", "20");
            // Then set new parameters with some overlapping keys
            var newParameters = new Dictionary<string, string>
            {
                { "page", "2" }, // Override existing parameter
                { "sort", "name" }
            };
            request.SetQueryParameters(newParameters);

            // Act
            var queryParameters = request.GetQueryParameters();

            // Assert
            Assert.NotNull(queryParameters);
            Assert.Equal(3, queryParameters.Count);
            Assert.Equal("2", queryParameters["page"]); // Should be overridden
            Assert.Equal("20", queryParameters["pageSize"]);
            Assert.Equal("name", queryParameters["sort"]);
        }

        private class TestRequestWithCustomAttributes : BaseRequest<object>
        {
            [RequestParameterAlias("user_name")]
            public string? Username { get; set; }

            [RequestParameterAlias("user_age")]
            public int Age { get; set; }

            [RequestParameterAlias("Method")]
            public string HttpMethod { get; set; } = "POST";
        }

        [Fact]
        public void GetQueryParameters_ShouldUseCustomAttributeAliases()
        {
            // Arrange
            var request = new TestRequestWithCustomAttributes();
            request.Username = "testuser";
            request.Age = 30;
            request.HttpMethod = "GET";

            // Act
            var queryParameters = request.GetQueryParameters();

            // Assert
            Assert.NotNull(queryParameters);
            Assert.Contains("user_name", queryParameters.Keys);
            Assert.Contains("user_age", queryParameters.Keys);
            Assert.Contains("Method", queryParameters.Keys);
            Assert.Equal("testuser", queryParameters["user_name"]);
            Assert.Equal("30", queryParameters["user_age"]);
            Assert.Equal("GET", queryParameters["Method"]);
        }

        [Fact]
        public void GetBody_ShouldUseCustomAttributeAliases()
        {
            // Arrange
            var request = new TestRequestWithCustomAttributes();
            request.Username = "testuser";
            request.Age = 30;
            request.HttpMethod = "POST";

            // Act
            var body = request.GetBody() as Dictionary<string, object>;

            // Assert
            Assert.NotNull(body);
            Assert.Contains("user_name", body.Keys);
            Assert.Contains("user_age", body.Keys);
            Assert.Contains("Method", body.Keys);
            Assert.Equal("testuser", body["user_name"]);
            Assert.Equal(30, body["user_age"]);
            Assert.Equal("POST", body["Method"]);
        }

        [Fact]
        public void GetBody_ShouldExcludeMethodPropertyWithoutAttribute()
        {
            // Arrange
            var request = new TestRequestWithProperties();
            request.Username = "testuser";
            request.Age = 30;
            request.Method = "GET";

            // Act
            var body = request.GetBody() as Dictionary<string, object>;

            // Assert
            Assert.NotNull(body);
            Assert.Contains("Username", body.Keys);
            Assert.Contains("Age", body.Keys);
            Assert.DoesNotContain("Method", body.Keys);
        }
    }
}
