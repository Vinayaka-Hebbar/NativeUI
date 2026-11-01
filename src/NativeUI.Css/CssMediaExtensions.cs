namespace NativeUI.Css;

/// <summary>
/// Provides extension methods for evaluating <see cref="CssMedia"/> conditions.
/// </summary>
public static class CssMediaExtensions
{
    /// <summary>
    /// Determines whether the media query condition matches the specified color scheme condition.
    /// </summary>
    /// <param name="media">The media query condition.</param>
    /// <param name="colorScheme">The active color scheme condition.</param>
    /// <returns><see langword="true"/> if the media condition matches; otherwise, <see langword="false"/>.</returns>
    public static bool Matches(this CssMedia media, CssColorSchemeCondition colorScheme)
    {
        return media switch
        {
            CssMedia m when m == CssMedia.None => true,
            CssMedia m when m == CssMedia.Dark => colorScheme == CssColorSchemeCondition.Dark,
            CssMedia m when m == CssMedia.Light => colorScheme == CssColorSchemeCondition.Light,
            _ => false
        };
    }
}
