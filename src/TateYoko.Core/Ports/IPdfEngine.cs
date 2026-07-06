namespace TateYoko.Core.Ports;

/// <summary>
/// Entry point to the PDF adapter (injected at the composition root). Opens source documents and
/// creates output writers, so Core can drive conversion without knowing any concrete PDF type.
/// </summary>
public interface IPdfEngine
{
    /// <summary>Opens the PDF at <paramref name="path"/> as a conversion source.</summary>
    ISourceDocument OpenSource(string path);

    /// <summary>Creates a new spread output writer.</summary>
    ISpreadWriter CreateWriter();
}
