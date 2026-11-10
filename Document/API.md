# MusselCalendar API reference

## 🗓️ MusselCalendarView

The calendar component. It fills the height of its parent, and other attributes such as `class`, `style` and `id` go to the root element.  

| Name | Type | Default | Description |
|---|---|---|---|
| `View` | `CalendarViewKind` | `Week` | `Day`, `Week`, `Days`, `Month`, `Schedule` or `Year` |
| `Date` | `DateOnly` | today | The day, the first of the N days or of the schedule, or a day in the week, the month or the year to show |
| `DayCount` | `int` | `4` | Days of `Days` |
| `FirstDayOfWeek` | `DayOfWeek` | `Sunday` | The first day of `Week`, `Month` and `Year` |
| `Items` | `IReadOnlyList<CalendarItem>` | empty | The items to show |
| `SlotMinutes` | `int` | `15` | The step of selecting, moving and resizing in the time grid |
| `DefaultMinutes` | `int` | `60` | The length of a range selected by a click, and of an all-day item dropped on the time grid |
| `HourHeight` | `int` | `48` | Pixels of an hour |
| `ScrollHour` | `int` | `8` | The hour at the top when the time grid appears |
| `MonthLanes` | `int` | `0` | Lanes of a month cell; `0` fits them to the row height. When a day has more items, the last lane shows how many are hidden |
| `OverlapMode` | `CalendarOverlapMode` | `Columns` | Overlapping timed items side by side (`Columns`) or stacked with an offset (`Stack`) |
| `ReadOnly` | `bool` | `false` | Stops selecting ranges, and moving and resizing items |
| `AllDayText` | `string` | `All day` | The label of the all-day lane, and the time of items that cover the whole day in the schedule |
| `MoreFormat` | `string` | `+{0}` | The text of the hidden items of a month cell |
| `EmptyText` | `string` | `No events` | The text of a schedule without items |
| `ItemCountFormat` | `string` | `Events: {0}` | The count of items added to the label of a day in the year view |
| `HourFormat`, `TimeFormat` | `string` | `H:mm` | Formats of the hour labels and of the times of items, such as `h tt` and `h:mm tt`. A single custom specifier needs `%` (`%H`), because one letter alone is a standard format |
| `MonthDayFormat` | `string` | `M/d` | The format of the first day of a month in the month view, such as `MMM d` |
| `ScheduleDayFormat` | `string` | `MMM, ddd` | The format beside each date of the schedule |
| `SecondaryOffset` | `TimeSpan?` | `null` | The difference of a second time zone from the shown hours, such as `-9:00` for UTC beside Japan time; adds its hour labels, none when `null` |
| `TimeZoneText`, `SecondaryTimeZoneText` | `string?` | `null` | Labels above the two columns of hour labels, shown with `SecondaryOffset` |
| `Culture` | `CultureInfo?` | `null` | Weekday names and labels; the current culture when `null`. Dates are written with the Gregorian calendar, also for cultures whose default calendar is another one |
| `TimeProvider` | `TimeProvider?` | `null` | Today and the current time line; the system clock when `null` |

On Interactive Server, the defaults of `Date` and `TimeProvider` come from the clock and time zone of the server. Pass the user's date and a `TimeProvider` with the user's time zone.  
The view does not convert time zones: give `SecondaryOffset` for the shown range, as one offset is used for the whole range.  

### Events

| Name | Arguments | Description |
|---|---|---|
| `RangeSelected` | `CalendarSelectEventArgs` | A click or a drag on empty space, with `Start`, `End`, `AllDay` and `Anchor` |
| `ItemClicked` | `CalendarItemEventArgs` | A click, a tap, Enter or Space on an item, with `Item` and `Anchor` |
| `ItemChanged` | `CalendarItemChangeEventArgs` | A move or a resize, with `Item` and the new `Start`, `End` and `AllDay` |
| `MoreClicked` | `CalendarMoreEventArgs` | The hidden items of a month cell, with `Day`, every item of the day in `Items`, and `Anchor` |
| `DayClicked` | `DateOnly` | The date of a day header, a month cell, a schedule day or a day of the year view |

`ItemChanged` does not change the item. Set the new values on the item to accept them; otherwise the item stays where it was.  
Read-only items raise `ItemClicked` only. With `ReadOnly`, every item does so, and `RangeSelected` is not raised; dates and hidden counts still raise their events.  
`Anchor` (`CalendarAnchor` with `Left`, `Top`, `Width` and `Height`) is the place of the item, the range or the hidden count in the viewport, for a popup.  

### Operations

| Operation | Mouse and pen | Touch | Keyboard |
|---|---|---|---|
| Select a range | Click, or drag on empty space | Tap, or long press and drag | |
| Open an item | Click | Tap | Enter or Space |
| Move an item | Drag | Long press and drag | Alt+Left or Right by a day, Alt+Up or Down by a slot |
| Change the end | Drag the bottom edge | Long press on the bottom edge and drag | Alt+Shift+Up or Down |
| Cancel a drag | Esc | | |

On touch, a long press released without moving acts as a tap.  
The schedule and the year views do not select or move: items of the schedule open by a click, a tap, Enter or Space, and a day of the year view raises `DayClicked`.  
Each month of the year view is an ARIA grid named by its title. Tab stops at one day of the year; arrow keys move by a day (Left or Right) or a week (Up or Down), Home and End to the first and last day of the week, Page Up and Page Down by a month within the year, and Enter or Space raises `DayClicked`. The date of today has `aria-current="date"` in every view.  

A timed item dropped on the all-day lane becomes an all-day item of that day, and an all-day item dropped on the time grid gets `DefaultMinutes`.  
A drag near the top or bottom of the time grid scrolls it. On touch, moving before the long press scrolls the page.  

## 📅 MusselMiniMonth

A small month to pick a date, also without `MusselCalendarView`. Other attributes such as `class` and `id` go to the root element.  

| Name | Type | Default | Description |
|---|---|---|---|
| `Date`, `DateChanged` | `DateOnly` | today | The selected date; works with `@bind-Date` |
| `Month`, `MonthChanged` | `DateOnly` | the month of `Date` | The shown month (its first day); works with `@bind-Month`. When `Month` is given, a new `Date` does not change the shown month |
| `FirstDayOfWeek` | `DayOfWeek` | `Sunday` | The first day of the week |
| `Highlight` | `CalendarRange?` | `null` | Days to mark, such as the range of the calendar view |
| `PreviousText`, `NextText` | `string` | `Previous month`, `Next month` | Labels of the arrow buttons |
| `Culture` | `CultureInfo?` | `null` | The month title and weekday names; the current culture when `null`, written with the Gregorian calendar |
| `TimeProvider` | `TimeProvider?` | `null` | Today; the system clock when `null` |

The arrow buttons change the shown month without changing `Date`. `MonthChanged` reports every change of the shown month: by the buttons, by the keys, and, without `Month`, by a new `Date` in another month.  
The days are an ARIA grid like the date picker of the WAI-ARIA Authoring Practices, named by the title, which is announced when the month changes. Tab stops at one day (the selected date, or the first day of the shown month); arrow keys move by a day or a week, Home and End to the first and last day of the week, Page Up and Page Down by a month and with Shift by a year, and Enter or Space selects. Moving out of the shown month shows the month of the day. The selected date has `aria-selected` and today `aria-current="date"`.  

## 📌 CalendarItem

| Name | Description |
|---|---|
| `Id` | Required; unique in `Items` |
| `Title` | Required |
| `Start`, `End` | Local times; `End` is not included, so an all-day item of one day ends at 0:00 of the next day |
| `AllDay` | Shows the item in the all-day lane |
| `Color` | A CSS color; `--mussel-calendar-event` when `null` |
| `ReadOnly` | The item cannot be moved or resized |

Timed items of 24 hours or more are shown in the all-day lane, and timed items over midnight are split by day.  

## 📐 CalendarRange

The days a view shows.  

| Name | Description |
|---|---|
| `new CalendarRange(start, until)` | The days from `start` to the day before `until` |
| `Start`, `End`, `Days` | The first day, the day after the last, and the number of days |
| `StartTime`, `EndTime` | `Start` and `End` at 0:00 |
| `Contains(day)` | Whether the range has the day |
| `Of(view, day, firstDayOfWeek, dayCount)` | The range of a view; a month covers whole weeks, the schedule 30 days from the day, and a year January to December |
| `Move(view, day, direction, dayCount)` | The date of the previous (`-1`) or next (`1`) page |
| `StartOfWeek(day, firstDayOfWeek)` | The first day of the week of the day |

## 🎨 Styles

Set the variables on the root element with `style` or a class of your own. Add `mussel-dark` to a container or the root element for the dark values (the same class as MusselChart). `MusselMiniMonth` follows `--mussel-primary-text` and `--mussel-muted-text`, and takes `--mussel-calendar-today`, `--mussel-calendar-today-text`, `--mussel-calendar-hover`, `--mussel-calendar-range` and `--mussel-calendar-mini-day-size` on its root.  

| Variable | Default | Description |
|---|---|---|
| `--mussel-calendar-line` | `--mussel-grid-line`, `#dadce0` | Grid lines |
| `--mussel-calendar-text`, `--mussel-calendar-muted` | `--mussel-primary-text`, `#3c4043` and `--mussel-muted-text`, `#70757a` | Text and labels |
| `--mussel-calendar-background`, `--mussel-calendar-outside` | `#ffffff`, `#f8f9fa` | The background, and days outside the month |
| `--mussel-calendar-today`, `--mussel-calendar-today-text` | `#1a73e8`, `#ffffff` | The date of today, the focus ring and the marks of the year view |
| `--mussel-calendar-now` | `#ea4335` | The current time line |
| `--mussel-calendar-event`, `--mussel-calendar-event-text` | `#039be5`, `#ffffff` | Items without `Color` |
| `--mussel-calendar-hover` | `rgba(60, 64, 67, 0.08)` | Dates, chips, hidden counts, schedule items and days of the year view under the pointer |
| `--mussel-calendar-range`, `--mussel-calendar-range-border` | `rgba(26, 115, 232, 0.16)`, `rgba(26, 115, 232, 0.6)` | The range while selecting |
| `--mussel-calendar-gutter` | `56px` | The width of the hour labels; twice with `SecondaryOffset` |
| `--mussel-calendar-lane-height` | `22px` | Lanes of the all-day row and month cells |
| `--mussel-calendar-schedule-date-width`, `--mussel-calendar-schedule-time-width` | `120px`, `120px` | The date and the time columns of the schedule |
| `--mussel-calendar-mini-day-size` | `26px` | A day of `MusselMiniMonth`; the columns and the digits follow |
