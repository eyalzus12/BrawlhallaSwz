using System;

namespace BrawlhallaSwz;

[Serializable]
public sealed class SwzFileNameException : Exception
{
    public SwzFileNameException() { }
    public SwzFileNameException(string message) : base(message) { }
    public SwzFileNameException(string message, Exception inner) : base(message, inner) { }
}
