using TateYoko.Core.Domain;

namespace TateYoko.Core.Ports;

/// <summary>
/// An opened source PDF. Provides the page count, each page's display size (rotation applied),
/// page-content references, and metadata. Implemented by the PDF adapter.
/// </summary>
public interface ISourceDocument : IDisposable
{
    int PageCount { get; }

    DocumentMetadata Metadata { get; }

    /// <summary>The page's display size, with rotation applied.</summary>
    PageDimension GetPageDimension(int pageIndex);

    /// <summary>An opaque content reference for the page.</summary>
    IPageContent GetPageContent(int pageIndex);
}
