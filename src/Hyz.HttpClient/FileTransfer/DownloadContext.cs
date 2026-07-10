using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Hyz.HttpClient
{
    /// <summary>
    /// 下载上下文类，用于跟踪断点续传状态
    /// </summary>
    public class DownloadContext
    {
        /// <summary>
        /// 下载URL
        /// </summary>
        public string Url { get; }

        /// <summary>
        /// 本地文件保存路径
        /// </summary>
        public string LocalFilePath { get; }

        /// <summary>
        /// 已下载字节数
        /// </summary>
        public long BytesDownloaded { get; internal set; }

        /// <summary>
        /// 总字节数（未知时为 0）
        /// </summary>
        public long TotalBytes { get; internal set; }

        /// <summary>
        /// ETag（用于检测文件变化）
        /// </summary>
        public string? ETag { get; internal set; }

        /// <summary>
        /// 最后修改时间（用于检测文件变化）
        /// </summary>
        public DateTime? LastModified { get; internal set; }

        /// <summary>
        /// 是否支持断点续传
        /// </summary>
        public bool SupportsResume { get; internal set; }

        /// <summary>
        /// 当前下载状态
        /// </summary>
        public DownloadStatus Status { get; internal set; } = DownloadStatus.Pending;

        /// <summary>
        /// 下载进度
        /// </summary>
        public DownloadProgress Progress { get; } = new DownloadProgress();

        /// <summary>
        /// 下载过程中发生的异常
        /// </summary>
        public Exception? Exception { get; internal set; }

        /// <summary>
        /// 取消令牌源
        /// </summary>
        private CancellationTokenSource? _cts;

        /// <summary>
        /// 初始化 DownloadContext
        /// </summary>
        /// <param name="url">下载URL</param>
        /// <param name="localFilePath">本地文件保存路径</param>
        public DownloadContext(string url, string localFilePath)
        {
            Url = url ?? throw new ArgumentNullException(nameof(url));
            LocalFilePath = localFilePath ?? throw new ArgumentNullException(nameof(localFilePath));
        }

        /// <summary>
        /// 暂停下载
        /// </summary>
        public void Pause()
        {
            if (Status == DownloadStatus.Downloading)
            {
                _cts?.Cancel();
                Status = DownloadStatus.Paused;
                Progress.Status = DownloadStatus.Paused;
            }
        }

        /// <summary>
        /// 取消下载
        /// </summary>
        public void Cancel()
        {
            _cts?.Cancel();
            Status = DownloadStatus.Cancelled;
            Progress.Status = DownloadStatus.Cancelled;
        }

        /// <summary>
        /// 获取取消令牌
        /// </summary>
        /// <returns>取消令牌</returns>
        internal CancellationToken GetCancellationToken()
        {
            _cts = new CancellationTokenSource();
            return _cts.Token;
        }

        /// <summary>
        /// 检查本地文件是否存在且有效
        /// </summary>
        /// <returns>是否存在有效的部分下载文件</returns>
        public bool HasPartialDownload()
        {
            if (!File.Exists(LocalFilePath))
                return false;

            var fileInfo = new FileInfo(LocalFilePath);
            return fileInfo.Length > 0;
        }

        /// <summary>
        /// 获取本地文件大小
        /// </summary>
        /// <returns>本地文件大小（字节）</returns>
        public long GetLocalFileSize()
        {
            if (!File.Exists(LocalFilePath))
                return 0;

            return new FileInfo(LocalFilePath).Length;
        }

        /// <summary>
        /// 清理部分下载的文件
        /// </summary>
        public void CleanupPartialFile()
        {
            if (File.Exists(LocalFilePath))
            {
                File.Delete(LocalFilePath);
            }
            BytesDownloaded = 0;
            Progress.BytesReceived = 0;
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _cts?.Dispose();
        }
    }
}