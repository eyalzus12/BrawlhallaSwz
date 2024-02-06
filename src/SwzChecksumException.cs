using System;
using System.Runtime.Serialization;

namespace BrawlhallaSwz;

[Serializable]
public class SwzChecksumException : Exception
{
    public SwzChecksumException() { }
    public SwzChecksumException(string message) : base(message) { }
    public SwzChecksumException(string message, Exception inner) : base(message, inner) { }
    protected SwzChecksumException(SerializationInfo info, StreamingContext context) : base(info, context) { }
}
