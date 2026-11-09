const threshold = 4;
const touchTolerance = 8;
const touchDelay = 250;
const scrollEdge = 40;
const scrollSpeed = 16;
const scrollMargin = 8;

const pad = (n) => String(n).padStart(2, "0");
const parse = (iso) => Date.parse(`${iso}Z`) / 60000;
const format = (minutes) => {
    const d = new Date(minutes * 60000);
    return `${d.getUTCFullYear()}-${pad(d.getUTCMonth() + 1)}-${pad(d.getUTCDate())}T${pad(d.getUTCHours())}:${pad(d.getUTCMinutes())}:00`;
};
const dayStart = (date) => parse(`${date}T00:00:00`);
const dayDiff = (from, to) => Math.round((dayStart(to) - dayStart(from)) / 1440);
const addDays = (date, days) => format(dayStart(date) + (days * 1440)).slice(0, 10);
const contains = (rect, x, y) => (x >= rect.left) && (x < rect.right) && (y >= rect.top) && (y < rect.bottom);
const arrowKeys = ["ArrowLeft", "ArrowRight", "ArrowUp", "ArrowDown"];
const anchorOf = (rect) => [rect.left, rect.top, rect.width, rect.height];
const preventDefault = (e) => e.preventDefault();
const themeNames = ["--mussel-calendar-range", "--mussel-calendar-range-border", "--mussel-calendar-event-text"];
const applyTheme = (element, theme) => {
    for (const [name, value] of theme) {
        element.style.setProperty(name, value);
    }
};

class Calendar {
    constructor(root, dotNet) {
        this.root = root;
        this.dotNet = dotNet;
        this.drag = null;
        this.pending = null;
        this.previews = [];
        this.frame = 0;
        this.settling = null;
        this.onPointerDown = (e) => this.pointerDown(e);
        this.onPointerMove = (e) => this.pointerMove(e);
        this.onPointerUp = (e) => this.pointerUp(e, true);
        this.onPointerCancel = (e) => this.pointerUp(e, false);
        this.onTouchStart = (e) => this.touchStart(e);
        this.onTouchMove = (e) => this.touchMove(e);
        this.onTouchEnd = (e) => this.touchEnd(e);
        this.onKeyDown = (e) => this.keyDown(e);
        this.onClick = (e) => this.clickRoot(e);
        this.onRootTouchMove = (e) => {
            if (this.drag && e.cancelable) {
                e.preventDefault();
            }
        };
        this.onWindowKeyDown = (e) => {
            if ((e.key === "Escape") && this.drag) {
                e.preventDefault();
                if (this.pending) {
                    this.pending.canceled = true;
                }
                this.finish(false);
            }
        };
        root.addEventListener("pointerdown", this.onPointerDown);
        root.addEventListener("touchstart", this.onTouchStart, { passive: true });
        root.addEventListener("keydown", this.onKeyDown);
        root.addEventListener("click", this.onClick);
        root.addEventListener("touchmove", this.onRootTouchMove, { passive: false });
        this.lanes = 0;
        this.resizeObserver = new ResizeObserver(() => this.fit());
        this.resizeObserver.observe(root);
    }

    get slot() {
        return Number(this.root.dataset.slot) || 15;
    }

    get hourHeight() {
        return Number(this.root.dataset.hourHeight) || 48;
    }

    get defaultMinutes() {
        return Number(this.root.dataset.defaultMinutes) || 60;
    }

    scroll(hour) {
        const body = this.root.querySelector(".mc-body");
        if (body) {
            body.scrollTop = Math.max((hour * this.hourHeight) - scrollMargin, 0);
        }
    }

    fit() {
        const week = this.root.querySelector(".mc-week");
        if ((this.root.dataset.fitLanes !== "true") || !week) {
            return;
        }
        const style = getComputedStyle(week);
        const tracks = style.gridTemplateRows.split(" ").map(parseFloat);
        const gap = parseFloat(style.rowGap) || 0;
        if ((tracks.length < 3) || !(tracks[1] > 0)) {
            return;
        }
        const lanes = Math.max(1, Math.floor((week.clientHeight - tracks[0] - gap) / (tracks[1] + gap)));
        if (lanes !== this.lanes) {
            this.lanes = lanes;
            this.dotNet.invokeMethodAsync("OnMonthLanes", lanes).catch(() => {
                this.lanes = 0;
            });
        }
    }

    columnAt(x) {
        const columns = Array.from(this.root.querySelectorAll(".mc-column")).map((element) => ({ element, rect: element.getBoundingClientRect() }));
        if (columns.length === 0) {
            return null;
        }
        if (x < columns[0].rect.left) {
            return columns[0];
        }
        return columns.find((c) => (x >= c.rect.left) && (x < c.rect.right)) ?? columns[columns.length - 1];
    }

    cellAt(x, y) {
        for (const cell of this.root.querySelectorAll(".mc-cell")) {
            if (contains(cell.getBoundingClientRect(), x, y)) {
                return cell;
            }
        }
        return null;
    }

    zoneAt(x, y) {
        const allDay = this.root.querySelector(".mc-allday");
        if (allDay && contains(allDay.getBoundingClientRect(), x, y)) {
            return "allday";
        }
        const body = this.root.querySelector(".mc-body");
        if (body && (y >= body.getBoundingClientRect().top)) {
            return "time";
        }
        return this.root.querySelector(".mc-month") ? "month" : null;
    }

    minuteIn(column, y, round) {
        const raw = (y - column.rect.top) / (this.hourHeight / 60);
        return Math.min(Math.max(round(raw / this.slot) * this.slot, 0), 1440);
    }

    timeAt(x, y, round) {
        const column = this.columnAt(x);
        return column ? { column, minutes: dayStart(column.element.dataset.date) + this.minuteIn(column, y, round) } : null;
    }

    itemOf(target) {
        const item = target instanceof Element ? target.closest(".mc-event[data-id], .mc-bar[data-id]") : null;
        return item && this.root.contains(item) ? item : null;
    }

    entryOf(target) {
        const entry = target instanceof Element ? target.closest(".mc-schedule-item[data-id]") : null;
        return entry && this.root.contains(entry) ? entry : null;
    }

    kindOf(target) {
        if (!(target instanceof Element) || target.closest("button")) {
            return null;
        }
        const handle = target.closest(".mc-resize");
        if (handle) {
            const item = this.itemOf(handle);
            return item ? { kind: "resize", item } : null;
        }
        const item = this.itemOf(target);
        if (item) {
            return { kind: item.classList.contains("mc-event") ? "move-time" : "move-day", item };
        }
        if (target.closest(".mc-column")) {
            return { kind: "create-time" };
        }
        return target.closest(".mc-cell") ? { kind: "create-day" } : null;
    }

    keyDown(e) {
        const entry = this.entryOf(e.target);
        if (entry) {
            if ((e.key === "Enter") || (e.key === " ")) {
                e.preventDefault();
                this.dotNet.invokeMethodAsync("OnItemClick", entry.dataset.id, ...anchorOf(entry.getBoundingClientRect()));
            }
            return;
        }
        const item = this.itemOf(e.target);
        if (!item || this.drag || this.settling) {
            return;
        }
        const id = item.dataset.id;
        if ((e.key === "Enter") || (e.key === " ")) {
            e.preventDefault();
            this.dotNet.invokeMethodAsync("OnItemClick", id, ...anchorOf(item.getBoundingClientRect()));
            return;
        }
        if (!e.altKey) {
            return;
        }
        if (item.dataset.readOnly === "true") {
            if (arrowKeys.includes(e.key)) {
                e.preventDefault();
            }
            return;
        }
        let call = null;
        if ((e.key === "ArrowLeft") || (e.key === "ArrowRight")) {
            call = this.dotNet.invokeMethodAsync("OnShift", id, e.key === "ArrowLeft" ? -1 : 1, 0);
        } else if (item.classList.contains("mc-event") && ((e.key === "ArrowUp") || (e.key === "ArrowDown"))) {
            const minutes = e.key === "ArrowUp" ? -this.slot : this.slot;
            call = e.shiftKey ? this.dotNet.invokeMethodAsync("OnExtend", id, minutes) : this.dotNet.invokeMethodAsync("OnShift", id, 0, minutes);
        }
        if (call) {
            e.preventDefault();
            this.settle(call, id);
        }
    }

    pointerDown(e) {
        if (this.drag || this.pending || this.settling || (e.pointerType === "touch") || (e.button !== 0)) {
            return;
        }
        const found = this.kindOf(e.target);
        if (!found) {
            return;
        }
        if (!found.item) {
            e.preventDefault();
        }
        this.pending = { ...found, x: e.clientX, y: e.clientY, pointerId: e.pointerId, touch: false };
        window.addEventListener("pointermove", this.onPointerMove, true);
        window.addEventListener("pointerup", this.onPointerUp, true);
        window.addEventListener("pointercancel", this.onPointerCancel, true);
        document.addEventListener("selectstart", preventDefault, true);
        document.addEventListener("dragstart", preventDefault, true);
    }

    removePointer() {
        window.removeEventListener("pointermove", this.onPointerMove, true);
        window.removeEventListener("pointerup", this.onPointerUp, true);
        window.removeEventListener("pointercancel", this.onPointerCancel, true);
        document.removeEventListener("selectstart", preventDefault, true);
        document.removeEventListener("dragstart", preventDefault, true);
    }

    pointerMove(e) {
        if (!this.pending || (e.pointerId !== this.pending.pointerId)) {
            return;
        }
        if ((e.pointerType === "mouse") && ((e.buttons & 1) === 0)) {
            this.pointerUp(e, true);
            return;
        }
        if (!this.drag && !this.pending.canceled && (Math.hypot(e.clientX - this.pending.x, e.clientY - this.pending.y) >= threshold)) {
            this.begin();
        }
        if (this.drag) {
            this.drag.x = e.clientX;
            this.drag.y = e.clientY;
            this.update();
        }
    }

    pointerUp(e, commit) {
        if (!this.pending || (e.pointerId !== this.pending.pointerId)) {
            return;
        }
        this.removePointer();
        const pending = this.pending;
        this.pending = null;
        if (this.drag) {
            this.drag.x = e.clientX;
            this.drag.y = e.clientY;
            this.suppressClick();
            this.finish(commit);
        } else if (pending.canceled) {
            this.suppressClick();
        } else if (commit) {
            this.click(pending);
        }
    }

    touchStart(e) {
        if (this.drag || this.pending || this.settling || (e.touches.length !== 1)) {
            return;
        }
        const found = this.kindOf(e.target);
        if (!found) {
            return;
        }
        const touch = e.touches[0];
        this.pending = { ...found, x: touch.clientX, y: touch.clientY, touchId: touch.identifier, touch: true, timer: 0 };
        this.pending.timer = setTimeout(() => {
            if (this.pending && !this.drag) {
                this.begin();
            }
        }, touchDelay);
        document.addEventListener("touchmove", this.onTouchMove, { passive: false });
        document.addEventListener("touchend", this.onTouchEnd, { passive: false });
        document.addEventListener("touchcancel", this.onTouchEnd, { passive: false });
        document.addEventListener("contextmenu", preventDefault, true);
    }

    touchOf(e) {
        return Array.from(e.changedTouches).find((t) => t.identifier === this.pending?.touchId);
    }

    touchMove(e) {
        const touch = this.touchOf(e);
        if (!touch) {
            return;
        }
        if (this.drag) {
            e.preventDefault();
            this.drag.x = touch.clientX;
            this.drag.y = touch.clientY;
            if (Math.hypot(touch.clientX - this.drag.originX, touch.clientY - this.drag.originY) > touchTolerance) {
                this.drag.moved = true;
            }
            this.update();
        } else if (Math.hypot(touch.clientX - this.pending.x, touch.clientY - this.pending.y) > touchTolerance) {
            this.clearTouch();
        }
    }

    touchEnd(e) {
        const touch = this.touchOf(e);
        if (!touch) {
            return;
        }
        const pending = this.pending;
        const dragging = !!this.drag;
        if (dragging) {
            this.drag.x = touch.clientX;
            this.drag.y = touch.clientY;
        }
        this.clearTouch();
        if (e.cancelable) {
            e.preventDefault();
        }
        if (dragging) {
            this.finish(e.type === "touchend");
        } else if ((e.type === "touchend") && !pending.canceled) {
            this.click(pending);
        }
    }

    clearTouch() {
        if (this.pending?.touch) {
            clearTimeout(this.pending.timer);
            this.pending = null;
        }
        document.removeEventListener("touchmove", this.onTouchMove, { passive: false });
        document.removeEventListener("touchend", this.onTouchEnd, { passive: false });
        document.removeEventListener("touchcancel", this.onTouchEnd, { passive: false });
        document.removeEventListener("contextmenu", preventDefault, true);
    }

    begin() {
        const p = this.pending;
        if (p.item?.dataset.readOnly === "true") {
            p.canceled = true;
            return;
        }
        const d = { kind: p.kind, item: p.item, x: p.x, y: p.y, originX: p.x, originY: p.y, moved: !p.touch, result: null };
        if (p.item) {
            d.id = p.item.dataset.id;
            d.start = p.item.dataset.start ? parse(p.item.dataset.start) : null;
            d.end = p.item.dataset.end ? parse(p.item.dataset.end) : null;
        }
        if (d.kind === "move-time") {
            const at = this.timeAt(p.x, p.y, (v) => v);
            d.grab = at ? at.minutes - d.start : 0;
        } else if (d.kind === "move-day") {
            d.grabDate = this.cellAt(p.x, p.y)?.dataset.date ?? null;
        } else if (d.kind === "create-time") {
            d.anchor = this.timeAt(p.x, p.y, Math.floor);
        } else if (d.kind === "create-day") {
            d.anchorDate = this.cellAt(p.x, p.y)?.dataset.date ?? null;
        } else if (d.kind === "resize") {
            d.column = this.columnAt(p.item.getBoundingClientRect().left + 1);
        }
        const style = getComputedStyle(this.root);
        d.theme = themeNames.map((name) => [name, style.getPropertyValue(name)]);
        for (const element of this.previews) {
            applyTheme(element, d.theme);
        }
        this.drag = d;
        if (d.id) {
            this.dim(d.id, true);
        }
        document.body.style.userSelect = "none";
        window.addEventListener("keydown", this.onWindowKeyDown, true);
        this.frame = requestAnimationFrame(() => this.tick());
        this.update();
    }

    dim(id, value) {
        for (const element of this.root.querySelectorAll(`[data-id="${CSS.escape(id)}"]`)) {
            element.classList.toggle("mc-dragging", value);
        }
    }

    tick() {
        const d = this.drag;
        if (!d) {
            return;
        }
        const body = this.root.querySelector(".mc-body");
        const zone = this.zoneAt(d.x, d.y);
        const scrollable = (d.kind === "create-time") || (d.kind === "resize") || ((d.kind === "move-time") && (zone !== "allday")) || ((d.kind === "move-day") && (zone === "time"));
        if (body && scrollable) {
            const rect = body.getBoundingClientRect();
            if ((d.x >= rect.left) && (d.x <= rect.right)) {
                const delta = d.y < rect.top + scrollEdge ? -scrollSpeed : d.y > rect.bottom - scrollEdge ? scrollSpeed : 0;
                if (delta !== 0) {
                    const before = body.scrollTop;
                    body.scrollTop += delta;
                    if (body.scrollTop !== before) {
                        this.update();
                    }
                }
            }
        }
        this.frame = requestAnimationFrame(() => this.tick());
    }

    update() {
        const d = this.drag;
        const zone = this.zoneAt(d.x, d.y);
        if ((d.kind === "create-time") && d.anchor) {
            d.anchor.column.rect = d.anchor.column.element.getBoundingClientRect();
            const minutes = this.minuteIn(d.anchor.column, d.y, Math.floor);
            const base = dayStart(d.anchor.column.element.dataset.date);
            const anchor = d.anchor.minutes - base;
            const start = Math.min(anchor, minutes);
            const end = Math.min(Math.max(anchor, minutes) + this.slot, 1440);
            d.result = { start: base + start, end: base + end, allDay: false };
            this.showTime(d.anchor.column, start, end, null);
        } else if ((d.kind === "create-day") && d.anchorDate) {
            const date = this.cellAt(d.x, d.y)?.dataset.date ?? d.anchorDate;
            const reverse = dayDiff(d.anchorDate, date) < 0;
            const first = reverse ? date : d.anchorDate;
            const last = reverse ? d.anchorDate : date;
            d.result = { start: dayStart(first), end: dayStart(addDays(last, 1)), allDay: true };
            this.showDays(first, addDays(last, 1));
        } else if (d.kind === "move-time") {
            if (zone === "allday") {
                const date = this.cellAt(d.x, d.y)?.dataset.date;
                if (date) {
                    d.result = { start: dayStart(date), end: dayStart(addDays(date, 1)), allDay: true };
                    this.showDays(date, addDays(date, 1));
                }
                return;
            }
            const at = this.timeAt(d.x, d.y, (v) => v);
            if (!at) {
                return;
            }
            const base = dayStart(at.column.element.dataset.date);
            const start = base + (Math.round((at.minutes - d.grab - base) / this.slot) * this.slot);
            const length = d.end - d.start;
            d.result = { start, end: start + length, allDay: false };
            this.showTime(at.column, start - base, Math.min(start - base + length, 1440), d.item);
        } else if (d.kind === "move-day") {
            if (zone === "time") {
                const at = this.timeAt(d.x, d.y, Math.floor);
                if (at) {
                    d.result = { start: at.minutes, end: at.minutes + this.defaultMinutes, allDay: false, convert: true };
                    const base = dayStart(at.column.element.dataset.date);
                    this.showTime(at.column, at.minutes - base, Math.min(at.minutes - base + this.defaultMinutes, 1440), d.item);
                }
                return;
            }
            const date = this.cellAt(d.x, d.y)?.dataset.date;
            if (date && d.grabDate) {
                const shift = dayDiff(d.grabDate, date);
                const first = d.item.dataset.first;
                const days = Number(d.item.dataset.days) || 1;
                d.result = { shift };
                this.showDays(addDays(first, shift), addDays(first, shift + days));
            }
        } else if ((d.kind === "resize") && d.column) {
            d.column.rect = d.column.element.getBoundingClientRect();
            const base = dayStart(d.column.element.dataset.date);
            const end = Math.max(base + this.minuteIn(d.column, d.y, Math.ceil), d.start + this.slot);
            d.result = { start: d.start, end, allDay: false };
            this.showTime(d.column, Math.max(d.start - base, 0), end - base, d.item);
        }
    }

    preview(index, item) {
        while (this.previews.length <= index) {
            const element = document.createElement("div");
            element.className = "mc-preview";
            element.setAttribute("aria-hidden", "true");
            if (this.drag) {
                applyTheme(element, this.drag.theme);
            }
            document.body.appendChild(element);
            this.previews.push(element);
        }
        const element = this.previews[index];
        element.style.display = "block";
        element.classList.toggle("mc-preview-range", !item);
        element.style.backgroundColor = item ? getComputedStyle(item).backgroundColor : "";
        element.textContent = item ? (item.querySelector(".mc-event-title, .mc-bar-title")?.textContent ?? "") : "";
        return element;
    }

    hidePreviews(from) {
        for (let i = from; i < this.previews.length; i++) {
            this.previews[i].style.display = "none";
        }
    }

    showTime(column, start, end, item) {
        const body = this.root.querySelector(".mc-body").getBoundingClientRect();
        const ppm = this.hourHeight / 60;
        const top = Math.max(column.rect.top + (start * ppm), body.top);
        const bottom = Math.min(column.rect.top + (end * ppm), body.bottom);
        const element = this.preview(0, item);
        Object.assign(element.style, {
            left: `${column.rect.left + 1}px`,
            top: `${top}px`,
            width: `${column.rect.width - 4}px`,
            height: `${Math.max(bottom - top, 0)}px`
        });
        this.hidePreviews(1);
    }

    showDays(first, end) {
        const rows = new Map();
        for (const cell of this.root.querySelectorAll(".mc-cell")) {
            const date = cell.dataset.date;
            if ((dayDiff(first, date) < 0) || (dayDiff(date, end) <= 0)) {
                continue;
            }
            const rect = cell.getBoundingClientRect();
            const key = Math.round(rect.top);
            const row = rows.get(key);
            if (row) {
                row.left = Math.min(row.left, rect.left);
                row.right = Math.max(row.right, rect.right);
            } else {
                rows.set(key, { left: rect.left, right: rect.right, top: rect.top, bottom: rect.bottom });
            }
        }
        let index = 0;
        for (const row of rows.values()) {
            Object.assign(this.preview(index, null).style, {
                left: `${row.left}px`,
                top: `${row.top}px`,
                width: `${row.right - row.left}px`,
                height: `${row.bottom - row.top}px`
            });
            index++;
        }
        this.hidePreviews(index);
    }

    finish(commit) {
        const d = this.drag;
        this.drag = null;
        cancelAnimationFrame(this.frame);
        window.removeEventListener("keydown", this.onWindowKeyDown, true);
        document.body.style.userSelect = "";
        const r = d?.result;
        let call = null;
        if (commit && d && !d.moved) {
            this.restore();
            this.click({ kind: d.kind, item: d.item, x: d.x, y: d.y });
            return;
        }
        if (commit && r) {
            if ((d.kind === "create-time") || (d.kind === "create-day")) {
                const rect = this.previews[0]?.getBoundingClientRect() ?? new DOMRect(d.x, d.y, 0, 0);
                this.dotNet.invokeMethodAsync("OnSelect", format(r.start), format(r.end), r.allDay, ...anchorOf(rect));
            } else if ((d.kind === "move-day") && !r.convert) {
                if (r.shift) {
                    call = this.dotNet.invokeMethodAsync("OnShift", d.id, r.shift, 0);
                }
            } else if ((r.start !== d.start) || (r.end !== d.end) || r.allDay || r.convert) {
                call = this.dotNet.invokeMethodAsync("OnMove", d.id, format(r.start), format(r.end), r.allDay);
            }
        }
        if (call) {
            this.settle(call, null);
        } else {
            this.restore();
        }
    }

    settle(call, focusId) {
        const state = { done: false, observer: null };
        this.settling = state;
        const end = () => {
            if (state.done) {
                return;
            }
            state.done = true;
            state.observer.disconnect();
            if (this.settling === state) {
                this.settling = null;
            }
            this.restore();
            if (focusId) {
                this.root.querySelector(`[data-id="${CSS.escape(focusId)}"]`)?.focus({ preventScroll: true });
            }
        };
        state.observer = new MutationObserver(end);
        state.observer.observe(this.root, { childList: true, subtree: true, attributes: true, attributeFilter: ["style", "data-start", "data-end"] });
        call.then(end, end);
    }

    restore() {
        this.hidePreviews(0);
        for (const element of this.root.querySelectorAll(".mc-dragging")) {
            element.classList.remove("mc-dragging");
        }
    }

    click(pending) {
        if (!pending) {
            return;
        }
        if (pending.item) {
            this.dotNet.invokeMethodAsync("OnItemClick", pending.item.dataset.id, ...anchorOf(pending.item.getBoundingClientRect()));
            return;
        }
        if (pending.kind === "create-time") {
            const at = this.timeAt(pending.x, pending.y, Math.floor);
            if (at) {
                const ppm = this.hourHeight / 60;
                const top = at.column.rect.top + ((at.minutes - dayStart(at.column.element.dataset.date)) * ppm);
                this.dotNet.invokeMethodAsync("OnSelect", format(at.minutes), format(at.minutes + this.defaultMinutes), false, at.column.rect.left, top, at.column.rect.width, this.defaultMinutes * ppm);
            }
        } else if (pending.kind === "create-day") {
            const cell = this.cellAt(pending.x, pending.y);
            if (cell) {
                const date = cell.dataset.date;
                this.dotNet.invokeMethodAsync("OnSelect", `${date}T00:00:00`, `${addDays(date, 1)}T00:00:00`, true, ...anchorOf(cell.getBoundingClientRect()));
            }
        }
    }

    clickRoot(e) {
        const entry = this.entryOf(e.target);
        if (entry) {
            e.preventDefault();
            this.dotNet.invokeMethodAsync("OnItemClick", entry.dataset.id, ...anchorOf(entry.getBoundingClientRect()));
            return;
        }
        const more = e.target instanceof Element ? e.target.closest(".mc-more") : null;
        if (!more || !this.root.contains(more)) {
            return;
        }
        e.preventDefault();
        this.dotNet.invokeMethodAsync("OnMoreClick", more.dataset.date, ...anchorOf(more.getBoundingClientRect()));
    }

    suppressClick() {
        const onClick = (e) => {
            e.preventDefault();
            e.stopPropagation();
        };
        window.addEventListener("click", onClick, true);
        setTimeout(() => window.removeEventListener("click", onClick, true), 0);
    }

    dispose() {
        if (this.drag) {
            this.finish(false);
        }
        this.settling?.observer?.disconnect();
        this.settling = null;
        this.clearTouch();
        this.pending = null;
        this.removePointer();
        this.root.removeEventListener("pointerdown", this.onPointerDown);
        this.root.removeEventListener("touchstart", this.onTouchStart, { passive: true });
        this.root.removeEventListener("keydown", this.onKeyDown);
        this.root.removeEventListener("click", this.onClick);
        this.root.removeEventListener("touchmove", this.onRootTouchMove, { passive: false });
        this.resizeObserver.disconnect();
        for (const element of this.previews) {
            element.remove();
        }
        this.previews = [];
    }
}

export function attach(dotNet, root, scrollHour) {
    const calendar = new Calendar(root, dotNet);
    calendar.scroll(scrollHour);
    return {
        scroll: (hour) => calendar.scroll(hour),
        fit: () => calendar.fit(),
        dispose: () => calendar.dispose()
    };
}
