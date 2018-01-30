using System;

namespace Mustachio.Rtf
{
    /// <summary>
    /// Indicates a parse error including line and character info.
    /// </summary>
    public class IndexedParseException : ParseException
    {
        internal IndexedParseException(Tokenizer.CharacterLocation location, string message, params object[] replacements)
            : this(message, replacements)
        {
            LineNumber = location.Line;
            CharacterOnLine = location.Character;
        }

        public IndexedParseException(string message, params object[] replacements)
            : base(message, replacements)
        {

        }
		/// <summary>
		/// The line of the expression in the expression
		/// </summary>
        public int LineNumber { get; set; }
		/// <summary>
		/// The character from left in the line of the expression
		/// </summary>
        public int CharacterOnLine { get; set; }
    }
}
