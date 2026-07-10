using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Hyz.HttpClient
{
    public class ProgressableStreamContent : HttpContent
    {
        private readonly Stream _content;
        private readonly int _bufferSize;
        private readonly Action<UploadProgress>? _progressCallback;
        private readonly long _totalBytes;
        private long _bytesSent;
        private DateTime _lastReportTime = DateTime.UtcNow;
        private long _lastBytesSent;
        private readonly int _minReportIntervalMs;

        public ProgressableStreamContent(Stream content, Action<UploadProgress>? progressCallback = null, int bufferSize = 8192, int minReportIntervalMs = 100)
        {
            _content = content ?? throw new ArgumentNullException(nameof(content));
            _progressCallback = progressCallback;
            _bufferSize = bufferSize > 0 ? bufferSize : 8192;
            _minReportIntervalMs = minReportIntervalMs > 0 ? minReportIntervalMs : 100;

            if (_content.CanSeek)
            {
                _totalBytes = _content.Length;
            }
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context)
        {
            var buffer = new byte[_bufferSize];
            int bytesRead;

            while ((bytesRead = await _content.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await stream.WriteAsync(buffer, 0, bytesRead);
                Interlocked.Add(ref _bytesSent, bytesRead);

                ReportProgress();
            }
        }

        private void ReportProgress()
        {
            if (_progressCallback == null)
                return;

            var now = DateTime.UtcNow;
            var elapsedMs = (now - _lastReportTime).TotalMilliseconds;

            if (elapsedMs < _minReportIntervalMs)
                return;

            var bytesSent = Interlocked.Read(ref _bytesSent);
            var bytesDelta = bytesSent - _lastBytesSent;
            var speed = (long)(bytesDelta / (elapsedMs / 1000.0));

            _progressCallback?.Invoke(new UploadProgress
            {
                BytesSent = bytesSent,
                TotalBytes = _totalBytes,
                BytesPerSecond = speed
            });

            _lastReportTime = now;
            _lastBytesSent = bytesSent;
        }

        protected override bool TryComputeLength(out long length)
        {
            length = _totalBytes;
            return _totalBytes > 0;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _content.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}