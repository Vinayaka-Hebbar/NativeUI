namespace NativeUI.Core;

/// <summary>
/// Describes a validation failure reported by a property binding.
/// </summary>
/// <param name="ErrorContent">The provider's error object or message.</param>
/// <param name="TargetProperty">The specific target binding property that produced this error.</param>
/// <param name="Kind">Distinguishes model/provider errors from binding conversion failures.</param>
/// <param name="PropertyName">The source property, or null for an entity-level error.</param>
public sealed record ValidationError(object ErrorContent, string? PropertyName = null, ValidationErrorKind Kind = ValidationErrorKind.Data, BindableProperty? TargetProperty = null)
{

    /// <summary>
    /// Gets the provider's display message.
    /// </summary>
    public string ErrorMessage => ErrorContent.ToString() ?? string.Empty;
}
