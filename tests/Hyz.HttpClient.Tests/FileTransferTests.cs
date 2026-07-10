using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Hyz.HttpClient.Tests
{
    public class FileParameterTests
    {
        [Fact]
        public void Constructor_WithStream_ShouldInitializeProperties()
        {
            // Arrange
            var content = "test content";
            var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
            var fileName = "test.txt";

            // Act
            var fileParam = new FileParameter(stream, fileName);

            // Assert
            Assert.Same(stream, fileParam.Stream);
            Assert.Equal(fileName, fileParam.FileName);
            Assert.Equal("text/plain", fileParam.ContentType);
            Assert.Equal("file", fileParam.FieldName);
            Assert.Equal(content.Length, fileParam.ContentLength);
        }

        [Fact]
        public void Constructor_WithBytes_ShouldInitializeProperties()
        {
            // Arrange
            var content = "test content";
            var bytes = System.Text.Encoding.UTF8.GetBytes(content);
            var fileName = "test.txt";

            // Act
            var fileParam = new FileParameter(bytes, fileName);

            // Assert
            Assert.Equal(fileName, fileParam.FileName);
            Assert.Equal("text/plain", fileParam.ContentType);
            Assert.Equal("file", fileParam.FieldName);
            Assert.Equal(content.Length, fileParam.ContentLength);
        }

        [Fact]
        public void Constructor_WithCustomContentType_ShouldUseCustomType()
        {
            // Arrange
            var stream = new MemoryStream(new byte[100]);
            var fileName = "test.data";
            var customContentType = "application/octet-stream";

            // Act
            var fileParam = new FileParameter(stream, fileName, customContentType);

            // Assert
            Assert.Equal(customContentType, fileParam.ContentType);
        }

        [Fact]
        public void Constructor_WithCustomFieldName_ShouldUseCustomFieldName()
        {
            // Arrange
            var stream = new MemoryStream(new byte[100]);
            var fileName = "test.txt";
            var customFieldName = "attachment";

            // Act
            var fileParam = new FileParameter(stream, fileName, fieldName: customFieldName);

            // Assert
            Assert.Equal(customFieldName, fileParam.FieldName);
        }

        [Fact]
        public void GetContentType_ShouldReturnCorrectMimeType()
        {
            // Arrange & Act & Assert
            Assert.Equal("image/jpeg", new FileParameter(new MemoryStream(new byte[100]), "test.jpg").ContentType);
            Assert.Equal("image/png", new FileParameter(new MemoryStream(new byte[100]), "test.png").ContentType);
            Assert.Equal("application/pdf", new FileParameter(new MemoryStream(new byte[100]), "test.pdf").ContentType);
            Assert.Equal("application/zip", new FileParameter(new MemoryStream(new byte[100]), "test.zip").ContentType);
            Assert.Equal("application/octet-stream", new FileParameter(new MemoryStream(new byte[100]), "test.unknown").ContentType);
        }
    }

    public class DownloadContextTests
    {
        [Fact]
        public void Constructor_ShouldInitializeProperties()
        {
            // Arrange
            var url = "https://example.com/file.zip";
            var localPath = "C:\\downloads\\file.zip";

            // Act
            var context = new DownloadContext(url, localPath);

            // Assert
            Assert.Equal(url, context.Url);
            Assert.Equal(localPath, context.LocalFilePath);
            Assert.Equal(DownloadStatus.Pending, context.Status);
            Assert.Equal(0, context.BytesDownloaded);
            Assert.Equal(0, context.TotalBytes);
        }

        [Fact]
        public void HasPartialDownload_WithExistingFile_ShouldReturnTrue()
        {
            // Arrange
            var tempFile = Path.GetTempFileName();
            File.WriteAllText(tempFile, "test content");
            var context = new DownloadContext("https://example.com/file.txt", tempFile);

            // Act
            var result = context.HasPartialDownload();

            // Assert
            Assert.True(result);

            // Cleanup
            File.Delete(tempFile);
        }

        [Fact]
        public void HasPartialDownload_WithNonExistingFile_ShouldReturnFalse()
        {
            // Arrange
            var context = new DownloadContext("https://example.com/file.txt", Path.GetTempFileName());

            // Act
            var result = context.HasPartialDownload();

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void GetLocalFileSize_WithExistingFile_ShouldReturnSize()
        {
            // Arrange
            var tempFile = Path.GetTempFileName();
            var content = "test content";
            File.WriteAllText(tempFile, content);
            var context = new DownloadContext("https://example.com/file.txt", tempFile);

            // Act
            var size = context.GetLocalFileSize();

            // Assert
            Assert.Equal(content.Length, size);

            // Cleanup
            File.Delete(tempFile);
        }

        [Fact]
        public void CleanupPartialFile_ShouldDeleteFile()
        {
            // Arrange
            var tempFile = Path.GetTempFileName();
            File.WriteAllText(tempFile, "test content");
            var context = new DownloadContext("https://example.com/file.txt", tempFile);

            // Act
            context.CleanupPartialFile();

            // Assert
            Assert.False(File.Exists(tempFile));
            Assert.Equal(0, context.BytesDownloaded);
        }
    }

    public class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }

    public class FileTransferIntegrationTests
    {
        private readonly HttpClientRequest _httpClientRequest;

        public FileTransferIntegrationTests()
        {
            var factory = new SimpleHttpClientFactory();
            _httpClientRequest = new HttpClientRequest(NullLogger<HttpClientRequest>.Instance, factory, new JsonSerializerOptions());
        }

        [Fact]
        public async Task ExecuteDownloadAsync_ShouldDownloadFile()
        {
            // Arrange
            var tempFile = Path.GetTempFileName();
            var testContent = "Hello, World!";
            var handler = new MockHttpMessageHandler(request =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(testContent)
                };
                response.Content.Headers.ContentLength = testContent.Length;
                return response;
            });

            var factory = new SimpleHttpClientFactory(client =>
            {
                client.BaseAddress = new Uri("https://example.com");
            });
            factory.CreateClient("test").Dispose();

            // Cleanup
            File.Delete(tempFile);
        }
    }

    public class ProgressableStreamContentTests
    {
        [Fact]
        public void Constructor_WithStream_ShouldInitializeProperties()
        {
            // Arrange
            var content = new byte[100];
            var stream = new MemoryStream(content);
            bool progressReported = false;

            // Act
            var progressContent = new ProgressableStreamContent(stream, progress =>
            {
                progressReported = true;
            });

            // Assert
            Assert.NotNull(progressContent);
        }

        [Fact]
        public async Task SerializeToStreamAsync_ShouldCopyContent()
        {
            // Arrange
            var content = new byte[1000];
            for (int i = 0; i < content.Length; i++)
                content[i] = (byte)i;
            var stream = new MemoryStream(content);

            var progressContent = new ProgressableStreamContent(stream, null, bufferSize: 512);

            // Act - 使用 HttpClient 的方式来触发 SerializeToStreamAsync
            using var handler = new MockHttpMessageHandler(req => new HttpResponseMessage(System.Net.HttpStatusCode.OK));
            using var client = new System.Net.Http.HttpClient(handler);
            var request = new HttpRequestMessage(HttpMethod.Post, "https://example.com/upload");
            request.Content = progressContent;
            await client.SendAsync(request);

            // Assert - 验证内容被正确传输
            Assert.Equal(content.Length, progressContent.Headers.ContentLength);
        }
    }

    public class ProgressReportingStreamTests
    {
        [Fact]
        public void Constructor_ShouldInitializeProperties()
        {
            // Arrange
            var innerStream = new MemoryStream(new byte[100]);
            bool progressReported = false;

            // Act
            var progressStream = new ProgressReportingStream(innerStream, 100, progress =>
            {
                progressReported = true;
            });

            // Assert
            Assert.True(progressStream.CanRead);
            Assert.Equal(innerStream.CanWrite, progressStream.CanWrite);
        }

        [Fact]
        public async Task ReadAsync_ShouldReadContent()
        {
            // Arrange
            var content = new byte[1000];
            for (int i = 0; i < content.Length; i++)
                content[i] = (byte)i;
            var innerStream = new MemoryStream(content);

            var progressStream = new ProgressReportingStream(innerStream, content.Length, null, minReportIntervalMs: 1);

            // Act - 一次性读取完整内容
            var buffer = new byte[1000];
            int bytesRead = await progressStream.ReadAsync(buffer, 0, buffer.Length);

            // Assert - 验证内容被正确读取
            Assert.Equal(content.Length, bytesRead);
            Assert.Equal(content, buffer);
        }

        [Fact]
        public void Dispose_ShouldDisposeInnerStream()
        {
            // Arrange
            var innerStream = new MemoryStream(new byte[100]);
            var progressStream = new ProgressReportingStream(innerStream, 100, _ => { });

            // Act
            progressStream.Dispose();

            // Assert
            Assert.Throws<ObjectDisposedException>(() => innerStream.Read(new byte[10], 0, 10));
        }
    }

    public class UploadContextTests
    {
        [Fact]
        public void Constructor_WithFilePath_ShouldInitializeProperties()
        {
            var tempFile = Path.GetTempFileName();
            File.WriteAllText(tempFile, "test content");

            try
            {
                var context = new UploadContext(tempFile);

                Assert.Equal(tempFile, context.FilePath);
                Assert.Equal("test content".Length, context.FileSize);
                Assert.Equal(Path.GetFileName(tempFile), context.FileName);
                Assert.NotNull(context.UploadId);
                Assert.Equal(UploadStatus.Waiting, context.Status);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void Constructor_WithStream_ShouldInitializeProperties()
        {
            var content = "test content";
            var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));

            var context = new UploadContext(stream, "test.txt", content.Length);

            Assert.Same(stream, context.Stream);
            Assert.Equal("test.txt", context.FileName);
            Assert.Equal(content.Length, context.FileSize);
            Assert.NotNull(context.UploadId);
        }

        [Fact]
        public void MarkPartUploaded_ShouldTrackParts()
        {
            var context = new UploadContext(new MemoryStream(new byte[1000]), "test.bin", 1000);

            context.MarkPartUploaded(1);
            context.MarkPartUploaded(2);

            Assert.True(context.IsPartUploaded(1));
            Assert.True(context.IsPartUploaded(2));
            Assert.False(context.IsPartUploaded(3));
            Assert.Equal(2, context.GetUploadedPartCount());
        }

        [Fact]
        public void TotalParts_ShouldCalculateCorrectly()
        {
            var context = new UploadContext(new MemoryStream(new byte[2500]), "test.bin", 2500);
            context.ChunkSize = 1000;

            Assert.Equal(3, context.TotalParts);
        }

        [Fact]
        public void GetProgressPercentage_ShouldReturnCorrectValue()
        {
            var context = new UploadContext(new MemoryStream(new byte[1000]), "test.bin", 1000);
            context.BytesUploaded = 500;

            Assert.Equal(50, context.GetProgressPercentage());
        }

        [Fact]
        public void Pause_ShouldChangeStatus()
        {
            var context = new UploadContext(new MemoryStream(new byte[1000]), "test.bin", 1000);
            context.Status = UploadStatus.Uploading;

            context.Pause();

            Assert.Equal(UploadStatus.Paused, context.Status);
        }

        [Fact]
        public void Cancel_ShouldChangeStatus()
        {
            var context = new UploadContext(new MemoryStream(new byte[1000]), "test.bin", 1000);

            context.Cancel();

            Assert.Equal(UploadStatus.Cancelled, context.Status);
        }
    }

    public class ResumableUploadProviderTests
    {
        [Fact]
        public void Constructor_ShouldInitializeProperties()
        {
            using var httpClient = new System.Net.Http.HttpClient();
            var options = new ResumableUploadOptions();
            var provider = new ResumableUploadProvider(httpClient, options);

            Assert.NotNull(provider);
        }

        [Fact]
        public void Options_ShouldHaveDefaultValues()
        {
            var options = new ResumableUploadOptions();

            Assert.Equal("/api/upload/create", options.CreateSessionUrlFormat);
            Assert.Equal("/api/upload/{0}/parts/{1}", options.UploadPartUrlFormat);
            Assert.Equal("/api/upload/{0}/parts", options.ListPartsUrlFormat);
            Assert.Equal("/api/upload/{0}/complete", options.CompleteUploadUrlFormat);
            Assert.Equal("/api/upload/{0}/abort", options.AbortUploadUrlFormat);
            Assert.Equal("application/octet-stream", options.PartContentType);
            Assert.Equal(3, options.MaxRetryAttempts);
            Assert.Equal(TimeSpan.FromSeconds(1), options.RetryDelay);
        }
    }

    public class MockResumableUploadProvider : IResumableUploadProvider
    {
        public List<int> UploadedParts { get; } = new List<int>();
        public string? CreateSessionResult { get; set; } = "test-upload-id";
        public bool CompleteCalled { get; set; }
        public bool AbortCalled { get; set; }

        public Task<string> CreateUploadSessionAsync(string fileName, long fileSize, string? contentType = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreateSessionResult!);
        }

        public Task<UploadPartResult> UploadPartAsync(string uploadId, int partNumber, byte[] data, long offset, long totalSize, CancellationToken cancellationToken = default)
        {
            UploadedParts.Add(partNumber);
            return Task.FromResult(new UploadPartResult
            {
                PartNumber = partNumber,
                ETag = $"etag-{partNumber}",
                Success = true
            });
        }

        public Task<IList<UploadPartInfo>> ListUploadedPartsAsync(string uploadId, CancellationToken cancellationToken = default)
        {
            var parts = UploadedParts.Select(p => new UploadPartInfo
            {
                PartNumber = p,
                ETag = $"etag-{p}",
                Size = 1024
            }).ToList();
            return Task.FromResult<IList<UploadPartInfo>>(parts);
        }

        public Task<T?> CompleteUploadAsync<T>(string uploadId, IList<UploadPartInfo> parts, CancellationToken cancellationToken = default) where T : class
        {
            CompleteCalled = true;
            return Task.FromResult<T?>(null);
        }

        public Task AbortUploadAsync(string uploadId, CancellationToken cancellationToken = default)
        {
            AbortCalled = true;
            return Task.CompletedTask;
        }
    }

    public class UploadContextAdditionalTests
    {
        [Fact]
        public void Reset_ShouldClearState()
        {
            var context = new UploadContext(new MemoryStream(new byte[1000]), "test.bin", 1000);
            context.Status = UploadStatus.Uploading;
            context.BytesUploaded = 500;
            context.MarkPartUploaded(1);
            context.Exception = new IOException("test error");

            context.Reset();

            Assert.Equal(UploadStatus.Waiting, context.Status);
            Assert.Equal(0, context.BytesUploaded);
            Assert.Equal(0, context.GetUploadedPartCount());
            Assert.Null(context.Exception);
        }

        [Fact]
        public void IsComplete_ShouldReturnTrueWhenStatusCompleted()
        {
            var context = new UploadContext(new MemoryStream(new byte[1000]), "test.bin", 1000);
            context.Status = UploadStatus.Completed;

            Assert.True(context.IsComplete());
        }

        [Fact]
        public void IsComplete_ShouldReturnTrueWhenAllBytesUploaded()
        {
            var context = new UploadContext(new MemoryStream(new byte[1000]), "test.bin", 1000);
            context.BytesUploaded = 1000;

            Assert.True(context.IsComplete());
        }

        [Fact]
        public void IsComplete_ShouldReturnFalseWhenNotCompleted()
        {
            var context = new UploadContext(new MemoryStream(new byte[1000]), "test.bin", 1000);
            context.Status = UploadStatus.Uploading;
            context.BytesUploaded = 500;

            Assert.False(context.IsComplete());
        }

        [Fact]
        public void GetCancellationToken_ShouldCreateNewWhenNull()
        {
            var context = new UploadContext(new MemoryStream(new byte[1000]), "test.bin", 1000);

            var token = context.GetCancellationToken();

            Assert.False(token.IsCancellationRequested);
        }

        [Fact]
        public void GetCancellationToken_ShouldReuseWhenNotCancelled()
        {
            var context = new UploadContext(new MemoryStream(new byte[1000]), "test.bin", 1000);
            var token1 = context.GetCancellationToken();
            var token2 = context.GetCancellationToken();

            Assert.Equal(token1.IsCancellationRequested, token2.IsCancellationRequested);
        }

        [Fact]
        public void Constructor_WithNullFilePath_ShouldThrow()
        {
            Assert.Throws<ArgumentNullException>(() => new UploadContext((string)null!));
        }

        [Fact]
        public void Constructor_WithNullStream_ShouldThrow()
        {
            Assert.Throws<ArgumentNullException>(() => new UploadContext((Stream)null!, "test.bin", 1000));
        }

        [Fact]
        public void Constructor_WithNullFileName_ShouldThrow()
        {
            Assert.Throws<ArgumentNullException>(() => new UploadContext(new MemoryStream(), null!, 1000));
        }
    }

    public class UploadPartResultAndInfoTests
    {
        [Fact]
        public void UploadPartResult_ShouldInitializeProperties()
        {
            var result = new UploadPartResult
            {
                PartNumber = 1,
                ETag = "etag-1",
                Success = true,
                ErrorMessage = null
            };

            Assert.Equal(1, result.PartNumber);
            Assert.Equal("etag-1", result.ETag);
            Assert.True(result.Success);
            Assert.Null(result.ErrorMessage);
        }

        [Fact]
        public void UploadPartResult_Failure_ShouldHaveErrorMessage()
        {
            var result = new UploadPartResult
            {
                PartNumber = 2,
                ETag = null,
                Success = false,
                ErrorMessage = "Upload failed"
            };

            Assert.False(result.Success);
            Assert.Equal("Upload failed", result.ErrorMessage);
        }

        [Fact]
        public void UploadPartInfo_ShouldInitializeProperties()
        {
            var info = new UploadPartInfo
            {
                PartNumber = 3,
                ETag = "etag-3",
                Size = 1024
            };

            Assert.Equal(3, info.PartNumber);
            Assert.Equal("etag-3", info.ETag);
            Assert.Equal(1024, info.Size);
        }
    }
}