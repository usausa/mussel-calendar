namespace Example.Client.Components.Pages;

public sealed partial class DatePage
{
    private static readonly DateOnly LastMonth = new(DateOnly.MaxValue.Year, DateOnly.MaxValue.Month, 1);

    private DateOnly date = DateOnly.FromDateTime(DateTime.Today);

    private DateOnly ShownMonth { get; set; } = new(DateTime.Today.Year, DateTime.Today.Month, 1);

    private DateOnly? NextMonth => ShownMonth < LastMonth ? ShownMonth.AddMonths(1) : null;

    private void OnNextMonthChanged(DateOnly value) => ShownMonth = value > DateOnly.MinValue ? value.AddMonths(-1) : value;
}
