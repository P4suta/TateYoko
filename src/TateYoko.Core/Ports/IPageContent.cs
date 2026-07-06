namespace TateYoko.Core.Ports;

/// <summary>
/// An opaque reference to one source page. Core receives it from the source without inspecting it
/// and passes it straight to <see cref="ISpreadWriter"/>; the PDF adapter interprets it.
/// </summary>
public interface IPageContent
{
}
