using Scriban;
using Scriban.Runtime;

namespace RedShirt.Example.Api.Scaffolding.Rendering;

/// <summary>
///     Renders the Scriban templates under Templates/ (copied beside the tool's binary at build time).
/// </summary>
internal sealed class TemplateRenderer(string templateDirectory)
{
    private readonly Dictionary<string, Template> _cache = new(StringComparer.Ordinal);

    public static TemplateRenderer Default => new(Path.Combine(AppContext.BaseDirectory, "Templates"));

    public string Render(string templatePath, ResourceModel model)
    {
        var template = Load(templatePath);

        var globals = new ScriptObject();
        globals.Import(typeof(TemplateFunctions));
        globals.SetValue("model", model, true);

        var context = new TemplateContext {StrictVariables = true};
        context.PushGlobal(globals);

        return template.Render(context).Replace("\r\n", "\n");
    }

    private Template Load(string templatePath)
    {
        if (_cache.TryGetValue(templatePath, out var cached))
        {
            return cached;
        }

        var path = Path.Combine(templateDirectory, templatePath);
        var template = Template.Parse(File.ReadAllText(path), path);
        if (template.HasErrors)
        {
            throw new InvalidOperationException(
                $"Template '{templatePath}' has errors:{Environment.NewLine}{string.Join(Environment.NewLine, template.Messages)}");
        }

        _cache[templatePath] = template;
        return template;
    }

    /// <summary>
    ///     Functions callable from templates (snake_case, e.g. <c>usings</c>).
    /// </summary>
    private static class TemplateFunctions
    {
        /// <summary>
        ///     Sorted, de-duplicated using directives followed by a blank line, or "" when there are none.
        /// </summary>
        public static string Usings(IEnumerable<object> namespaces)
        {
            var lines = namespaces
                .Select(ns => ns.ToString()!)
                .Where(ns => ns.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .Select(ns => $"using {ns};\n")
                .ToList();

            return lines.Count == 0 ? "" : string.Concat(lines) + "\n";
        }
    }
}
