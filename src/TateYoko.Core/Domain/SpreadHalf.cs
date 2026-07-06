namespace TateYoko.Core.Domain;

/// <summary>
/// One half of a spread, abstracted over reading direction. For this RTL app,
/// Leading = right half, Trailing = left half.
/// </summary>
public enum SpreadHalf
{
    /// <summary>The reading-start side (right in RTL).</summary>
    Leading,

    /// <summary>The reading-end side (left in RTL).</summary>
    Trailing,
}
