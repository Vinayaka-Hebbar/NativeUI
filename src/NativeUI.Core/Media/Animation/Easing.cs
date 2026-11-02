using System.Globalization;

namespace NativeUI.Media.Animation;

/// <summary>
/// Represents a CSS timing function as the control points of a cubic Bezier curve from (0, 0) to (1, 1).
/// </summary>
/// <param name="X1">The horizontal position of the first control point, in the range [0, 1].</param>
/// <param name="Y1">The vertical position of the first control point.</param>
/// <param name="X2">The horizontal position of the second control point, in the range [0, 1].</param>
/// <param name="Y2">The vertical position of the second control point.</param>
public readonly record struct Easing(double X1, double Y1, double X2, double Y2)
{
    private const int NewtonIterations = 8;
    private const double SolveTolerance = 1e-5;

    /// <summary>Gets the CSS <c>linear</c> timing function.</summary>
    public static Easing Linear { get; } = new(0, 0, 1, 1);

    /// <summary>Gets the CSS <c>ease</c> timing function.</summary>
    public static Easing Ease { get; } = new(0.25, 0.1, 0.25, 1);

    /// <summary>Gets the CSS <c>ease-in</c> timing function.</summary>
    public static Easing EaseIn { get; } = new(0.42, 0, 1, 1);

    /// <summary>Gets the CSS <c>ease-out</c> timing function.</summary>
    public static Easing EaseOut { get; } = new(0, 0, 0.58, 1);

    /// <summary>Gets the CSS <c>ease-in-out</c> timing function.</summary>
    public static Easing EaseInOut { get; } = new(0.42, 0, 0.58, 1);

    /// <summary>
    /// Returns the eased progress for <paramref name="progress"/>, which is limited to the range [0, 1].
    /// </summary>
    public double Evaluate(double progress)
    {
        double x = Math.Clamp(progress, 0, 1);
        if (x is 0 or 1 || (X1 == Y1 && X2 == Y2))
        {
            return x;
        }

        return Sample(Y1, Y2, SolveCurveParameter(x));
    }

    /// <summary>
    /// Formats this timing function as a CSS <c>cubic-bezier()</c> value.
    /// </summary>
    public string ToCssString()
    {
        return string.Create(CultureInfo.InvariantCulture, $"cubic-bezier({X1}, {Y1}, {X2}, {Y2})");
    }

    /// <summary>
    /// Finds the curve parameter whose horizontal position is <paramref name="x"/>, using Newton's method and then bisection
    /// where the curve is too flat for it to converge.
    /// </summary>
    private double SolveCurveParameter(double x)
    {
        double t = x;
        for (int i = 0; i < NewtonIterations; i++)
        {
            double error = Sample(X1, X2, t) - x;
            if (Math.Abs(error) < SolveTolerance)
            {
                return t;
            }

            double slope = SampleDerivative(X1, X2, t);
            if (Math.Abs(slope) < SolveTolerance)
            {
                break;
            }

            t -= error / slope;
        }

        double low = 0;
        double high = 1;
        t = x;
        while (high - low > SolveTolerance)
        {
            if (Sample(X1, X2, t) < x)
            {
                low = t;
            }
            else
            {
                high = t;
            }

            t = (low + high) / 2;
        }

        return t;
    }

    private static double Sample(double first, double second, double t)
    {
        double inverse = 1 - t;
        return (3 * inverse * inverse * t * first) + (3 * inverse * t * t * second) + (t * t * t);
    }

    private static double SampleDerivative(double first, double second, double t)
    {
        double inverse = 1 - t;
        return (3 * inverse * inverse * first) + (6 * inverse * t * (second - first)) + (3 * t * t * (1 - second));
    }
}
