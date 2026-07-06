using TateYoko.Core.Domain;

namespace TateYoko.Core.Ports;

/// <summary>One source page's content paired with its computed position in a spread.</summary>
public readonly record struct PagePlacement(IPageContent Content, LayoutPosition Position);
