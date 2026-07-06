using TateYoko.Core.Domain;

namespace TateYoko.Core.Ports;

/// <summary>Assembles and saves the spread PDF one page at a time. Implemented by the PDF adapter.</summary>
public interface ISpreadWriter : IDisposable
{
    /// <summary>Adds one spread page and draws each page's content at its position.</summary>
    void AddSpread(SpreadSpec spec, IReadOnlyList<PagePlacement> placements);

    /// <summary>Applies metadata to the output document.</summary>
    void ApplyMetadata(DocumentMetadata metadata);

    /// <summary>Writes the assembled document to <paramref name="destinationPath"/>.</summary>
    void Save(string destinationPath);
}
