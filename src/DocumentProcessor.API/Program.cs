using System.Text.Json.Serialization;
using DocumentProcessor.API.Endpoints;
using DocumentProcessor.Application.Common.Files;
using Microsoft.AspNetCore.Http.Features;
using DocumentProcessor.API.Errors;
using DocumentProcessor.Application;
using DocumentProcessor.Infrastructure;
using DocumentProcessor.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<FormOptions>(options =>
    options.MultipartBodyLengthLimit =
        SupportedDocumentMediaTypes.MaxFileSizeBytes + 64 * 1024);
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<AppDbContext>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAntiforgery();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    await app.ApplyDatabaseMigrationsAsync();
}

app.MapHealthChecks("/health");
app.MapDocumentEndpoints();

app.Run();

public partial class Program;
