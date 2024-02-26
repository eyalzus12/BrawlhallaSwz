using System;
using System.Runtime.Serialization;

namespace BrawlhallaSwz;

[Serializable]
public class SwzFileChecksumException : Exception
{
    public SwzFileChecksumException() { }
    public SwzFileChecksumException(string message) : base(message) { }
    public SwzFileChecksumException(string message, Exception inner) : base(message, inner) { }
    protected SwzFileChecksumException(SerializationInfo info, StreamingContext context) : base(info, context) { }
}