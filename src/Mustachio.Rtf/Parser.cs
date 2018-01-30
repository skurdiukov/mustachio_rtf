#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

#endregion

namespace Mustachio.Rtf
{
    /// <summary>
    /// The main entry point for this library. Use the static "Parse" methods to create template functions.
    /// Functions are safe for reuse, so you may parse and cache the resulting function.
    /// </summary>
    public class Parser
    {
        private const int BufferSize = 2048;

        public static ExtendedParseInformation Parse(string template)
        {
            return ParseWithOptions(new ParserOptions(template));
        }

        /// <summary>
        /// Parses the Template with the given options.
        /// </summary>
        /// <param name="parsingOptions">A set of options.</param>
        /// <returns></returns>
        public static ExtendedParseInformation ParseWithOptions(ParserOptions parsingOptions)
        {
            if (parsingOptions == null)
            {
                throw new ArgumentNullException(nameof(parsingOptions));
            }

            if (parsingOptions.StreamFactory == null)
            {
                throw new ArgumentNullException(nameof(parsingOptions), "The given Stream is null");
            }

            var tokens = new Queue<TokenPair>(Tokenizer.Tokenize(parsingOptions.Template));
            var inferredModel = new InferredTemplateModel();

            var internalTemplate = Parse(tokens, parsingOptions, parsingOptions.WithModelInference ? inferredModel : null);
            Func<IDictionary<string, object>, CancellationToken, Stream> template = (model, token) =>
            {
                var targetStream = parsingOptions.StreamFactory();
                if (!targetStream.CanWrite)
                {
                    throw new InvalidOperationException("The stream is ReadOnly");
                }

                using (var streamWriter = new StreamWriter(targetStream, parsingOptions.Encoding, BufferSize, true))
                {
                    var context = new ContextObject
                    {
                        Value = model,
                        Key = "",
                        Options = parsingOptions,
                        CancellationToken = token
                    };
                    internalTemplate(streamWriter, context);
                    streamWriter.Flush();
                }
                return targetStream;
            };

            var result = new ExtendedParseInformation
            {
                InferredModel = inferredModel,
                ParsedTemplateWithCancellation = template
            };

            return result;
        }

        private static Action<StreamWriter, ContextObject> Parse(Queue<TokenPair> tokens, ParserOptions options,
            InferredTemplateModel currentScope = null)
        {
            var buildArray = new List<Action<StreamWriter, ContextObject>>();

            while (tokens.Any())
            {
                var currentToken = tokens.Dequeue();
                switch (currentToken.Type)
                {
                    case TokenType.Comment:
                        break;
                    case TokenType.Content:
                        buildArray.Add(HandleContent(currentToken.Value));
                        break;
                    case TokenType.CollectionOpen:
                        buildArray.Add(HandleCollectionOpen(currentToken, tokens, options, currentScope));
                        break;
                    case TokenType.ElementOpen:
                        buildArray.Add(HandleElementOpen(currentToken, tokens, options, currentScope));
                        break;
                    case TokenType.InvertedElementOpen:
                        buildArray.Add(HandleInvertedElementOpen(currentToken, tokens, options, currentScope));
                        break;
                    case TokenType.CollectionClose:
                    case TokenType.ElementClose:
                        // This should immediately return if we're in the element scope,
                        // and if we're not, this should have been detected by the tokenizer!
                        return (builder, context) =>
                        {
                            foreach (var a in buildArray.TakeWhile(e => StopOrAbortBuilding(context)))
                            {
                                a(builder, context);
                            }
                        };
                    case TokenType.Format:
                        buildArray.Add(HandleFormattingValue(currentToken, options, currentScope));
                        break;
                    case TokenType.PrintFormatted:
                        buildArray.Add(PrintFormattedValues(currentToken, options, currentScope));
                        break;
                    case TokenType.PrintSelf:
                        buildArray.Add(HandleSingleValue(currentToken, options, currentScope));
                        break;
                    case TokenType.EscapedSingleValue:
                    case TokenType.UnescapedSingleValue:
                        buildArray.Add(HandleSingleValue(currentToken, options, currentScope));
                        break;
                }
            }

            return (builder, context) =>
            {
                foreach (var a in buildArray.TakeWhile(e => StopOrAbortBuilding(context)))
                {
                    a(builder, context);
                }
            };
        }

        private static bool StopOrAbortBuilding(ContextObject context)
        {
            if (context.AbortGeneration || context.CancellationToken.IsCancellationRequested)
            {
                return false;
            }

            return true;
        }

        private static Action<StreamWriter, ContextObject> PrintFormattedValues(
            TokenPair currentToken,
            ParserOptions options,
            InferredTemplateModel currentScope)
        {
            return (builder, context) =>
            {
                if (context == null)
                {
                    return;
                }
                string value = null;
                if (context.FormattingValue != null)
                {
                    value = context.FormattingValue.ToString();
                    context.FormattingValue = null;
                }
                HandleContent(value)(builder, context);
            };
        }

        private static Action<StreamWriter, ContextObject> HandleFormattingValue(
            TokenPair currentToken,
            ParserOptions options,
            InferredTemplateModel scope)
        {
            return (builder, context) =>
            {
                scope = scope?.GetInferredModelForPath(currentToken.Value, InferredTemplateModel.UsedAs.Scalar);

                if (context == null)
                {
                    return;
                }
                var c = context.GetContextForPath(currentToken.Value, true);

                context.FormattingValue = c.Format(currentToken.FormatAs, c.Value);
            };
        }

        private static string RtfEncodeString(string context, ParserOptions options)
        {
            var src = options.Encoding.GetBytes(context);
            return Encoding.ASCII.GetString(src.SelectMany(
                b =>
                {
                    if (b > 0x7F || b == 0x7B || b == 0x7D || b == 0x5C)
                    {
                        return Encoding.ASCII.GetBytes($"\\'{b:x2}");
                    }

                    return new[] { b };
                }).ToArray());
        }

        private static Action<StreamWriter, ContextObject> HandleSingleValue(TokenPair token, ParserOptions options,
            InferredTemplateModel scope)
        {
            scope = scope?.GetInferredModelForPath(token.Value, InferredTemplateModel.UsedAs.Scalar);

            return (builder, context) =>
            {
                //try to locate the value in the context, if it exists, append it.
                var c = context?.GetContextForPath(token.Value);
                if (c?.Value != null)
                {
                    if (token.Type == TokenType.EscapedSingleValue && !options.DisableContentEscaping)
                    {
                        HandleContent(RtfEncodeString(c.ToString(), options))(builder, c);
                    }
                    else
                    {
                        HandleContent(c.ToString())(builder, c);
                    }
                }
            };
        }

        internal static void WriteContent(StreamWriter builder, string content, ContextObject context)
        {
            content = content ?? context.Options.Null;

            var sourceCount = builder.BaseStream.Length;
            var binaryContent = context.Options.Encoding.GetBytes(content);

            var contentLength = binaryContent.Length;
            if (context.Options.MaxSize == 0)
            {
                builder.BaseStream.Write(binaryContent, 0, contentLength);
                return;
            }

            if (sourceCount >= context.Options.MaxSize)
            {
                context.AbortGeneration = true;
            }

            var overflow = sourceCount + contentLength - context.Options.MaxSize;
            if (overflow <= 0)
            {
                builder.BaseStream.Write(binaryContent, 0, contentLength);
            }
            else
            {
                builder.BaseStream.Write(binaryContent, 0, (int)(contentLength - overflow));
            }
        }

        private static Action<StreamWriter, ContextObject> HandleContent(string token)
        {
            return (builder, context) => { WriteContent(builder, token, context); };
        }

        private static Action<StreamWriter, ContextObject> HandleInvertedElementOpen(TokenPair token,
            Queue<TokenPair> remainder,
            ParserOptions options, InferredTemplateModel scope)
        {
            scope = scope?.GetInferredModelForPath(token.Value, InferredTemplateModel.UsedAs.ConditionalValue);

            var innerTemplate = Parse(remainder, options, scope);

            return (builder, context) =>
            {
                var c = context.GetContextForPath(token.Value);
                //"falsey" values by Javascript standards...
                if (!c.Exists())
                {
                    innerTemplate(builder, c);
                }
            };
        }


        private static Action<StreamWriter, ContextObject> HandleCollectionOpen(TokenPair token, Queue<TokenPair> remainder,
            ParserOptions options, InferredTemplateModel scope)
        {
            scope = scope?.GetInferredModelForPath(token.Value, InferredTemplateModel.UsedAs.Collection);

            var innerTemplate = Parse(remainder, options, scope);

            return (builder, context) =>
            {
                //if we're in the same scope, just negating, then we want to use the same object
                var c = context.GetContextForPath(token.Value);

                //"falsey" values by Javascript standards...
                if (!c.Exists())
                {
                    return;
                }

                if (c.Value is IEnumerable enumerable && !(enumerable is string) && !(enumerable is IDictionary<string, object>))
                {
                    var index = 0;
                    var enumerator = enumerable.GetEnumerator();
                    if (enumerator.MoveNext())
                    {
                        var current = enumerator.Current;
                        object next;
                        do
                        {
                            if (enumerator.MoveNext())
                            {
                                next = enumerator.Current;
                            }
                            else
                            {
                                next = null;
                            }
                            var innerContext = new ContextCollection(index, next == null)
                            {
                                Value = current,
                                Key = string.Format("[{0}]", index),
                                Options = options,
                                Parent = c
                            };
                            innerTemplate(builder, innerContext);
                            index++;
                            current = next;
                        } while (current != null);
                    }
                }
                else
                {
                    throw new IndexedParseException(
                    "'{0}' is used like an array by the template, but is a scalar value or object in your model.", token.Value);
                }
            };
        }

        private static Action<StreamWriter, ContextObject> HandleElementOpen(TokenPair token, Queue<TokenPair> remainder,
            ParserOptions options, InferredTemplateModel scope)
        {
            scope = scope?.GetInferredModelForPath(token.Value, InferredTemplateModel.UsedAs.ConditionalValue);

            var innerTemplate = Parse(remainder, options, scope);

            return (builder, context) =>
            {
                var c = context.GetContextForPath(token.Value);
                //"falsey" values by Javascript standards...
                if (c.Exists())
                {
                    innerTemplate(builder, c);
                }
            };
        }
    }
}