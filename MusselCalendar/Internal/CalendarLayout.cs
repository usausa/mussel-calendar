namespace MusselCalendar.Internal;

internal readonly record struct TimedSegment(CalendarItem Item, int Day, int StartMinute, int EndMinute, bool IsStart, bool IsEnd, double Left, double Width, int Layer);

internal readonly record struct BarSegment(CalendarItem Item, int Column, int Span, int Lane, bool IsStart, bool IsEnd);

internal sealed record MonthWeek(DateOnly Start, IReadOnlyList<BarSegment> Bars, IReadOnlyList<int> More);

internal sealed record ScheduleDay(DateOnly Date, IReadOnlyList<CalendarItem> Items);

internal static class CalendarLayout
{
    public const int MinutesPerDay = 1440;

    private const double StackWidthFactor = 1.7;

    public static bool InAllDayLane(CalendarItem item) =>
        item.AllDay || ((item.End - item.Start) >= TimeSpan.FromDays(1));

    public static DateOnly FirstDay(CalendarItem item) => DateOnly.FromDateTime(item.Start);

    public static DateOnly EndDay(CalendarItem item)
    {
        var last = DateOnly.FromDateTime(item.End);
        if (!item.AllDay && (item.End.TimeOfDay > TimeSpan.Zero))
        {
            last = last.AddDays(1);
        }

        var first = FirstDay(item);
        return last.DayNumber > first.DayNumber ? last : first.AddDays(1);
    }

    public static int DayCount(CalendarItem item) => EndDay(item).DayNumber - FirstDay(item).DayNumber;

    public static bool CoversDay(CalendarItem item, DateOnly day)
    {
        if (item.AllDay)
        {
            return true;
        }

        var start = day.ToDateTime(TimeOnly.MinValue);
        return (item.Start <= start) && (item.End >= start.AddDays(1));
    }

    public static List<ScheduleDay> BuildSchedule(IReadOnlyList<CalendarItem> items, CalendarRange range)
    {
        var result = new List<ScheduleDay>();
        for (var day = range.Start; day < range.End; day = day.AddDays(1))
        {
            var current = day;
            var found = items
                .Where(x => (FirstDay(x) <= current) && (current < EndDay(x)))
                .OrderBy(x => !CoversDay(x, current))
                .ThenBy(static x => x.Start)
                .ToList();
            if (found.Count > 0)
            {
                result.Add(new ScheduleDay(current, found));
            }
        }

        return result;
    }

    public static Dictionary<DateOnly, int> CountDays(IEnumerable<CalendarItem> items, CalendarRange range)
    {
        var counts = new Dictionary<DateOnly, int>();
        foreach (var item in items)
        {
            var first = FirstDay(item);
            var end = EndDay(item);
            for (var day = first < range.Start ? range.Start : first; (day < end) && (day < range.End); day = day.AddDays(1))
            {
                counts[day] = counts.GetValueOrDefault(day) + 1;
            }
        }

        return counts;
    }

    public static List<TimedSegment> BuildTimed(IReadOnlyList<CalendarItem> items, DateOnly firstDay, int days, int minMinutes, CalendarOverlapMode overlapMode)
    {
        var result = new List<TimedSegment>();
        var pieces = new List<Piece>();
        for (var day = 0; day < days; day++)
        {
            var dayStart = firstDay.AddDays(day).ToDateTime(TimeOnly.MinValue);
            var dayEnd = dayStart.AddDays(1);
            pieces.Clear();
            for (var order = 0; order < items.Count; order++)
            {
                var item = items[order];
                var itemEnd = item.End > item.Start ? item.End : item.Start.AddTicks(1);
                if (InAllDayLane(item) || (itemEnd <= dayStart) || (item.Start >= dayEnd))
                {
                    continue;
                }

                var start = item.Start < dayStart ? dayStart : item.Start;
                var end = itemEnd > dayEnd ? dayEnd : itemEnd;
                var startMinute = (int)(start - dayStart).TotalMinutes;
                var endMinute = Math.Min(Math.Max((int)(end - dayStart).TotalMinutes, startMinute + minMinutes), MinutesPerDay);
                pieces.Add(new Piece(item, startMinute, endMinute, item.Start >= dayStart, itemEnd <= dayEnd, order));
            }

            Place(pieces, day, overlapMode, result);
        }

        return result;
    }

    public static List<BarSegment> BuildBars(IEnumerable<CalendarItem> items, DateOnly firstDay, int days, out int laneCount)
    {
        var last = firstDay.AddDays(days);
        var spans = new List<Span>();
        var order = 0;
        foreach (var item in items)
        {
            order++;
            var start = FirstDay(item);
            var end = EndDay(item);
            if ((end.DayNumber <= firstDay.DayNumber) || (start.DayNumber >= last.DayNumber))
            {
                continue;
            }

            var column = Math.Max(start.DayNumber - firstDay.DayNumber, 0);
            var endColumn = Math.Min(end.DayNumber - firstDay.DayNumber, days);
            spans.Add(new Span(item, column, endColumn, start.DayNumber >= firstDay.DayNumber, end.DayNumber <= last.DayNumber, order));
        }

        spans.Sort(CompareSpans);

        var lanes = new List<bool[]>();
        var result = new List<BarSegment>();
        foreach (var span in spans)
        {
            var lane = lanes.FindIndex(used => IsFree(used, span.Column, span.End));
            if (lane < 0)
            {
                lanes.Add(new bool[days]);
                lane = lanes.Count - 1;
            }

            for (var i = span.Column; i < span.End; i++)
            {
                lanes[lane][i] = true;
            }

            result.Add(new BarSegment(span.Item, span.Column, span.End - span.Column, lane, span.IsStart, span.IsEnd));
        }

        laneCount = lanes.Count;
        return result;
    }

    public static MonthWeek BuildMonthWeek(IEnumerable<CalendarItem> items, DateOnly weekStart, int maxLanes)
    {
        var bars = BuildBars(items, weekStart, 7, out _);
        var limit = Math.Max(maxLanes, 1);
        var visible = new int[7];
        Array.Fill(visible, limit);
        bool changed;
        do
        {
            changed = false;
            foreach (var bar in bars)
            {
                if (IsShown(bar, visible))
                {
                    continue;
                }

                for (var i = bar.Column; i < bar.Column + bar.Span; i++)
                {
                    if (visible[i] == limit)
                    {
                        visible[i] = limit - 1;
                        changed = true;
                    }
                }
            }
        }
        while (changed);

        var shown = new List<BarSegment>();
        var more = new int[7];
        foreach (var bar in bars)
        {
            if (IsShown(bar, visible))
            {
                shown.Add(bar);
                continue;
            }

            for (var i = bar.Column; i < bar.Column + bar.Span; i++)
            {
                more[i]++;
            }
        }

        return new MonthWeek(weekStart, shown, more);
    }

    private static bool IsShown(BarSegment bar, int[] visible)
    {
        for (var i = bar.Column; i < bar.Column + bar.Span; i++)
        {
            if (bar.Lane >= visible[i])
            {
                return false;
            }
        }

        return true;
    }

    private static int CompareSpans(Span a, Span b)
    {
        var compare = a.Column.CompareTo(b.Column);
        if (compare == 0)
        {
            compare = (b.End - b.Column).CompareTo(a.End - a.Column);
        }

        if (compare == 0)
        {
            compare = b.Item.AllDay.CompareTo(a.Item.AllDay);
        }

        if (compare == 0)
        {
            compare = a.Item.Start.CompareTo(b.Item.Start);
        }

        return compare == 0 ? a.Order.CompareTo(b.Order) : compare;
    }

    private static bool IsFree(bool[] used, int start, int end)
    {
        for (var i = start; i < end; i++)
        {
            if (used[i])
            {
                return false;
            }
        }

        return true;
    }

    private static void Place(List<Piece> pieces, int day, CalendarOverlapMode overlapMode, List<TimedSegment> result)
    {
        pieces.Sort(ComparePieces);
        var index = 0;
        while (index < pieces.Count)
        {
            var last = index;
            var clusterEnd = pieces[index].End;
            while ((last + 1 < pieces.Count) && (pieces[last + 1].Start < clusterEnd))
            {
                last++;
                clusterEnd = Math.Max(clusterEnd, pieces[last].End);
            }

            var columnEnds = new List<int>();
            var columns = new int[last - index + 1];
            for (var i = index; i <= last; i++)
            {
                var start = pieces[i].Start;
                var column = columnEnds.FindIndex(end => end <= start);
                if (column < 0)
                {
                    columnEnds.Add(pieces[i].End);
                    column = columnEnds.Count - 1;
                }
                else
                {
                    columnEnds[column] = pieces[i].End;
                }

                columns[i - index] = column;
            }

            var count = columnEnds.Count;
            for (var i = index; i <= last; i++)
            {
                var column = columns[i - index];
                var span = 1;
                while ((column + span < count) && !Collides(pieces, columns, index, last, i, column + span))
                {
                    span++;
                }

                var left = (double)column / count;
                var width = (double)span / count;
                if ((overlapMode == CalendarOverlapMode.Stack) && (column + span < count))
                {
                    width = Math.Min(1 - left, width * StackWidthFactor);
                }

                var piece = pieces[i];
                result.Add(new TimedSegment(piece.Item, day, piece.Start, piece.End, piece.IsStart, piece.IsEnd, left, width, column));
            }

            index = last + 1;
        }
    }

    private static int ComparePieces(Piece a, Piece b)
    {
        var compare = a.Start.CompareTo(b.Start);
        if (compare == 0)
        {
            compare = (b.End - b.Start).CompareTo(a.End - a.Start);
        }

        return compare == 0 ? a.Order.CompareTo(b.Order) : compare;
    }

    private static bool Collides(List<Piece> pieces, int[] columns, int first, int last, int target, int column)
    {
        for (var i = first; i <= last; i++)
        {
            if ((columns[i - first] == column) && (pieces[i].Start < pieces[target].End) && (pieces[i].End > pieces[target].Start))
            {
                return true;
            }
        }

        return false;
    }

    private readonly record struct Piece(CalendarItem Item, int Start, int End, bool IsStart, bool IsEnd, int Order);

    private readonly record struct Span(CalendarItem Item, int Column, int End, bool IsStart, bool IsEnd, int Order);
}
