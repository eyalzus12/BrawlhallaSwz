using System;
using System.Runtime.Serialization;

namespace BrawlhallaSwz;

[Serializable]
public class SwzFileNameException : Exception
{
    public SwzFileNameException() { }
    public SwzFileNameException(string message) : base(message) { }
    public SwzFileNameException(string message, Exception inner) : base(message, inner) { }
    protected SwzFileNameException(SerializationInfo info, StreamingContext context) : base(info, context) { }
}
