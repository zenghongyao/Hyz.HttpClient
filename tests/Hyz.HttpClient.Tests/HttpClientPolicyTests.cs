using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Hyz.HttpClient;
using Polly.CircuitBreaker;
using Xunit;

namespace Hyz.HttpClient.Tests
{
    /// <summary>
    /// HttpClientPolicy测试
    /// </summary>
    public class HttpClientPolicyTests : IDisposable
    {
        public HttpClientPolicyTests()
        {
            // 每个测试前重置状态
            HttpClientPolicy.ClearAllPipelines();
            HttpClientPolicy.ConfigureRetry(new HttpClientPolicy.RetryOptions());
            HttpClientPolicy.ConfigureCircuitBreaker(new HttpClientPolicy.CircuitBreakerOptions());
        }

        public void Dispose()
        {
            HttpClientPolicy.ClearAllPipelines();
        }

        [Fact]
        public void GetPipeline_ShouldReturnPipeline()
        {
            // Act
            var pipeline = HttpClientPolicy.GetPipeline("default", "GET", "/api/users");

            // Assert
            Assert.NotNull(pipeline);
        }

        [Fact]
        public void GetPipeline_SameKey_ShouldReturnSamePipeline()
        {
            // Act
            var pipeline1 = HttpClientPolicy.GetPipeline("default", "GET", "/api/users");
            var pipeline2 = HttpClientPolicy.GetPipeline("default", "GET", "/api/users");

            // Assert
            Assert.Same(pipeline1, pipeline2);
        }

        [Fact]
        public void GetPipeline_DifferentMethod_ShouldReturnDifferentPipeline()
        {
            // Act
            var pipeline1 = HttpClientPolicy.GetPipeline("default", "GET", "/api/users");
            var pipeline2 = HttpClientPolicy.GetPipeline("default", "POST", "/api/users");

            // Assert
            Assert.NotSame(pipeline1, pipeline2);
        }

        [Fact]
        public void GetPipeline_DifferentPath_ShouldReturnDifferentPipeline()
        {
            // Act
            var pipeline1 = HttpClientPolicy.GetPipeline("default", "GET", "/api/users");
            var pipeline2 = HttpClientPolicy.GetPipeline("default", "GET", "/api/orders");

            // Assert
            Assert.NotSame(pipeline1, pipeline2);
        }

        [Fact]
        public void GetPipeline_DifferentClientName_ShouldReturnDifferentPipeline()
        {
            // Act
            var pipeline1 = HttpClientPolicy.GetPipeline("default", "GET", "/api/users");
            var pipeline2 = HttpClientPolicy.GetPipeline("payment", "GET", "/api/users");

            // Assert
            Assert.NotSame(pipeline1, pipeline2);
        }

        [Fact]
        public void GetPipeline_QueryParams_ShouldShareSamePipeline()
        {
            // Act - 同一个路径不同查询参数应共享断路器
            var pipeline1 = HttpClientPolicy.GetPipeline("default", "GET", "/api/users?page=1");
            var pipeline2 = HttpClientPolicy.GetPipeline("default", "GET", "/api/users?page=2");

            // Assert
            Assert.Same(pipeline1, pipeline2);
        }

        [Fact]
        public void GetPipeline_FullUrl_ShouldExtractBasePath()
        {
            // Act - 完整 URL 应该提取基础路径
            var pipeline1 = HttpClientPolicy.GetPipeline("default", "GET", "https://api.example.com/api/users?page=1");
            var pipeline2 = HttpClientPolicy.GetPipeline("default", "GET", "/api/users");

            // Assert
            Assert.Same(pipeline1, pipeline2);
        }

        [Fact]
        public void ConfigureRetry_ShouldUpdateAndClearPipelines()
        {
            // Arrange
            var oldPipeline = HttpClientPolicy.GetPipeline("default", "GET", "/api/users");

            // Act
            HttpClientPolicy.ConfigureRetry(new HttpClientPolicy.RetryOptions
            {
                MaxRetryAttempts = 2
            });

            var newPipeline = HttpClientPolicy.GetPipeline("default", "GET", "/api/users");

            // Assert
            Assert.NotNull(newPipeline);
            // 配置变更后旧管道被清除，新请求会创建新管道
            Assert.NotSame(oldPipeline, newPipeline);
        }

        [Fact]
        public void ConfigureCircuitBreaker_ShouldUpdateAndClearPipelines()
        {
            // Arrange
            var oldPipeline = HttpClientPolicy.GetPipeline("default", "GET", "/api/users");

            // Act
            HttpClientPolicy.ConfigureCircuitBreaker(new HttpClientPolicy.CircuitBreakerOptions
            {
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(10),
                MinimumThroughput = 5,
                BreakDuration = TimeSpan.FromSeconds(30)
            });

            var newPipeline = HttpClientPolicy.GetPipeline("default", "GET", "/api/users");

            // Assert
            Assert.NotNull(newPipeline);
            Assert.NotSame(oldPipeline, newPipeline);
        }

        [Fact]
        public void RetryOptions_ShouldHaveDefaultValues()
        {
            // Act
            var options = new HttpClientPolicy.RetryOptions();

            // Assert
            Assert.Equal(3, options.MaxRetryAttempts);
            Assert.Equal(Polly.DelayBackoffType.Exponential, options.BackoffType);
            Assert.Equal(TimeSpan.FromMilliseconds(200), options.InitialDelay);
            Assert.Null(options.OnRetry);
        }

        [Fact]
        public void CircuitBreakerOptions_ShouldHaveDefaultValues()
        {
            // Act
            var options = new HttpClientPolicy.CircuitBreakerOptions();

            // Assert
            Assert.Equal(0.5, options.FailureRatio);
            Assert.Equal(TimeSpan.FromSeconds(2), options.SamplingDuration);
            Assert.Equal(4, options.MinimumThroughput);
            Assert.Equal(TimeSpan.FromSeconds(3), options.BreakDuration);
            Assert.Null(options.OnOpened);
            Assert.Null(options.OnClosed);
            Assert.Null(options.OnHalfOpened);
        }

        [Fact]
        public void CanConfigureMultipleTimes()
        {
            // Arrange
            HttpClientPolicy.ConfigureRetry(new HttpClientPolicy.RetryOptions
            {
                MaxRetryAttempts = 5
            });

            // Act
            HttpClientPolicy.ConfigureRetry(new HttpClientPolicy.RetryOptions
            {
                MaxRetryAttempts = 7
            });

            var pipeline = HttpClientPolicy.GetPipeline("default", "GET", "/api/test");

            // Assert
            Assert.NotNull(pipeline);
        }

        [Fact]
        public void CleanupUnusedPipelines_ShouldRemoveUnusedEntries()
        {
            // Arrange
            HttpClientPolicy.GetPipeline("default", "GET", "/api/users");
            HttpClientPolicy.GetPipeline("default", "GET", "/api/orders");
            Assert.Equal(2, HttpClientPolicy.PipelineCount);

            // Act - 清理立即过期的管道
            var removed = HttpClientPolicy.CleanupUnusedPipelines(TimeSpan.Zero);

            // Assert
            Assert.Equal(2, removed);
            Assert.Equal(0, HttpClientPolicy.PipelineCount);
        }

        [Fact]
        public void BuildPipelineKey_ShouldGenerateCorrectFormat()
        {
            // Act
            var key = HttpClientPolicy.BuildPipelineKey("default", "GET", "/api/users");

            // Assert
            Assert.Equal("default:GET:/api/users", key);
        }

        [Fact]
        public void ExtractBasePath_ShouldStripQueryParams()
        {
            // Act
            var path1 = HttpClientPolicy.ExtractBasePath("/api/users?page=1&size=10");
            var path2 = HttpClientPolicy.ExtractBasePath("/api/users");
            var path3 = HttpClientPolicy.ExtractBasePath("https://example.com/api/users?id=1");

            // Assert
            Assert.Equal("/api/users", path1);
            Assert.Equal("/api/users", path2);
            Assert.Equal("/api/users", path3);
        }

        [Fact]
        public void ExtractBasePath_EmptyInput_ShouldReturnEmpty()
        {
            // Act
            var path = HttpClientPolicy.ExtractBasePath("");

            // Assert
            Assert.Equal("", path);
        }

        [Fact]
        public void StartPipelineCleanup_ShouldBeIdempotent()
        {
            // Act - 多次调用不应抛异常
            HttpClientPolicy.StartPipelineCleanup();
            HttpClientPolicy.StartPipelineCleanup();
            HttpClientPolicy.StartPipelineCleanup();

            // Assert - 不抛异常即为通过
            Assert.True(true);
        }

        [Fact]
        public void StopPipelineCleanup_ShouldBeIdempotent()
        {
            // Act - 多次调用不应抛异常
            HttpClientPolicy.StopPipelineCleanup();
            HttpClientPolicy.StopPipelineCleanup();

            // Assert - 不抛异常即为通过
            Assert.True(true);
        }

        [Fact]
        public void PipelineCleanup_AutoRenewal_ShouldKeepActivePipeline()
        {
            var originalMaxIdle = HttpClientPolicy.PipelineCleanupMaxIdleTime;
            try
            {
                // 设置很短的清理窗口
                HttpClientPolicy.PipelineCleanupMaxIdleTime = TimeSpan.FromMilliseconds(100);

                // 创建管道并记录数量
                var beforeCount = HttpClientPolicy.PipelineCount;
                HttpClientPolicy.GetPipeline("default", "GET", "/api/users");
                Assert.Equal(beforeCount + 1, HttpClientPolicy.PipelineCount);

                // 等待 30ms（小于清理窗口 100ms）
                Thread.Sleep(30);

                // 再次访问管道（自动续期 LastAccessTime）
                HttpClientPolicy.GetPipeline("default", "GET", "/api/users");

                // 清理 80ms 未使用的管道 - 我们的管道刚被续期，不应被清理
                var removed = HttpClientPolicy.CleanupUnusedPipelines(TimeSpan.FromMilliseconds(80));
                Assert.Equal(0, removed); // 没有管道被清理，因为刚被续期
            }
            finally
            {
                HttpClientPolicy.PipelineCleanupMaxIdleTime = originalMaxIdle;
                HttpClientPolicy.CleanupUnusedPipelines(TimeSpan.Zero);
            }
        }

        [Fact]
        public void PipelineCleanup_ShouldRemoveIdlePipeline()
        {
            var originalMaxIdle = HttpClientPolicy.PipelineCleanupMaxIdleTime;
            try
            {
                // 设置很短的清理窗口
                HttpClientPolicy.PipelineCleanupMaxIdleTime = TimeSpan.FromMilliseconds(50);

                // 创建管道
                var beforeCount = HttpClientPolicy.PipelineCount;
                HttpClientPolicy.GetPipeline("default", "GET", "/api/users");
                Assert.Equal(beforeCount + 1, HttpClientPolicy.PipelineCount);

                // 等待 100ms（超过清理窗口 50ms），期间不访问管道
                Thread.Sleep(100);

                // 清理 50ms 未使用的管道 - 我们的管道应该被清理
                var removed = HttpClientPolicy.CleanupUnusedPipelines(TimeSpan.FromMilliseconds(50));
                Assert.True(removed >= 1); // 至少清理了 1 个管道（我们创建的）
            }
            finally
            {
                HttpClientPolicy.PipelineCleanupMaxIdleTime = originalMaxIdle;
                HttpClientPolicy.CleanupUnusedPipelines(TimeSpan.Zero);
            }
        }
    }
}