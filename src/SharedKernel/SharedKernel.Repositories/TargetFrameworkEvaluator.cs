using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace PandaBear.Repositories;

/// <summary>
/// Statically evaluates the <c>TargetFramework</c>, <c>TargetFrameworks</c>, and <c>RollForward</c> properties of a
/// project like a local build that has no global properties and no environment variables.
/// <para>
/// Order: the <c>Directory.Build.props</c> chain, then the project file body, then, for <c>RollForward</c> only, the
/// <c>Directory.Build.targets</c> chain. The chain starts at the nearest such file at or above the project directory.
/// Each file contributes its top-level <c>PropertyGroup</c> elements, in document order, and an <c>Import</c> whose
/// <c>Project</c> value calls <c>GetPathOfFileAbove</c> for the same file name continues with the nearest such file
/// strictly above the importing file's directory, at that point. Every other import, and everything that is not a
/// top-level <c>PropertyGroup</c> or <c>Import</c> such as <c>Choose</c> and <c>Target</c>, is ignored.
/// </para>
/// <para>
/// Property names are case-insensitive. <c>$(Name)</c> becomes the current value, or an empty string when the property
/// is unset. A <c>Condition</c> on a group, a property, or an import is read by <see cref="MsBuildCondition"/>; a
/// property whose condition is false is skipped. A condition that cannot be read skips the assignment, and for
/// <c>TargetFramework</c>, <c>TargetFrameworks</c>, and <c>RollForward</c> it is also reported, as is a value of those
/// properties that still holds <c>$(</c>, <c>@(</c>, or <c>%(</c> after substitution. An import whose condition cannot
/// be read is followed.
/// </para>
/// </summary>
internal sealed partial class TargetFrameworkEvaluator
{
    private const string PropsName = "Directory.Build.props";
    private const string TargetsName = "Directory.Build.targets";

    private readonly MsBuildFileSet _files;
    private readonly bool _checkRollForward;
    private readonly Dictionary<string, string> _properties = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<VerificationIssue> _issues = [];
    private bool _inTargets;
    private string _targetFrameworkFile = string.Empty;

    private TargetFrameworkEvaluator(MsBuildFileSet files, bool checkRollForward)
    {
        _files = files;
        _checkRollForward = checkRollForward;
    }

    /// <summary>
    /// Evaluates a project.
    /// </summary>
    /// <param name="files">The files that the evaluation may read.</param>
    /// <param name="project">The repository-relative path of the project file.</param>
    /// <param name="checkRollForward">Whether <c>RollForward</c> matters, so that an unreadable condition on it is reported.</param>
    public static TargetFrameworkEvaluation Evaluate(MsBuildFileSet files, string project, bool checkRollForward)
    {
        TargetFrameworkEvaluator evaluator = new(files, checkRollForward);
        var directory = Parent(project);
        if (files.FindAbove(directory, PropsName) is { } props)
        {
            evaluator.Process(props);
        }

        evaluator.Process(project);
        var targetFramework = evaluator.Get("TargetFramework");
        var targetFrameworks = evaluator.Get("TargetFrameworks");
        var targetFrameworkFile = evaluator._targetFrameworkFile;
        evaluator._inTargets = true;
        if (files.FindAbove(directory, TargetsName) is { } targets)
        {
            evaluator.Process(targets);
        }

        return new TargetFrameworkEvaluation(targetFramework, targetFrameworks, targetFrameworkFile, evaluator.Get("RollForward"), evaluator._issues);
    }

    /// <summary>
    /// Evaluates the top-level properties of one file, which stands alone: no <c>Directory.Build.props</c> chain is read.
    /// </summary>
    /// <param name="files">The files that the evaluation may read.</param>
    /// <param name="path">The repository-relative path of the file; it must have been read into <paramref name="files"/>.</param>
    /// <returns>The final value of each property, by case-insensitive name.</returns>
    public static IReadOnlyDictionary<string, string> EvaluateProperties(MsBuildFileSet files, string path)
    {
        TargetFrameworkEvaluator evaluator = new(files, checkRollForward: false);
        evaluator.Process(path);
        return evaluator._properties;
    }

    private static string Parent(string path)
    {
        var separator = path.LastIndexOf('/');
        return separator < 0 ? string.Empty : path[..separator];
    }

    private string Get(string name) => _properties.GetValueOrDefault(name, string.Empty);

    private string Substitute(string text) =>
        Reference().Replace(text, match => _properties.GetValueOrDefault(match.Groups[1].Value, string.Empty));

    private bool IsReported(string name) =>
        name.Equals("TargetFramework", StringComparison.OrdinalIgnoreCase)
        || name.Equals("TargetFrameworks", StringComparison.OrdinalIgnoreCase)
        || (_checkRollForward && name.Equals("RollForward", StringComparison.OrdinalIgnoreCase));

    private bool? Condition(XElement element) =>
        MsBuildCondition.Evaluate((string?)element.Attribute("Condition"), Substitute);

    private void Process(string path)
    {
        var root = _files.Find(path, out var failure);
        if (root is null)
        {
            _issues.Add(new VerificationIssue(path, failure ?? "Is missing from the working tree."));
            return;
        }

        foreach (var element in root.Elements())
        {
            switch (element.Name.LocalName)
            {
                case "PropertyGroup":
                    var group = Condition(element);
                    foreach (var property in element.Elements())
                    {
                        Assign(path, property, group);
                    }

                    break;
                case "Import" when Condition(element) is not false:
                    var project = (string?)element.Attribute("Project") ?? string.Empty;
                    if (project.Contains("GetPathOfFileAbove(", StringComparison.OrdinalIgnoreCase))
                    {
                        var name = project.Contains(TargetsName, StringComparison.OrdinalIgnoreCase) ? TargetsName : PropsName;
                        var above = Parent(Parent(path));
                        if (Parent(path).Length > 0 && project.Contains(name, StringComparison.OrdinalIgnoreCase) && _files.FindAbove(above, name) is { } next)
                        {
                            Process(next);
                        }
                    }

                    break;
            }
        }
    }

    private void Assign(string path, XElement property, bool? group)
    {
        var name = property.Name.LocalName;
        if (_inTargets && (name.Equals("TargetFramework", StringComparison.OrdinalIgnoreCase) || name.Equals("TargetFrameworks", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var own = Condition(property);
        if (group is false || own is false)
        {
            return;
        }

        if (group is null || own is null)
        {
            if (IsReported(name))
            {
                _issues.Add(new VerificationIssue(path, $"Cannot evaluate the condition of {name}; the check reads only '…' == '…' conditions."));
            }

            return;
        }

        var value = Substitute(property.Value.Trim());
        if (IsReported(name) && MsBuildCondition.Unresolved(value))
        {
            _issues.Add(new VerificationIssue(path, $"Cannot evaluate the value of {name}; the check reads only text and $(Property) references."));
            return;
        }

        _properties[name] = value;
        if (value.Length > 0 && name.Equals("TargetFramework", StringComparison.OrdinalIgnoreCase))
        {
            _targetFrameworkFile = path;
        }
    }

    [GeneratedRegex(@"\$\(([A-Za-z_][A-Za-z0-9_.\-]*)\)", RegexOptions.CultureInvariant)]
    private static partial Regex Reference();
}
