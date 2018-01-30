using System;

namespace Mustachio.Rtf
{
    public class ParseException : Exception
    {
        public ParseException(string message, params object[] replacements)
            : base(string.Format(message, replacements)) { }
    }
}
