namespace TateYoko.Engine;

/// <summary>Determines how the first source page is placed.</summary>
public enum FirstPageMode
{
    /// <summary>Pairs pages from page 1: 1-2, 3-4, and so on.</summary>
    Standard = 0,

    /// <summary>Places page 1 alone on the leading (right) side, then pairs from page 2.</summary>
    Cover = 1,

    /// <summary>Places page 1 alone on the trailing (left) side, then pairs from page 2.</summary>
    LeadingBlank = 2,
}
