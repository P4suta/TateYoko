namespace TateYoko.Core.Domain;

/// <summary>Document metadata carried over from the source PDF; absent fields are <c>null</c>.</summary>
public sealed record DocumentMetadata(
    string? Title = null,
    string? Author = null,
    string? Subject = null,
    string? Keywords = null,
    string? Creator = null,
    DateTime? CreationDate = null)
{
    public static readonly DocumentMetadata Empty = new();
}
