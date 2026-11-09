# MusselCalendar API reference

## 🗓️ MusselCalendarView

The calendar component. It fills the height of its parent, and other attributes such as `class`, `style` and `id` go to the root element.  

| Name | Type | Default | Description |
|---|---|---|---|
| `View` | `CalendarViewKind` | `Week` | `Day`, `Week`, `Days` or `Month` |
| `Date` | `DateOnly` | today | The day, the first of the N days, or a day in the week or the month to show |
| `DayCount` | `int` | `4` | Days of `Days` |
| `FirstDayOfWeek` | `DayOfWeek` | `Sunday` | The first day of `Week` and `Month` |
| `Items` | `IReadOnlyList<CalendarItem>` | empty | The items to show |
| `SlotMinutes` | `int` | `15` | The step of selecting, moving and resizing in the time grid |
| `DefaultMinutes` | `int` | `60` | The length of a range selected by a click, and of an all-day item dropped on the time grid |
| `HourHeight` | `int` | `48` | Pixels of an hour |
| `ScrollHour` | `int` | `8` | The hour at the top when the time grid appears |
| `MonthLanes` | `int` | `3` | Lanes of a month cell; when a day has more items, the last lane shows how many are hidden |
| `OverlapMode` | `CalendarOverlapMode` | `Columns` | Overlapping timed items side by side (`Columns`) or stacked with an offset (`Stack`) |
| `AllDayText` | `string` | `All day` | The label of the all-day lane |
| `MoreFormat` | `string` | `+{0}` | The text of the hidden items of a month cell |
| `Culture` | `CultureInfo?` | `null` | Weekday names and labels; the current culture when `null` |
| `TimeProvider` | `TimeProvider?` | `null` | Today and the current time line; the system clock when `null` |

### Events

| Name | Arguments | Description |
|---|---|---|
| `RangeSelected` | `CalendarSelectEventArgs` | A click or a drag on empty space, with `Start`, `End`, `AllDay` and `Anchor` |
| `ItemClicked` | `CalendarItemEventArgs` | A click, a tap, Enter or Space on an item, with `Item` and `Anchor` |
| `ItemChanged` | `CalendarItemChangeEventArgs` | A move or a resize, with `Item` and the new `Start`, `End` and `AllDay` |
| `MoreClicked` | `CalendarMoreEventArgs` | The hidden items of a month cell, with `Day`, every item of the day in `Items`, and `Anchor` |
| `DayClicked` | `DateOnly` | The date of a day header or a month cell |

`ItemChanged` does not change the item. Set the new values on the item to accept them; otherwise the item stays where it was.  
Read-only items raise `ItemClicked` only.  
`Anchor` (`CalendarAnchor` with `Left`, `Top`, `Width` and `Height`) is the place of the item, the range or the click in the viewport, for a popup.  

### Operations

| Operation | Mouse and pen | Touch | Keyboard |
|---|---|---|---|
| Select a range | Click, or drag on empty space | Tap, or long press and drag | |
| Open an item | Click | Tap | Enter or Space |
| Move an item | Drag | Long press and drag | Alt+Left or Right by a day, Alt+Up or Down by a slot |
| Change the end | Drag the bottom edge | Long press on the bottom edge and drag | Alt+Shift+Up or Down |
| Cancel a drag | Esc | | |

A timed item dropped on the all-day lane becomes an all-day item of that day, and an all-day item dropped on the time grid gets `DefaultMinutes`.  
A drag near the top or bottom of the time grid scrolls it. On touch, moving before the long press scrolls the page.  

## 📅 MusselMiniMonth

A small month to pick a date. Other attributes such as `class` and `id` go to the root element.  

| Name | Type | Default | Description |
|---|---|---|---|
| `Date`, `DateChanged` | `DateOnly` | today | The selected date; works with `@bind-Date` |
| `FirstDayOfWeek` | `DayOfWeek` | `Sunday` | The first day of the week |
| `Highlight` | `CalendarRange?` | `null` | Days to mark, such as the range of the calendar view |
| `PreviousText`, `NextText` | `string` | `Previous month`, `Next month` | Labels of the arrow buttons |
| `Culture` | `CultureInfo?` | `null` | The month title and weekday names; the current culture when `null` |
| `TimeProvider` | `TimeProvider?` | `null` | Today; the system clock when `null` |

The arrows change the shown month without changing `Date`.  

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
| `Of(view, day, firstDayOfWeek, dayCount)` | The range of a view; a month covers whole weeks |
| `Move(view, day, direction, dayCount)` | The date of the previous (`-1`) or next (`1`) page |
| `StartOfWeek(day, firstDayOfWeek)` | The first day of the week of the day |

## 🎨 Styles

Set the variables on the root element with `style` or a class of your own. `MusselMiniMonth` follows `--mussel-primary-text` and `--mussel-muted-text`.  

| Variable | Default | Description |
|---|---|---|
| `--mussel-calendar-line` | `--mussel-grid-line`, `#dadce0` | Grid lines |
| `--mussel-calendar-text`, `--mussel-calendar-muted` | `--mussel-primary-text`, `#3c4043` and `--mussel-muted-text`, `#70757a` | Text and labels |
| `--mussel-calendar-background`, `--mussel-calendar-outside` | `#ffffff`, `#f8f9fa` | The background, and days outside the month |
| `--mussel-calendar-today`, `--mussel-calendar-today-text` | `#1a73e8`, `#ffffff` | The date of today and the focus ring |
| `--mussel-calendar-now` | `#ea4335` | The current time line |
| `--mussel-calendar-event`, `--mussel-calendar-event-text` | `#039be5`, `#ffffff` | Items without `Color` |
| `--mussel-calendar-gutter` | `56px` | The width of the hour labels |
| `--mussel-calendar-lane-height` | `22px` | Lanes of the all-day row and month cells |
