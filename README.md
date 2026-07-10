# Hyz.HttpClient

> 优雅的 HttpClient 封装，让你的 API 调用更加丝滑！

## ✨ 特性

- 🚀 **多种 HTTP 方法支持**：GET、POST、PUT、DELETE、PATCH
- 🔄 **自动重试机制**：支持指数退避，可配置重试次数
- ⚡ **熔断保护**：按端点隔离的断路器，防止雪崩效应，支持自动恢复和自动清理
- 🎯 **灵活的请求管理**：请求头、查询参数、请求体统一管理
- 📦 **类型安全**：强类型的请求和响应
- 🔒 **线程安全**：ConcurrentDictionary 管理管道，支持并发访问
- 🎨 **优雅的 API 设计**：简单易用，开箱即用
- 🎉 **直接实例化支持**：BaseRequest 类现在可以直接实例化，无需创建子类
- 🔗 **属性自动合并**：子类的属性会自动与 SetBody() 设置的参数合并
- 🔍 **属性自动作为查询参数**：子类的公共属性会自动作为查询参数添加到 URL 中
- 🏷️ **请求参数别名**：支持使用特性为请求参数设置别名，灵活控制序列化名称
- 🐫 **参数命名控制**：支持小驼峰命名自动转换，字典参数可独立控制命名方式
- 🔍 **请求拦截器**：支持请求前后的AOP拦截，可用于日志记录、请求验证、性能监控等场景
- 🔐 **证书配置**：支持HTTPS证书验证、客户端证书、自定义证书验证回调等

## 📦 安装

```bash
dotnet add package Hyz.HttpClient
```

## 🚀 快速开始

### 无依赖注入环境（.NET Framework）

如果你在 .NET Framework 环境下使用，或者不想使用依赖注入，可以使用 `HyzHttpClientFactory` 工厂类，开箱即用：

```csharp
using Hyz.HttpClient;

// 方式1：使用静态方法快速创建
var httpClientRequest = HyzHttpClientFactory.CreateInstance();

// 方式2：使用全局配置 + 静态方法
HyzHttpClientFactory.Initialize(
    baseAddress: new Uri("https://api.example.com"),
    timeout: TimeSpan.FromSeconds(30)
);
var httpClientRequest = HyzHttpClientFactory.CreateInstance();

// 方式3：使用实例化工厂
var factory = new HyzHttpClientFactory(
    baseAddress: new Uri("https://api.example.com"),
    timeout: TimeSpan.FromSeconds(30)
);
var httpClientRequest = factory.Create();

// 方式4：创建时指定配置（覆盖全局配置）
var httpClientRequest = HyzHttpClientFactory.CreateInstance(
    baseAddress: new Uri("https://custom.example.com"),
    timeout: TimeSpan.FromSeconds(15)
);

// 方式5：创建命名客户端
var httpClientRequest = HyzHttpClientFactory.CreateNamedInstance("MyApi", client =>
{
    client.DefaultRequestHeaders.Add("X-Custom-Header", "value");
});
```

### 1. 注册服务（依赖注入环境）

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

#### 断路器隔离机制

断路器按 `{ClientName}:{HTTPMethod}:{BasePath}` 自动隔离，不同的 API 端点使用独立的断路器，互不影响：

```
GET  /api/users   → default:GET:/api/users    → 断路器 A
POST /api/users   → default:POST:/api/users   → 断路器 B（独立）
GET  /api/orders  → default:GET:/api/orders   → 断路器 C（独立）
```

- 相同路径 + 相同方法的请求共享同一个断路器
- 查询参数不同不影响隔离（`/api/users?page=1` 和 `/api/users?page=2` 共享断路器）
- 不同命名 HttpClient 的请求拥有独立的断路器
- API A 触发熔断 → 只影响 API A，其他 API 正常运行
- 超过 5 分钟未使用的断路器会自动清理释放（可配置）

```csharp
// 手动清理空闲断路器
var removed = HttpClientPolicy.CleanupUnusedPipelines(TimeSpan.FromMinutes(5));

// 查看当前断路器数量
var count = HttpClientPolicy.PipelineCount;
```

#### 断路器自动清理

当服务注册时（`AddHyzHttpClient()`），会自动启动后台清理任务，定期清理长时间未使用的断路器管道，避免内存泄漏：

```csharp
// 配置清理参数（可选，以下为默认值）
HttpClientPolicy.PipelineCleanupMaxIdleTime = TimeSpan.FromMinutes(5);  // 最大空闲时间
HttpClientPolicy.PipelineCleanupInterval = TimeSpan.FromMinutes(1);     // 检查间隔

// 启动/停止自动清理（通常不需要手动调用，AddHyzHttpClient 会自动启动）
HttpClientPolicy.StartPipelineCleanup();   // 幂等，重复调用安全
HttpClientPolicy.StopPipelineCleanup();     // 停止后台任务（应用退出时）
```

**自动续期机制**：每次请求访问断路器管道时，`LastAccessTime` 会自动更新，因此活跃的管道不会被清理。只有长时间未使用的管道才会被移除。详见[断路器隔离机制](#断路器隔离机制)。

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

### 请求拦截器

请求拦截器允许你在请求发送前和请求完成后执行自定义逻辑，常用于日志记录、请求验证、性能监控等场景。

#### 请求前拦截器

```csharp
// 配置请求前拦截器
HttpClientPolicy.OnRequestSending = context =>
{
    Console.WriteLine($"=== 请求信息 ===");
    Console.WriteLine($"请求时间: {context.RequestTime}");
    Console.WriteLine($"HTTP方法: {context.HttpMethod}");
    Console.WriteLine($"请求地址: {context.FullUrl}");
    Console.WriteLine($"请求头: {string.Join(", ", context.Headers?.Keys ?? Array.Empty<string>())}");
    Console.WriteLine($"查询参数: {string.Join(", ", context.QueryParameters?.Keys ?? Array.Empty<string>())}");
    
    if (!string.IsNullOrEmpty(context.BodyJson))
    {
        Console.WriteLine($"请求体: {context.BodyJson}");
    }
    
    // 可以在这里进行请求验证
    if (string.IsNullOrEmpty(context.FullUrl))
    {
        throw new InvalidOperationException("请求地址不能为空");
    }
};
```

#### 请求后拦截器

```csharp
// 配置请求后拦截器
HttpClientPolicy.OnRequestCompleted = context =>
{
    Console.WriteLine($"=== 响应信息 ===");
    Console.WriteLine($"响应时间: {context.ResponseTime}");
    Console.WriteLine($"请求耗时: {context.Duration.TotalMilliseconds}ms");
    Console.WriteLine($"状态码: {context.StatusCode}");
    Console.WriteLine($"是否成功: {context.IsSuccess}");
    
    if (context.Exception != null)
    {
        Console.WriteLine($"异常信息: {context.Exception.Message}");
    }
    
    if (!string.IsNullOrEmpty(context.ResponseContent))
    {
        Console.WriteLine($"响应内容: {context.ResponseContent}");
    }
};
```

#### 完整示例：日志记录

```csharp
// 配置请求日志记录
HttpClientPolicy.OnRequestSending = context =>
{
    // 记录请求日志
    LogRequest(context);
};

HttpClientPolicy.OnRequestCompleted = context =>
{
    // 记录响应日志
    LogResponse(context);
    
    // 性能监控：慢请求告警
    if (context.Duration.TotalSeconds > 3)
    {
        Console.WriteLine($"[告警] 慢请求: {context.RequestContext.FullUrl} 耗时 {context.Duration.TotalSeconds}s");
    }
};

private void LogRequest(RequestInterceptionContext context)
{
    Console.WriteLine($"[{context.RequestTime:yyyy-MM-dd HH:mm:ss}] {context.HttpMethod} {context.FullUrl}");
}

private void LogResponse(ResponseInterceptionContext context)
{
    var status = context.IsSuccess ? "成功" : "失败";
    Console.WriteLine($"[{context.ResponseTime:yyyy-MM-dd HH:mm:ss}] {status} ({context.StatusCode}) - {context.Duration.TotalMilliseconds}ms");
}
```

#### 请求上下文属性

**RequestInterceptionContext（请求上下文）**

| 属性 | 类型 | 说明 |
|------|------|------|
| `RequestApi` | string | 请求API地址 |
| `FullUrl` | string | 完整请求URL（包含查询参数） |
| `HttpMethod` | string | HTTP方法 |
| `Headers` | IDictionary | 请求头 |
| `QueryParameters` | IDictionary | 查询参数 |
| `Body` | object | 请求体对象 |
| `BodyJson` | string | 请求体JSON字符串 |
| `RequestTime` | DateTime | 请求时间 |
| `Items` | IDictionary | 自定义数据 |

**ResponseInterceptionContext（响应上下文）**

| 属性 | 类型 | 说明 |
|------|------|------|
| `RequestContext` | RequestInterceptionContext | 请求上下文 |
| `ResponseMessage` | HttpResponseMessage | HTTP响应消息 |
| `StatusCode` | int | 响应状态码 |
| `IsSuccess` | bool | 是否成功 |
| `ResponseContent` | string | 响应内容 |
| `ResponseTime` | DateTime | 响应时间 |
| `Duration` | TimeSpan | 请求耗时 |
| `Exception` | Exception | 异常信息 |

### 证书配置

在 HTTPS 请求场景中，支持以下证书配置方式：

> **默认行为**：
> - 使用 `AddHyzHttpClient()` 不传证书配置时，默认忽略证书验证错误（`HttpClientPolicy.IgnoreCertificateErrors = true`）
> - 使用 `AddHyzHttpClient(..., configureCertificate: cert => { ... })` 传入证书配置时，默认启用严格证书验证（`IgnoreCertificateErrors = false`）

#### 全局配置

```csharp
// 在程序启动时设置（Program.cs 或 Startup.cs）
HttpClientPolicy.IgnoreCertificateErrors = false; // 生产环境推荐
```

#### 忽略证书验证（开发/测试环境）

```csharp
// 方式1：使用默认配置（自动忽略证书错误）
services.AddHyzHttpClient("MyApiClient",
    configureClient: client =>
    {
        client.BaseAddress = new Uri("https://self-signed.example.com");
        client.Timeout = TimeSpan.FromSeconds(30);
    });

// 方式2：显式配置忽略证书（覆盖默认行为）
services.AddHyzHttpClient("MyApiClient",
    configureClient: client => client.BaseAddress = new Uri("https://api.example.com"),
    configureJsonSerializer: null,
    configureCertificate: cert =>
    {
        cert.IgnoreCertificateErrors = true;
    });
```

#### 启用严格证书验证（生产环境）

```csharp
// 传入 configureCertificate 参数后，默认启用严格验证
services.AddHyzHttpClient("MyApiClient",
    configureClient: client => client.BaseAddress = new Uri("https://api.example.com"),
    configureJsonSerializer: null,
    configureCertificate: cert =>
    {
        // 此时 IgnoreCertificateErrors 默认为 false
        // 可以配置自定义验证逻辑
        cert.ServerCertificateValidationCallback = (message, certificate, chain, errors) =>
        {
            if (certificate != null && certificate.Thumbprint == "YOUR_CERT_THUMBPRINT")
            {
                return true;
            }
            return false;
        };
    });
```

#### 添加客户端证书

```csharp
services.AddHyzHttpClient("MyApiClient",
    configureClient: client =>
    {
        client.BaseAddress = new Uri("https://api.example.com");
    },
    configureCertificate: cert =>
    {
        // 方式1：从文件添加客户端证书
        cert.AddClientCertificate("path/to/client.pfx", "certificate_password");
        
        // 方式2：从字节数组添加
        // cert.AddClientCertificate(certificateBytes, "password");
        
        // 方式3：直接设置证书集合
        // cert.ClientCertificates = new X509CertificateCollection { certificate };
    });
```

#### 配置 SSL 协议版本

```csharp
services.AddHyzHttpClient("MyApiClient",
    configureCertificate: cert =>
    {
        // 指定 SSL 协议版本
        cert.SslProtocols = System.Security.Authentication.SslProtocols.Tls12 | 
                           System.Security.Authentication.SslProtocols.Tls13;
    });
```

#### 完整示例：双向证书认证

```csharp
// Program.cs 或 Startup.cs
services.AddHyzHttpClient("SecureApiClient",
    configureClient: client =>
    {
        client.BaseAddress = new Uri("https://secure-api.example.com");
        client.Timeout = TimeSpan.FromSeconds(30);
    },
    configureJsonSerializer: json =>
    {
        json.PropertyNameCaseInsensitive = true;
        json.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    },
    configureCertificate: cert =>
    {
        // 添加客户端证书
        cert.AddClientCertificate("certs/client.pfx", "client_password");
        
        // 自定义服务器证书验证
        cert.ServerCertificateValidationCallback = (message, serverCert, chain, errors) =>
        {
            if (serverCert == null) return false;
            
            // 验证服务器证书指纹
            var validThumbprints = new[] { "SERVER_CERT_THUMBPRINT_1", "SERVER_CERT_THUMBPRINT_2" };
            return validThumbprints.Contains(serverCert.Thumbprint);
        };
        
        // 指定 TLS 版本
        cert.SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;
    });
```

### CertificateOptions 属性

| 属性 | 类型 | 说明 |
|------|------|------|
| `IgnoreCertificateErrors` | bool | 是否忽略证书错误（仅开发/测试环境） |
| `ServerCertificateValidationCallback` | Func | 服务器证书验证回调 |
| `ClientCertificates` | X509CertificateCollection | 客户端证书集合 |
| `SslProtocols` | SslProtocols? | SSL 协议版本 |

### HyzHttpClientFactory（无 DI 环境）

`HyzHttpClientFactory` 是专门为 .NET Framework 等无依赖注入环境设计的工厂类，支持静态方法快速调用和实例化配置两种模式。

#### 全局配置

```csharp
// 设置全局默认 BaseAddress
HyzHttpClientFactory.GlobalBaseAddress = new Uri("https://api.example.com");

// 设置全局默认超时时间
HyzHttpClientFactory.GlobalTimeout = TimeSpan.FromSeconds(30);

// 设置全局证书配置
HyzHttpClientFactory.GlobalCertificateOptions = new CertificateOptions
{
    IgnoreCertificateErrors = false
};

// 一次性初始化所有全局配置
HyzHttpClientFactory.Initialize(
    baseAddress: new Uri("https://api.example.com"),
    timeout: TimeSpan.FromSeconds(30),
    certificateOptions: certificateOptions,
    jsonOptions: jsonSerializerOptions
);
```

#### 静态方法

```csharp
// 创建默认实例
var request = HyzHttpClientFactory.CreateInstance();

// 创建时指定配置（覆盖全局配置）
var request = HyzHttpClientFactory.CreateInstance(
    logger: customLogger,
    baseAddress: new Uri("https://custom.example.com"),
    timeout: TimeSpan.FromSeconds(15),
    jsonOptions: customJsonOptions
);

// 创建命名客户端
var request = HyzHttpClientFactory.CreateNamedInstance("MyApi", client =>
{
    client.DefaultRequestHeaders.Add("X-Custom-Header", "value");
});
```

#### 实例方法

```csharp
var factory = new HyzHttpClientFactory();

// 创建默认实例
var request = factory.Create();

// 创建时指定配置
var request = factory.Create(
    logger: customLogger,
    baseAddress: new Uri("https://custom.example.com"),
    timeout: TimeSpan.FromSeconds(15),
    jsonOptions: customJsonOptions
);

// 创建命名客户端
var request = factory.CreateNamedClient("MyApi", client =>
{
    client.DefaultRequestHeaders.Add("X-Custom-Header", "value");
});
```

#### HyzHttpClientFactory 属性

| 属性 | 类型 | 说明 |
|------|------|------|
| `Instance` | `HyzHttpClientFactory` | 获取全局单例实例 |
| `GlobalBaseAddress` | `Uri?` | 全局默认 BaseAddress |
| `GlobalTimeout` | `TimeSpan?` | 全局默认超时时间 |
| `GlobalCertificateOptions` | `CertificateOptions?` | 全局默认证书配置 |
| `GlobalJsonOptions` | `JsonSerializerOptions?` | 全局默认 JSON 序列化配置 |

#### HyzHttpClientFactory 方法

| 方法 | 说明 |
|------|------|
| `Create()` | 创建 `HttpClientRequest` 实例（使用全局配置） |
| `Create(logger)` | 创建 `HttpClientRequest` 实例（指定日志记录器） |
| `Create(logger, baseAddress, timeout, jsonOptions)` | 创建 `HttpClientRequest` 实例（完整配置） |
| `CreateNamedClient(name, configureClient, logger, jsonOptions)` | 创建命名客户端实例 |
| `CreateInstance()` | 静态方法：创建 `HttpClientRequest` 实例 |
| `CreateInstance(logger)` | 静态方法：创建 `HttpClientRequest` 实例（指定日志记录器） |
| `CreateInstance(logger, baseAddress, timeout, jsonOptions)` | 静态方法：创建 `HttpClientRequest` 实例（完整配置） |
| `CreateNamedInstance(name, configureClient, logger, jsonOptions)` | 静态方法：创建命名客户端实例 |
| `Initialize(baseAddress, timeout, certificateOptions, jsonOptions)` | 静态方法：初始化全局配置 |

#### 日志记录器

`HyzHttpClientFactory` 默认使用 `NullLogger<HttpClientRequest>`，不会输出日志。如果需要日志，可以传入自定义的 `ILogger<HttpClientRequest>` 实现：

```csharp
// 使用控制台日志
var logger = LoggerFactory.Create(builder => builder.AddConsole())
    .CreateLogger<HttpClientRequest>();

var request = HyzHttpClientFactory.CreateInstance(logger);
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

| 属性/方法 | 说明 |
|------|------|
| `PreserveDictionaryKeyNaming` | 全局配置：是否保持字典参数的原始命名（默认 false，即转小驼峰） |
| `IgnoreCertificateErrors` | 全局配置：是否忽略证书验证错误（默认 true，便于开发调试） |
| `OnRequestSending` | 请求前拦截器：在请求发送前调用 |
| `OnRequestCompleted` | 请求后拦截器：在请求完成后调用 |
| `ConfigureRetry(options)` | 配置重试策略（会清除现有断路器管道） |
| `ConfigureCircuitBreaker(options)` | 配置熔断策略（会清除现有断路器管道） |
| `GetPipeline(clientName, method, path)` | 获取或创建隔离的断路器管道 |
| `BuildPipelineKey(clientName, method, path)` | 构建管道 Key：`{ClientName}:{METHOD}:{BasePath}` |
| `ExtractBasePath(path)` | 从 URL 提取基础路径（去除查询参数和域名） |
| `CleanupUnusedPipelines(maxIdleTime)` | 清理超过空闲时间的断路器管道，返回清理数量 |
| `PipelineCount` | 当前管道数量 |
| `PipelineCleanupMaxIdleTime` | 管道最大空闲时间（默认 5 分钟），超过后自动清理 |
| `PipelineCleanupInterval` | 自动清理检查间隔（默认 1 分钟） |
| `StartPipelineCleanup()` | 启动自动清理后台任务（幂等，`AddHyzHttpClient` 自动调用） |
| `StopPipelineCleanup()` | 停止自动清理后台任务 |
| `ClearAllPipelines()` | 清除所有管道（测试用） |
| `ClearInterceptors()` | 清除所有拦截器回调 |

### RequestInterceptionContext

| 属性 | 说明 |
|------|------|
| `RequestApi` | 请求API地址 |
| `FullUrl` | 完整请求URL（包含查询参数） |
| `HttpMethod` | HTTP方法 |
| `Headers` | 请求头字典 |
| `QueryParameters` | 查询参数字典 |
| `Body` | 请求体对象 |
| `BodyJson` | 请求体JSON字符串 |
| `RequestTime` | 请求时间 |
| `Items` | 自定义数据字典 |

### ResponseInterceptionContext

| 属性 | 说明 |
|------|------|
| `RequestContext` | 请求上下文 |
| `ResponseMessage` | HTTP响应消息 |
| `StatusCode` | 响应状态码 |
| `IsSuccess` | 是否成功 |
| `ResponseContent` | 响应内容 |
| `ResponseTime` | 响应时间 |
| `Duration` | 请求耗时 |
| `Exception` | 异常信息（如果请求失败） |


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

> **断路器隔离**：不同 API 端点（`GET /api/users`、`POST /api/users`、`GET /api/orders`）使用独立的断路器，一个端点熔断不会影响其他端点。同名 HttpClient + 相同路径 + 相同方法的请求共享断路器。详细说明见[断路器隔离机制](#断路器隔离机制)。

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
