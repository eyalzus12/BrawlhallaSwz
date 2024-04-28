using System;

namespace BrawlhallaSwz;

[Serializable]
public class SwzKeyChecksumException : Exception
{
    public SwzKeyChecksumException() { }
    public SwzKeyChecksumException(string message) : base(message) { }
    public SwzKeyChecksumException(string message, Exception inner) : base(message, inner) { }
}