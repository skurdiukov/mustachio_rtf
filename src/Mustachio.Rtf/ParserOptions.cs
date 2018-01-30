using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Mustachio.Rtf
{
    /// <summary>
    /// Options for Parsing run
    /// </summary>
    public class ParserOptions
    {
        public ParserOptions(string template)
            : this(template, null)
        {
        }

        public ParserOptions(string template, Func<Stream> targetStream)
            : this(template, targetStream, null)
        {
        }

        public ParserOptions(string template, Func<Stream> targetStream, Encoding encoding)
        {
            Template = template;
            StreamFactory = targetStream ?? (() => new MemoryStream());
            Encoding = encoding ?? Encoding.GetEncoding(1251);
            Formatters = new Dictionary<Type, FormatTemplateElement>();
            Null = string.Empty;
        }

        public ParserOptions(string template, Func<Stream> targetStream, Encoding encoding, long maxSize, bool disableContentEscaping = false, bool withModelInference = false)
            : this(template, targetStream, encoding)
        {
            MaxSize = maxSize;
            DisableContentEscaping = disableContentEscaping;
            WithModelInference = withModelInference;
        }

        public ParserOptions(string template, Func<Stream> targetStream, Encoding encoding, bool disableContentEscaping = false, bool withModelInference = false)
            : this(template, targetStream, encoding, 0, disableContentEscaping, withModelInference)
        {

        }

        /// <summary>
        /// Adds an Formatter overwrite or new Formatter for an Type
        /// </summary>
        public IDictionary<Type, FormatTemplateElement> Formatters { get; }

        public void AddFormatter<T>(Func<T, string, object> formatter)
        {
            Formatters.Add(typeof(T), (sourceObject, argument) =>
            {
                if (!(sourceObject is T))
                {
                    return sourceObject;
                }

                return formatter((T)sourceObject, argument);
            });
        }

        /// <summary>
        /// The template content to parse.
        /// </summary>
        public string Template { get; }

        /// <summary>
        /// In some cases, content should not be escaped (such as when rendering text bodies and subjects in emails).
        /// By default, we use content escaping, but this parameter allows it to be disabled.
        /// </summary>
        public bool DisableContentEscaping { get; }

        /// <summary>
        /// Parse the template, and capture paths used in the template to determine a suitable structure for the required
        /// model.
        /// </summary>
        public bool WithModelInference { get; }

        /// <summary>
        /// Defines a Max size for the Generated Template.
        /// Zero for no unlimited
        /// </summary>
        public long MaxSize { get; }


        /// <summary>
        /// If no static SourceStream is used the SourceFactory can be used to create a new stream for each template
        /// </summary>
        public Func<Stream> StreamFactory { get; }

        /// <summary>
        /// In what encoding should the text be written
        /// Default is UTF8
        /// </summary>
        public Encoding Encoding { get; }

        /// <summary>
        /// Defines how NULL values are exposed to the Template default is String.Empty
        /// </summary>
        public string Null { get; set; }
    }
}