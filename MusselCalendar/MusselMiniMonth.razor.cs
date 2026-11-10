namespace MusselCalendar;

using System.Globalization;

using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

using MusselCalendar.Internal;

public sealed partial class MusselMiniMonth : IAsyncDisposable
{
    private const string ModulePath = "./_content/MusselCalendar/MusselCalendar.js";

    private const string DateFormat = "yyyy-MM-dd";

    private static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(1);

    private readonly string titleId = $"mm-{Guid.NewGuid():N}";

    private ElementReference element;

    private IJSObjectReference? module;

    private IJSObjectReference? instance;

    private DotNetObjectReference<MusselMiniMonth>? reference;

    private DateOnly month;

    private DateOnly shown;

    private DateOnly? shownMonth;

    private DateOnly? focusDay;

    private DateOnly today;

    private ITimer? timer;

    private bool initialized;

    private bool monthGiven;

    private bool focusPending;

    private bool disposed;

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

    private CultureInfo FormatCulture { get; set; } = CultureInfo.CurrentCulture;

    private string RootClass => AdditionalAttributes?.GetValueOrDefault("class") is { } extra ? $"mussel-mini-month {extra}" : "mussel-mini-month";

    private TimeProvider Clock => TimeProvider ?? TimeProvider.System;

    private DateOnly Today => DateOnly.FromDateTime(Clock.GetLocalNow().DateTime);

    private DateOnly Start => CalendarRange.StartOfWeek(month, FirstDayOfWeek);

    private DateOnly FocusDay => focusDay is { } day && InMonth(day) ? day : InMonth(Date) ? Date : month;

    public override Task SetParametersAsync(ParameterView parameters)
    {
        monthGiven = parameters.TryGetValue<DateOnly>(nameof(Month), out _);
        return base.SetParametersAsync(parameters);
    }

    protected override void OnInitialized()
    {
        timer = Clock.CreateTimer(OnTick, null, TickInterval, TickInterval);
    }

    protected override Task OnParametersSetAsync()
    {
        today = Today;
        FormatCulture = CalendarCulture.Gregorian(Culture ?? CultureInfo.CurrentCulture);
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

            if (focusPending && (instance is not null) && focusDay is { } day && InMonth(day))
            {
                focusPending = false;
                await instance.InvokeVoidAsync("focus").ConfigureAwait(true);
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
    public async Task OnFocusDate(string date)
    {
        if (disposed || !TryParse(date, out var day))
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

    [JSInvokable]
    public Task OnSelectDate(string date) =>
        disposed || !TryParse(date, out var day) ? Task.CompletedTask : SelectAsync(day);

    private async Task AttachAsync()
    {
        module = await Runtime.InvokeAsync<IJSObjectReference>("import", ModulePath).ConfigureAwait(true);
        if (disposed)
        {
            await ReleaseAsync().ConfigureAwait(true);
            return;
        }

        reference = DotNetObjectReference.Create(this);
        instance = await module.InvokeAsync<IJSObjectReference>("attachMiniMonth", reference, element).ConfigureAwait(true);
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
