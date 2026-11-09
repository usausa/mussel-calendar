namespace MusselCalendar;

public sealed class CalendarMoreEventArgs : EventArgs
{
    public CalendarMoreEventArgs(DateOnly day, IReadOnlyList<CalendarItem> items, CalendarAnchor anchor)
    {
        Day = day;
        Items = items;
        Anchor = anchor;
    }

    public DateOnly Day { get; }

    public IReadOnlyList<CalendarItem> Items { get; }

    public CalendarAnchor Anchor { get; }
}
