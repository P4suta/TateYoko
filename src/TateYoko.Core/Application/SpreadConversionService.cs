using TateYoko.Core.Domain;
using TateYoko.Core.Ports;

namespace TateYoko.Core.Application;

/// <summary>
/// The spread-conversion use case: opens the source, pairs pages (<see cref="Pagination"/>),
/// computes each spread's layout (<see cref="SpreadLayoutCalculator"/>), and draws to the writer.
/// Depends only on <see cref="IPdfEngine"/>, never on a concrete PDF library type.
/// </summary>
public sealed class SpreadConversionService
{
    private readonly IPdfEngine _engine;
    private readonly SpreadLayoutCalculator _calculator = new();

    public SpreadConversionService(IPdfEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        _engine = engine;
    }

    /// <summary>Converts one PDF into an RTL spread and saves it.</summary>
    public void Convert(SpreadRequest request, IConversionProgress? progress = null)
    {
        ArgumentNullException.ThrowIfNull(request);

        using ISourceDocument source = _engine.OpenSource(request.InputPath);
        IReadOnlyList<PagePairSpec> specs = Pagination.Paginate(request.FirstPageMode, source.PageCount);

        using ISpreadWriter writer = _engine.CreateWriter();
        int done = 0;
        foreach (PagePairSpec spec in specs)
        {
            (SpreadSpec frame, IReadOnlyList<PagePlacement> placements) = BuildSpread(source, spec);
            writer.AddSpread(frame, placements);
            progress?.Report(++done, specs.Count);
        }

        writer.ApplyMetadata(source.Metadata);
        writer.Save(request.OutputPath);
    }

    /// <summary>Resolves one page pair into a frame size plus its placement list.</summary>
    private (SpreadSpec Frame, IReadOnlyList<PagePlacement> Placements) BuildSpread(
        ISourceDocument source, PagePairSpec spec)
    {
        switch (spec)
        {
            case PagePairSpec.Pair pair:
                {
                    PageDimension first = source.GetPageDimension(pair.FirstIndex);
                    PageDimension second = source.GetPageDimension(pair.SecondIndex);
                    SpreadLayout layout = _calculator.Calculate(first, second);
                    var placements = new PagePlacement[]
                    {
                    new(source.GetPageContent(pair.FirstIndex), layout.FirstPosition),
                    new(source.GetPageContent(pair.SecondIndex), layout.SecondPosition!.Value),
                    };
                    return (layout.Spec, placements);
                }

            case PagePairSpec.Single single:
                {
                    PageDimension page = source.GetPageDimension(single.PageIndex);
                    SpreadLayout layout = _calculator.CalculateSingle(page, single.Half);
                    var placements = new PagePlacement[]
                    {
                    new(source.GetPageContent(single.PageIndex), layout.FirstPosition),
                    };
                    return (layout.Spec, placements);
                }

            default:
                throw new SpreadException(ErrorKind.Internal, $"unknown spec: {spec.GetType().Name}");
        }
    }
}
