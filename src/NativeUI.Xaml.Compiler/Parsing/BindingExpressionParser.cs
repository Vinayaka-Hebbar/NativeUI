namespace NativeUI.Xaml.Compiler;

/// <summary>
/// Parses XAML <c>{Binding ...}</c> markup extension expressions into <see cref="BindingExpressionNode"/> AST nodes.
/// </summary>
public static class BindingExpressionParser
{
    /// <summary>
    /// Determines whether an attribute string is a markup extension enclosed in <c>{...}</c>.
    /// </summary>
    public static bool IsMarkupExtension(string value)
    {
        string trimmed = value.Trim();
        return trimmed.StartsWith('{') && trimmed.EndsWith('}') && !trimmed.StartsWith("{}", StringComparison.Ordinal);
    }

    /// <summary>
    /// Attempts to parse a <c>{Binding ...}</c> expression into a <see cref="BindingExpressionNode"/>.
    /// </summary>
    public static bool TryParse(
        string rawValue,
        XamlSourcePosition position,
        out BindingExpressionNode? expression,
        out string? errorMessage)
    {
        expression = null;
        errorMessage = null;

        string trimmed = rawValue.Trim();
        if (!IsMarkupExtension(trimmed))
        {
            errorMessage = "Binding expression must be enclosed in '{' and '}'.";
            return false;
        }

        string inner = trimmed[1..^1].Trim();
        if (!inner.StartsWith("Binding", StringComparison.Ordinal))
        {
            errorMessage = $"Unsupported markup extension '{inner}'. Expected '{{Binding ...}}'.";
            return false;
        }

        string argsText = inner["Binding".Length..].Trim();
        if (string.IsNullOrEmpty(argsText))
        {
            errorMessage = "Binding expression requires a property path.";
            return false;
        }

        string? path = null;
        string mode = "OneWay";

        string[] parts = argsText.Split(',', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            string clause = parts[i].Trim();
            int eqIndex = clause.IndexOf('=');
            if (eqIndex < 0)
            {
                if (i == 0 && path is null)
                {
                    path = clause;
                }
                else
                {
                    errorMessage = $"Invalid binding clause '{clause}'.";
                    return false;
                }
            }
            else
            {
                string key = clause[..eqIndex].Trim();
                string val = clause[(eqIndex + 1)..].Trim();
                if (string.Equals(key, "Path", StringComparison.Ordinal))
                {
                    path = val;
                }
                else if (string.Equals(key, "Mode", StringComparison.Ordinal))
                {
                    if (val is not ("OneWay" or "TwoWay" or "OneTime"))
                    {
                        errorMessage = $"Unsupported BindingMode '{val}'. Allowed values: OneWay, TwoWay, OneTime.";
                        return false;
                    }

                    mode = val;
                }
                else
                {
                    errorMessage = $"Unknown Binding parameter '{key}'.";
                    return false;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(path) || !IsValidPropertyPath(path))
        {
            errorMessage = $"Invalid binding property path '{path}'.";
            return false;
        }

        expression = new BindingExpressionNode(path, mode, position);
        return true;
    }

    private static bool IsValidPropertyPath(string path)
    {
        string[] segments = path.Split('.');
        if (segments.Length == 0)
        {
            return false;
        }

        foreach (string segment in segments)
        {
            if (string.IsNullOrEmpty(segment))
            {
                return false;
            }

            if (!(char.IsLetter(segment[0]) || segment[0] == '_'))
            {
                return false;
            }

            for (int i = 1; i < segment.Length; i++)
            {
                if (!(char.IsLetterOrDigit(segment[i]) || segment[i] == '_'))
                {
                    return false;
                }
            }
        }

        return true;
    }
}
