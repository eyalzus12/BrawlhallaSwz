using System;
using System.Runtime.Serialization;

namespace BrawlhallaSwz;

[Serializable]
public class SwzFileSizeException : Exception
{
    public SwzFileSizeException() { }
    public SwzFileSizeException(string message) : base(message) { }
    public SwzFileSizeException(string message, Exception inner) : base(message, inner) { }
    protected SwzFileSizeException(SerializationInfo info, StreamingContext context) : base(info, context) { }
}
