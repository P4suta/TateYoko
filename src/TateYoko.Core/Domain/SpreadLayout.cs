namespace TateYoko.Core.Domain;

/// <summary>The computed geometry of one spread: the frame size and each page's position.</summary>
/// <param name="Spec">The spread frame size.</param>
/// <param name="FirstPosition">The position of the first (or single) page.</param>
/// <param name="SecondPosition">The position of the second page; <c>null</c> for a single spread.</param>
public readonly record struct SpreadLayout(
    SpreadSpec Spec,
    LayoutPosition FirstPosition,
    LayoutPosition? SecondPosition);
