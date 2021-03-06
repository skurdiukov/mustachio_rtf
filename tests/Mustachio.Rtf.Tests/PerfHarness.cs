using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

using NUnit.Framework;

namespace Mustachio.Rtf.Tests
{
    public static class StreamExtentions
    {
        public static string Stringify(this Stream source, bool disposeOriginal = true, Encoding encoding = null)
        {
            encoding = encoding ?? Encoding.ASCII;
            try
            {
                source.Seek(0, SeekOrigin.Begin);
                if (source is MemoryStream stream)
                {
                    return encoding.GetString(stream.ToArray());
                }

                using (var ms = new MemoryStream())
                {
                    source.CopyToAsync(ms);
                    return ms.Stringify(disposeOriginal, encoding);
                }
            }
            finally
            {
                if (disposeOriginal)
                {
                    source.Dispose();
                }
            }
        }
    }

    public class PerfHarness
    {
        [Theory]
        [TestCase("Model Depth", 5, 30000, 10, 5000)]
        [TestCase("Model Depth", 10, 30000, 10, 5000)]
        [TestCase("Model Depth", 100, 30000, 10, 5000)]
        [TestCase("Substitutions", 5, 30000, 10, 5000)]
        [TestCase("Substitutions", 5, 30000, 50, 5000)]
        [TestCase("Substitutions", 5, 30000, 100, 5000)]
        [TestCase("Template Size", 5, 15000, 10, 5000)]
        [TestCase("Template Size", 5, 25000, 10, 5000)]
        [TestCase("Template Size", 5, 30000, 10, 5000)]
        [TestCase("Template Size", 5, 50000, 10, 5000)]
        [TestCase("Template Size", 5, 100000, 10, 5000)]
        public void TestRuns(string variation, int modelDepth, int sizeOfTemplate, int inserts, int runs)
        {
            var model = ConstructModelAndPath(modelDepth);
            var baseTemplate = Enumerable.Range(1, 5).Aggregate("", (seed, current) => seed += " [[" + model.Item2 + "]]");
            while (baseTemplate.Length <= sizeOfTemplate)
            {
                baseTemplate += model.Item2 + "\r\n";
            }

            Func<IDictionary<string, object>, Stream> template = null;

            //make sure this class is JIT'd before we start timing.
            Parser.ParseWithOptions(new ParserOptions("asdf"));

            var totalTime = Stopwatch.StartNew();
            var parseTime = Stopwatch.StartNew();
            for (var i = 0; i < runs; i++)
            {
                template = Parser.ParseWithOptions(new ParserOptions(baseTemplate, () => new MemoryStream())).ParsedTemplate;
            }

            parseTime.Stop();

            var renderTime = Stopwatch.StartNew();
            for (var i = 0; i < runs; i++)
            {
                using (var f = template(model.Item1))
                {
                }
            }
            renderTime.Stop();
            totalTime.Stop();
            Console.WriteLine("Variation: '{8}', Time/Run: {7}ms, Runs: {0}x, Model Depth: {1}, SubstitutionCount: {2}, Template Size: {3}, ParseTime: {4}, RenderTime: {5}, Total Time: {6}",
                runs, modelDepth, inserts, sizeOfTemplate, parseTime.Elapsed, renderTime.Elapsed, totalTime.Elapsed, totalTime.ElapsedMilliseconds / (double)runs, variation);
        }

        private Tuple<Dictionary<string, object>, string> ConstructModelAndPath(int modelDepth, string path = null)
        {
            path = Guid.NewGuid().ToString("n");
            var model = new Dictionary<string, object>();

            if (modelDepth > 1)
            {
                var child = ConstructModelAndPath(modelDepth - 1, path);
                model[path] = child.Item1;
                path = path + "." + child.Item2;
            }

            return Tuple.Create(model, path);
        }

    }
}
