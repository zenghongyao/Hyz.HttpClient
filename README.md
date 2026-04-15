# Hyz.HttpClient

> 优雅的 HttpClient 封装，让你的 API 调用更加丝滑！

## ✨ 特性

- 🚀 **多种 HTTP 方法支持**：GET、POST、PUT、DELETE、PATCH
- 🔄 **自动重试机制**：支持指数退避，可配置重试次数
- ⚡ **熔断保护**：防止雪崩效应，支持自动恢复
- 🎯 **灵活的请求管理**：请求头、查询参数、请求体统一管理
- 📦 **类型安全**：强类型的请求和响应
- 🔒 **线程安全**：策略缓存优化，支持并发配置更新
- 🎨 **优雅的 API 设计**：简单易用，开箱即用
- 🎉 **直接实例化支持**：BaseRequest 类现在可以直接实例化，无需创建子类
- 🔗 **属性自动合并**：子类的属性会自动与 SetBody() 设置的参数合并
- 🔍 **属性自动作为查询参数**：子类的公共属性会自动作为查询参数添加到 URL 中
- 🏷️ **请求参数别名**：支持使用特性为请求参数设置别名，灵活控制序列化名称
- 🐫 **参数命名控制**：支持小驼峰命名自动转换，字典参数可独立控制命名方式

## 📦 安装

```bash
dotnet add package Hyz.HttpClient
```

## 🚀 快速开始

### 1. 注册服务

```csharp
using Hyz.HttpClient;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

// 方式1：使用默认配置
services.AddHyzHttpClient();

// 方式2：自定义HttpClient名称
services.AddHyzHttpClient("MyApi");

// 方式3：配置HttpClient
services.AddHyzHttpClient("MyApi", client =>
{
    client.BaseAddress = new Uri("https://api.example.com");
    client.Timeout = TimeSpan.FromSeconds(30);
});

var serviceProvider = services.BuildServiceProvider();
```

### 2. 注入并使用

#### 2.1 直接使用 BaseRequest（新增特性）

```csharp
public class UserService
{
    private readonly HttpClientRequest _httpClientService;

    public UserService(HttpClientRequest httpClientService)
    {
        _httpClientService = httpClientService;
    }

    // GET 请求 - 直接使用 BaseRequest
    public async Task<List<User>?> GetUsersAsync(int page = 1, int pageSize = 20)
    {
        // 直接实例化 BaseRequest，无需创建子类
        var request = new BaseRequest<UserListResponse>();
        request.SetRequestApi("/api/users");

        // 添加查询参数
        request.AddQueryParameter("page", page.ToString());
        request.AddQueryParameter("pageSize", pageSize.ToString());

        var response = await _httpClientService.ExecuteGetAsync<UserListResponse>(request);
        return response?.Result == true ? response.Users : null;
    }

    // POST 请求 - 直接使用 BaseRequest
    public async Task<User?> CreateUserAsync(CreateUserDto userDto)
    {
        var request = new BaseRequest<UserResponse>();
        request.SetRequestApi("/api/users");

        // 设置请求体
        request.SetBody(userDto);

        var response = await _httpClientService.ExecutePostAsync<UserResponse>(request);
        return response?.Result == true ? response.User : null;
    }

    // 使用 SetQueryParameters 合并参数
    public async Task<List<User>?> GetUsersWithMergedParametersAsync(int page = 1, int pageSize = 20, string sort = "name")
    {
        var request = new BaseRequest<UserListResponse>();
        request.SetRequestApi("/api/users");

        // 先添加一些参数
        request.AddQueryParameter("page", page.ToString());
        request.AddQueryParameter("pageSize", pageSize.ToString());

        // 然后设置新参数，会与现有参数合并
        var newParameters = new Dictionary<string, string>
        {
            { "sort", sort },
            { "order", "asc" }
        };
        request.SetQueryParameters(newParameters);

        // 最终查询参数会包含：page, pageSize, sort, order
        var response = await _httpClientService.ExecuteGetAsync<UserListResponse>(request);
        return response?.Result == true ? response.Users : null;
    }
}
```

#### 2.2 使用继承的请求类（属性自动合并特性）

```csharp
// 继承 BaseRequest 创建自己的请求类
public class UserRequest : BaseRequest<UserListResponse>
{
    // 这些属性会自动作为查询参数添加到 URL 中（自动转小驼峰）
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Status { get; set; } = "active";
}

// 使用继承的请求类
public async Task<List<User>?> GetUsersWithAutoQueryParamsAsync()
{
    var request = new UserRequest();
    request.SetRequestApi("/api/users");
    
    // 无需手动添加查询参数，Page、PageSize、Status 会自动添加
    // URL 会自动拼接为：/api/users?page=1&pageSize=20&status=active

    var response = await _httpClientService.ExecuteGetAsync<UserListResponse>(request);
    return response?.Result == true ? response.Users : null;
}

// 继承 BaseRequest 创建登录请求类
public class LoginRequest : BaseRequest<LoginResponse>
{
    // 这些属性会自动与 SetBody() 设置的参数合并（自动转小驼峰）
    public string? Username { get; set; }
    public string? Password { get; set; }
}

// 使用继承的请求类（属性自动合并）
public async Task<string?> LoginAsync(string username, string password)
{
    var request = new LoginRequest();
    request.SetRequestApi("/api/login");
    
    // 设置属性
    request.Username = username;
    request.Password = password;
    
    // 可以额外设置其他参数，会与属性自动合并
    request.SetBody(new { RememberMe = true });
    
    // 请求体最终会包含：{ "username": "...", "password": "...", "rememberMe": true }

    var response = await _httpClientService.ExecutePostAsync<LoginResponse>(request);
    return response?.Result == true ? response.Token : null;
}

```

### 3. 自定义请求类（高级用法）

对于更复杂的场景，你可以创建更详细的自定义请求类：

```csharp
// 继承 BaseRequest 创建复杂的请求类
public class SearchRequest : BaseRequest<SearchResponse>
{
    // 这些属性会自动作为查询参数（自动转小驼峰）
    public string? Keyword { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    
    // 复杂类型属性不会作为查询参数，但会在请求体中使用
    public FilterOptions? Filters { get; set; }
}

public class FilterOptions
{
    public List<string>? Categories { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
}

// 使用复杂的自定义请求类
public async Task<SearchResponse?> SearchAsync(string keyword, List<string> categories)
{
    var request = new SearchRequest();
    request.SetRequestApi("/api/search");
    
    // 设置属性，Keyword、Page、PageSize 会自动作为查询参数
    request.Keyword = keyword;
    request.Categories = categories;
    
    // URL 会自动拼接为：/api/search?keyword=...&page=1&pageSize=20

    var response = await _httpClientService.ExecuteGetAsync<SearchResponse>(request);
    return response;
}

// POST 请求示例
public async Task<CreateProductResponse?> CreateProductAsync(ProductDto product)
{
    var request = new BaseRequest<CreateProductResponse>();
    request.SetRequestApi("/api/products");
    
    // 直接设置请求体
    request.SetBody(product);

    var response = await _httpClientService.ExecutePostAsync<CreateProductResponse>(request);
    return response;
}
```

### 4. 使用请求参数别名特性

你可以使用 `RequestParameterAlias` 特性为请求参数设置别名，灵活控制序列化名称：

```csharp
using Hyz.HttpClient;

// 使用 RequestParameterAlias 特性设置参数别名
public class UserRequest : BaseRequest<UserResponse>
{
    [RequestParameterAlias("user_name")]
    public string? Username { get; set; }

    [RequestParameterAlias("user_age")]
    public int Age { get; set; }

    // 为 Method 属性设置别名，使其作为请求参数
    [RequestParameterAlias("Method")]
    public string HttpMethod { get; set; } = "POST";
}

// 使用带别名的请求类
public async Task<UserResponse?> GetUserAsync(string username, int age)
{
    var request = new UserRequest();
    request.SetRequestApi("/api/users");
    
    // 设置属性
    request.Username = username;
    request.Age = age;
    request.HttpMethod = "GET";
    
    // URL 会自动拼接为：/api/users?user_name=...&user_age=...&Method=GET
    // 请求体也会使用别名：{ "user_name": "...", "user_age": ..., "Method": "GET" }

    var response = await _httpClientService.ExecuteGetAsync<UserResponse>(request);
    return response;
}
```

## 📝 高级用法

### 配置重试策略

```csharp
using Hyz.HttpClient;

// 配置重试选项
HttpClientPolicy.ConfigureRetry(new HttpClientPolicy.RetryOptions
{
    MaxRetryAttempts = 5,  // 重试5次
    BackoffType = DelayBackoffType.Exponential,  // 指数退避
    InitialDelay = TimeSpan.FromMilliseconds(500),  // 初始延迟500ms
    OnRetry = args =>
    {
        Console.WriteLine($"重试第 {args.AttemptNumber} 次");
        return default;
    }
});
```

### 配置熔断策略

```csharp
// 配置熔断选项
HttpClientPolicy.ConfigureCircuitBreaker(new HttpClientPolicy.CircuitBreakerOptions
{
    FailureRatio = 0.5,  // 失败率达到50%时熔断
    SamplingDuration = TimeSpan.FromSeconds(10),  // 采样窗口10秒
    MinimumThroughput = 10,  // 最小吞吐量10次
    BreakDuration = TimeSpan.FromSeconds(30),  // 熔断持续时间30秒
    OnOpened = args => Console.WriteLine("熔断已打开"),
    OnClosed = args => Console.WriteLine("熔断已关闭"),
    OnHalfOpened = args => Console.WriteLine("熔断半开状态")
});
```

### 使用请求头

```csharp
var request = new BaseApiRequest<UserListResponse>();

// 添加单个请求头
request.AddHeader("Authorization", "Bearer token123");
request.AddHeader("Content-Type", "application/json");
request.AddHeader("X-Request-ID", Guid.NewGuid().ToString());

// 批量设置请求头
var headers = new Dictionary<string, string>
{
    { "X-Client-Version", "1.0.0" },
    { "X-Platform", "Web" }
};
request.SetHeaders(headers);
```

### 使用查询参数

```csharp
var request = new BaseApiRequest<UserListResponse>();
request.SetRequestApi("/api/users");

// 添加查询参数
request.AddQueryParameter("page", "1");
request.AddQueryParameter("pageSize", "20");
request.AddQueryParameter("status", "active");

// 批量设置查询参数
var queryParams = new Dictionary<string, string>
{
    { "keyword", "john" },
    { "sort", "name" },
    { "order", "asc" }
};
request.SetQueryParameters(queryParams);

// URL 自动拼接为：/api/users?page=1&pageSize=20&status=active
```

### 禁用重试

```csharp
// 对于非幂等性操作，可以禁用重试
var response = await _httpClientService.ExecutePostAsync<CreateUserResponse>(
    request,
    enableRetry: false
);
```

### 参数命名控制

#### 命名规则说明

| 参数来源 | 默认行为 | 控制方式 |
|---------|---------|---------|
| **实体类属性** | 自动转小驼峰命名 | 使用 `[RequestParameterAlias]` 指定别名 |
| **字典参数** | 自动转小驼峰命名 | 通过 `PreserveDictionaryKeyNaming` 控制 |

#### 全局配置

```csharp
// 全局配置：字典参数自动转小驼峰（默认）
HttpClientPolicy.PreserveDictionaryKeyNaming = false;

// 全局配置：字典参数保持原始 key 名称
HttpClientPolicy.PreserveDictionaryKeyNaming = true;
```

#### 单个请求配置

```csharp
// 全局设置为小驼峰，但这个请求保持原名
HttpClientPolicy.PreserveDictionaryKeyNaming = false;

var request = new BaseRequest<UserResponse>();
request.PreserveDictionaryKeyNaming = true;  // 覆盖全局设置
request.AddQueryParameter("User_Name", "test"); // → User_Name=test（保持原名）

// 全局设置为保持原名，但这个请求使用小驼峰
HttpClientPolicy.PreserveDictionaryKeyNaming = true;

var request = new BaseRequest<UserResponse>();
request.PreserveDictionaryKeyNaming = false;  // 覆盖全局设置
request.AddQueryParameter("User_Name", "test"); // → user_Name=test（转小驼峰）
```

#### 实体类属性命名

```csharp
// 实体类属性默认自动转小驼峰
public class UserRequest : BaseRequest<UserResponse>
{
    public string? UserName { get; set; }   // → userName
    public int PageSize { get; set; }       // → pageSize
    
    // 使用别名特性覆盖默认命名
    [RequestParameterAlias("user_id")]
    public int UserId { get; set; }         // → user_id
}

// 使用示例
var request = new UserRequest();
request.SetRequestApi("/api/users");
request.UserName = "test";
request.PageSize = 20;
request.UserId = 123;
// URL: /api/users?userName=test&pageSize=20&user_id=123
```

#### 字典参数命名

```csharp
// 字典方式设置的参数，受 PreserveDictionaryKeyNaming 控制
var request = new BaseRequest<UserResponse>();
request.SetRequestApi("/api/users");

// 查询参数
request.AddQueryParameter("User_Name", "test");
request.AddQueryParameter("Page_Size", "20");

// 请求体
request.SetBody(new Dictionary<string, object>
{
    { "User_Id", 123 },
    { "Created_At", DateTime.Now }
});

// PreserveDictionaryKeyNaming = false（默认）时：
// URL: /api/users?user_Name=test&page_Size=20
// Body: { "user_Id": 123, "created_At": "..." }

// PreserveDictionaryKeyNaming = true 时：
// URL: /api/users?User_Name=test&Page_Size=20
// Body: { "User_Id": 123, "Created_At": "..." }
```

#### 优先级规则

```
单个请求设置 (request.PreserveDictionaryKeyNaming) > 全局设置 (HttpClientPolicy.PreserveDictionaryKeyNaming)
别名特性 [RequestParameterAlias] > 默认命名规则
```

## 🎯 API 参考

### HttpClientRequest

| 方法 | 说明 |
|------|------|
| `ExecuteGetAsync<T>()` | 发送 GET 请求 |
| `ExecutePostAsync<T>()` | 发送 POST 请求 |
| `ExecutePutAsync<T>()` | 发送 PUT 请求 |
| `ExecuteDeleteAsync<T>()` | 发送 DELETE 请求 |
| `ExecutePatchAsync<T>()` | 发送 PATCH 请求 |
| `ExecuteAsync<T>()` | 通用方法，支持任意 HTTP 方法 |

### BaseRequest<T>

| 属性/方法 | 说明 |
|-----------|------|
| `SetRequestApi(string path)` | 设置 API 路径 |
| `GetRequestApi()` | 获取 API 路径 |
| `AddHeader(key, value)` | 添加单个请求头 |
| `SetHeaders(dictionary)` | 批量设置请求头 |
| `GetHeaders()` | 获取请求头字典 |
| `AddQueryParameter(key, value)` | 添加单个查询参数（会与子类属性合并，显式设置的参数优先级高于子类属性） |
| `SetQueryParameters(dictionary)` | 批量设置查询参数（会与现有查询参数合并，显式设置的参数优先级高于现有参数） |
| `GetQueryParameters()` | 获取所有合并的查询参数（包括通过AddQueryParameter、SetQueryParameters设置的参数和子类的属性） |
| `GetQueryParametersUrl()` | 获取所有合并的查询参数（包括通过AddQueryParameter、SetQueryParameters设置的参数和子类的属性）返回拼接好的 URL 查询字符串（即 ?key1=value1&key2=value2格式） |
| `SetBody(object)` | 设置请求体（会与子类属性自动合并） |
| `GetBody()` | 获取请求体对象 |
| `Method` | HTTP 方法（GET/POST/PUT/DELETE/PATCH） |
| `PreserveDictionaryKeyNaming` | 是否保持字典参数的原始命名（单个请求级别，null 时使用全局配置） |

### HttpClientPolicy

| 属性 | 说明 |
|------|------|
| `PreserveDictionaryKeyNaming` | 全局配置：是否保持字典参数的原始命名（默认 false，即转小驼峰） |


## 💡 最佳实践

### 1. 合理配置重试次数

```csharp
// 建议：3-5 次
HttpClientPolicy.ConfigureRetry(new HttpClientPolicy.RetryOptions
{
    MaxRetryAttempts = 3
});
```

### 2. 选择合适的退避策略

```csharp
// 指数退避通常是最佳选择
BackoffType = DelayBackoffType.Exponential
```

### 3. 设置合理的熔断参数

```csharp
// 根据业务特点调整
FailureRatio = 0.5,           // 失败率阈值 0.5-0.8
SamplingDuration = 10s,      // 采样窗口 10-30 秒
MinimumThroughput = 10,      // 最小吞吐量 5-10
BreakDuration = 30s          // 熔断时长 30-60 秒
```

### 4. 使用请求头追踪

```csharp
request.AddHeader("X-Request-ID", Guid.NewGuid().ToString());
```

### 5. HTTP 方法选择建议

| 方法 | 用途 | 场景 |
|------|------|------|
| GET | 获取资源 | 查询数据、列表、详情 |
| POST | 创建资源 | 新增记录、提交表单 |
| PUT | 完整更新 | 更新整个资源 |
| PATCH | 部分更新 | 更新资源的部分字段 |
| DELETE | 删除资源 | 删除记录 |

### 6. 直接实例化 BaseRequest 的最佳实践

```csharp
// 对于简单请求，直接使用 BaseRequest
var request = new BaseRequest<UserResponse>();
request.SetRequestApi("/api/users");
request.Method = "GET";
request.AddQueryParameter("id", "123");

// 对于复杂请求，创建专用的请求类
public class UserRequest : BaseRequest<UserResponse>
{
    public int Id { get; set; }
    public string? Name { get; set; }
}

var request = new UserRequest();
request.SetRequestApi("/api/users");
request.Method = "GET";
request.Id = 123;
// Id 会自动作为查询参数
```

### 7. 使用子类属性自动作为查询参数的最佳实践

```csharp
// 为查询参数创建专用的请求类
public class SearchRequest : BaseRequest<SearchResponse>
{
    // 这些属性会自动作为查询参数（自动转小驼峰）
    public string? Keyword { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SortBy { get; set; } = "name";
    public string? Order { get; set; } = "asc";
}

// 使用时只需设置属性
var request = new SearchRequest();
request.SetRequestApi("/api/search");
request.Method = "GET";
request.Keyword = "test";
// URL 会自动拼接为：/api/search?keyword=test&page=1&pageSize=20&sortBy=name&order=asc
```

### 8. 使用属性自动合并的最佳实践

```csharp
// 为请求体创建专用的请求类
public class CreateUserRequest : BaseRequest<UserResponse>
{
    // 这些属性会自动与 SetBody() 设置的参数合并（自动转小驼峰）
    public string? Username { get; set; }
    public string? Email { get; set; }
    public string? Password { get; set; }
}

// 使用时设置属性并添加额外参数
var request = new CreateUserRequest();
request.SetRequestApi("/api/users");
request.Method = "POST";
request.Username = "testuser";
request.Email = "test@example.com";
request.Password = "password123";

// 添加额外参数，会与属性自动合并
request.SetBody(new { Role = "user", Active = true });
// 请求体最终会包含：{ "username": "testuser", "email": "test@example.com", "password": "password123", "role": "user", "active": true }
```


## 📄 许可证

MIT License - 详见 [LICENSE](LICENSE) 文件

**如果这个项目对你有帮助，请给它一个 ⭐️**
