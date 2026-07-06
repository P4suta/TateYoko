namespace TateYoko.Core.Domain;

/// <summary>How the first page opens, which determines how pages are paired.</summary>
public enum FirstPageMode
{
    /// <summary>Standard: pair page 1 with page 2 from the reading start (<c>1·2, 3·4, …</c>).</summary>
    Standard,

    /// <summary>Cover: place page 1 alone on the leading side, then pair from page 2 (<c>[1], 2·3, …</c>).</summary>
    Cover,

    /// <summary>Leading blank: place page 1 alone on the trailing side after an implicit blank, then pair from page 2 (<c>[▢|1], 2·3, …</c>).</summary>
    LeadingBlank,
}
