using System;
using System.IO;

namespace Hyz.HttpClient
{
    /// <summary>
    /// 文件参数类，用于封装文件上传的相关信息
    /// </summary>
    public class FileParameter
    {
        /// <summary>
        /// 文件流
        /// </summary>
        public Stream Stream { get; }

        /// <summary>
        /// 文件名（包含扩展名）
        /// </summary>
        public string FileName { get; }

        /// <summary>
        /// 内容类型（MIME类型）
        /// </summary>
        public string ContentType { get; }

        /// <summary>
        /// 表单字段名称（默认 "file"）
        /// </summary>
        public string FieldName { get; }

        /// <summary>
        /// 文件大小（字节）
        /// </summary>
        public long ContentLength { get; }

        /// <summary>
        /// 初始化 FileParameter（使用 Stream）
        /// </summary>
        /// <param name="stream">文件流</param>
        /// <param name="fileName">文件名</param>
        /// <param name="contentType">内容类型</param>
        /// <param name="fieldName">表单字段名称</param>
        public FileParameter(Stream stream, string fileName, string? contentType = null, string fieldName = "file")
        {
            Stream = stream ?? throw new ArgumentNullException(nameof(stream));
            FileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
            ContentType = contentType ?? GetContentType(fileName);
            FieldName = fieldName;
            ContentLength = stream.CanSeek ? stream.Length : 0;
        }

        /// <summary>
        /// 初始化 FileParameter（使用 byte[]）
        /// </summary>
        /// <param name="bytes">文件字节数组</param>
        /// <param name="fileName">文件名</param>
        /// <param name="contentType">内容类型</param>
        /// <param name="fieldName">表单字段名称</param>
        public FileParameter(byte[] bytes, string fileName, string? contentType = null, string fieldName = "file")
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            Stream = new MemoryStream(bytes);
            FileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
            ContentType = contentType ?? GetContentType(fileName);
            FieldName = fieldName;
            ContentLength = bytes.Length;
        }

        /// <summary>
        /// 根据文件名获取内容类型
        /// </summary>
        /// <param name="fileName">文件名</param>
        /// <returns>内容类型</returns>
        private string GetContentType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".webp" => "image/webp",
                ".svg" => "image/svg+xml",
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".ppt" => "application/vnd.ms-powerpoint",
                ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                ".zip" => "application/zip",
                ".rar" => "application/x-rar-compressed",
                ".7z" => "application/x-7z-compressed",
                ".tar" => "application/x-tar",
                ".gz" => "application/gzip",
                ".txt" => "text/plain",
                ".json" => "application/json",
                ".xml" => "application/xml",
                ".html" => "text/html",
                ".css" => "text/css",
                ".js" => "application/javascript",
                ".mp4" => "video/mp4",
                ".avi" => "video/x-msvideo",
                ".mov" => "video/quicktime",
                ".mp3" => "audio/mpeg",
                ".wav" => "audio/wav",
                ".flac" => "audio/flac",
                _ => "application/octet-stream"
            };
        }
    }

    /// <summary>
    /// 上传进度信息
    /// </summary>
    public class UploadProgress
    {
        /// <summary>
        /// 已上传字节数
        /// </summary>
        public long BytesSent { get; set; }

        /// <summary>
        /// 总字节数
        /// </summary>
        public long TotalBytes { get; set; }

        /// <summary>
        /// 上传进度百分比（0-100）
        /// </summary>
        public double Percentage => TotalBytes > 0 ? (BytesSent * 100.0 / TotalBytes) : 0;

        /// <summary>
        /// 上传速度（字节/秒）
        /// </summary>
        public long BytesPerSecond { get; set; }

        /// <summary>
        /// 剩余时间（秒）
        /// </summary>
        public double RemainingSeconds => BytesPerSecond > 0 ? (TotalBytes - BytesSent) / (double)BytesPerSecond : 0;

        /// <summary>
        /// 上传状态
        /// </summary>
        public UploadStatus Status { get; set; }
    }

    /// <summary>
    /// 下载进度信息
    /// </summary>
    public class DownloadProgress
    {
        /// <summary>
        /// 已下载字节数
        /// </summary>
        public long BytesReceived { get; set; }

        /// <summary>
        /// 总字节数（未知时为 0）
        /// </summary>
        public long TotalBytes { get; set; }

        /// <summary>
        /// 下载进度百分比（0-100，未知总大小时为 -1）
        /// </summary>
        public double Percentage => TotalBytes > 0 ? (BytesReceived * 100.0 / TotalBytes) : -1;

        /// <summary>
        /// 下载速度（字节/秒）
        /// </summary>
        public long BytesPerSecond { get; set; }

        /// <summary>
        /// 剩余时间（秒，未知总大小时为 -1）
        /// </summary>
        public double RemainingSeconds => TotalBytes > 0 && BytesPerSecond > 0 ? (TotalBytes - BytesReceived) / (double)BytesPerSecond : -1;

        /// <summary>
        /// 是否支持断点续传
        /// </summary>
        public bool SupportsResume { get; set; }

        /// <summary>
        /// 当前下载状态
        /// </summary>
        public DownloadStatus Status { get; set; } = DownloadStatus.Downloading;
    }

    /// <summary>
    /// 下载状态枚举
    /// </summary>
    public enum DownloadStatus
    {
        /// <summary>
        /// 等待中
        /// </summary>
        Pending,

        /// <summary>
        /// 下载中
        /// </summary>
        Downloading,

        /// <summary>
        /// 暂停中
        /// </summary>
        Paused,

        /// <summary>
        /// 已完成
        /// </summary>
        Completed,

        /// <summary>
        /// 失败
        /// </summary>
        Failed,

        /// <summary>
        /// 已取消
        /// </summary>
        Cancelled
    }
}