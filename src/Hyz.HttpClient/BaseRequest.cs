using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;

namespace Hyz.HttpClient
{
    /// <summary>
    /// 基础HTTP请求实现
    /// </summary>
    /// <typeparam name="T">响应类型</typeparam>
    public class BaseRequest<T> : IBaseRequest<T>
        where T : class
    {
        private string _requestApi = string.Empty;
        private IDictionary<string, string>? _headers;
        private IDictionary<string, string>? _queryParameters;
        private object? _body;

        /// <summary>
        /// HTTP方法（不调用通用方法可以忽略）
        /// </summary>
        public string Method { get; set; } = "POST";

        /// <summary>
        /// 获取请求API路径
        /// </summary>
        public string GetRequestApi()
        {
            return _requestApi;
        }

        /// <summary>
        /// 检查类型是否为简单类型
        /// </summary>
        private static bool IsSimpleType(Type type)
        {
            return type.IsPrimitive || 
                   type == typeof(string) || 
                   type == typeof(decimal) || 
                   type == typeof(DateTime) || 
                   type == typeof(DateTimeOffset) || 
                   type == typeof(TimeSpan) || 
                   type == typeof(Guid);
        }

        /// <summary>
        /// 设置请求API路径
        /// </summary>
        public void SetRequestApi(string? path)
        {
            if (!string.IsNullOrEmpty(path))
                _requestApi = path!;
        }

        /// <summary>
        /// 获取请求头
        /// </summary>
        public IDictionary<string, string>? GetHeaders()
        {
            return _headers;
        }

        /// <summary>
        /// 添加单个请求头
        /// </summary>
        public void AddHeader(string key, string value)
        {
            _headers ??= new Dictionary<string, string>();
            _headers[key] = value;
        }

        /// <summary>
        /// 设置请求头
        /// </summary>
        public void SetHeaders(IDictionary<string, string>? headers)
        {
            if (headers == null || headers.Count == 0)
            {
                _headers = null;
                return;
            }

            _headers = new Dictionary<string, string>(headers);
        }

        /// <summary>
        /// 获取查询参数URL
        /// </summary>
        public string? GetQueryParametersUrl()
        {
            // 获取所有合并的查询参数
            var allQueryParameters = GetQueryParameters();

            // 如果有查询参数，将其拼接到URL中
            if (allQueryParameters != null && allQueryParameters.Count > 0)
            {
                var queryString = string.Join("&", allQueryParameters.Select(kvp =>
                    $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));

                var separator = _requestApi.Contains('?') ? "&" : "?";
                return $"{separator}{queryString}";
            }
            return null;
        }


        /// <summary>
        /// 获取查询参数
        /// </summary>
        public IDictionary<string, string>? GetQueryParameters()
        {
            // 创建一个字典来存储所有查询参数
            var allQueryParameters = new Dictionary<string, string>();

            // 添加通过AddQueryParameter/SetQueryParameters设置的查询参数
            if (_queryParameters != null && _queryParameters.Count > 0)
            {
                foreach (var kvp in _queryParameters)
                {
                    allQueryParameters[kvp.Key] = kvp.Value;
                }
            }

            // 添加子类的公共属性作为查询参数
            var properties = GetType().GetProperties();
            foreach (var property in properties)
            {
                // 排除Method属性，因为它不是查询参数的一部分
                if (property.Name != nameof(Method))
                {
                    var value = property.GetValue(this);
                    if (value != null)
                    {
                        // 只添加简单类型的属性作为查询参数
                        if (IsSimpleType(property.PropertyType))
                        {
                            // 只有当该属性还没有在查询参数中时才添加
                            if (!allQueryParameters.ContainsKey(property.Name))
                            {
                                allQueryParameters[property.Name] = value.ToString()!;
                            }
                        }
                    }
                }
            }

            return allQueryParameters.Count > 0 ? allQueryParameters : null;
        }

        /// <summary>
        /// 添加单个查询参数
        /// </summary>
        /// <remarks>添加的参数会与子类的属性合并，显式设置的参数优先级高于子类属性</remarks>
        public void AddQueryParameter(string key, string value)
        {
            _queryParameters ??= new Dictionary<string, string>();
            _queryParameters[key] = value;
        }

        /// <summary>
        /// 设置查询参数
        /// </summary>
        /// <remarks>设置的参数会与现有的查询参数合并，显式设置的参数优先级高于现有参数</remarks>
        public void SetQueryParameters(IDictionary<string, string>? parameters)
        {
            if (parameters == null || parameters.Count == 0)
            {
                _queryParameters = null;
                return;
            }

            // 如果现有查询参数不为空，合并参数
            if (_queryParameters != null && _queryParameters.Count > 0)
            {
                foreach (var kvp in parameters)
                {
                    _queryParameters[kvp.Key] = kvp.Value;
                }
            }
            else
            {
                // 如果现有查询参数为空，直接设置
                _queryParameters = new Dictionary<string, string>(parameters);
            }
        }

        /// <summary>
        /// 获取请求体内容
        /// </summary>
        public object? GetBody()
        {
            if (_body == null)
            {
                // 如果没有通过SetBody()设置请求体，返回当前实例（包含子类的属性）
                var mergedBody = new Dictionary<string, object>();
                var currentProperties = GetType().GetProperties();

                foreach (var property in currentProperties)
                {
                    // 排除Method属性，因为它不是请求体的一部分
                    if (property.Name != nameof(Method))
                    {
                        var value = property.GetValue(this);
                        if (value != null)
                        {
                            mergedBody[property.Name] = value;
                        }
                    }
                }
                return mergedBody;
            }
            else if (this != _body && (IsAnonymousType(_body.GetType()) || _body is IDictionary<string, object>))
            {
                // 获取当前实例的所有公共属性
                var currentProperties = GetType().GetProperties();
                bool hasProperties = false;
                
                // 检查是否有除了Method之外的公共属性
                foreach (var property in currentProperties)
                {
                    if (property.Name != nameof(Method) && property.GetValue(this) != null)
                    {
                        hasProperties = true;
                        break;
                    }
                }

                // 如果没有属性，直接返回原始请求体
                if (!hasProperties)
                {
                    return _body;
                }

                // 如果有属性，尝试合并
                // 创建一个新的字典来存储合并后的属性
                var mergedBody = new Dictionary<string, object>();

                // 添加当前实例的所有公共属性
                foreach (var property in currentProperties)
                {
                    // 排除Method属性，因为它不是请求体的一部分
                    if (property.Name != nameof(Method))
                    {
                        var value = property.GetValue(this);
                        if (value != null)
                        {
                            mergedBody[property.Name] = value;
                        }
                    }
                }

                // 如果是字典，直接添加所有键值对
                if (_body is IDictionary<string, object> dict)
                {
                    foreach (var kvp in dict)
                    {
                        mergedBody[kvp.Key] = kvp.Value;
                    }
                }
                // 如果是匿名对象，获取其所有公共属性
                else
                {
                    var bodyProperties = _body.GetType().GetProperties();
                    foreach (var property in bodyProperties)
                    {
                        var value = property.GetValue(_body);
                        if (value != null)
                        {
                            mergedBody[property.Name] = value;
                        }
                    }
                }

                return mergedBody;
            }

            // 如果通过SetBody()设置了请求体，且不是匿名对象或字典，直接返回该请求体
            return _body;
        }

        /// <summary>
        /// 检查类型是否为匿名类型
        /// </summary>
        private static bool IsAnonymousType(Type type)
        {
            return type.Namespace == null && type.IsSealed && type.IsClass && type.Name.Contains("AnonymousType");
        }

        /// <summary>
        /// 设置请求体内容
        /// </summary>
        public void SetBody(object? body)
        {
            _body = body;
        }
    }
}
