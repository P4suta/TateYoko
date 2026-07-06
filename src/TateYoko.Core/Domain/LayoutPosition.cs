namespace TateYoko.Core.Domain;

/// <summary>
/// A page's position within a spread frame, in points: horizontal offset from the left edge and
/// the vertical centering gap. Vertically symmetric, so it is independent of the renderer's
/// origin (top-left or bottom-left).
/// </summary>
public readonly record struct LayoutPosition(float OffsetXPt, float OffsetYPt);
