using System.Security.Cryptography;

namespace TateYoko.Engine.Internal;

internal sealed class SensitiveMemoryStream : MemoryStream
{
    protected override void Dispose(bool disposing)
    {
        if (disposing && TryGetBuffer(out ArraySegment<byte> buffer))
        {
            CryptographicOperations.ZeroMemory(buffer.AsSpan());
        }

        base.Dispose(disposing);
    }
}
