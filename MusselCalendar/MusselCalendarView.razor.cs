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

    private ElementReference element;

    private IJSObjectReference? module;

    private IJSObjectReference? instance;

    private DotNetObjectReference<MusselCalendarView>? reference;

    private ITimer? timer;

    private CalendarRange range;

    private List<BarSegment> allDayBars = [];

    private List<TimedSegment> timed = [];

    private int allDayLanes;

    private int fittedLanes = FallbackLanes;

    private int fitKey;

    private DateOnly today;

    private bool timeGridShown;

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
    public string AllDayText { get; set; } = "All day";

    [Parameter]
    public string MoreFormat { get; set; } = "+{0}";

    [Parameter]
    public string HourFormat { get; set; } = "H:mm";

    [Parameter]
    public string TimeFormat { get; set; } = "H:mm";

    [Parameter]
    public string MonthDayFormat { get; set; } = "M/d";

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

    private CultureInfo FormatCulture => Culture ?? CultureInfo.CurrentCulture;

    private TimeProvider Clock => TimeProvider ?? TimeProvider.System;

    private DateTime Now => Clock.GetLocalNow().DateTime;

    private bool FitsLanes => MonthLanes <= 0;

    private int Lanes => FitsLanes ? fittedLanes : MonthLanes;

    private int FitKey => FitsLanes ? weeks.Count : 0;

    private string FitText => FitsLanes ? "true" : "false";

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
        range = CalendarRange.Of(View, Date, FirstDayOfWeek, DayCount);
        if (View == CalendarViewKind.Month)
        {
            BuildMonth();
            allDayBars = [];
            timed = [];
            allDayLanes = 0;
        }
        else
        {
            weeks.Clear();
            allDayBars = CalendarLayout.BuildBars(Items.Where(CalendarLayout.InAllDayLane), range.Start, range.Days, out allDayLanes);
            timed = CalendarLayout.BuildTimed(Items, range.Start, range.Days, SlotMinutes, OverlapMode);
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        var showTimeGrid = View != CalendarViewKind.Month;
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

    [JSInvokable]
    public Task OnSelect(string start, string until, bool allDay, double left, double top, double width, double height) =>
        disposed
            ? Task.CompletedTask
            : RangeSelected.InvokeAsync(new CalendarSelectEventArgs(Parse(start), Parse(until), allDay, new CalendarAnchor(left, top, width, height)));

    [JSInvokable]
    public Task OnItemClick(string id, double left, double top, double width, double height)
    {
        var item = Find(id);
        return (item is null) || disposed
            ? Task.CompletedTask
            : ItemClicked.InvokeAsync(new CalendarItemEventArgs(item, new CalendarAnchor(left, top, width, height)));
    }

    [JSInvokable]
    public Task OnMove(string id, string start, string until, bool allDay)
    {
        var item = Find(id);
        return (item is null) || item.ReadOnly || disposed
            ? Task.CompletedTask
            : ItemChanged.InvokeAsync(new CalendarItemChangeEventArgs(item, Parse(start), Parse(until), allDay));
    }

    [JSInvokable]
    public Task OnShift(string id, int days, int minutes)
    {
        var item = Find(id);
        if ((item is null) || item.ReadOnly || disposed)
        {
            return Task.CompletedTask;
        }

        var offset = TimeSpan.FromDays(days) + TimeSpan.FromMinutes(minutes);
        return ItemChanged.InvokeAsync(new CalendarItemChangeEventArgs(item, item.Start + offset, item.End + offset, item.AllDay));
    }

    [JSInvokable]
    public Task OnExtend(string id, int minutes)
    {
        var item = Find(id);
        if ((item is null) || item.ReadOnly || item.AllDay || disposed)
        {
            return Task.CompletedTask;
        }

        var end = item.End.AddMinutes(minutes);
        return end <= item.Start
            ? Task.CompletedTask
            : ItemChanged.InvokeAsync(new CalendarItemChangeEventArgs(item, item.Start, end, false));
    }

    [JSInvokable]
    public void OnMonthLanes(int lanes)
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

    private async Task AttachAsync()
    {
        module = await Runtime.InvokeAsync<IJSObjectReference>("import", ModulePath).ConfigureAwait(true);
        if (disposed)
        {
            await ReleaseAsync().ConfigureAwait(true);
            return;
        }

        reference = DotNetObjectReference.Create(this);
        instance = await module.InvokeAsync<IJSObjectReference>("attach", reference, element, ScrollHour).ConfigureAwait(true);
        if (disposed)
        {
            await ReleaseAsync().ConfigureAwait(true);
        }
    }

    private async Task ReleaseAsync()
    {
        var attached = instance;
        var loaded = module;
        instance = null;
        module = null;
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

    private void BuildMonth()
    {
        weeks.Clear();
        for (var start = range.Start; start < range.End; start = start.AddDays(7))
        {
            weeks.Add(CalendarLayout.BuildMonthWeek(Items, start, Lanes));
        }
    }

    [JSInvokable]
    public Task OnMoreClick(string date, double left, double top, double width, double height)
    {
        if (disposed || !DateOnly.TryParseExact(date, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
        {
            return Task.CompletedTask;
        }

        var items = Items
            .Where(x => (CalendarLayout.FirstDay(x) <= day) && (day < CalendarLayout.EndDay(x)))
            .OrderBy(static x => !x.AllDay)
            .ThenBy(static x => x.Start)
            .ToList();
        return MoreClicked.InvokeAsync(new CalendarMoreEventArgs(day, items, new CalendarAnchor(left, top, width, height)));
    }

    private void OnTick(object? state) =>
        _ = InvokeAsync(() =>
        {
            today = DateOnly.FromDateTime(Now);
            StateHasChanged();
        });

    private CalendarItem? Find(string id) => Items.FirstOrDefault(x => String.Equals(x.Id, id, StringComparison.Ordinal));

    private static DateTime Parse(string value) => DateTime.ParseExact(value, DateTimeFormat, CultureInfo.InvariantCulture);

    private static string IsoDate(DateOnly day) => day.ToString(DateFormat, CultureInfo.InvariantCulture);

    private static string IsoDateTime(DateTime value) => value.ToString(DateTimeFormat, CultureInfo.InvariantCulture);

    private static string ReadOnlyText(CalendarItem item) => item.ReadOnly ? "true" : "false";

    private static string ColorStyle(CalendarItem item) => item.Color is null ? String.Empty : $"--mc-color: {item.Color};";

    private string WeekdayName(DateOnly day) => FormatCulture.DateTimeFormat.GetAbbreviatedDayName(day.DayOfWeek);

    private string DayLabel(DateOnly day) => day.ToString("D", FormatCulture);

    private string HourText(int hour) => new DateTime(2000, 1, 1, hour, 0, 0, DateTimeKind.Unspecified).ToString(HourFormat, FormatCulture);

    private string TimeText(DateTime value) => value.ToString(TimeFormat, FormatCulture);

    private string MoreText(int count) => String.Format(FormatCulture, MoreFormat, count);

    private string MonthDateText(DateOnly day) => day.Day == 1 ? day.ToString(MonthDayFormat, FormatCulture) : day.Day.ToString(FormatCulture);

    private string ItemLabel(CalendarItem item) =>
        item.AllDay
            ? $"{item.Title}, {AllDayText}"
            : $"{item.Title}, {item.Start.ToString("g", FormatCulture)} – {item.End.ToString("g", FormatCulture)}";

    private string DayHeadClass(DateOnly day) => day == today ? "mc-day-head mc-today" : "mc-day-head";

    private string MonthCellClass(DateOnly day)
    {
        var css = day.Month != Date.Month ? "mc-cell mc-month-cell mc-outside" : "mc-cell mc-month-cell";
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
