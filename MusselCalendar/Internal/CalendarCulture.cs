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
}
