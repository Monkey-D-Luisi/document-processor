using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using DocumentProcessor.Application.Common.Interfaces;
using DocumentProcessor.Domain.Documents;
using DocumentProcessor.Domain.Repositories;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DocumentProcessor.API.Tests.Documents;

public sealed class DocumentUploadEndpointTests : IClassFixture<DocumentUploadEndpointTests.TestApiFactory>
{
    private readonly TestApiFactory factory;

    public static TheoryData<string, string, byte[]> SupportedFiles => new()
    {
        { "invoice.pdf", "application/pdf", "%PDF-1.7 test"u8.ToArray() },
        { "invoice.jpg", "image/jpeg", [0xFF, 0xD8, 0xFF, 0x00] },
        { "invoice.png", "image/png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A] },
        { "invoice.webp", "image/webp", "RIFF\0\0\0\0WEBP"u8.ToArray() },
    };

    public DocumentUploadEndpointTests(TestApiFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Upload_requires_antiforgery_token()
    {
        using var client = factory.CreateClient();
        using var content = CreateForm("invoice.pdf", "application/pdf", "%PDF-1.7 test"u8.ToArray());

        var response = await client.PostAsync("/api/v1/documents/process", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [MemberData(nameof(SupportedFiles))]
    public async Task Upload_returns_validated_result_for_supported_pdf_and_images(
        string fileName,
        string mediaType,
        byte[] bytes)
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
        });
        var token = await GetAntiforgeryTokenAsync(client);
        using var content = CreateForm(fileName, mediaType, bytes);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/documents/process")
        {
            Content = content,
        };
        request.Headers.Add("X-CSRF-TOKEN", token);

        var response = await client.SendAsync(request);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        result.GetProperty("mediaType").GetString().Should().Be(mediaType);
        result.GetProperty("status").GetString().Should().Be("Completed");
        result.GetProperty("analysis").GetProperty("requiresReview").GetBoolean().Should().BeFalse();

        var documentId = result.GetProperty("id").GetGuid();
        var stored = await client.GetFromJsonAsync<JsonElement>($"/api/v1/documents/{documentId}");
        stored.GetProperty("id").GetGuid().Should().Be(documentId);
        stored.GetProperty("status").GetString().Should().Be("Completed");
    }

    [Fact]
    public async Task Oversized_upload_returns_payload_too_large()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
        });
        var token = await GetAntiforgeryTokenAsync(client);
        using var content = CreateForm(
            "large.pdf",
            "application/pdf",
            new byte[DocumentProcessor.Application.Common.Files.SupportedDocumentMediaTypes.MaxFileSizeBytes + 1]);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/documents/process")
        {
            Content = content,
        };
        request.Headers.Add("X-CSRF-TOKEN", token);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
    }

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/v1/documents/antiforgery");
        response.EnsureSuccessStatusCode();
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return json.RootElement.GetProperty("requestToken").GetString()!;
    }

    private static MultipartFormDataContent CreateForm(string name, string mediaType, byte[] content)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
        form.Add(file, "file", name);
        return form;
    }

    public sealed class TestApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["OpenAI:ApiKey"] = "test-key-not-used",
                }));

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IDocumentAnalyzer>();
                services.AddSingleton<IDocumentAnalyzer, FakeDocumentAnalyzer>();
                services.RemoveAll<IDocumentRepository>();
                services.AddSingleton<IDocumentRepository, InMemoryDocumentRepository>();
            });
        }
    }

    private sealed class FakeDocumentAnalyzer : IDocumentAnalyzer
    {
        public Task<DocumentAnalyzerResult> AnalyzeAsync(
            DocumentAnalyzerInput input,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new DocumentAnalyzerResult(
                new DocumentAnalysisCandidate(
                    "invoice", "Example SL", null, "Customer SL", null, "F-001",
                    "2026-08-25", null, "EUR", 100m, 21m, 121m,
                    [new DocumentLineCandidate("Service", 1m, 100m, 21m, 100m, TaxAmount: 21m)],
                    [], [])
                {
                    VatBreakdown = [new DocumentTaxBreakdownCandidate(100m, 21m, 21m)],
                },
                new ProviderProcessingMetrics("Fake", "synthetic", 12, 100, 20, 0.0001m)));
    }

    private sealed class InMemoryDocumentRepository : IDocumentRepository
    {
        private readonly ConcurrentDictionary<Guid, Document> documents = new();

        public Task AddAsync(Document document, CancellationToken cancellationToken = default)
        {
            documents[document.Id] = document;
            return Task.CompletedTask;
        }

        public Task<Document?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            documents.TryGetValue(id, out var document);
            return Task.FromResult(document);
        }

        public Task UpdateAsync(Document document, CancellationToken cancellationToken = default)
        {
            documents[document.Id] = document;
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
