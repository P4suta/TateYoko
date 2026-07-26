namespace TateYoko.App.Services;

internal interface IDiagnosticLog
{
    void Write(Exception exception);
}
