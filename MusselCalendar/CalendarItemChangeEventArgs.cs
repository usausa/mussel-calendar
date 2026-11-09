namespace MusselCalendar;

public sealed class CalendarItemChangeEventArgs : EventArgs
{
    public CalendarItemChangeEventArgs(CalendarItem item, DateTime start, DateTime until, bool allDay)
    {
        Item = item;
        Start = start;
        End = until;
        AllDay = allDay;
    }

    public CalendarItem Item { get; }

    public DateTime Start { get; }

    public DateTime End { get; }

    public bool AllDay { get; }
}
