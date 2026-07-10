using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Hyz.HttpClient
{
    /// <summary>
    /// 分片上传提供者接口，定义分片上传的核心操作
    /// </summary>
    public interface IResumableUploadProvider
    {
        /// <summary>
        /// 创建上传会话
        /// </summary>
        /// <param name="fileName">文件名</param>
        /// <param name="fileSize">文件大小（字节）</param>
        /// <param name="contentType">内容类型（可选）</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>服务器返回的上传会话ID</returns>
        Task<string> CreateUploadSessionAsync(string fileName, long fileSize, string? contentType = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 上传单个分片
        /// </summary>
        /// <param name="uploadId">上传会话ID</param>
        /// <param name="partNumber">分片编号（从1开始）</param>
        /// <param name="data">分片数据</param>
        /// <param name="offset">分片在文件中的偏移量</param>
        /// <param name="totalSize">文件总大小</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>上传结果，包含分片编号、ETag 和是否成功</returns>
        Task<UploadPartResult> UploadPartAsync(string uploadId, int partNumber, byte[] data, long offset, long totalSize, CancellationToken cancellationToken = default);

        /// <summary>
        /// 列出已上传的分片
        /// </summary>
        /// <param name="uploadId">上传会话ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>已上传的分片信息列表</returns>
        Task<IList<UploadPartInfo>> ListUploadedPartsAsync(string uploadId, CancellationToken cancellationToken = default);

        /// <summary>
        /// 完成上传（合并分片）
        /// </summary>
        /// <typeparam name="T">响应类型</typeparam>
        /// <param name="uploadId">上传会话ID</param>
        /// <param name="parts">所有已上传的分片信息</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>服务器返回的响应结果</returns>
        Task<T?> CompleteUploadAsync<T>(string uploadId, IList<UploadPartInfo> parts, CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// 取消上传，清理服务器上的临时分片
        /// </summary>
        /// <param name="uploadId">上传会话ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>任务</returns>
        Task AbortUploadAsync(string uploadId, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// 分片上传结果
    /// </summary>
    public class UploadPartResult
    {
        /// <summary>
        /// 分片编号
        /// </summary>
        public int PartNumber { get; set; }

        /// <summary>
        /// 服务器返回的 ETag，用于验证分片完整性
        /// </summary>
        public string? ETag { get; set; }

        /// <summary>
        /// 是否上传成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 错误消息（如果上传失败）
        /// </summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// 分片信息
    /// </summary>
    public class UploadPartInfo
    {
        /// <summary>
        /// 分片编号
        /// </summary>
        public int PartNumber { get; set; }

        /// <summary>
        /// 服务器返回的 ETag
        /// </summary>
        public string? ETag { get; set; }

        /// <summary>
        /// 分片大小（字节）
        /// </summary>
        public long Size { get; set; }
    }
}