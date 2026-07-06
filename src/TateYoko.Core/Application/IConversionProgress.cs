namespace TateYoko.Core.Application;

/// <summary>Progress callback, invoked once per spread written.</summary>
public interface IConversionProgress
{
    /// <param name="completedSpreads">Spreads written so far.</param>
    /// <param name="totalSpreads">Total spreads to write.</param>
    void Report(int completedSpreads, int totalSpreads);
}
