using System;
using System.Runtime.Serialization;

namespace BrawlhallaSwz;

[Serializable]
public class SwzKeyChecksumException : Exception
{
    public SwzKeyChecksumException() { }
    public SwzKeyChecksumException(string message) : base(message) { }
    public SwzKeyChecksumException(string message, Exception inner) : base(message, inner) { }
    protected SwzKeyChecksumException(SerializationInfo info, StreamingContext context) : base(info, context) { }
}