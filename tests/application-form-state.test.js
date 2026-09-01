const test = require('node:test');
const assert = require('node:assert/strict');
const {
  campusOptions,
  filterFacilities,
  selectedIsVisible,
  modalActions
} = require('../wwwroot/js/application-form-state.js');

const facilities = [
  { id: 1, type: 'Yurt', name: 'A Yurdu', campusLocation: ' Battalgazi Yerleşkesi ', isApplicationOpen: true },
  { id: 2, type: 'Yurt', name: 'B Yurdu', campusLocation: 'battalgazi yerleşkesi', isApplicationOpen: true },
  { id: 3, type: 'Yurt', name: 'C Yurdu', campusLocation: 'Yeşilyurt Yerleşkesi', isApplicationOpen: true },
  { id: 4, type: 'Yurt', name: 'D Yurdu', campusLocation: '', isApplicationOpen: true },
  { id: 5, type: 'Lojman', name: 'A Lojmanı', campusLocation: 'Merkez Kampüs', isApplicationOpen: true },
  { id: 6, type: 'Lojman', name: 'Kapalı Lojman', campusLocation: 'Yeşilyurt Yerleşkesi', isApplicationOpen: false }
];

test('campus options are unique, trimmed, non-empty and sorted with Turkish locale', () => {
  assert.deepEqual(campusOptions(facilities, 'Yurt'), ['Battalgazi Yerleşkesi', 'Yeşilyurt Yerleşkesi']);
});

test('campus options depend on accommodation type', () => {
  assert.deepEqual(campusOptions(facilities, 'Lojman'), ['Merkez Kampüs']);
});

test('all campuses returns every open facility of selected type', () => {
  assert.deepEqual(filterFacilities(facilities, 'Yurt', '').map(x => x.id), [1, 2, 3, 4]);
});

test('selected campus returns exact normalized matches only', () => {
  assert.deepEqual(filterFacilities(facilities, 'Yurt', 'Battalgazi Yerleşkesi').map(x => x.id), [1, 2]);
});

test('closed facilities are excluded from campus options and visible facilities', () => {
  assert.deepEqual(filterFacilities(facilities, 'Lojman', 'Yeşilyurt Yerleşkesi'), []);
});

test('type change can detect invalid selected facility', () => {
  const selected = facilities[0];
  assert.equal(selectedIsVisible(selected, filterFacilities(facilities, 'Lojman', '')), false);
});

test('valid selected facility remains visible for matching filters', () => {
  const selected = facilities[2];
  assert.equal(selectedIsVisible(selected, filterFacilities(facilities, 'Yurt', 'Yeşilyurt Yerleşkesi')), true);
});

test('ready actions show only close and submit', () => {
  assert.deepEqual(modalActions('ready', false), {
    showCancel: true,
    showSubmit: true,
    disableSubmit: false,
    showCopy: false,
    showTrack: false,
    showClose: false
  });
});

test('submitting disables submit and hides result actions', () => {
  const actions = modalActions('submitting', false);
  assert.equal(actions.showSubmit, true);
  assert.equal(actions.disableSubmit, true);
  assert.equal(actions.showCopy, false);
  assert.equal(actions.showTrack, false);
});

test('success actions hide submit and show copy, track and close', () => {
  assert.deepEqual(modalActions('success', true), {
    showCancel: false,
    showSubmit: false,
    disableSubmit: false,
    showCopy: true,
    showTrack: true,
    showClose: true
  });
});

test('error actions keep form retryable without result actions', () => {
  const actions = modalActions('error', false);
  assert.equal(actions.showCancel, true);
  assert.equal(actions.showSubmit, true);
  assert.equal(actions.disableSubmit, false);
  assert.equal(actions.showCopy, false);
  assert.equal(actions.showTrack, false);
});
