namespace MusselCalendar;

using System.Globalization;

using Microsoft.AspNetCore.Components;

public sealed partial class MusselMiniMonth : IDisposable
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(1);

    private DateOnly month;

    private DateOnly shown;

    private DateOnly today;

    private ITimer? timer;

    [Parameter]
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Parameter]
    public EventCallback<DateOnly> DateChanged { get; set; }

    [Parameter]
    public DayOfWeek FirstDayOfWeek { get; set; } = DayOfWeek.Sunday;

    [Parameter]
    public CalendarRange? Highlight { get; set; }

    [Parameter]
    public string PreviousText { get; set; } = "Previous month";

    [Parameter]
    public string NextText { get; set; } = "Next month";

    [Parameter]
    public CultureInfo? Culture { get; set; }

    [Parameter]
    public TimeProvider? TimeProvider { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private CultureInfo FormatCulture => Culture ?? CultureInfo.CurrentCulture;

    private string RootClass => AdditionalAttributes?.GetValueOrDefault("class") is { } extra ? $"mussel-mini-month {extra}" : "mussel-mini-month";

    private TimeProvider Clock => TimeProvider ?? TimeProvider.System;

    private DateOnly Today => DateOnly.FromDateTime(Clock.GetLocalNow().DateTime);

    private DateOnly Start => CalendarRange.StartOfWeek(month, FirstDayOfWeek);

    protected override void OnInitialized()
    {
        timer = Clock.CreateTimer(OnTick, null, TickInterval, TickInterval);
    }

    protected override void OnParametersSet()
    {
        today = Today;
        if (Date != shown)
        {
            shown = Date;
            month = new DateOnly(Date.Year, Date.Month, 1);
        }
    }

    public void Dispose() => timer?.Dispose();

    private void OnTick(object? state) =>
        _ = InvokeAsync(() =>
        {
            var day = Today;
            if (day != today)
            {
                today = day;
                StateHasChanged();
            }
        });

    private void Shift(int months) => month = month.AddMonths(months);

    private string DayClass(DateOnly day)
    {
        var css = day.Month != month.Month ? "mm-day mm-outside" : "mm-day";
        if (Highlight is { } value && value.Contains(day))
        {
            css += " mm-in-range";
        }

        if (day == Date)
        {
            css += " mm-selected";
        }

        return day == today ? css + " mm-today" : css;
    }
}
