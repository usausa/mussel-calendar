namespace MusselCalendar.Internal;

internal static class CalendarColor
{
    public static bool IsSafe(string value)
    {
        if ((value.Length is 0 or > 64) || value.Contains("url(", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        foreach (var ch in value)
        {
            if (!Char.IsAsciiLetterOrDigit(ch) && (ch is not ('#' or '(' or ')' or ',' or '.' or '%' or ' ' or '-' or '/')))
            {
                return false;
            }
        }

        return true;
    }

    public static string? DateStyle(CalendarDayMark? mark) => (mark?.Color is { } color) && IsSafe(color) ? $"color: {color}" : null;
}
