namespace TateYoko.Engine;

internal enum PdfSpreadError
{
    InvalidRequest = 0,
    InputNotFound = 1,
    ReadFailed = 2,
    UnsupportedFile = 3,
    PasswordRequired = 4,
    InvalidPassword = 5,
    CorruptedPdf = 6,
    InvalidPage = 7,
    WriteFailed = 8,
    Internal = 9,
    UnsupportedPdfFeature = 10,
}
