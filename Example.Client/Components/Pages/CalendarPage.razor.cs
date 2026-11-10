namespace Example.Client.Components.Pages;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

public sealed partial class CalendarPage
{
    private const string DefaultColor = "#039be5";

    private const int DayCount = 4;

    private readonly IReadOnlyList<SampleCalendar> calendars = SampleData.CreateCalendars();

    private readonly List<CalendarItem> items = [];

    private readonly Dictionary<string, SampleCalendar> owners = new(StringComparer.Ordinal);

    private CalendarViewKind view = CalendarViewKind.Week;

    private DateOnly date = DateOnly.FromDateTime(DateTime.Today);

    private DayOfWeek firstDayOfWeek = DayOfWeek.Sunday;

    private CalendarOverlapMode overlapMode = CalendarOverlapMode.Columns;

    private bool secondary;

    private bool readOnly;

    private CalendarSelectEventArgs? draft;

    private string draftTitle = string.Empty;

    private SampleCalendar? draftCalendar;

    private CalendarItemEventArgs? selected;

    private CalendarMoreEventArgs? more;

    private CalendarRange Range => CalendarRange.Of(view, date, firstDayOfWeek, DayCount);

    private List<CalendarItem> VisibleItems => [.. items.Where(x => owners[x.Id].Visible)];

    private TimeSpan LocalOffset => TimeZoneInfo.Local.GetUtcOffset(Range.StartTime);

    private TimeSpan? SecondaryOffset => secondary ? -LocalOffset : null;

    private string LocalZoneText
    {
        get
        {
            var offset = LocalOffset.Duration();
            var sign = LocalOffset < TimeSpan.Zero ? "-" : "+";
            return offset.Minutes == 0
                ? String.Create(CultureInfo.InvariantCulture, $"UTC{sign}{offset.Hours}")
                : String.Create(CultureInfo.InvariantCulture, $"UTC{sign}{offset.Hours}:{offset.Minutes:00}");
        }
    }

    private string Title
    {
        get
        {
            var culture = CultureInfo.CurrentCulture;
            if (view == CalendarViewKind.Day)
            {
                return date.ToString("yyyy年M月d日 (ddd)", culture);
            }

            if (view == CalendarViewKind.Month)
            {
                return date.ToString("yyyy年M月", culture);
            }

            if (view == CalendarViewKind.Year)
            {
                return date.ToString("yyyy年", culture);
            }

            var first = Range.Start;
            var last = Range.End.AddDays(-1);
            if (view == CalendarViewKind.Schedule)
            {
                return $"{first.ToString("yyyy年M月d日", culture)} – {last.ToString(first.Year == last.Year ? "M月d日" : "yyyy年M月d日", culture)}";
            }

            if (first.Month == last.Month)
            {
                return first.ToString("yyyy年M月", culture);
            }

            return $"{first.ToString("yyyy年M月", culture)} – {last.ToString(first.Year == last.Year ? "M月" : "yyyy年M月", culture)}";
        }
    }

    protected override void OnInitialized()
    {
        foreach (var (item, calendar) in SampleData.CreateItems(calendars, DateOnly.FromDateTime(DateTime.Today)))
        {
            items.Add(item);
            owners[item.Id] = calendar;
        }

        draftCalendar = calendars[0];
    }

    private void GoToday() => date = DateOnly.FromDateTime(DateTime.Today);

    private void Move(int direction) => date = CalendarRange.Move(view, date, direction, DayCount);

    private void OnFirstDayChanged(ChangeEventArgs e) =>
        firstDayOfWeek = (DayOfWeek)Int32.Parse((string)e.Value!, CultureInfo.InvariantCulture);

    private void OnOverlapChanged(ChangeEventArgs e) =>
        overlapMode = (CalendarOverlapMode)Int32.Parse((string)e.Value!, CultureInfo.InvariantCulture);

    private void OnSecondaryChanged(ChangeEventArgs e) => secondary = (string?)e.Value == "1";

    private void OnReadOnlyChanged(ChangeEventArgs e) => readOnly = (string?)e.Value == "1";

    private void OnDayClicked(DateOnly day)
    {
        view = CalendarViewKind.Day;
        date = day;
    }

    private void CreateNow()
    {
        var now = DateTime.Now;
        var start = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Unspecified).AddHours(1);
        Open(new CalendarSelectEventArgs(start, start.AddHours(1), false, new CalendarAnchor(280, 120, 0, 0)));
    }

    private void OnRangeSelected(CalendarSelectEventArgs e) => Open(e);

    private void Open(CalendarSelectEventArgs e)
    {
        Close();
        draft = e;
        draftTitle = string.Empty;
    }

    private void OnDraftKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
        {
            SaveDraft();
        }
        else if (e.Key == "Escape")
        {
            Close();
        }
    }

    private void SaveDraft()
    {
        if (draft is null)
        {
            return;
        }

        var calendar = draftCalendar ?? calendars[0];
        var item = new CalendarItem
        {
            Id = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture),
            Title = String.IsNullOrWhiteSpace(draftTitle) ? "(タイトルなし)" : draftTitle.Trim(),
            Start = draft.Start,
            End = draft.End,
            AllDay = draft.AllDay,
            Color = calendar.Color
        };
        items.Add(item);
        owners[item.Id] = calendar;
        Close();
    }

    private void OnItemClicked(CalendarItemEventArgs e)
    {
        Close();
        selected = e;
    }

    private static void OnItemChanged(CalendarItemChangeEventArgs e)
    {
        e.Item.Start = e.Start;
        e.Item.End = e.End;
        e.Item.AllDay = e.AllDay;
    }

    private void OnMoreClicked(CalendarMoreEventArgs e)
    {
        Close();
        more = e;
    }

    private void OpenFromMore(CalendarItem item)
    {
        var anchor = more?.Anchor ?? default;
        Close();
        selected = new CalendarItemEventArgs(item, anchor);
    }

    private void DeleteSelected()
    {
        if (selected is not null)
        {
            items.Remove(selected.Item);
            owners.Remove(selected.Item.Id);
        }

        Close();
    }

    private void Close()
    {
        draft = null;
        selected = null;
        more = null;
    }

    private string CalendarName(CalendarItem item) => owners.TryGetValue(item.Id, out var calendar) ? calendar.Name : string.Empty;

    private static string PopupStyle(CalendarAnchor anchor)
    {
        var left = anchor.Left + anchor.Width + 8;
        return String.Create(CultureInfo.InvariantCulture, $"left: clamp(8px, {left:0}px, calc(100vw - 336px)); top: clamp(8px, {anchor.Top:0}px, calc(100vh - 280px));");
    }

    private static string RangeText(DateTime start, DateTime end, bool allDay)
    {
        var culture = CultureInfo.CurrentCulture;
        if (allDay)
        {
            var last = end.AddDays(-1);
            return last.Date == start.Date
                ? start.ToString("M月d日 (ddd)", culture)
                : $"{start.ToString("M月d日 (ddd)", culture)} – {last.ToString("M月d日 (ddd)", culture)}";
        }

        return (start.Date == end.Date) || (end == start.Date.AddDays(1))
            ? $"{start.ToString("M月d日 (ddd)", culture)} {start.ToString("H:mm", culture)} – {end.ToString("H:mm", culture)}"
            : $"{start.ToString("M月d日 (ddd) H:mm", culture)} – {end.ToString("M月d日 (ddd) H:mm", culture)}";
    }
}
