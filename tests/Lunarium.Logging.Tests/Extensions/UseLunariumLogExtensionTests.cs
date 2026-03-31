// Copyright 2026 Cyanflower
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Lunarium.Logging.Extensions;
using Lunarium.Logging.Config.Configurator;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Lunarium.Logging.Tests.Extensions;

/// <summary>
/// Tests for:
///   Section 13 — LunariumLoggerExtensions.UseLunariumLog(ILoggingBuilder, ...)
///   Section 14 — LunariumHostBuilderExtensions.UseLunariumLog(IHostBuilder, ...)
/// </summary>
public class UseLunariumLogExtensionTests
{
    // ────────────────────────────────────────────────────────────────────────────
    // Helper: minimal ILoggingBuilder backed by a real IServiceCollection mock
    // ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Minimal ILoggingBuilder that captures the Add() calls so we can retrieve
    /// the registered ILogger instance for proper disposal.
    /// </summary>
    private sealed class FakeLoggingBuilder : ILoggingBuilder
    {
        private readonly IServiceCollection _services;
        public IServiceCollection Services => _services;

        public FakeLoggingBuilder(IServiceCollection services)
        {
            _services = services;
        }
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Section 13 — LunariumLoggerExtensions.UseLunariumLog(ILoggingBuilder)
    // ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void UseLunariumLog_ILoggingBuilder_NullConfigureSinks_ThrowsArgumentNullException()
    {
        var servicesMock = Substitute.For<IServiceCollection>();
        var builder = new FakeLoggingBuilder(servicesMock);

        Action act = () => builder.UseLunariumLog(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("configureSinks");
    }

    [Fact]
    public async Task UseLunariumLog_ILoggingBuilder_ReturnsSameBuilder_AndInvokesConfigureSinks()
    {
        var servicesMock = Substitute.For<IServiceCollection>();
        var builder = new FakeLoggingBuilder(servicesMock);
        bool sinksCalled = false;

        Lunarium.Logging.ILogger? capturedLogger = null;
        servicesMock.When(s => s.Add(Arg.Any<ServiceDescriptor>()))
            .Do(call =>
            {
                var descriptor = call.Arg<ServiceDescriptor>();
                if (descriptor.ServiceType == typeof(Lunarium.Logging.ILogger))
                    capturedLogger = descriptor.ImplementationInstance as Lunarium.Logging.ILogger;
            });

        var result = builder.UseLunariumLog(lb =>
        {
            sinksCalled = true;
            lb.AddConsoleSink();
        });

        result.Should().BeSameAs(builder);
        sinksCalled.Should().BeTrue();

        if (capturedLogger != null)
            await capturedLogger.DisposeAsync();
    }

    [Fact]
    public async Task UseLunariumLog_ILoggingBuilder_RegistersLoggerAsSingleton()
    {
        var servicesMock = Substitute.For<IServiceCollection>();
        var builder = new FakeLoggingBuilder(servicesMock);

        ServiceDescriptor? capturedDescriptor = null;
        servicesMock.When(s => s.Add(Arg.Any<ServiceDescriptor>()))
            .Do(call =>
            {
                var d = call.Arg<ServiceDescriptor>();
                if (d.ServiceType == typeof(Lunarium.Logging.ILogger))
                    capturedDescriptor = d;
            });

        builder.UseLunariumLog(lb => lb.AddConsoleSink());

        capturedDescriptor.Should().NotBeNull();
        capturedDescriptor!.Lifetime.Should().Be(ServiceLifetime.Singleton);
        capturedDescriptor.ImplementationInstance.Should().BeAssignableTo<Lunarium.Logging.ILogger>();

        if (capturedDescriptor.ImplementationInstance is Lunarium.Logging.ILogger logger)
            await logger.DisposeAsync();
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Section 14 — LunariumHostBuilderExtensions.UseLunariumLog(IHostBuilder)
    // ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void UseLunariumLog_IHostBuilder_NullConfigureSinks_ThrowsArgumentNullException()
    {
        var hostBuilderMock = Substitute.For<IHostBuilder>();

        Action act = () => hostBuilderMock.UseLunariumLog(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("configureSinks");
    }

    [Fact]
    public void UseLunariumLog_IHostBuilder_CallsConfigureServices_ReturnsSameBuilder()
    {
        var hostBuilderMock = Substitute.For<IHostBuilder>();
        hostBuilderMock
            .ConfigureServices(Arg.Any<Action<HostBuilderContext, IServiceCollection>>())
            .Returns(hostBuilderMock);

        var result = hostBuilderMock.UseLunariumLog(lb => lb.AddConsoleSink());

        result.Should().BeSameAs(hostBuilderMock);
        hostBuilderMock.Received(1)
            .ConfigureServices(Arg.Any<Action<HostBuilderContext, IServiceCollection>>());
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Section 15 — configureGlobal parameter paths
    // ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UseLunariumLog_WithConfigureGlobal_AppliesGlobalConfig()
    {
        // Reset singleton guards so GlobalConfigurator.Configure() is allowed
        GlobalConfigLock.Configured = false;
        typeof(GlobalConfigurator)
            .GetField("_isConfiguring",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(null, false);

        var servicesMock = Substitute.For<IServiceCollection>();
        var builder = new FakeLoggingBuilder(servicesMock);

        Lunarium.Logging.ILogger? capturedLogger = null;
        servicesMock.When(s => s.Add(Arg.Any<ServiceDescriptor>()))
            .Do(call =>
            {
                var descriptor = call.Arg<ServiceDescriptor>();
                if (descriptor.ServiceType == typeof(Lunarium.Logging.ILogger))
                    capturedLogger = descriptor.ImplementationInstance as Lunarium.Logging.ILogger;
            });

        var result = builder.UseLunariumLog(
            lb => lb.AddConsoleSink(),
            configureGlobal: cb => cb.UseLocalTimeZone());

        result.Should().BeSameAs(builder);
        GlobalConfigLock.Configured.Should().BeTrue("configureGlobal should have applied the config");

        if (capturedLogger != null)
            await capturedLogger.DisposeAsync();
    }

    [Fact]
    public async Task UseLunariumLog_WithConfigureGlobalThatFails_LogsErrorButReturns()
    {
        // Ensure Configured = true so GlobalConfigurator.Configure() throws
        GlobalConfigLock.Configured = true;

        var servicesMock = Substitute.For<IServiceCollection>();
        var builder = new FakeLoggingBuilder(servicesMock);

        Lunarium.Logging.ILogger? capturedLogger = null;
        servicesMock.When(s => s.Add(Arg.Any<ServiceDescriptor>()))
            .Do(call =>
            {
                var descriptor = call.Arg<ServiceDescriptor>();
                if (descriptor.ServiceType == typeof(Lunarium.Logging.ILogger))
                    capturedLogger = descriptor.ImplementationInstance as Lunarium.Logging.ILogger;
            });

        // configureGlobal triggers GlobalConfigurator.Configure() → throws (already configured)
        // The exception should be caught internally and logged, not propagated
        ILoggingBuilder? result = null;
        var act = () =>
        {
            result = builder.UseLunariumLog(
                lb => lb.AddConsoleSink(),
                configureGlobal: _ => { /* any delegate triggers Configure() internally */ });
        };

        act.Should().NotThrow("exceptions in configureGlobal should be caught and logged internally");
        result.Should().BeSameAs(builder);

        if (capturedLogger != null)
            await capturedLogger.DisposeAsync();
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Section 16 — UseLunariumLog(ILoggingBuilder, IConfiguration, loggerName)
    // ────────────────────────────────────────────────────────────────────────────

    private static IConfiguration MakeConfiguration(string json)
    {
        return new ConfigurationBuilder()
            .AddJsonStream(new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)))
            .Build();
    }

    [Fact]
    public void UseLunariumLog_ILoggingBuilder_NullConfiguration_ThrowsArgumentNullException()
    {
        var servicesMock = Substitute.For<IServiceCollection>();
        var builder = new FakeLoggingBuilder(servicesMock);

        Action act = () => builder.UseLunariumLog((IConfiguration)null!, "MyLogger");

        act.Should().Throw<ArgumentNullException>().WithParameterName("configuration");
    }

    [Fact]
    public void UseLunariumLog_ILoggingBuilder_EmptyLoggerName_ThrowsArgumentException()
    {
        var servicesMock = Substitute.For<IServiceCollection>();
        var builder = new FakeLoggingBuilder(servicesMock);
        var cfg = MakeConfiguration("{}");

        Action act = () => builder.UseLunariumLog(cfg, "");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UseLunariumLog_ILoggingBuilder_LoggerNotFound_ThrowsInvalidOperationException()
    {
        var servicesMock = Substitute.For<IServiceCollection>();
        var builder = new FakeLoggingBuilder(servicesMock);
        var cfg = MakeConfiguration("{}");

        Action act = () => builder.UseLunariumLog(cfg, "NonExistentLogger");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*NonExistentLogger*");
    }

    [Fact]
    public async Task UseLunariumLog_ILoggingBuilder_ValidConfig_RegistersSingletonAndReturnsBuilder()
    {
        const string json = """
            {
              "LunariumLogging": {
                "LoggerConfigs": [
                  {
                    "LoggerName": "TestApp",
                    "ConsoleSinks": {
                      "console": {}
                    }
                  }
                ]
              }
            }
            """;

        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        var builder = new FakeLoggingBuilder(services);
        var cfg = MakeConfiguration(json);

        var result = builder.UseLunariumLog(cfg, "TestApp");

        result.Should().BeSameAs(builder);

        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(Lunarium.Logging.ILogger));
        descriptor.Should().NotBeNull();
        descriptor!.Lifetime.Should().Be(ServiceLifetime.Singleton);
        descriptor.ImplementationInstance.Should().BeAssignableTo<Lunarium.Logging.ILogger>();

        if (descriptor.ImplementationInstance is Lunarium.Logging.ILogger logger)
            await logger.DisposeAsync();
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Section 17 — UseLunariumLog(IHostBuilder, IConfiguration, loggerName)
    // ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void UseLunariumLog_IHostBuilder_NullConfiguration_ThrowsArgumentNullException()
    {
        var hostBuilderMock = Substitute.For<IHostBuilder>();

        Action act = () => hostBuilderMock.UseLunariumLog((IConfiguration)null!, "MyLogger");

        act.Should().Throw<ArgumentNullException>().WithParameterName("configuration");
    }

    [Fact]
    public void UseLunariumLog_IHostBuilder_EmptyLoggerName_ThrowsArgumentException()
    {
        var hostBuilderMock = Substitute.For<IHostBuilder>();
        var cfg = MakeConfiguration("{}");

        Action act = () => hostBuilderMock.UseLunariumLog(cfg, "");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UseLunariumLog_IHostBuilder_ValidConfig_CallsConfigureServicesAndReturnsSameBuilder()
    {
        var hostBuilderMock = Substitute.For<IHostBuilder>();
        hostBuilderMock
            .ConfigureServices(Arg.Any<Action<HostBuilderContext, IServiceCollection>>())
            .Returns(hostBuilderMock);
        var cfg = MakeConfiguration("{}");

        var result = hostBuilderMock.UseLunariumLog(cfg, "SomeLogger");

        result.Should().BeSameAs(hostBuilderMock);
        hostBuilderMock.Received(1)
            .ConfigureServices(Arg.Any<Action<HostBuilderContext, IServiceCollection>>());
    }
}
