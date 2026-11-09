namespace MusselCalendar;

public sealed class CalendarItemEventArgs : EventArgs
{
    public CalendarItemEventArgs(CalendarItem item, CalendarAnchor anchor)
    {
        Item = item;
        Anchor = anchor;
    }

    public CalendarItem Item { get; }

    public CalendarAnchor Anchor { get; }
}
