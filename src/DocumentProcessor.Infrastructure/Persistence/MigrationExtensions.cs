using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DocumentProcessor.Domain.Documents;

namespace DocumentProcessor.Infrastructure.Persistence;

public static class MigrationExtensions
{
    public static async Task ApplyDatabaseMigrationsAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.MigrateAsync();
        var now = DateTimeOffset.UtcNow;
        await dbContext.Documents
            .Where(document => document.Status == nameof(DocumentStatus.Pending) ||
                               document.Status == nameof(DocumentStatus.Processing))
            .ExecuteUpdateAsync(update => update
                .SetProperty(document => document.Status, nameof(DocumentStatus.Failed))
                .SetProperty(
                    document => document.FailureReason,
                    "Processing was interrupted before the document could be completed.")
                .SetProperty(document => document.UpdatedAtUtc, now));
    }
}
