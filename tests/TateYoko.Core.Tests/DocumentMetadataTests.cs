using TateYoko.Core.Domain;

namespace TateYoko.Core.Tests;

/// <summary>Tests the <see cref="DocumentMetadata"/> record: the <see cref="DocumentMetadata.Empty"/> sentinel and value semantics.</summary>
public sealed class DocumentMetadataTests
{
    [Fact]
    public void EmptyHasAllNullFields()
    {
        DocumentMetadata empty = DocumentMetadata.Empty;
        Assert.Null(empty.Title);
        Assert.Null(empty.Author);
        Assert.Null(empty.Subject);
        Assert.Null(empty.Keywords);
        Assert.Null(empty.Creator);
        Assert.Null(empty.CreationDate);
    }

    [Fact]
    public void DefaultConstructionEqualsEmpty() =>
        Assert.Equal(DocumentMetadata.Empty, new DocumentMetadata());

    [Fact]
    public void EqualByValue()
    {
        var date = new DateTime(2026, 7, 6, 0, 0, 0, DateTimeKind.Utc);
        Assert.Equal(
            new DocumentMetadata("T", "A", CreationDate: date),
            new DocumentMetadata("T", "A", CreationDate: date));
    }

    [Fact]
    public void DiffersWhenAFieldDiffers() =>
        Assert.NotEqual(new DocumentMetadata("T", "A"), new DocumentMetadata("T", "B"));

    [Fact]
    public void WithExpressionOverridesASingleField()
    {
        DocumentMetadata updated = DocumentMetadata.Empty with { Title = "Book" };
        Assert.Equal("Book", updated.Title);
        Assert.Null(updated.Author);
    }
}
