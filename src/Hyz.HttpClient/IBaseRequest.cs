using System.Collections.Generic;

namespace Hyz.HttpClient
{
    /// <summary>
    /// 基础HTTP请求接口
    /// </summary>
    /// <typeparam name="T">响应类型</typeparam>
    public interface IBaseRequest<T> where T : class
    {
        /// <summary>
        /// 获取请求API路径
        /// </summary>
        /// <returns>API路径</returns>
        string GetRequestApi();

        /// <summary>
        /// 设置请求API路径
        /// </summary>
        /// <param name="path">API路径</param>
        void SetRequestApi(string? path);

        /// <summary>
        /// HTTP方法（GET/POST/PUT/DELETE/PATCH）
        /// </summary>
        string Method { get; set; }

        /// <summary>
        /// 获取请求头
        /// </summary>
        /// <returns>请求头字典</returns>
        IDictionary<string, string>? GetHeaders();

        /// <summary>
        /// 添加单个请求头
        /// </summary>
        /// <param name="key">请求头键</param>
        /// <param name="value">请求头值</param>
        void AddHeader(string key, string value);

        /// <summary>
        /// 设置请求头
        /// </summary>
        /// <param name="headers">请求头字典</param>
        void SetHeaders(IDictionary<string, string>? headers);

        /// <summary>
        /// 获取查询参数URL
        /// </summary>
        string? GetQueryParametersUrl();

        /// <summary>
        /// 获取查询参数
        /// </summary>
        /// <returns>查询参数字典</returns>
        IDictionary<string, string>? GetQueryParameters();

        /// <summary>
        /// 添加单个查询参数
        /// </summary>
        /// <param name="key">查询参数键</param>
        /// <param name="value">查询参数值</param>
        void AddQueryParameter(string key, string value);

        /// <summary>
        /// 设置查询参数
        /// </summary>
        /// <param name="parameters">查询参数字典</param>
        void SetQueryParameters(IDictionary<string, string>? parameters);

        /// <summary>
        /// 获取请求体内容
        /// </summary>
        /// <returns>请求体对象</returns>
        object? GetBody();

        /// <summary>
        /// 设置请求体内容
        /// </summary>
        /// <param name="body">请求体对象</param>
        void SetBody(object? body);
    }
}
