using TateYoko.Core.Domain;

namespace TateYoko.Core.Application;

/// <summary>Input for one conversion: source path, output path, and how the first page opens.</summary>
public sealed record SpreadRequest(string InputPath, string OutputPath, FirstPageMode FirstPageMode);
