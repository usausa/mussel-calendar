namespace MusselCalendar;

public sealed class CalendarItem
{
    public required string Id { get; init; }

    public required string Title { get; set; }

    public DateTime Start { get; set; }

    public DateTime End { get; set; }

    public bool AllDay { get; set; }

    public string? Color { get; set; }

    public bool ReadOnly { get; set; }
}
