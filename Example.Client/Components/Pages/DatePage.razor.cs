namespace Example.Client.Components.Pages;

public sealed partial class DatePage
{
    private DateOnly date = DateOnly.FromDateTime(DateTime.Today);

    private DateOnly ShownMonth { get; set; } = new(DateTime.Today.Year, DateTime.Today.Month, 1);

    private DateOnly NextMonth => ShownMonth.AddMonths(1);

    private void OnNextMonthChanged(DateOnly value) => ShownMonth = value.AddMonths(-1);
}
