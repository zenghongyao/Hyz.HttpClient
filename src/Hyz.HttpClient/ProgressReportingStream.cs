using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Hyz.HttpClient
{
    /// <summary>
    /// 支持下载进度报告的 Stream 包装器
    /// </summary>
    public class ProgressReportingStream : Stream
    {
        /// <summary>
        /// 内部流
        /// </summary>
        private readonly Stream _innerStream;

        /// <summary>
        /// 总字节数
        /// </summary>
        private readonly long _totalBytes;

        /// <summary>
        /// 进度回调
        /// </summary>
        private readonly Action<DownloadProgress> _progressCallback;

        /// <summary>
        /// 已读取字节数
        /// </summary>
        private long _bytesRead;

        /// <summary>
        /// 上次报告时间
        /// </summary>
        private DateTime _lastReportTime = DateTime.UtcNow;

        /// <summary>
        /// 上次报告时已读取字节数
        /// </summary>
        private long _lastBytesRead;

        /// <summary>
        /// 最小报告间隔（毫秒，默认 100ms）
        /// </summary>
        private readonly int _minReportIntervalMs;

        /// <summary>
        /// 是否已标记为完成
        /// </summary>
        private bool _completed;

        /// <summary>
        /// 初始化 ProgressReportingStream
        /// </summary>
        /// <param name="innerStream">内部流</param>
        /// <param name="totalBytes">总字节数</param>
        /// <param name="progressCallback">进度回调</param>
        /// <param name="minReportIntervalMs">最小报告间隔（毫秒，默认 100ms）</param>
        public ProgressReportingStream(Stream innerStream, long totalBytes, Action<DownloadProgress> progressCallback, int minReportIntervalMs = 100)
        {
            _innerStream = innerStream ?? throw new ArgumentNullException(nameof(innerStream));
            _totalBytes = totalBytes;
            _progressCallback = progressCallback;
            _minReportIntervalMs = minReportIntervalMs > 0 ? minReportIntervalMs : 100;
        }

        /// <summary>
        /// 获取一个值，该值指示当前流是否支持读取
        /// </summary>
        public override bool CanRead => _innerStream.CanRead;

        /// <summary>
        /// 获取一个值，该值指示当前流是否支持查找
        /// </summary>
        public override bool CanSeek => _innerStream.CanSeek;

        /// <summary>
        /// 获取一个值，该值指示当前流是否支持写入
        /// </summary>
        public override bool CanWrite => _innerStream.CanWrite;

        /// <summary>
        /// 获取流长度（字节）
        /// </summary>
        public override long Length => _innerStream.Length;

        /// <summary>
        /// 获取或设置流中的当前位置
        /// </summary>
        public override long Position
        {
            get => _innerStream.Position;
            set => _innerStream.Position = value;
        }

        /// <summary>
        /// 清除所有缓冲区并使所有缓冲数据写入基础设备
        /// </summary>
        public override void Flush()
        {
            _innerStream.Flush();
        }

        /// <summary>
        /// 清除所有缓冲区并使所有缓冲数据异步写入基础设备
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>任务</returns>
        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return _innerStream.FlushAsync(cancellationToken);
        }

        /// <summary>
        /// 从流中读取字节块并将数据写入指定的缓冲区
        /// </summary>
        /// <param name="buffer">字节数组</param>
        /// <param name="offset">开始写入的位置</param>
        /// <param name="count">要读取的最大字节数</param>
        /// <returns>读取的字节数</returns>
        public override int Read(byte[] buffer, int offset, int count)
        {
            int bytesRead = _innerStream.Read(buffer, offset, count);
            if (bytesRead > 0)
            {
                Interlocked.Add(ref _bytesRead, bytesRead);
                ReportProgress();
            }
            else if (!_completed)
            {
                _completed = true;
                ReportFinalProgress();
            }
            return bytesRead;
        }

        /// <summary>
        /// 从流中异步读取字节块并将数据写入指定的缓冲区
        /// </summary>
        /// <param name="buffer">字节数组</param>
        /// <param name="offset">开始写入的位置</param>
        /// <param name="count">要读取的最大字节数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>读取的字节数</returns>
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            int bytesRead = await _innerStream.ReadAsync(buffer, offset, count, cancellationToken);
            if (bytesRead > 0)
            {
                Interlocked.Add(ref _bytesRead, bytesRead);
                ReportProgress();
            }
            else if (!_completed)
            {
                _completed = true;
                ReportFinalProgress();
            }
            return bytesRead;
        }

        /// <summary>
        /// 设置流中的当前位置
        /// </summary>
        /// <param name="offset">相对于 origin 参数的位置</param>
        /// <param name="origin">参考点</param>
        /// <returns>新位置</returns>
        public override long Seek(long offset, SeekOrigin origin)
        {
            return _innerStream.Seek(offset, origin);
        }

        /// <summary>
        /// 设置流长度
        /// </summary>
        /// <param name="value">新长度</param>
        public override void SetLength(long value)
        {
            _innerStream.SetLength(value);
        }

        /// <summary>
        /// 将字节块写入当前流
        /// </summary>
        /// <param name="buffer">字节数组</param>
        /// <param name="offset">开始读取的位置</param>
        /// <param name="count">要写入的字节数</param>
        public override void Write(byte[] buffer, int offset, int count)
        {
            _innerStream.Write(buffer, offset, count);
        }

        /// <summary>
        /// 将字节块异步写入当前流
        /// </summary>
        /// <param name="buffer">字节数组</param>
        /// <param name="offset">开始读取的位置</param>
        /// <param name="count">要写入的字节数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>任务</returns>
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return _innerStream.WriteAsync(buffer, offset, count, cancellationToken);
        }

        /// <summary>
        /// 报告进度
        /// </summary>
        private void ReportProgress()
        {
            var now = DateTime.UtcNow;
            var elapsedMs = (now - _lastReportTime).TotalMilliseconds;

            if (elapsedMs < _minReportIntervalMs)
                return;

            var bytesRead = Interlocked.Read(ref _bytesRead);
            var bytesDelta = bytesRead - _lastBytesRead;
            var speed = (long)(bytesDelta / (elapsedMs / 1000.0));

            _progressCallback?.Invoke(new DownloadProgress
            {
                BytesReceived = bytesRead,
                TotalBytes = _totalBytes,
                BytesPerSecond = speed,
                Status = DownloadStatus.Downloading
            });

            _lastReportTime = now;
            _lastBytesRead = bytesRead;
        }

        /// <summary>
        /// 报告最终进度
        /// </summary>
        private void ReportFinalProgress()
        {
            var bytesRead = Interlocked.Read(ref _bytesRead);
            _progressCallback?.Invoke(new DownloadProgress
            {
                BytesReceived = bytesRead,
                TotalBytes = _totalBytes,
                Status = DownloadStatus.Completed
            });
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        /// <param name="disposing">是否正在释放托管资源</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _innerStream.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}