using System.Globalization;

namespace NativeUI.Media;

internal sealed record PathCommand(char Kind, double[] Values);

internal static class PathDataParser
{
    public static IReadOnlyList<PathCommand> Parse(string text)
    {
        Reader reader = new(text);
        List<PathCommand> result = [];
        double x = 0, y = 0, startX = 0, startY = 0, controlX = 0, controlY = 0;
        char command = '\0', previous = '\0';
        while (reader.HasMore)
        {
            if (reader.IsCommand)
            {
                command = reader.ReadCommand();
            }
            else if (command == '\0' || char.ToUpperInvariant(command) == 'Z')
            {
                throw new FormatException("A path command is required.");
            }

            bool relative = char.IsLower(command);
            char kind = char.ToUpperInvariant(command);
            if (result.Count == 0 && kind != 'M')
            {
                throw new FormatException("A path must begin with MoveTo.");
            }

            double offsetX = relative ? x : 0;
            double offsetY = relative ? y : 0;
            double[] values;
            switch (kind)
            {
                case 'Z':
                    values = [];
                    x = startX;
                    y = startY;
                    break;
                case 'M':
                case 'L':
                case 'T':
                    double endX = reader.Number() + offsetX;
                    double endY = reader.Number() + offsetY;
                    if (kind == 'T')
                    {
                        double cx = previous is 'Q' or 'T' ? 2 * x - controlX : x;
                        double cy = previous is 'Q' or 'T' ? 2 * y - controlY : y;
                        values = [cx, cy, endX, endY];
                        controlX = cx;
                        controlY = cy;
                    }
                    else
                    {
                        values = [endX, endY];
                    }

                    x = endX;
                    y = endY;
                    if (kind == 'M')
                    {
                        startX = x;
                        startY = y;
                        command = relative ? 'l' : 'L';
                    }
                    break;
                case 'H':
                    x = reader.Number() + offsetX;
                    values = [x, y];
                    break;
                case 'V':
                    y = reader.Number() + offsetY;
                    values = [x, y];
                    break;
                case 'Q':
                    controlX = reader.Number() + offsetX;
                    controlY = reader.Number() + offsetY;
                    x = reader.Number() + offsetX;
                    y = reader.Number() + offsetY;
                    values = [controlX, controlY, x, y];
                    break;
                case 'C':
                case 'S':
                    double firstX = kind == 'C' ? reader.Number() + offsetX : previous is 'C' or 'S' ? 2 * x - controlX : x;
                    double firstY = kind == 'C' ? reader.Number() + offsetY : previous is 'C' or 'S' ? 2 * y - controlY : y;
                    controlX = reader.Number() + offsetX;
                    controlY = reader.Number() + offsetY;
                    x = reader.Number() + offsetX;
                    y = reader.Number() + offsetY;
                    values = [firstX, firstY, controlX, controlY, x, y];
                    break;
                case 'A':
                    double rx = reader.Number();
                    double ry = reader.Number();
                    if (rx < 0 || ry < 0)
                    {
                        throw new FormatException("Arc radii must not be negative.");
                    }
                    double rotation = reader.Number();
                    double large = reader.Flag();
                    double sweep = reader.Flag();
                    x = reader.Number() + offsetX;
                    y = reader.Number() + offsetY;
                    values = [rx, ry, rotation, large, sweep, x, y];
                    break;
                default:
                    throw new FormatException($"Unsupported path command '{command}'.");
            }

            result.Add(new PathCommand(kind switch { 'H' or 'V' => 'L', 'T' => 'Q', 'S' => 'C', _ => kind }, values));
            previous = kind;
            if (result.Count > 100_000)
            {
                throw new FormatException("A path exceeds 100,000 commands.");
            }
        }

        return result;
    }

    private sealed class Reader(string text)
    {
        private int _position;
        public bool HasMore { get { Skip(); return _position < text.Length; } }
        public bool IsCommand => HasMore && char.IsAsciiLetter(text[_position]);
        public char ReadCommand() => text[_position++];

        public double Flag()
        {
            Skip();
            if (_position >= text.Length || text[_position] is not ('0' or '1'))
            {
                throw new FormatException("An arc flag must be 0 or 1.");
            }
            return text[_position++] - '0';
        }

        public double Number()
        {
            Skip();
            int start = _position;
            if (_position < text.Length && text[_position] is '+' or '-') _position++;
            while (_position < text.Length && char.IsAsciiDigit(text[_position])) _position++;
            if (_position < text.Length && text[_position] == '.')
            {
                _position++;
                while (_position < text.Length && char.IsAsciiDigit(text[_position])) _position++;
            }
            if (_position < text.Length && text[_position] is 'e' or 'E')
            {
                _position++;
                if (_position < text.Length && text[_position] is '+' or '-') _position++;
                while (_position < text.Length && char.IsAsciiDigit(text[_position])) _position++;
            }
            if (!double.TryParse(text.AsSpan(start, _position - start), NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || !double.IsFinite(value) || Math.Abs(value) > 1e9)
            {
                throw new FormatException($"Invalid or excessive path coordinate at offset {start}.");
            }
            return value;
        }

        private void Skip()
        {
            while (_position < text.Length && (char.IsWhiteSpace(text[_position]) || text[_position] == ',')) _position++;
        }
    }
}
