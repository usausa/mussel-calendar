namespace MusselCalendar;

using System.Globalization;

using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

using MusselCalendar.Internal;

public sealed partial class MusselMiniMonth : IAsyncDisposable
{
    private const string DateFormat = "yyyy-MM-dd";

    private readonly string titleId = $"mm-{Guid.NewGuid():N}";

    private ElementReference element;

    private IJSObjectReference? module;

    private IJSObjectReference? instance;

    private DotNetObjectReference<MiniMonthScriptBridge>? self;

    private CultureInfo? givenCulture;

    private DateOnly month;

    private DateOnly shown;

    private DateOnly? shownMonth;

    private DateOnly? focusDay;

    private DateOnly today;

    private ITimer? timer;

    private bool initialized;

    private bool monthGiven;

    private bool focusPending;

    [Inject]
    private IJSRuntime Runtime { get; set; } = default!;

    [Parameter]
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Parameter]
    public EventCallback<DateOnly> DateChanged { get; set; }

    [Parameter]
    public DateOnly Month { get; set; }

    [Parameter]
    public EventCallback<DateOnly> MonthChanged { get; set; }

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

    internal CultureInfo FormatCulture { get; private set; } = CultureInfo.CurrentCulture;

    internal bool IsDisposed { get; private set; }

    private string RootClass => AdditionalAttributes?.GetValueOrDefault("class") is { } extra ? $"mussel-mini-month {extra}" : "mussel-mini-month";

    private TimeProvider Clock => TimeProvider ?? TimeProvider.System;

    private DateOnly Today => DateOnly.FromDateTime(Clock.GetLocalNow().DateTime);

    private DateOnly FocusDay => (focusDay is { } day) && InMonth(day) ? day : InMonth(Date) ? Date : month;

    public override Task SetParametersAsync(ParameterView parameters)
    {
        monthGiven = parameters.TryGetValue<DateOnly>(nameof(Month), out _);
        return base.SetParametersAsync(parameters);
    }

    protected override void OnInitialized()
    {
        var second = Clock.GetLocalNow().Second;
        timer = Clock.CreateTimer(OnTick, null, TimeSpan.FromSeconds(60 - second), TimeSpan.FromMinutes(1));
    }

    protected override Task OnParametersSetAsync()
    {
        today = Today;
        var culture = Culture ?? CultureInfo.CurrentCulture;
        if (!ReferenceEquals(culture, givenCulture))
        {
            givenCulture = culture;
            FormatCulture = CalendarCulture.Gregorian(culture);
        }

        var previous = month;
        if (monthGiven && (Month != shownMonth))
        {
            shownMonth = Month;
            month = FirstOfMonth(Month);
        }

        if (Date != shown)
        {
            shown = Date;
            if (!monthGiven)
            {
                month = FirstOfMonth(Date);
            }
        }

        if (!initialized)
        {
            initialized = true;
            return Task.CompletedTask;
        }

        return !monthGiven && (month != previous) ? MonthChanged.InvokeAsync(month) : Task.CompletedTask;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        try
        {
            if (firstRender)
            {
                await AttachAsync().ConfigureAwait(true);
                return;
            }

            if ((instance is not null) && TakePendingFocus())
            {
                await instance.InvokeVoidAsync("focus").ConfigureAwait(true);
            }
        }
        catch (JSDisconnectedException)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        IsDisposed = true;
        timer?.Dispose();
        await ReleaseAsync().ConfigureAwait(true);
        self?.Dispose();
    }

    internal async Task OnFocusDateAsync(string date)
    {
        if (!TryParse(date, out var day))
        {
            return;
        }

        focusDay = day;
        var first = FirstOfMonth(day);
        if (first != month)
        {
            focusPending = true;
            await ShowAsync(first).ConfigureAwait(true);
        }

        StateHasChanged();
    }

    internal async Task OnSelectDateAsync(string date)
    {
        if (TryParse(date, out var day))
        {
            await SelectAsync(day).ConfigureAwait(true);
        }
    }

    internal async Task ReportAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not (JSDisconnectedException or OperationCanceledException))
        {
            if (!IsDisposed)
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
            module = await Runtime.InvokeAsync<IJSObjectReference>("import", MusselCalendarView.ModulePath).ConfigureAwait(true);
            if (IsDisposed)
            {
                await ReleaseAsync().ConfigureAwait(true);
                return;
            }

            self = DotNetObjectReference.Create(new MiniMonthScriptBridge(this));
            var attached = await module.InvokeAsync<IJSObjectReference>("attachMiniMonth", element, self).ConfigureAwait(true);
            instance = attached;
            if (IsDisposed)
            {
                await ReleaseAsync().ConfigureAwait(true);
                return;
            }

            if (TakePendingFocus())
            {
                await ScriptCalls.SendAsync(attached, "focus").ConfigureAwait(true);
            }

            if (!IsDisposed)
            {
                await attached.InvokeVoidAsync("ready").ConfigureAwait(true);
            }
        }
        catch (JSDisconnectedException)
        {
        }
    }

    private bool TakePendingFocus()
    {
        if (!focusPending || (focusDay is not { } day) || !InMonth(day))
        {
            return false;
        }

        focusPending = false;
        return true;
    }

    private Task ReleaseAsync()
    {
        var attached = instance;
        var loaded = module;
        instance = null;
        module = null;
        return ScriptCalls.ReleaseAsync(attached, loaded);
    }

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

    private Task ShiftAsync(int months)
    {
        focusPending = false;
        var target = (month.Year * 12) + month.Month - 1 + months;
        if ((target < DateOnly.MinValue.Year * 12) || (target > (DateOnly.MaxValue.Year * 12) + 11))
        {
            return Task.CompletedTask;
        }

        return ShowAsync(month.AddMonths(months));
    }

    private Task ShowAsync(DateOnly first)
    {
        if (first == month)
        {
            return Task.CompletedTask;
        }

        month = first;
        return MonthChanged.InvokeAsync(first);
    }

    private Task SelectAsync(DateOnly day)
    {
        focusDay = day;
        focusPending = !monthGiven && (FirstOfMonth(day) != month);
        return DateChanged.InvokeAsync(day);
    }

    private bool InMonth(DateOnly day) => (day.Year == month.Year) && (day.Month == month.Month);

    private DateOnly? DayAt(int cell)
    {
        var number = month.DayNumber - CalendarRange.DaysIntoWeek(month, FirstDayOfWeek) + cell;
        return (number < 0) || (number > DateOnly.MaxValue.DayNumber) ? null : DateOnly.FromDayNumber(number);
    }

    private static DateOnly FirstOfMonth(DateOnly day) => new(day.Year, day.Month, 1);

    private static bool TryParse(string value, out DateOnly day) =>
        DateOnly.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out day);

    private static string IsoDate(DateOnly day) => day.ToString(DateFormat, CultureInfo.InvariantCulture);

    private string TabIndex(DateOnly day) => day == FocusDay ? "0" : "-1";

    private string? SelectedText(DateOnly day) => day == Date ? "true" : null;

    private string? CurrentText(DateOnly day) => day == today ? "date" : null;

    private string DayClass(DateOnly day)
    {
        var css = InMonth(day) ? "mm-day" : "mm-day mm-outside";
        if ((Highlight is { } value) && value.Contains(day))
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
