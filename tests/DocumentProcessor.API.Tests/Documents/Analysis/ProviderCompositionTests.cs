using DocumentProcessor.Application.Documents.Analysis.Ports;
using DocumentProcessor.Application.Documents.Analysis.Services;
using DocumentProcessor.Application.Documents.Analysis.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentProcessor.API.Tests.Documents.Analysis;

public sealed class ProviderCompositionTests
{
    [Fact]
    public async Task Api_host_starts_and_resolves_the_configured_openai_provider_chain()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/v1/documents/antiforgery");
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var analyzer = scope.ServiceProvider.GetRequiredService<IDocumentAnalyzer>();
        Assert.IsType<ConfiguredDocumentAnalyzer>(analyzer);

        var providerFactory = scope.ServiceProvider.GetRequiredService<IDocumentAnalysisProviderFactory>();
        var provider = providerFactory.Get(DocumentAnalysisProviderType.OpenAi);

        Assert.Equal("OpenAiDocumentAnalyzer", provider.GetType().Name);
        Assert.Equal(DocumentAnalysisProviderType.OpenAi, provider.ProviderType);
    }

    private sealed class ApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["OpenAI:ApiKey"] = "integration-test-placeholder-not-used",
                }));
        }
    }
}
