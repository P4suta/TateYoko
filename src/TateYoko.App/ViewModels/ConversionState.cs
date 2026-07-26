namespace TateYoko.App.ViewModels;

internal enum ConversionState
{
    Idle = 0,
    Ready = 1,
    Converting = 2,
    PasswordRequired = 3,
    Done = 4,
    Error = 5,
}
