namespace TateYoko.Engine;

internal interface IPdfSpreadConverter
{
    Task<PdfSpreadResult> ConvertAsync(
        PdfSpreadRequest request,
        IProgress<PdfSpreadProgress>? progress = null,
        CancellationToken cancellationToken = default
    );
}
