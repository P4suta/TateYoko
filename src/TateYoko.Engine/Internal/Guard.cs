namespace TateYoko.Engine.Internal;

internal static class Guard
{
    internal static void Defined<TEnum>(TEnum value, string name)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new PdfSpreadException(PdfSpreadError.InvalidRequest, $"undefined-{name}");
        }
    }

    internal static void FinitePositive(double value, string name)
    {
        if (!double.IsFinite(value) || value <= 0)
        {
            throw new PdfSpreadException(PdfSpreadError.InvalidRequest, $"invalid-{name}");
        }
    }
}
