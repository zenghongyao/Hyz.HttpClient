using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Hyz.HttpClient
{
    /// <summary>
    /// 上传状态枚举
    /// </summary>
    public enum UploadStatus
    {
        /// <summary>
        /// 等待上传
        /// </summary>
        Waiting,

        /// <summary>
        /// 上传中
        /// </summary>
        Uploading,

        /// <summary>
        /// 已暂停
        /// </summary>
        Paused,

        /// <summary>
        /// 已取消
        /// </summary>
        Cancelled,

        /// <summary>
        /// 已完成
        /// </summary>
        Completed,

        /// <summary>
        /// 失败
        /// </summary>
        Failed
    }

    /// <summary>
    /// 上传上下文，用于跟踪分片上传的状态和进度
    /// </summary>
    public class UploadContext
    {
        private readonly object _lockObj = new object();
        private UploadStatus _status = UploadStatus.Waiting;
        private long _bytesUploaded = 0;
        private Exception? _exception;
        private CancellationTokenSource? _cts;

        /// <summary>
        /// 初始化上传上下文（从文件路径）
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <exception cref="ArgumentNullException">当 filePath 为 null 或空时抛出</exception>
        public UploadContext(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentNullException(nameof(filePath));

            FilePath = filePath;
            FileName = Path.GetFileName(filePath);
            FileInfo = new FileInfo(filePath);
            FileSize = FileInfo.Exists ? FileInfo.Length : 0;
            LastModified = FileInfo.Exists ? FileInfo.LastWriteTimeUtc : DateTime.MinValue;
            UploadId = Guid.NewGuid().ToString();
            UploadedParts = new HashSet<int>();
        }

        /// <summary>
        /// 初始化上传上下文（从 Stream）
        /// </summary>
        /// <param name="stream">文件流</param>
        /// <param name="fileName">文件名</param>
        /// <param name="contentLength">内容长度</param>
        /// <exception cref="ArgumentNullException">当 stream 或 fileName 为 null 时抛出</exception>
        public UploadContext(Stream stream, string fileName, long contentLength)
        {
            Stream = stream ?? throw new ArgumentNullException(nameof(stream));
            FileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
            FileSize = contentLength;
            UploadId = Guid.NewGuid().ToString();
            UploadedParts = new HashSet<int>();
        }

        /// <summary>
        /// 文件路径（如果从文件创建）
        /// </summary>
        public string? FilePath { get; }

        /// <summary>
        /// 文件流（如果从流创建）
        /// </summary>
        public Stream? Stream { get; }

        /// <summary>
        /// 文件名
        /// </summary>
        public string? FileName { get; }

        /// <summary>
        /// 文件大小（字节）
        /// </summary>
        public long FileSize { get; }

        /// <summary>
        /// 文件最后修改时间
        /// </summary>
        public DateTime LastModified { get; }

        /// <summary>
        /// 客户端生成的上传ID
        /// </summary>
        public string UploadId { get; set; }

        /// <summary>
        /// 服务器返回的上传会话ID
        /// </summary>
        public string? ServerUploadId { get; set; }

        /// <summary>
        /// 分片大小（字节），默认 1MB
        /// </summary>
        public int ChunkSize { get; set; } = 1024 * 1024;

        /// <summary>
        /// 总分片数
        /// </summary>
        public int TotalParts
        {
            get
            {
                if (FileSize <= 0) return 0;
                return (int)Math.Ceiling((double)FileSize / ChunkSize);
            }
        }

        /// <summary>
        /// 已上传的分片编号集合
        /// </summary>
        public ISet<int> UploadedParts { get; }

        /// <summary>
        /// 当前上传状态
        /// </summary>
        public UploadStatus Status
        {
            get => _status;
            set => _status = value;
        }

        /// <summary>
        /// 已上传字节数（线程安全）
        /// </summary>
        public long BytesUploaded
        {
            get => _bytesUploaded;
            set => Interlocked.Exchange(ref _bytesUploaded, value);
        }

        /// <summary>
        /// 上传过程中发生的异常
        /// </summary>
        public Exception? Exception
        {
            get => _exception;
            set => _exception = value;
        }

        /// <summary>
        /// 文件信息（如果从文件创建）
        /// </summary>
        public FileInfo? FileInfo { get; }

        /// <summary>
        /// 并发上传线程数，默认 3
        /// </summary>
        public int ConcurrentThreads { get; set; } = 3;

        /// <summary>
        /// 标记指定分片已上传
        /// </summary>
        /// <param name="partNumber">分片编号（从1开始）</param>
        public void MarkPartUploaded(int partNumber)
        {
            lock (_lockObj)
            {
                UploadedParts.Add(partNumber);
            }
        }

        /// <summary>
        /// 检查指定分片是否已上传
        /// </summary>
        /// <param name="partNumber">分片编号（从1开始）</param>
        /// <returns>如果已上传返回 true，否则返回 false</returns>
        public bool IsPartUploaded(int partNumber)
        {
            lock (_lockObj)
            {
                return UploadedParts.Contains(partNumber);
            }
        }

        /// <summary>
        /// 获取已上传的分片数量
        /// </summary>
        /// <returns>已上传的分片数量</returns>
        public int GetUploadedPartCount()
        {
            lock (_lockObj)
            {
                return UploadedParts.Count;
            }
        }

        /// <summary>
        /// 获取上传进度百分比
        /// </summary>
        /// <returns>进度百分比（0-100）</returns>
        public double GetProgressPercentage()
        {
            if (FileSize <= 0) return 0;
            return (double)BytesUploaded / FileSize * 100;
        }

        /// <summary>
        /// 检查上传是否已完成
        /// </summary>
        /// <returns>如果已完成返回 true，否则返回 false</returns>
        public bool IsComplete()
        {
            return Status == UploadStatus.Completed ||
                   (FileSize > 0 && BytesUploaded >= FileSize);
        }

        /// <summary>
        /// 暂停上传
        /// </summary>
        public void Pause()
        {
            if (Status == UploadStatus.Uploading)
            {
                Status = UploadStatus.Paused;
                _cts?.Cancel();
            }
        }

        /// <summary>
        /// 取消上传
        /// </summary>
        public void Cancel()
        {
            Status = UploadStatus.Cancelled;
            _cts?.Cancel();
        }

        /// <summary>
        /// 获取取消令牌，用于取消上传操作
        /// </summary>
        /// <returns>取消令牌</returns>
        public CancellationToken GetCancellationToken()
        {
            if (_cts == null || _cts.IsCancellationRequested)
            {
                _cts = new CancellationTokenSource();
            }
            return _cts.Token;
        }

        /// <summary>
        /// 重置上传上下文状态，可用于重新开始上传
        /// </summary>
        public void Reset()
        {
            Status = UploadStatus.Waiting;
            BytesUploaded = 0;
            Exception = null;
            UploadedParts.Clear();
            _cts?.Dispose();
            _cts = null;
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _cts?.Dispose();
            Stream?.Dispose();
        }
    }
}