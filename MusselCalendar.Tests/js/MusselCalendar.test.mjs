import { test } from 'node:test';
import assert from 'node:assert/strict';
import { addDays, addMonths, format, targetOf } from '../../MusselCalendar/wwwroot/MusselCalendar.js';

const key = (date, name, cellIndex = 0, shiftKey = false) => targetOf({ dataset: { date }, cellIndex }, { key: name, shiftKey });

test('dates before the year 1000 keep four digits', () => {
    assert.equal(addDays('0001-01-01', 1), '0001-01-02');
    assert.equal(addDays('0999-12-31', 1), '1000-01-01');
    assert.equal(addMonths('0050-01-31', 1), '0050-02-28');
    assert.equal(addMonths('0001-03-15', -1), '0001-02-15');
    assert.equal(format(Date.parse('0005-06-07T08:09:00Z') / 60000), '0005-06-07T08:09:00');
});

test('keys stop at the first and the last day', () => {
    assert.equal(key('0001-01-01', 'ArrowLeft'), null);
    assert.equal(key('0001-01-15', 'PageUp'), null);
    assert.equal(key('9999-12-31', 'ArrowRight'), null);
    assert.equal(key('9999-12-15', 'PageDown', 0, true), null);
    assert.equal(key('0001-02-15', 'PageUp'), '0001-01-15');
    assert.equal(key('0001-01-02', 'Home', 1), '0001-01-01');
});
