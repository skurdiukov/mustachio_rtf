using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;

using FluentAssertions;

using NUnit.Framework;

namespace Mustachio.Rtf.Tests
{
    [TestFixture]
    public class TemplateFixture
    {
        [DatapointSource]
        public int[] Sizes = { 200, 80_000, 200_000 };

        [Theory]
        public void TemplateMaxSizeLimit(int maxSize)
        {
            var tempdata = new List<string>();
            var sizeOfOneChar = ParserFixture.DefaultEncoding.GetByteCount(" ");
            for (var i = 0; i < maxSize / sizeOfOneChar; i++)
            {
                tempdata.Add(" ");
            }

            var template = "[[#each Data]][[?]][[/each]]";
            var templateFunc = Parser.ParseWithOptions(new ParserOptions(template, null, ParserFixture.DefaultEncoding, maxSize));
            var templateStream = templateFunc.ParsedTemplate(new Dictionary<string, object>
            {
                {"Data", tempdata}
            });

            templateStream.Length.Should().Be(maxSize);
        }

        [Theory]
        public void TemplateMaxSizeOverLimit(int maxSize)
        {
            var tempdata = new List<string>();
            var sizeOfOneChar = ParserFixture.DefaultEncoding.GetByteCount(" ");
            for (var i = 0; i < maxSize * sizeOfOneChar; i++)
            {
                tempdata.Add(" ");
            }

            var template = "[[#each Data]][[?]][[/each]]";
            var templateFunc = Parser.ParseWithOptions(new ParserOptions(template, null, ParserFixture.DefaultEncoding, maxSize));
            var templateStream = templateFunc.ParsedTemplate(new Dictionary<string, object>
            {
                {"Data", tempdata}
            });

            templateStream.Length.Should().Be(maxSize);
        }

        [Test]
        public void TemplateRendersContentWithNoVariables()
        {
            // arrange
            const string PlainText = "ASDF";
            var template = Parser.ParseWithOptions(new ParserOptions("ASDF", null, ParserFixture.DefaultEncoding));

            // act
            var rendered = template.ParsedTemplate(null).Stringify(true, ParserFixture.DefaultEncoding);

            // assert
            rendered.Should().Be(PlainText);
        }

        [Test]
        public void RtfIsNotEscapedWhenUsingUnsafeSyntaxes()
        {
            var model = new Dictionary<string, object>
            {
                ["stuff"] = "{inner}",
            };

            var plainText = @"[[[stuff]]]";
            var rendered = Parser.ParseWithOptions(new ParserOptions(plainText, null, ParserFixture.DefaultEncoding)).ParsedTemplate(model).Stringify(true, ParserFixture.DefaultEncoding);

            rendered.Should().Be("{inner}");

            plainText = @"[[&stuff]]";
            rendered = Parser.ParseWithOptions(new ParserOptions(plainText, null, ParserFixture.DefaultEncoding)).ParsedTemplate(model).Stringify(true, ParserFixture.DefaultEncoding);
            rendered.Should().Be("{inner}");
        }

        [Test]
        public void RtfIsEscapedByDefault()
        {
            // arrange
            const string PlainText = "[[stuff]]";
            var model = new Dictionary<string, object>
            {
                ["stuff"] = "{inner}"
            };

            // act
            var rendered = Parser.ParseWithOptions(new ParserOptions(PlainText, null, ParserFixture.DefaultEncoding)).ParsedTemplate(model).Stringify(true, ParserFixture.DefaultEncoding);

            // assert
            rendered.Should().Be(@"\'7binner\'7d");
        }

        [Test]
        public void CommentsAreExcludedFromOutput()
        {
            var model = new Dictionary<string, object>();

            var plainText = @"as[[!stu
            ff]]df";
            var rendered = Parser.ParseWithOptions(new ParserOptions(plainText, null, ParserFixture.DefaultEncoding)).ParsedTemplate(model).Stringify(true, ParserFixture.DefaultEncoding);

            rendered.Should().Be("asdf");
        }

        [Test]
        public void NegationGroupRendersContentWhenValueNotSet()
        {
            var model = new Dictionary<string, object>();

            var plainText = @"[[^stuff]]No Stuff Here.[[/stuff]]";
            var rendered = Parser.ParseWithOptions(new ParserOptions(plainText, null, ParserFixture.DefaultEncoding)).ParsedTemplate(model).Stringify(true, ParserFixture.DefaultEncoding);

            Assert.AreEqual("No Stuff Here.", rendered);
        }

        [Test]
        public void CommentShouldNotRendered()
        {
            var model = new Dictionary<string, object>();
            var plainText = @"[[! This is comment ]]";

            var rendered = Parser.Parse(plainText).ParsedTemplate(model).Stringify();

            Assert.AreEqual(string.Empty, rendered);
        }


        [Test]
        public void TemplateRendersWithComplexEachPath()
        {
            var template = @"[[#each Company.ceo.products]]<li>[[ name ]] and [[version]] and has a CEO: [[../../last_name]]</li>[[/each]]";

            var parsedTemplate = Parser.ParseWithOptions(new ParserOptions(template, null, ParserFixture.DefaultEncoding));

            var model = new Dictionary<string, object>();

            var company = new Dictionary<string, object>();
            model["Company"] = company;

            var ceo = new Dictionary<string, object>();
            company["ceo"] = ceo;
            ceo["last_name"] = "Smith";

            var products = Enumerable.Range(0, 3).Select(k =>
            {
                var r = new Dictionary<String, object>();
                r["name"] = "name " + k;
                r["version"] = "version " + k;
                return r;
            }).ToArray();

            ceo["products"] = products;

            var result = parsedTemplate.ParsedTemplate(model).Stringify(true, ParserFixture.DefaultEncoding);

            Assert.AreEqual("<li>name 0 and version 0 and has a CEO: Smith</li>" +
                "<li>name 1 and version 1 and has a CEO: Smith</li>" +
                "<li>name 2 and version 2 and has a CEO: Smith</li>", result);
        }

        [Test]
        public void TemplateShouldProcessVariablesInInvertedGroup()
        {
            var model = new Dictionary<string, object>
            {
                { "not_here" , false },
                { "placeholder" , "a placeholder value" }
            };

            var template = "[[^not_here]][[../placeholder]][[/not_here]]";

            var result = Parser.ParseWithOptions(new ParserOptions(template, null, ParserFixture.DefaultEncoding)).ParsedTemplate(model).Stringify(true, ParserFixture.DefaultEncoding);

            Assert.AreEqual("a placeholder value", result);
        }

        [TestCase(new int[] { })]
        [TestCase(false)]
        [TestCase("")]
        [TestCase(0.0)]
        [TestCase(0)]
        public void TemplatesShouldNotRenderFalseyComplexStructures(object falseyModelValue)
        {
            var model = new Dictionary<string, object>
            {
                { "outer_level", falseyModelValue}
            };

            var template = "[[#outer_level]]Shouldn't be rendered![[inner_level]][[/outer_level]]";

            var result = Parser.ParseWithOptions(new ParserOptions(template, null, ParserFixture.DefaultEncoding)).ParsedTemplate(model).Stringify(true, ParserFixture.DefaultEncoding);

            Assert.AreEqual(String.Empty, result);
        }

        [TestCase(new int[] { })]
        [TestCase(false)]
        [TestCase("")]
        [TestCase(0.0)]
        [TestCase(0)]
        public void TemplateShouldTreatFalseyValuesAsEmptyArray(object falseyModelValue)
        {
            var model = new Dictionary<String, object>
            {
                { "locations", falseyModelValue}
            };

            var template = "[[#each locations]]Shouldn't be rendered![[/each]]";

            var result = Parser.ParseWithOptions(new ParserOptions(template, null, ParserFixture.DefaultEncoding)).ParsedTemplate(model).Stringify(true, ParserFixture.DefaultEncoding);

            Assert.AreEqual(String.Empty, result);
        }

        [TestCase(0)]
        [TestCase(0.0)]
        public void TemplateShouldRenderZeroValue(object value)
        {
            var model = new Dictionary<String, object>
            {
                { "times_won", value}
            };

            var template = "You've won [[times_won]] times!";

            var result = Parser.ParseWithOptions(new ParserOptions(template, null, ParserFixture.DefaultEncoding)).ParsedTemplate(model).Stringify(true, ParserFixture.DefaultEncoding);

            Assert.AreEqual("You've won 0 times!", result);
        }

        [Test]
        public void TemplateShouldRenderFalseValue()
        {
            var model = new Dictionary<string, object>
            {
                { "times_won", false}
            };

            var template = "You've won [[times_won]] times!";

            var result = Parser.ParseWithOptions(new ParserOptions(template, null, ParserFixture.DefaultEncoding)).ParsedTemplate(model).Stringify(true, ParserFixture.DefaultEncoding);

            Assert.AreEqual("You've won False times!", result);
        }

        [Test]
        public void TemplateShouldNotRenderNullValue()
        {
            var model = new Dictionary<string, object>
            {
                { "times_won", null}
            };

            var template = "You've won [[times_won]] times!";

            var result = Parser.ParseWithOptions(new ParserOptions(template, null, ParserFixture.DefaultEncoding)).ParsedTemplate(model).Stringify(true, ParserFixture.DefaultEncoding);

            Assert.AreEqual("You've won  times!", result);
        }

        [Test]
        public void TemplateWithDateFormat()
        {
            var model = new Dictionary<string, object>
            {
                { "date", new DateTime(2018, 1, 31) },
            };

            var template = "Date: [[date(dd.MM.yyyy)]]";

            var result = Parser.Parse(template).ParsedTemplate(model).Stringify();

            Assert.AreEqual("Date: 31.01.2018", result);
        }

        [Test]
        public void TemplateWithNestedObjectAndFormat()
        {
            dynamic model = new ExpandoObject();
            model.obj = new ExpandoObject();
            model.obj.date = new DateTime(2018, 1, 31);

            var template = "Date: [[#obj]][[date(dd.MM.yyyy)]][[/obj]]";

            var result = Parser.Parse(template).ParsedTemplate((IDictionary<string, object>)model).Stringify();

            Assert.AreEqual("Date: 31.01.2018", result);
        }

        [Test]
        public void TemplateWithNestedObjectAndFormat2()
        {
            var model = new Dictionary<string, object>
            {
                { "obj", new { date = new DateTime(2018, 1, 31) } },
                { "obj2", "." },
            };

            var template = "Date: [[obj.date(dd.MM.yyyy)]][[obj2]]";

            var result = Parser.Parse(template).ParsedTemplate(model).Stringify();

            Assert.AreEqual("Date: 31.01.2018.", result);
        }
    }
}
