namespace NativeUI.Tests;

internal static class Assert
{
    public static void True(bool condition, string? message = null)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message ?? "Expected condition to be true, but was false.");
        }
    }

    public static void False(bool condition, string? message = null)
    {
        if (condition)
        {
            throw new InvalidOperationException(message ?? "Expected condition to be false, but was true.");
        }
    }

    public static void Equal<T>(T expected, T actual, string? message = null)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException(
                message ?? $"Expected <{expected}>, but actual was <{actual}>.");
        }
    }

    public static void NotNull(object? value, string? message = null)
    {
        if (value is null)
        {
            throw new InvalidOperationException(message ?? "Expected value to be non-null.");
        }
    }

    public static void Null(object? value, string? message = null)
    {
        if (value is not null)
        {
            throw new InvalidOperationException(message ?? $"Expected null, but was <{value}>.");
        }
    }

    public static void Same(object expected, object actual, string? message = null)
    {
        if (!ReferenceEquals(expected, actual))
        {
            throw new InvalidOperationException(message ?? "Expected references to point to the same instance.");
        }
    }

    public static void Contains(string expectedSubstring, string actualText, string? message = null)
    {
        if (!actualText.Contains(expectedSubstring, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                message ?? $"Expected string to contain '{expectedSubstring}', but actual was:\n{actualText}");
        }
    }
}

internal static class TestRunner
{
    public static int RunSuite(string suiteName, IReadOnlyList<(string Name, Action Test)> tests)
    {
        int passed = 0;
        int failed = 0;

        Console.WriteLine($"Running {suiteName} ({tests.Count} tests)...");
        foreach ((string name, Action test) in tests)
        {
            try
            {
                test();
                passed++;
                Console.WriteLine($"  [PASS] {name}");
            }
            catch (Exception ex)
            {
                failed++;
                Console.Error.WriteLine($"  [FAIL] {name}: {ex.Message}");
            }
        }

        Console.WriteLine($"{suiteName}: {passed} passed, {failed} failed.");
        return failed == 0 ? 0 : 1;
    }

    public static string ReadFixture(string relativeFileName)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", relativeFileName);
        return File.ReadAllText(path);
    }
}
