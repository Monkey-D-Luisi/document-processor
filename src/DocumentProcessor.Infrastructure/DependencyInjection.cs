using DocumentProcessor.Application.Common.Interfaces;
using DocumentProcessor.Application.Common.Models;
using DocumentProcessor.Domain.Repositories;
using DocumentProcessor.Infrastructure.AI;
using DocumentProcessor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenAI.Responses;
using OpenAI;
using System.ClientModel;
using System.ClientModel.Primitives;

namespace DocumentProcessor.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Database")
            ?? throw new InvalidOperationException(
                "Connection string 'Database' is not configured.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsAssembly(
                    typeof(AppDbContext).Assembly.FullName)));

        services
            .AddOptions<DocumentAnalysisProviderOptions>()
            .Bind(configuration.GetSection(DocumentAnalysisProviderOptions.SectionName))
            .Validate(options => DocumentAnalysisProviderTypeCodec.TryParse(options.Provider, out _),
                "DocumentAnalysis:Provider must be a supported provider ID.")
            .ValidateOnStart();

        services.AddSingleton(
            typeof(DocumentAnalysisProviderType),
            serviceProvider => DocumentAnalysisProviderTypeCodec.Parse(
                serviceProvider.GetRequiredService<IOptions<DocumentAnalysisProviderOptions>>().Value.Provider));

        services
            .AddOptions<OpenAiDocumentOptions>()
            .Bind(configuration.GetSection(OpenAiDocumentOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.ApiKey),
                "OpenAI:ApiKey is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Model),
                "OpenAI:Model is required.")
            .Validate(options => options.ModelPricing.TryGetValue(options.Model, out var pricing) &&
                                 pricing.InputCostPerMillionTokensUsd >= 0 &&
                                 pricing.CachedInputCostPerMillionTokensUsd >= 0 &&
                                 pricing.OutputCostPerMillionTokensUsd >= 0 &&
                                 pricing.LongContextThresholdTokens > 0 &&
                                 pricing.LongContextInputCostPerMillionTokensUsd > 0 &&
                                 pricing.LongContextCachedInputCostPerMillionTokensUsd > 0 &&
                                 pricing.LongContextOutputCostPerMillionTokensUsd > 0,
                "OpenAI:Model must have non-negative token pricing configured.")
            .Validate(options => options.MaxOutputTokenCount is > 0 and <= 128_000,
                "OpenAI:MaxOutputTokenCount must be between 1 and 128000.")
            .ValidateOnStart();

        services.AddSingleton(provider =>
        {
            var options = provider.GetRequiredService<IOptions<OpenAiDocumentOptions>>().Value;
            return new ResponsesClient(
                new ApiKeyCredential(options.ApiKey),
                new ResponsesClientOptions
                {
                    RetryPolicy = new ClientRetryPolicy(0),
                    NetworkTimeout = TimeSpan.FromSeconds(120),
                });
        });

        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddAttributedServices(
            typeof(DocumentProcessor.Application.DependencyInjection).Assembly,
            typeof(DependencyInjection).Assembly);

        return services;
    }
}
