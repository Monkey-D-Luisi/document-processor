using DocumentProcessor.Application.Common.DependencyInjection;
using DocumentProcessor.Application.Documents.Analysis.Ports;
using DocumentProcessor.Application.Documents.Analysis.Services;
using DocumentProcessor.Application.Documents.Analysis.Models;
using DocumentProcessor.Domain.Documents.Repositories;
using DocumentProcessor.Infrastructure.Documents.Analysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DocumentProcessor.Infrastructure.Tests;

public sealed class IoCRegistrationTests
{
    [Fact]
    public void Infrastructure_registers_attributed_services_from_known_assemblies()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(CreateConfiguration("openai"));

        Assert.Equal(ServiceLifetime.Scoped, GetDescriptor<IDocumentAnalyzer>(services).Lifetime);
        Assert.Equal(ServiceLifetime.Scoped, GetDescriptor<IDocumentAnalysisProviderFactory>(services).Lifetime);
        Assert.Equal(ServiceLifetime.Scoped, GetDescriptor<IDocumentAnalysisProvider>(services).Lifetime);
        Assert.Equal(ServiceLifetime.Scoped, GetDescriptor<IDocumentRepository>(services).Lifetime);
        Assert.Equal(ServiceLifetime.Singleton, GetDescriptor<IOpenAiResponsesClient>(services).Lifetime);
    }

    [Fact]
    public void Configured_analyzer_resolves_openai_provider_without_making_a_provider_call()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(CreateConfiguration("openai", includeOpenAiCredentials: true));
        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        var analyzer = scope.ServiceProvider.GetRequiredService<IDocumentAnalyzer>();
        var provider = scope.ServiceProvider
            .GetRequiredService<IDocumentAnalysisProviderFactory>()
            .Get(DocumentAnalysisProviderType.OpenAi);

        Assert.IsType<ConfiguredDocumentAnalyzer>(analyzer);
        Assert.IsType<OpenAiDocumentAnalyzer>(provider);
    }

    [Fact]
    public void Attributed_services_allow_multiple_scoped_implementations_and_share_scope_instances()
    {
        var services = new ServiceCollection();
        IoCServiceCollectionExtensions.AddAttributedTypes(
            services,
            [typeof(TestProviderOne), typeof(TestProviderTwo)]);

        var descriptors = services
            .Where(descriptor => descriptor.ServiceType == typeof(ITestProvider))
            .ToArray();
        Assert.Equal(2, descriptors.Length);
        Assert.All(descriptors, descriptor => Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime));

        using var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
        });
        using var firstScope = serviceProvider.CreateScope();
        var firstProvider = firstScope.ServiceProvider.GetRequiredService<TestProviderOne>();
        var resolvedProviders = firstScope.ServiceProvider.GetServices<ITestProvider>().ToArray();

        Assert.Contains(firstProvider, resolvedProviders);

        using var secondScope = serviceProvider.CreateScope();
        var secondProvider = secondScope.ServiceProvider.GetRequiredService<TestProviderOne>();
        Assert.NotSame(firstProvider, secondProvider);
    }

    [Fact]
    public void Attributed_registration_rejects_duplicate_single_service_declarations_atomically()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            IoCServiceCollectionExtensions.AddAttributedTypes(
                services,
                [typeof(DuplicateProviderOne), typeof(DuplicateProviderTwo)]));

        Assert.Contains("Duplicate single [IoC] service", exception.Message, StringComparison.Ordinal);
        Assert.Empty(services);
    }

    [Fact]
    public void Attributed_registration_rejects_incompatible_lifetimes()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            IoCServiceCollectionExtensions.AddAttributedTypes(
                services,
                [typeof(ScopedProvider), typeof(SingletonProvider)]));

        Assert.Contains("Conflicting [IoC] lifetimes", exception.Message, StringComparison.Ordinal);
        Assert.Empty(services);
    }

    [Fact]
    public void Attributed_registration_rejects_implementation_that_does_not_match_service()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            IoCServiceCollectionExtensions.AddAttributedTypes(
                services,
                [typeof(InvalidProvider)]));

        Assert.Contains("Invalid [IoC] declaration", exception.Message, StringComparison.Ordinal);
        Assert.Empty(services);
    }

    [Fact]
    public void Attributed_registration_rejects_existing_explicit_service_without_partial_changes()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDocumentAnalyzer, ExistingDocumentAnalyzer>();
        var existingCount = services.Count;

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddAttributedServices(
            typeof(DocumentProcessor.Application.DependencyInjection).Assembly,
            typeof(DocumentProcessor.Infrastructure.DependencyInjection).Assembly));

        Assert.Contains("already registered explicitly", exception.Message, StringComparison.Ordinal);
        Assert.Equal(existingCount, services.Count);
    }

    [Fact]
    public void Document_analysis_provider_configuration_resolves_a_typed_identity()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(CreateConfiguration(" OpenAI "));
        using var serviceProvider = services.BuildServiceProvider();

        Assert.Equal(
            DocumentAnalysisProviderType.OpenAi,
            serviceProvider.GetRequiredService<DocumentAnalysisProviderType>());
    }

    [Fact]
    public void Document_analysis_provider_configuration_rejects_unknown_id()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(CreateConfiguration("anthropic"));
        using var serviceProvider = services.BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(
            () => serviceProvider.GetRequiredService<DocumentAnalysisProviderType>());

        Assert.Contains("DocumentAnalysis:Provider", exception.Message, StringComparison.Ordinal);
    }

    private static ServiceDescriptor GetDescriptor<TService>(IServiceCollection services) =>
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(TService));

    private static IConfiguration CreateConfiguration(
        string? provider,
        bool includeOpenAiCredentials = false)
    {
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Database"] =
                "Host=localhost;Port=5438;Database=document_processor;Username=document_processor;Password=document_processor",
            ["DocumentAnalysis:Provider"] = provider,
        };

        if (includeOpenAiCredentials)
        {
            settings["OpenAI:ApiKey"] = "not-a-real-key-used-only-for-client-construction";
            settings["OpenAI:Model"] = "gpt-6-luna";
            settings["OpenAI:ModelPricing:gpt-6-luna:InputCostPerMillionTokensUsd"] = "0.1";
            settings["OpenAI:ModelPricing:gpt-6-luna:CachedInputCostPerMillionTokensUsd"] = "0.01";
            settings["OpenAI:ModelPricing:gpt-6-luna:OutputCostPerMillionTokensUsd"] = "0.5";
            settings["OpenAI:ModelPricing:gpt-6-luna:LongContextThresholdTokens"] = "272000";
            settings["OpenAI:ModelPricing:gpt-6-luna:LongContextInputCostPerMillionTokensUsd"] = "0.2";
            settings["OpenAI:ModelPricing:gpt-6-luna:LongContextCachedInputCostPerMillionTokensUsd"] = "0.02";
            settings["OpenAI:ModelPricing:gpt-6-luna:LongContextOutputCostPerMillionTokensUsd"] = "0.75";
        }

        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    }

    private sealed class ExistingDocumentAnalyzer : IDocumentAnalyzer
    {
        public Task<DocumentAnalyzerResult> AnalyzeAsync(
            DocumentAnalyzerInput input,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}

public interface ITestProvider
{
}

[IoC(typeof(ITestProvider), IoCLifetime.Scoped, allowMultiple: true)]
public sealed class TestProviderOne : ITestProvider
{
}

[IoC(typeof(ITestProvider), IoCLifetime.Scoped, allowMultiple: true)]
public sealed class TestProviderTwo : ITestProvider
{
}

public interface IDuplicateProvider
{
}

[IoC(typeof(IDuplicateProvider))]
public sealed class DuplicateProviderOne : IDuplicateProvider
{
}

[IoC(typeof(IDuplicateProvider))]
public sealed class DuplicateProviderTwo : IDuplicateProvider
{
}

public interface IScopedOrSingletonProvider
{
}

[IoC(typeof(IScopedOrSingletonProvider), IoCLifetime.Scoped, allowMultiple: true)]
public sealed class ScopedProvider : IScopedOrSingletonProvider
{
}

[IoC(typeof(IScopedOrSingletonProvider), IoCLifetime.Singleton, allowMultiple: true)]
public sealed class SingletonProvider : IScopedOrSingletonProvider
{
}

public interface IExpectedProvider
{
}

[IoC(typeof(IExpectedProvider))]
public sealed class InvalidProvider
{
}
