using System;

namespace BrawlhallaSwz;

[Serializable]
public sealed class SwzFileSizeException : Exception
{
    public SwzFileSizeException() { }
    public SwzFileSizeException(string message) : base(message) { }
    public SwzFileSizeException(string message, Exception inner) : base(message, inner) { }
}
