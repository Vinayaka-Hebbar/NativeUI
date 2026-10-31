using System.Globalization;
using NativeUI.Layout;
using NativeUI.Tests;

namespace NativeUI.Core.Tests;

/// <summary>
/// Verifies runtime Unicode lookup behavior independently of the generator's packed lookup implementation.
/// </summary>
internal static class UnicodeDataTablesTests
{
    internal static void VerifyPrivateUseDefaults()
    {
        (int Start, int End)[] ranges = [(0xE000, 0xF8FF), (0xF0000, 0xFFFFD), (0x100000, 0x10FFFD)];
        foreach ((int start, int end) in ranges)
        {
            for (int codePoint = start; codePoint <= end; codePoint++)
            {
                Assert.Equal(BidiClass.L, UnicodeDataTables.GetBidiClass(codePoint));
                Assert.Equal((byte)0, UnicodeDataTables.GetLineBreakProperty(codePoint));
                Assert.Equal((byte)0, UnicodeDataTables.GetWordBreakProperty(codePoint));
            }
        }

        Assert.Equal(BidiClass.R, UnicodeDataTables.GetBidiClass(0x0590));
        Assert.Equal(BidiClass.AL, UnicodeDataTables.GetBidiClass(0x086F));
        Assert.Equal(BidiClass.ET, UnicodeDataTables.GetBidiClass(0x20CF));
        Assert.Equal(BidiClass.BN, UnicodeDataTables.GetBidiClass(0x10FFFF));
        Assert.Equal((byte)10, UnicodeDataTables.GetLineBreakProperty('\r'));
        Assert.Equal((byte)17, UnicodeDataTables.GetLineBreakProperty('\n'));
        Assert.Equal((byte)8, UnicodeDataTables.GetWordBreakProperty('\r'));
        Assert.Equal((byte)10, UnicodeDataTables.GetWordBreakProperty('\n'));
    }

    internal static void VerifyBracketMappings()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Fixtures/Unicode/BidiBrackets.txt");
        int bracketCount = 0;
        foreach (string rawLine in File.ReadLines(path))
        {
            string line = rawLine.Split('#', 2)[0].Trim();
            if (line.Length == 0)
            {
                continue;
            }

            string[] fields = line.Split(';', StringSplitOptions.TrimEntries);
            int codePoint = int.Parse(fields[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            int partner = int.Parse(fields[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            bool isOpen = fields[2] == "o";

            Assert.Equal(isOpen, UnicodeDataTables.TryGetMatchingClosingBracket(codePoint, out int actualPartner));
            Assert.Equal(!isOpen, UnicodeDataTables.IsClosingBracket(codePoint));
            if (isOpen)
            {
                Assert.Equal(partner, actualPartner);
            }

            bracketCount++;
        }

        Assert.Equal(128, bracketCount);
        Assert.False(UnicodeDataTables.IsClosingBracket('A'));
        Assert.False(UnicodeDataTables.IsClosingBracket(-1));
        Assert.False(UnicodeDataTables.IsClosingBracket(0x110000));
    }
}
