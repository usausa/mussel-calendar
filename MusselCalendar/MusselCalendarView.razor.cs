namespace MusselCalendar;

using System.Globalization;

using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

using MusselCalendar.Internal;

public sealed partial class MusselCalendarView
{
    private const string ModulePath = "./_content/MusselCalendar/MusselCalendar.js";

    private const string DateFormat = "yyyy-MM-dd";

    private const string DateTimeFormat = "yyyy-MM-dd'T'HH:mm:ss";

    private const int ShortHeight = 36;

    private const int FallbackLanes = 3;

    private readonly List<MonthWeek> weeks = [];

    private readonly string idPrefix = $"mc-{Guid.NewGuid():N}";

    private readonly HashSet<string> seenIds = new(StringComparer.Ordinal);

    private readonly HashSet<string> repeatedIds = new(StringComparer.Ordinal);

    private ElementReference element;

    private IJSObjectReference? module;

    private IJSObjectReference? instance;

    private DotNetObjectReference<CalendarScriptBridge>? reference;

    private CultureInfo? givenCulture;

    private ITimer? timer;

    private CalendarRange range;

    private List<BarSegment> allDayBars = [];

    private List<TimedSegment> timed = [];

    private List<ScheduleDay> schedule = [];

    private Dictionary<DateOnly, int> dayCounts = [];

    private int allDayLanes;

    private int fittedLanes = FallbackLanes;

    private int fitKey;

    private DateOnly today;

    private DateOnly? yearFocus;

    private DateOnly month;

    private bool timeGridShown;

    private bool pendingScroll;

    private bool pendingFit;

    private bool disposed;

    [Inject]
    private IJSRuntime Runtime { get; set; } = default!;

    [Parameter]
    public CalendarViewKind View { get; set; } = CalendarViewKind.Week;

    [Parameter]
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Parameter]
    public int DayCount { get; set; } = 4;

    [Parameter]
    public DayOfWeek FirstDayOfWeek { get; set; } = DayOfWeek.Sunday;

    [Parameter]
    public IReadOnlyList<CalendarItem> Items { get; set; } = [];

    [Parameter]
    public int SlotMinutes { get; set; } = 15;

    [Parameter]
    public int DefaultMinutes { get; set; } = 60;

    [Parameter]
    public int HourHeight { get; set; } = 48;

    [Parameter]
    public int ScrollHour { get; set; } = 8;

    [Parameter]
    public int MonthLanes { get; set; }

    [Parameter]
    public CalendarOverlapMode OverlapMode { get; set; } = CalendarOverlapMode.Columns;

    [Parameter]
    public bool ReadOnly { get; set; }

    [Parameter]
    public string AllDayText { get; set; } = "All day";

    [Parameter]
    public string MoreFormat { get; set; } = "+{0}";

    [Parameter]
    public string EmptyText { get; set; } = "No events";

    [Parameter]
    public string ItemCountFormat { get; set; } = "Events: {0}";

    [Parameter]
    public string HourFormat { get; set; } = "H:mm";

    [Parameter]
    public string TimeFormat { get; set; } = "H:mm";

    [Parameter]
    public string? MonthDayFormat { get; set; }

    [Parameter]
    public string ScheduleDayFormat { get; set; } = "MMM, ddd";

    [Parameter]
    public TimeSpan? SecondaryOffset { get; set; }

    [Parameter]
    public string? TimeZoneText { get; set; }

    [Parameter]
    public string? SecondaryTimeZoneText { get; set; }

    [Parameter]
    public CultureInfo? Culture { get; set; }

    [Parameter]
    public TimeProvider? TimeProvider { get; set; }

    [Parameter]
    public EventCallback<CalendarSelectEventArgs> RangeSelected { get; set; }

    [Parameter]
    public EventCallback<CalendarItemEventArgs> ItemClicked { get; set; }

    [Parameter]
    public EventCallback<CalendarItemChangeEventArgs> ItemChanged { get; set; }

    [Parameter]
    public EventCallback<CalendarMoreEventArgs> MoreClicked { get; set; }

    [Parameter]
    public EventCallback<DateOnly> DayClicked { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    internal CultureInfo FormatCulture { get; private set; } = CultureInfo.CurrentCulture;

    private TimeProvider Clock => TimeProvider ?? TimeProvider.System;

    private DateTime Now => Clock.GetLocalNow().DateTime;

    private bool FitsLanes => MonthLanes <= 0;

    private int Lanes => FitsLanes ? fittedLanes : MonthLanes;

    private int FitKey => FitsLanes ? weeks.Count : 0;

    private string FitText => FitsLanes ? "true" : "false";

    private string ViewReadOnlyText => ReadOnly ? "true" : "false";

    private bool ShowsTimeGrid => View is CalendarViewKind.Day or CalendarViewKind.Week or CalendarViewKind.Days;

    private bool IsDual => SecondaryOffset.HasValue;

    private string TimeGridClass => IsDual ? "mc-timegrid mc-dual" : "mc-timegrid";

    private DateOnly YearFocusDay => (yearFocus is { } day) && range.Contains(day) ? day : range.Contains(Date) ? Date : range.Start;

    private string RootClass => AdditionalAttributes?.GetValueOrDefault("class") is { } extra ? $"mussel-calendar {extra}" : "mussel-calendar";

    private string RootStyle
    {
        get
        {
            var style = String.Create(CultureInfo.InvariantCulture, $"--mussel-calendar-hour-height: {HourHeight}px");
            return AdditionalAttributes?.GetValueOrDefault("style") is { } extra ? $"{style}; {extra}" : style;
        }
    }

    protected override void OnInitialized()
    {
        var second = Clock.GetLocalNow().Second;
        timer = Clock.CreateTimer(OnTick, null, TimeSpan.FromSeconds(60 - second), TimeSpan.FromMinutes(1));
    }

    protected override void OnParametersSet()
    {
        today = DateOnly.FromDateTime(Now);
        var culture = Culture ?? CultureInfo.CurrentCulture;
        if (!ReferenceEquals(culture, givenCulture))
        {
            givenCulture = culture;
            FormatCulture = CalendarCulture.Gregorian(culture);
        }

        FindRepeatedIds();
        range = CalendarRange.Of(View, Date, FirstDayOfWeek, DayCount);
        weeks.Clear();
        allDayBars = [];
        timed = [];
        allDayLanes = 0;
        schedule = [];
        dayCounts = [];
        switch (View)
        {
            case CalendarViewKind.Month:
                month = CalendarRange.MonthOf(Date);
                BuildMonth();
                break;
            case CalendarViewKind.Schedule:
                schedule = CalendarLayout.BuildSchedule(Items, range);
                break;
            case CalendarViewKind.Year:
                dayCounts = CalendarLayout.CountDays(Items, range);
                break;
            default:
                allDayBars = CalendarLayout.BuildBars(Items.Where(CalendarLayout.InAllDayLane), range.Start, range.Days, out allDayLanes);
                timed = CalendarLayout.BuildTimed(Items, range.Start, range.Days, SlotMinutes, OverlapMode);
                break;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        var showTimeGrid = ShowsTimeGrid;
        var scroll = showTimeGrid && !timeGridShown;
        timeGridShown = showTimeGrid;
        var key = FitKey;
        var fit = (key != fitKey) && (key > 0);
        fitKey = key;
        try
        {
            if (firstRender)
            {
                await AttachAsync().ConfigureAwait(true);
                return;
            }

            if (instance is null)
            {
                pendingScroll |= scroll;
                pendingFit |= fit;
                return;
            }

            if (scroll)
            {
                await instance.InvokeVoidAsync("scroll", ScrollHour).ConfigureAwait(true);
            }

            if (fit)
            {
                await instance.InvokeVoidAsync("fit").ConfigureAwait(true);
            }
        }
        catch (JSDisconnectedException)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        disposed = true;
        timer?.Dispose();
        try
        {
            await ReleaseAsync().ConfigureAwait(true);
        }
        catch (JSDisconnectedException)
        {
        }

        reference?.Dispose();
    }

    internal Task OnSelect(string start, string until, bool allDay, double left, double top, double width, double height) =>
        disposed || ReadOnly || !TryParse(start, out var begin) || !TryParse(until, out var end)
            ? Task.CompletedTask
            : ReportAsync(() => RangeSelected.InvokeAsync(new CalendarSelectEventArgs(begin, end, allDay, new CalendarAnchor(left, top, width, height))));

    internal Task OnItemClick(string id, double left, double top, double width, double height)
    {
        var item = Find(id);
        return (item is null) || disposed
            ? Task.CompletedTask
            : ReportAsync(() => ItemClicked.InvokeAsync(new CalendarItemEventArgs(item, new CalendarAnchor(left, top, width, height))));
    }

    internal Task OnMove(string id, string start, string until, bool allDay)
    {
        var item = Find(id);
        return (item is null) || item.ReadOnly || ReadOnly || disposed || !TryParse(start, out var begin) || !TryParse(until, out var end)
            ? Task.CompletedTask
            : ReportAsync(() => ItemChanged.InvokeAsync(new CalendarItemChangeEventArgs(item, DateTime.SpecifyKind(begin, item.Start.Kind), DateTime.SpecifyKind(end, item.End.Kind), allDay)));
    }

    internal Task OnShift(string id, int days, int minutes)
    {
        var item = Find(id);
        if ((item is null) || item.ReadOnly || ReadOnly || disposed)
        {
            return Task.CompletedTask;
        }

        var offset = TimeSpan.FromDays(days) + TimeSpan.FromMinutes(minutes);
        return ReportAsync(() => ItemChanged.InvokeAsync(new CalendarItemChangeEventArgs(item, item.Start + offset, item.End + offset, item.AllDay)));
    }

    internal Task OnExtend(string id, int minutes)
    {
        var item = Find(id);
        if ((item is null) || item.ReadOnly || item.AllDay || ReadOnly || disposed)
        {
            return Task.CompletedTask;
        }

        var end = item.End.AddMinutes(minutes);
        return end <= item.Start
            ? Task.CompletedTask
            : ReportAsync(() => ItemChanged.InvokeAsync(new CalendarItemChangeEventArgs(item, item.Start, end, false)));
    }

    internal void OnMonthLanes(int lanes)
    {
        if (disposed || !FitsLanes || (lanes < 1) || (lanes == fittedLanes))
        {
            return;
        }

        fittedLanes = lanes;
        if (View == CalendarViewKind.Month)
        {
            BuildMonth();
            StateHasChanged();
        }
    }

    private async Task ReportAsync(Func<Task> operation)
    {
        try
        {
            await operation().ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not (JSDisconnectedException or OperationCanceledException))
        {
            if (!disposed)
            {
                await DispatchExceptionAsync(ex).ConfigureAwait(true);
            }

            throw;
        }
    }

    private async Task AttachAsync()
    {
        try
        {
            module = await Runtime.InvokeAsync<IJSObjectReference>("import", ModulePath).ConfigureAwait(true);
            if (disposed)
            {
                await ReleaseAsync().ConfigureAwait(true);
                return;
            }

            reference = DotNetObjectReference.Create(new CalendarScriptBridge(this));
            instance = await module.InvokeAsync<IJSObjectReference>("attach", element, reference, ScrollHour).ConfigureAwait(true);
            if (disposed)
            {
                await ReleaseAsync().ConfigureAwait(true);
                return;
            }

            if (pendingScroll)
            {
                pendingScroll = false;
                await instance.InvokeVoidAsync("scroll", ScrollHour).ConfigureAwait(true);
            }

            if (pendingFit)
            {
                pendingFit = false;
                await instance.InvokeVoidAsync("fit").ConfigureAwait(true);
            }
        }
        catch (JSDisconnectedException)
        {
        }
    }

    private async Task ReleaseAsync()
    {
        var attached = instance;
        var loaded = module;
        instance = null;
        module = null;
        try
        {
            if (attached is not null)
            {
                await attached.InvokeVoidAsync("dispose").ConfigureAwait(true);
                await attached.DisposeAsync().ConfigureAwait(true);
            }

            if (loaded is not null)
            {
                await loaded.DisposeAsync().ConfigureAwait(true);
            }
        }
        catch (JSDisconnectedException)
        {
        }
        catch (JSException)
        {
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void BuildMonth()
    {
        weeks.Clear();
        for (var start = range.Start; start < range.End; start = start.AddDays(7))
        {
            weeks.Add(CalendarLayout.BuildMonthWeek(Items, start, Lanes));
        }
    }

    internal Task OnDayClick(string date) =>
        disposed || !TryParseDate(date, out var day) ? Task.CompletedTask : ReportAsync(() => DayClicked.InvokeAsync(day));

    internal void OnYearFocus(string date)
    {
        if (!disposed && TryParseDate(date, out var day))
        {
            yearFocus = day;
            StateHasChanged();
        }
    }

    internal Task OnMoreClick(string date, double left, double top, double width, double height)
    {
        if (disposed || !TryParseDate(date, out var day))
        {
            return Task.CompletedTask;
        }

        var items = Items
            .Where(x => (CalendarLayout.FirstDay(x) <= day) && (day < CalendarLayout.EndDay(x)))
            .OrderBy(static x => !x.AllDay)
            .ThenBy(static x => x.Start)
            .ToList();
        return ReportAsync(() => MoreClicked.InvokeAsync(new CalendarMoreEventArgs(day, items, new CalendarAnchor(left, top, width, height))));
    }

    private void OnTick(object? state) =>
        _ = InvokeAsync(() =>
        {
            var day = DateOnly.FromDateTime(Now);
            if (!ShowsTimeGrid && (day == today))
            {
                return;
            }

            today = day;
            StateHasChanged();
        });

    private CalendarItem? Find(string id) => Items.FirstOrDefault(x => String.Equals(x.Id, id, StringComparison.Ordinal));

    private static bool TryParse(string value, out DateTime time) =>
        DateTime.TryParseExact(value, DateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out time);

    private static bool TryParseDate(string value, out DateOnly day) =>
        DateOnly.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out day);

    private static int WeekCount(int offset, int days) => (offset + days + 6) / 7;

    private static string IsoDate(DateOnly day) => day.ToString(DateFormat, CultureInfo.InvariantCulture);

    private static string IsoDateTime(DateTime value) => value.ToString(DateTimeFormat, CultureInfo.InvariantCulture);

    private string ReadOnlyText(CalendarItem item) => ReadOnly || item.ReadOnly ? "true" : "false";

    private string? KeyOf(CalendarItem item) => repeatedIds.Contains(item.Id) ? null : item.Id;

    private static string ColorStyle(CalendarItem item) => (item.Color is { } color) && IsSafeColor(color) ? $"--mc-color: {color};" : String.Empty;

    private static bool IsSafeColor(string value)
    {
        if ((value.Length is 0 or > 64) || value.Contains("url(", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        foreach (var ch in value)
        {
            if (!char.IsAsciiLetterOrDigit(ch) && (ch is not ('#' or '(' or ')' or ',' or '.' or '%' or ' ' or '-' or '/')))
            {
                return false;
            }
        }

        return true;
    }

    private void FindRepeatedIds()
    {
        seenIds.Clear();
        repeatedIds.Clear();
        foreach (var item in Items)
        {
            if (!seenIds.Add(item.Id))
            {
                repeatedIds.Add(item.Id);
            }
        }
    }

    private string WeekdayName(DateOnly day) => FormatCulture.DateTimeFormat.GetAbbreviatedDayName(day.DayOfWeek);

    private string DayLabel(DateOnly day) => day.ToString("D", FormatCulture);

    private string HourText(int hour) => new DateTime(2000, 1, 1, hour, 0, 0, DateTimeKind.Unspecified).ToString(HourFormat, FormatCulture);

    private string SecondaryHourText(int hour)
    {
        var offset = (int)Math.Round(SecondaryOffset.GetValueOrDefault().TotalMinutes);
        var minutes = (hour * 60) + offset;
        minutes = ((minutes % CalendarLayout.MinutesPerDay) + CalendarLayout.MinutesPerDay) % CalendarLayout.MinutesPerDay;
        var format = offset % 60 == 0 ? HourFormat : TimeFormat;
        return new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Unspecified).AddMinutes(minutes).ToString(format, FormatCulture);
    }

    private string ScheduleDayClass(DateOnly day) => day == today ? "mc-schedule-day mc-today" : "mc-schedule-day";

    private string ScheduleDayText(DateOnly day) => day.ToString(ScheduleDayFormat, FormatCulture);

    private string ScheduleTimeText(CalendarItem item, DateOnly day)
    {
        if (CalendarLayout.CoversDay(item, day))
        {
            return AllDayText;
        }

        var start = day.ToDateTime(TimeOnly.MinValue);
        var startsToday = item.Start >= start;
        var endsToday = item.End <= start.AddDays(1);
        if (startsToday && endsToday)
        {
            return $"{TimeText(item.Start)} – {TimeText(item.End)}";
        }

        return startsToday ? $"{TimeText(item.Start)} –" : $"– {TimeText(item.End)}";
    }

    private string MonthName(DateOnly day) => FormatCulture.DateTimeFormat.GetMonthName(day.Month);

    private string ShortestDayName(int index) => FormatCulture.DateTimeFormat.GetShortestDayName((DayOfWeek)(((int)FirstDayOfWeek + index) % 7));

    private string DayName(int index) => FormatCulture.DateTimeFormat.GetDayName((DayOfWeek)(((int)FirstDayOfWeek + index) % 7));

    private string? CurrentText(DateOnly day) => day == today ? "date" : null;

    private string YearDayClass(DateOnly day)
    {
        var css = dayCounts.ContainsKey(day) ? "mc-year-day mc-marked" : "mc-year-day";
        return day == today ? css + " mc-today" : css;
    }

    private string YearTabIndex(DateOnly day) => day == YearFocusDay ? "0" : "-1";

    private string YearDayLabel(DateOnly day) =>
        dayCounts.TryGetValue(day, out var count)
            ? $"{DayLabel(day)}, {String.Format(FormatCulture, ItemCountFormat, count)}"
            : DayLabel(day);

    private string TimeText(DateTime value) => value.ToString(TimeFormat, FormatCulture);

    private string MoreText(int count) => String.Format(FormatCulture, MoreFormat, count);

    private string MonthDateText(DateOnly day) => day.Day == 1 ? day.ToString(MonthDayFormat ?? (CalendarCulture.IsDayFirst(FormatCulture) ? "d/M" : "M/d"), FormatCulture) : day.Day.ToString(FormatCulture);

    private string ItemLabel(CalendarItem item) =>
        item.AllDay
            ? $"{item.Title}, {AllDayText}"
            : $"{item.Title}, {item.Start.ToString("g", FormatCulture)} – {item.End.ToString("g", FormatCulture)}";

    private string DayHeadClass(DateOnly day) => day == today ? "mc-day-head mc-today" : "mc-day-head";

    private string MonthCellClass(DateOnly day)
    {
        var css = day.Month != month.Month ? "mc-cell mc-month-cell mc-outside" : "mc-cell mc-month-cell";
        return day == today ? css + " mc-today" : css;
    }

    private static bool IsChip(BarSegment bar) => !bar.Item.AllDay && (CalendarLayout.DayCount(bar.Item) == 1);

    private static string BarClass(BarSegment bar)
    {
        var css = bar.IsStart ? "mc-bar" : "mc-bar mc-continues-left";
        return bar.IsEnd ? css : css + " mc-continues-right";
    }

    private static string MonthBarClass(BarSegment bar) => IsChip(bar) ? "mc-bar mc-chip" : BarClass(bar);

    private static string BarStyle(BarSegment bar, int columnOffset, int laneOffset) =>
        String.Create(CultureInfo.InvariantCulture, $"grid-column: {bar.Column + columnOffset} / span {bar.Span}; grid-row: {bar.Lane + laneOffset}; {ColorStyle(bar.Item)}");

    private string EventClass(TimedSegment segment) =>
        (segment.EndMinute - segment.StartMinute) * HourHeight / 60.0 < ShortHeight ? "mc-event mc-short" : "mc-event";

    private string EventStyle(TimedSegment segment)
    {
        var top = segment.StartMinute * HourHeight / 60.0;
        var height = Math.Max((segment.EndMinute - segment.StartMinute) * HourHeight / 60.0, 14);
        var left = segment.Left * 100;
        var width = segment.Width * 100;
        return String.Create(
            CultureInfo.InvariantCulture,
            $"top: {top:0.##}px; height: {height:0.##}px; left: calc({left:0.##}% + 1px); width: calc({width:0.##}% - 4px); z-index: {segment.Layer + 1}; {ColorStyle(segment.Item)}");
    }

    private string NowTop() => (Now.TimeOfDay.TotalMinutes * HourHeight / 60.0).ToString("0.##", CultureInfo.InvariantCulture);
}
