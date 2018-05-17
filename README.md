# Mustachio.Rtf
A Lightweight, powerful, flavorful, templating engine for C# and other .net-based languages.

#### What's this for?

*Mustachio.Rtf* allows you to create simple text-based templates that are fast and safe to render. It's based on [Mustachio] project.

#### How to use Mustachio:

```csharp
// Parse the template:
var sourceTemplate = "Dear [[name]], this is definitely a personalized note to you. Very truly yours, [[sender]]"
var template = Mustachio.Parser.Parse(sourceTemplate);

// Create the values for the template model:
dynamic model = new ExpandoObject();
model.name = "John";
model.sender = "Sally";

// Combine the model with the template to get content:
var content = template(model);
```

#### Installing Mustachio.Rtf:

Mustachio.Rtf can be installed via [NuGet](https://www.nuget.org/packages/Mustachio.Rtf/):

```bash
Install-Package Mustachio.Rtf
```

##### Key differences between Mustachio.Rtf and [Mustachio]

Mustachio contains a few modifications to the core Mustache language that are important.

1. Escape char replaced from `{` to `[`
2. Characted escaping now based on rtf rules, rather then html
3. Added support to value formating from @JPVenson patch

[Mustachio]: https://github.com/wildbit/mustachio
