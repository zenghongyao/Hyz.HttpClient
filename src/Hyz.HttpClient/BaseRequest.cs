using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Reflection;

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
        /// HTTP方法（不使用通用方法可以省略）
        /// </summary>
        public string Method { get; set; } = "POST";

        /// <summary>
        /// 获取请求API地址
        /// </summary>
        public string GetRequestApi()
        {
            return _requestApi;
        }

        /// <summary>
        /// 判断类型是否为简单类型
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
        /// 设置请求API地址
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

            // 如果有查询参数，将其附加到URL中
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
        /// 检查属性是否有自定义请求参数别名特性
        /// </summary>
        /// <param name="property">属性信息</param>
        /// <returns>是否有自定义特性</returns>
        private bool HasRequestParameterAliasAttribute(System.Reflection.PropertyInfo property)
        {
            return property.GetCustomAttributes(typeof(RequestParameterAliasAttribute), true).Any();
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
            var accessors = CreatePropertyAccessors(GetType());
            
            for (int i = 0; i < properties.Length; i++)
            {
                var property = properties[i];
                var (propertyName, getter) = accessors[i];
                
                // 排除名称为Method的属性，除非它有自定义特性（允许通过别名使用）
                if (property.Name != nameof(Method) || HasRequestParameterAliasAttribute(property))
                {
                    var value = getter(this);
                    if (value != null)
                    {
                        // 只添加简单类型的属性作为查询参数
                        if (IsSimpleType(value.GetType()))
                        {
                            // 只有当该属性还没有在查询参数中时才添加
                            if (!allQueryParameters.ContainsKey(propertyName))
                            {
                                allQueryParameters[propertyName] = value.ToString()!;
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
        /// 属性访问器缓存
        /// </summary>
        private static readonly Dictionary<Type, List<(string Name, Func<object, object> Getter)>> _propertyAccessors = new Dictionary<Type, List<(string Name, Func<object, object> Getter)>>();

        /// <summary>
        /// 获取属性的自定义别名
        /// </summary>
        /// <param name="property">属性信息</param>
        /// <returns>自定义别名，如果没有则返回属性名</returns>
        private string GetPropertyAlias(System.Reflection.PropertyInfo property)
        {
            var attribute = property.GetCustomAttributes(typeof(RequestParameterAliasAttribute), true)        
                .FirstOrDefault() as RequestParameterAliasAttribute;
            return attribute?.Alias ?? property.Name;
        }

        /// <summary>
        /// 为类型创建属性访问器
        /// </summary>
        /// <param name="type">类型</param>
        /// <returns>属性名称和访问器的列表</returns>
        private List<(string Name, Func<object, object> Getter)> CreatePropertyAccessors(Type type)
        {
            if (_propertyAccessors.TryGetValue(type, out var accessors))
            {
                return accessors;
            }

            var accessorList = new List<(string Name, Func<object, object> Getter)>();
            var properties = type.GetProperties();

            foreach (var property in properties)
            {
                var propertyAlias = GetPropertyAlias(property);
                var getter = CreatePropertyGetter(property);
                accessorList.Add((propertyAlias, getter));
            }

            _propertyAccessors[type] = accessorList;
            return accessorList;
        }

        /// <summary>
        /// 为属性创建访问器委托
        /// </summary>
        /// <param name="property">属性信息</param>
        /// <returns>属性访问器委托</returns>
        private Func<object, object> CreatePropertyGetter(PropertyInfo property)
        {
            var instance = Expression.Parameter(typeof(object), "instance");
            var cast = Expression.Convert(instance, property.DeclaringType!);
            var propertyAccess = Expression.Property(cast, property);
            var convertResult = Expression.Convert(propertyAccess, typeof(object));
            var lambda = Expression.Lambda<Func<object, object>>(convertResult, instance);
            return lambda.Compile();
        }

        /// <summary>
        /// 处理对象，生成后序列化和层级验证器结构体存储分区统计信息
        /// </summary>
        /// <param name="obj">要序列化对象转换为字典统计信息</param>
        /// <returns>结构体存储分区统计信息</returns>
        private object ProcessObject(object obj)
        {
            if (obj == null)
            {
                return null;
            }

            var objType = obj.GetType();

            // 如果是SimpleType，不需要序列化处理
            if (IsSimpleType(objType))
            {
                return obj;
            }

            // 如果是Dictionary，生成原始位置结构体存储分区统计信息
            if (obj is IDictionary<string, object> dict)
            {
                var result = new Dictionary<string, object>();
                foreach (var kvp in dict)
                {
                    result[kvp.Key] = ProcessObject(kvp.Value);
                }
                return result;
            }

            // 如果是Array或类型，结构体存储分区统计信息
            if (objType.IsArray)
            {
                var array = (Array)obj;
                var result = new object[array.Length];
                for (int i = 0; i < array.Length; i++)
                {
                    result[i] = ProcessObject(array.GetValue(i));
                }
                return result;
            }

            // 如果是List或类型，结构体存储分区统计信息
            if (typeof(IEnumerable<object>).IsAssignableFrom(objType))
            {
                var enumerable = (IEnumerable<object>)obj;
                return enumerable.Select(item => ProcessObject(item)).ToArray();
            }

            // 如果是class或类型，结构体存储分区统计信息
            var resultDict = new Dictionary<string, object?>();
            var properties = objType.GetProperties();
            var accessors = CreatePropertyAccessors(objType);
            
            for (int i = 0; i < properties.Length; i++)
            {
                var property = properties[i];
                var (propertyName, getter) = accessors[i];
                
                // 只排除名称为Method的属性，除非它有自定义特性（允许通过别名使用）
                if (property.Name != nameof(Method) || HasRequestParameterAliasAttribute(property))
                {
                    var value = getter(obj);
                    if (value != null)
                    {
                        resultDict[propertyName] = ProcessObject(value);
                    }
                }
            }

            return resultDict;
        }

        /// <summary>
        /// 获取请求体内容
        /// </summary>
        public object? GetBody()
        {
            if (_body == null)
            {
                // 如果没有通过SetBody()设置请求体，返回当前实例（包含子类的属性）
                var processedBody = ProcessObject(this);
                var bodyDict = processedBody as Dictionary<string, object>;
                // 不再自动移除Method键，因为ProcessObject已经处理了排除逻辑
                return bodyDict?.Count > 0 ? bodyDict : null;
            }
            else if (this != _body && (IsAnonymousType(_body.GetType()) || _body is IDictionary<string, object>))
            {
                // 检查是否有公共属性（包括通过别名设置为Method的属性）
                bool hasProperties = false;
                var accessors = CreatePropertyAccessors(GetType());
                var properties = GetType().GetProperties();
                
                for (int i = 0; i < properties.Length; i++)
                {
                    var property = properties[i];
                    var (propertyName, getter) = accessors[i];
                    
                    // 检查所有属性，包括通过别名设置为Method的属性
                    if (property.Name != nameof(Method) || HasRequestParameterAliasAttribute(property))
                    {
                        if (getter(this) != null)
                        {
                            hasProperties = true;
                            break;
                        }
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
                var currentBody = ProcessObject(this) as Dictionary<string, object>;
                if (currentBody != null)
                {
                    // 不再自动移除Method键，因为ProcessObject已经处理了排除逻辑
                    foreach (var kvp in currentBody)
                    {
                        mergedBody[kvp.Key] = kvp.Value;
                    }
                }

                // 如果是字典，直接添加所有键值对
                if (_body is IDictionary<string, object> dict)
                {
                    foreach (var kvp in dict)
                    {
                        mergedBody[kvp.Key] = ProcessObject(kvp.Value);
                    }
                }
                // 如果是匿名对象，获取所有公共属性
                else
                {
                    var bodyProcessed = ProcessObject(_body);
                    if (bodyProcessed is Dictionary<string, object> bodyDict)
                    {
                        foreach (var kvp in bodyDict)
                        {
                            mergedBody[kvp.Key] = kvp.Value;
                        }
                    }
                }

                return mergedBody;
            }

            // 如果通过SetBody()设置了请求体，并且不是匿名对象或字典，直接返回该请求体
            return ProcessObject(_body);
        }

        /// <summary>
        /// 判断类型是否为匿名类型
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