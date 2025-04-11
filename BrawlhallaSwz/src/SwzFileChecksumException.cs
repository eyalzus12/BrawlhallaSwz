using System;

namespace BrawlhallaSwz;

[Serializable]
public sealed class SwzFileChecksumException : Exception
{
    public SwzFileChecksumException() { }
    public SwzFileChecksumException(string message) : base(message) { }
    public SwzFileChecksumException(string message, Exception inner) : base(message, inner) { }
}