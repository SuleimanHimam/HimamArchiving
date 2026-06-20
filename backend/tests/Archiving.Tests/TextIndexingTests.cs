using Archiving.Application.Common.Interfaces;
using Archiving.Domain.Entities;
using Archiving.Domain.Enums;
using Archiving.Infrastructure.Persistence;
using Archiving.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Archiving.Tests;

public class TextIndexingTests
{
    private static AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private sealed class FakeStorage : IFileStorage
    {
        public Task<StoredFile> SaveAsync(string f, string n, Stream c, CancellationToken ct = default) =>
            Task.FromResult(new StoredFile("k", 0, "x"));
        public Task<Stream?> OpenAsync(string key, CancellationToken ct = default) =>
            Task.FromResult<Stream?>(new MemoryStream(new byte[] { 1, 2, 3 }));
        public Task DeleteAsync(string key, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeExtractor : ITextExtractionService
    {
        public bool CanExtract(string ext) => ext.TrimStart('.').Equals("pdf", StringComparison.OrdinalIgnoreCase);
        public Task<TextExtractionResult> ExtractAsync(byte[] b, string ext, CancellationToken ct = default) =>
            Task.FromResult(new TextExtractionResult("hello world", "Embedded"));
    }

    private static TextIndexingService NewService(AppDbContext db) =>
        new(db, new FakeStorage(), new FakeExtractor(), NullLogger<TextIndexingService>.Instance);

    [Fact]
    public async Task Extractable_attachment_becomes_Done_with_text()
    {
        using var db = NewDb();
        db.DocumentAttachments.Add(new DocumentAttachment
        {
            Id = 1, DocumentId = 1, FileName = "a.pdf", FileExtension = "pdf", StorageKey = "k",
            ExtractionStatus = TextExtractionStatus.Pending,
        });
        await db.SaveChangesAsync();

        var processed = await NewService(db).SweepPendingAsync(10);

        var a = await db.DocumentAttachments.FindAsync(1L);
        Assert.Equal(1, processed);
        Assert.Equal(TextExtractionStatus.Done, a!.ExtractionStatus);
        Assert.Equal("hello world", a.ExtractedText);
        Assert.Equal("Embedded", a.ExtractionSource);
        Assert.NotNull(a.TextExtractedAt);
    }

    [Fact]
    public async Task Non_extractable_attachment_is_Skipped()
    {
        using var db = NewDb();
        db.DocumentAttachments.Add(new DocumentAttachment
        {
            Id = 2, DocumentId = 1, FileName = "a.zip", FileExtension = "zip", StorageKey = "k",
            ExtractionStatus = TextExtractionStatus.Pending,
        });
        await db.SaveChangesAsync();

        await NewService(db).SweepPendingAsync(10);

        var a = await db.DocumentAttachments.FindAsync(2L);
        Assert.Equal(TextExtractionStatus.Skipped, a!.ExtractionStatus);
        Assert.Null(a.ExtractedText);
    }
}
