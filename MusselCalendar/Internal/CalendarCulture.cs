namespace MusselCalendar.Internal;

using System.Globalization;

internal static class CalendarCulture
{
    public static CultureInfo Gregorian(CultureInfo culture)
    {
        if (culture.DateTimeFormat.Calendar is GregorianCalendar)
        {
            return culture;
        }

        var calendar = culture.OptionalCalendars.OfType<GregorianCalendar>().FirstOrDefault();
        if (calendar is null)
        {
            return culture;
        }

        var clone = (CultureInfo)culture.Clone();
        clone.DateTimeFormat.Calendar = calendar;
        return clone;
    }

    public static bool IsDayFirst(CultureInfo culture)
    {
        var pattern = culture.DateTimeFormat.ShortDatePattern;
        var quote = '\0';
        for (var i = 0; i < pattern.Length; i++)
        {
            var c = pattern[i];
            if (quote != '\0')
            {
                if (c == quote)
                {
                    quote = '\0';
                }
            }
            else if (c is '\'' or '"')
            {
                quote = c;
            }
            else if (c == '\\')
            {
                i++;
            }
            else if (c is 'd' or 'M')
            {
                return c == 'd';
            }
        }

        return false;
    }
}
