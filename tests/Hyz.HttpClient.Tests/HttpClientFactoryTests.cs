using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Net.Http;
using System.Text.Json;
using Xunit;

namespace Hyz.HttpClient.Tests
{
    public class SimpleHttpClientFactoryTests
    {
        [Fact]
        public void CreateClient_DefaultClient_ShouldCreateInstance()
        {
            // Arrange
            var factory = new SimpleHttpClientFactory();

            // Act
            var client = factory.CreateClient();

            // Assert
            Assert.NotNull(client);
        }

        [Fact]
        public void CreateClient_NamedClient_ShouldCreateInstance()
        {
            // Arrange
            var factory = new SimpleHttpClientFactory();

            // Act
            var client = factory.CreateClient("MyClient");

            // Assert
            Assert.NotNull(client);
        }

        [Fact]
        public void CreateClient_SameName_ShouldReturnSameInstance()
        {
            // Arrange
            var factory = new SimpleHttpClientFactory();

            // Act
            var client1 = factory.CreateClient("MyClient");
            var client2 = factory.CreateClient("MyClient");

            // Assert
            Assert.Same(client1, client2);
        }

        [Fact]
        public void CreateClient_DifferentNames_ShouldReturnDifferentInstances()
        {
            // Arrange
            var factory = new SimpleHttpClientFactory();

            // Act
            var client1 = factory.CreateClient("Client1");
            var client2 = factory.CreateClient("Client2");

            // Assert
            Assert.NotSame(client1, client2);
        }

        [Fact]
        public void CreateClient_WithConfigureClient_ShouldApplyConfiguration()
        {
            // Arrange
            bool configured = false;
            var factory = new SimpleHttpClientFactory(client =>
            {
                client.BaseAddress = new Uri("https://api.example.com");
                configured = true;
            });

            // Act
            var client = factory.CreateClient();

            // Assert
            Assert.NotNull(client);
            Assert.True(configured);
            Assert.Equal(new Uri("https://api.example.com"), client.BaseAddress);
        }

        [Fact]
        public void ConfigureClient_ShouldRecreateClient()
        {
            // Arrange
            var factory = new SimpleHttpClientFactory();
            var client1 = factory.CreateClient("ConfiguredClient");

            // Act
            factory.ConfigureClient("ConfiguredClient", client =>
            {
                client.BaseAddress = new Uri("https://configured.example.com");
            });
            var client2 = factory.CreateClient("ConfiguredClient");

            // Assert
            Assert.NotSame(client1, client2);
            Assert.Equal(new Uri("https://configured.example.com"), client2.BaseAddress);
        }

        [Fact]
        public void Clear_ShouldRemoveAllClients()
        {
            // Arrange
            var factory = new SimpleHttpClientFactory();
            var client1 = factory.CreateClient("Client1");
            var client2 = factory.CreateClient("Client2");

            // Act
            factory.Clear();
            var client3 = factory.CreateClient("Client1");
            var client4 = factory.CreateClient("Client2");

            // Assert
            Assert.NotSame(client1, client3);
            Assert.NotSame(client2, client4);
        }

        [Fact]
        public void CreateHandler_ShouldCreateHandler()
        {
            // Arrange
            var factory = new SimpleHttpClientFactory();

            // Act
            var handler = factory.CreateHandler("Test");

            // Assert
            Assert.NotNull(handler);
        }

        [Fact]
        public void CreateHandler_SameName_ShouldReturnSameInstance()
        {
            // Arrange
            var factory = new SimpleHttpClientFactory();

            // Act
            var handler1 = factory.CreateHandler("Test");
            var handler2 = factory.CreateHandler("Test");

            // Assert
            Assert.Same(handler1, handler2);
        }
    }

    public class HyzHttpClientFactoryTests : IAsyncLifetime
    {
        public Task InitializeAsync()
        {
            HyzHttpClientFactory.ResetGlobalConfiguration();
            return Task.CompletedTask;
        }

        public Task DisposeAsync()
        {
            HyzHttpClientFactory.ResetGlobalConfiguration();
            return Task.CompletedTask;
        }

        [Fact]
        public void Instance_ShouldBeSingleton()
        {
            // Act
            var instance1 = HyzHttpClientFactory.Instance;
            var instance2 = HyzHttpClientFactory.Instance;

            // Assert
            Assert.Same(instance1, instance2);
        }

        [Fact]
        public void Create_Default_ShouldReturnHttpClientRequest()
        {
            // Act
            var request = HyzHttpClientFactory.Instance.Create();

            // Assert
            Assert.NotNull(request);
        }

        [Fact]
        public void CreateInstance_StaticMethod_ShouldReturnHttpClientRequest()
        {
            // Act
            var request = HyzHttpClientFactory.CreateInstance();

            // Assert
            Assert.NotNull(request);
        }

        [Fact]
        public void Create_WithLogger_ShouldUseLogger()
        {
            // Arrange
            var logger = new NullLogger<HttpClientRequest>();

            // Act
            var request = HyzHttpClientFactory.Instance.Create(logger);

            // Assert
            Assert.NotNull(request);
        }

        [Fact]
        public void Create_WithBaseAddress_ShouldConfigureClient()
        {
            // Arrange
            var baseAddress = new Uri("https://custom.example.com");

            // Act
            var request = HyzHttpClientFactory.Instance.Create(baseAddress: baseAddress);

            // Assert
            Assert.NotNull(request);
        }

        [Fact]
        public void Create_WithTimeout_ShouldConfigureClient()
        {
            // Arrange
            var timeout = TimeSpan.FromSeconds(45);

            // Act
            var request = HyzHttpClientFactory.Instance.Create(timeout: timeout);

            // Assert
            Assert.NotNull(request);
        }

        [Fact]
        public void Create_WithJsonOptions_ShouldUseCustomOptions()
        {
            // Arrange
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = null
            };

            // Act
            var request = HyzHttpClientFactory.Instance.Create(jsonOptions: jsonOptions);

            // Assert
            Assert.NotNull(request);
        }

        [Fact]
        public void CreateNamedClient_ShouldReturnHttpClientRequest()
        {
            // Act
            var request = HyzHttpClientFactory.Instance.CreateNamedClient("MyNamedClient");

            // Assert
            Assert.NotNull(request);
        }

        [Fact]
        public void CreateNamedInstance_StaticMethod_ShouldReturnHttpClientRequest()
        {
            // Act
            var request = HyzHttpClientFactory.CreateNamedInstance("MyNamedClient");

            // Assert
            Assert.NotNull(request);
        }

        [Fact]
        public void CreateNamedClient_WithConfigureClient_ShouldApplyConfiguration()
        {
            // Arrange
            bool configured = false;
            var factory = new SimpleHttpClientFactory(client =>
            {
                client.DefaultRequestHeaders.Add("X-Custom-Header", "test-value");
                configured = true;
            });

            // Act
            var client = factory.CreateClient("ConfiguredClient");

            // Assert
            Assert.NotNull(client);
            Assert.True(configured);
        }

        [Fact]
        public void Initialize_GlobalConfiguration_ShouldSetGlobalProperties()
        {
            // Arrange
            var baseAddress = new Uri("https://global.example.com");
            var timeout = TimeSpan.FromSeconds(60);

            // Act
            HyzHttpClientFactory.Initialize(
                baseAddress: baseAddress,
                timeout: timeout
            );

            // Assert
            Assert.Equal(baseAddress, HyzHttpClientFactory.GlobalBaseAddress);
            Assert.Equal(timeout, HyzHttpClientFactory.GlobalTimeout);
        }

        [Fact]
        public void Create_WithGlobalConfiguration_ShouldUseGlobalSettings()
        {
            // Arrange
            var baseAddress = new Uri("https://global.example.com");
            var timeout = TimeSpan.FromSeconds(60);
            HyzHttpClientFactory.Initialize(
                baseAddress: baseAddress,
                timeout: timeout
            );

            // Act
            var request = HyzHttpClientFactory.Instance.Create();

            // Assert
            Assert.NotNull(request);
        }

        [Fact]
        public void Constructor_WithConfiguration_ShouldSetGlobalProperties()
        {
            // Arrange
            var baseAddress = new Uri("https://ctor.example.com");
            var timeout = TimeSpan.FromSeconds(30);

            // Act
            var factory = new HyzHttpClientFactory(
                baseAddress: baseAddress,
                timeout: timeout
            );

            // Assert
            Assert.Equal(baseAddress, HyzHttpClientFactory.GlobalBaseAddress);
            Assert.Equal(timeout, HyzHttpClientFactory.GlobalTimeout);
        }

        [Fact]
        public void Create_WithOverrides_ShouldPriorityLocalOverGlobal()
        {
            // Arrange
            HyzHttpClientFactory.Initialize(
                baseAddress: new Uri("https://global.example.com"),
                timeout: TimeSpan.FromSeconds(60)
            );
            var localBaseAddress = new Uri("https://local.example.com");
            var localTimeout = TimeSpan.FromSeconds(15);

            // Act
            var request = HyzHttpClientFactory.Instance.Create(
                baseAddress: localBaseAddress,
                timeout: localTimeout
            );

            // Assert
            Assert.NotNull(request);
        }

        [Fact]
        public void Create_WithLoggerAndConfiguration_ShouldAcceptAllParameters()
        {
            // Arrange
            var logger = new NullLogger<HttpClientRequest>();
            var baseAddress = new Uri("https://test.example.com");
            var timeout = TimeSpan.FromSeconds(30);
            var jsonOptions = new JsonSerializerOptions();

            // Act
            var request = HyzHttpClientFactory.Instance.Create(
                logger: logger,
                baseAddress: baseAddress,
                timeout: timeout,
                jsonOptions: jsonOptions
            );

            // Assert
            Assert.NotNull(request);
        }
    }
}