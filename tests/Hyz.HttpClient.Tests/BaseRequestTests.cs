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

            // Assert - 实体类属性默认使用小驼峰命名
            Assert.Contains("username=testuser", url);
            Assert.Contains("age=30", url);
            Assert.Contains("city=Beijing", url);
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
            // Add explicit query parameter with the same name as a property (using lowercase to match camelCase)
            request.AddQueryParameter("username", "overrideuser");
            request.AddQueryParameter("age", "40");

            // Act
            var url = request.GetQueryParametersUrl();

            // Assert - 实体类属性默认使用小驼峰命名
            Assert.Contains("username=overrideuser", url); // Explicit parameter should take priority
            Assert.Contains("age=40", url); // Explicit parameter should take priority
            Assert.Contains("city=Beijing", url); // No explicit parameter, so use property value
        }

        [Fact]
        public void GetQueryParameters_ShouldReturnMergedParameters()
        {
            // Arrange
            var request = new TestRequestWithProperties();
            request.Username = "testuser";
            request.Age = 30;
            request.City = "Beijing";
            // Add explicit query parameter with the same name as a property (using lowercase to match camelCase)
            request.AddQueryParameter("username", "overrideuser");
            request.AddQueryParameter("page", "1");

            // Act
            var queryParameters = request.GetQueryParameters();

            // Assert - 实体类属性默认使用小驼峰命名
            Assert.NotNull(queryParameters);
            Assert.Equal(4, queryParameters.Count);
            Assert.Equal("overrideuser", queryParameters["username"]); // Explicit parameter should take priority
            Assert.Equal("30", queryParameters["age"]); // Property value
            Assert.Equal("Beijing", queryParameters["city"]); // Property value
            Assert.Equal("1", queryParameters["page"]); // Explicit parameter
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

            // Assert - 实体类属性默认使用小驼峰命名
            Assert.NotNull(body);
            Assert.Contains("username", body.Keys);
            Assert.Contains("age", body.Keys);
            Assert.DoesNotContain("Method", body.Keys);
            Assert.DoesNotContain("method", body.Keys);
        }

        [Fact]
        public void PreserveDictionaryKeyNaming_RequestLevel_ShouldOverrideGlobal()
        {
            // Arrange - 保存原始全局设置
            var originalGlobalSetting = HttpClientPolicy.PreserveDictionaryKeyNaming;

            try
            {
                // 设置全局为 false（转小驼峰）
                HttpClientPolicy.PreserveDictionaryKeyNaming = false;

                var request = new TestRequest();
                request.SetRequestApi("/api/test");
                
                // 单个请求设置为 true（保持原名）
                request.PreserveDictionaryKeyNaming = true;
                
                // 使用字典方式添加查询参数
                request.AddQueryParameter("User_Name", "testuser");
                request.AddQueryParameter("Page_Size", "20");

                // Act
                var url = request.GetQueryParametersUrl();

                // Assert - 单个请求设置覆盖全局设置，保持原名
                Assert.Contains("User_Name=testuser", url);
                Assert.Contains("Page_Size=20", url);
            }
            finally
            {
                // 恢复原始全局设置
                HttpClientPolicy.PreserveDictionaryKeyNaming = originalGlobalSetting;
            }
        }

        [Fact]
        public void PreserveDictionaryKeyNaming_RequestLevelFalse_ShouldConvertToCamelCase()
        {
            // Arrange - 保存原始全局设置
            var originalGlobalSetting = HttpClientPolicy.PreserveDictionaryKeyNaming;

            try
            {
                // 设置全局为 true（保持原名）
                HttpClientPolicy.PreserveDictionaryKeyNaming = true;

                var request = new TestRequest();
                request.SetRequestApi("/api/test");
                
                // 单个请求设置为 false（转小驼峰）
                request.PreserveDictionaryKeyNaming = false;
                
                // 使用字典方式添加查询参数
                request.AddQueryParameter("User_Name", "testuser");
                request.AddQueryParameter("Page_Size", "20");

                // Act
                var url = request.GetQueryParametersUrl();

                // Assert - 单个请求设置覆盖全局设置，转小驼峰
                Assert.Contains("user_Name=testuser", url);
                Assert.Contains("page_Size=20", url);
            }
            finally
            {
                // 恢复原始全局设置
                HttpClientPolicy.PreserveDictionaryKeyNaming = originalGlobalSetting;
            }
        }

        [Fact]
        public void PreserveDictionaryKeyNaming_RequestLevelNull_ShouldUseGlobal()
        {
            // Arrange - 保存原始全局设置
            var originalGlobalSetting = HttpClientPolicy.PreserveDictionaryKeyNaming;

            try
            {
                // 设置全局为 true（保持原名）
                HttpClientPolicy.PreserveDictionaryKeyNaming = true;

                var request = new TestRequest();
                request.SetRequestApi("/api/test");
                
                // 单个请求设置为 null（使用全局设置）
                request.PreserveDictionaryKeyNaming = null;
                
                // 使用字典方式添加查询参数
                request.AddQueryParameter("User_Name", "testuser");

                // Act
                var url = request.GetQueryParametersUrl();

                // Assert - 使用全局设置，保持原名
                Assert.Contains("User_Name=testuser", url);

                // 修改全局设置
                HttpClientPolicy.PreserveDictionaryKeyNaming = false;

                var request2 = new TestRequest();
                request2.SetRequestApi("/api/test");
                request2.PreserveDictionaryKeyNaming = null;
                request2.AddQueryParameter("User_Name", "testuser");

                var url2 = request2.GetQueryParametersUrl();

                // Assert - 使用全局设置，转小驼峰
                Assert.Contains("user_Name=testuser", url2);
            }
            finally
            {
                // 恢复原始全局设置
                HttpClientPolicy.PreserveDictionaryKeyNaming = originalGlobalSetting;
            }
        }

        [Fact]
        public void PreserveDictionaryKeyNaming_BodyDictionary_ShouldRespectRequestLevel()
        {
            // Arrange - 保存原始全局设置
            var originalGlobalSetting = HttpClientPolicy.PreserveDictionaryKeyNaming;

            try
            {
                // 设置全局为 false（转小驼峰）
                HttpClientPolicy.PreserveDictionaryKeyNaming = false;

                var request = new TestRequest();
                request.SetRequestApi("/api/test");
                
                // 单个请求设置为 true（保持原名）
                request.PreserveDictionaryKeyNaming = true;
                
                // 使用字典方式设置请求体
                request.SetBody(new Dictionary<string, object>
                {
                    { "User_Id", 123 },
                    { "Created_At", "2024-01-01" }
                });

                // Act
                var body = request.GetBody() as Dictionary<string, object>;

                // Assert - 单个请求设置覆盖全局设置，保持原名
                Assert.NotNull(body);
                Assert.Contains("User_Id", body.Keys);
                Assert.Contains("Created_At", body.Keys);
            }
            finally
            {
                // 恢复原始全局设置
                HttpClientPolicy.PreserveDictionaryKeyNaming = originalGlobalSetting;
            }
        }
    }
}
