using System.Text.RegularExpressions;

namespace Lighthouse.Utils;

/// <summary>
/// Validates compose project names before they are passed to the docker CLI as
/// <c>-p "{projectName}"</c>. Although commands run with UseShellExecute=false (so there
/// is no shell to inject into), an unvalidated name containing a quote or whitespace can
/// still break argument parsing or target an unintended project. Names are restricted to
/// an allowlist of safe characters.
/// </summary>
public static partial class ComposeProjectNameValidator
{
    // Must start with an alphanumeric, then allow alphanumerics plus _ . -
    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9_.-]*$")]
    private static partial Regex ValidPattern();

    public const int MaxLength = 255;

    public static bool IsValid(string? projectName)
    {
        return !string.IsNullOrWhiteSpace(projectName)
            && projectName.Length <= MaxLength
            && ValidPattern().IsMatch(projectName);
    }
}
