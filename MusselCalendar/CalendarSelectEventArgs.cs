namespace MusselCalendar;

public sealed class CalendarSelectEventArgs : EventArgs
{
    public CalendarSelectEventArgs(DateTime start, DateTime until, bool allDay, CalendarAnchor anchor)
    {
        Start = start;
        End = until;
        AllDay = allDay;
        Anchor = anchor;
    }

    public DateTime Start { get; }

    public DateTime End { get; }

    public bool AllDay { get; }

    public CalendarAnchor Anchor { get; }
}
