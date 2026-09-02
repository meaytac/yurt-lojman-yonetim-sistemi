const test = require('node:test');
const assert = require('node:assert/strict');
const {
  campusOptions,
  filterFacilities,
  selectedIsVisible,
  modalActions,
  validateApplicationForm,
  buildApplicationFormData
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

test('application payload uses backend field names for dormitory applications', () => {
  const formData = buildApplicationFormData(fakeForm({ AccommodationType: 'Yurt' }), { id: 17, type: 'Yurt' }, 'idem-1');
  const keys = Array.from(formData.keys());

  assert.equal(formData.get('FullName'), 'Ayşe Yılmaz');
  assert.equal(formData.get('Email'), 'ayse@example.test');
  assert.equal(formData.get('TcNo'), '12345678901');
  assert.equal(formData.get('PhoneNumber'), '+905551112233');
  assert.equal(formData.get('StudentStaffNo'), 'OGR-42');
  assert.equal(formData.get('ApplicantRole'), 'Ogrenci');
  assert.equal(formData.get('AccommodationType'), 'Yurt');
  assert.equal(formData.get('DormitoryId'), '17');
  assert.equal(keys.includes('HousingUnitId'), false);
  assert.equal(formData.get('ApplicantNote'), 'Sessiz oda tercihi');
  assert.equal(formData.get('IdempotencyKey'), 'idem-1');
  assert.equal(formData.get('Consent'), 'true');
  assert.equal(keys.includes('Document'), true);
});

test('application payload sends only housing unit id for housing applications', () => {
  const formData = buildApplicationFormData(fakeForm({ AccommodationType: 'Lojman', ApplicantRole: 'Personel' }), { id: 23, type: 'Lojman' }, 'idem-2');
  const keys = Array.from(formData.keys());

  assert.equal(formData.get('ApplicantRole'), 'Personel');
  assert.equal(formData.get('AccommodationType'), 'Lojman');
  assert.equal(formData.get('HousingUnitId'), '23');
  assert.equal(keys.includes('DormitoryId'), false);
});

test('frontend validation returns short Turkish field errors without duplicates', () => {
  const errors = validateApplicationForm(fakeForm({
    FullName: ' ',
    Email: 'yanlis',
    PhoneNumber: ' ',
    TcNo: '123',
    StudentStaffNo: '',
    Consent: false
  }), null);

  assert.deepEqual(errors, {
    FullName: 'Ad soyad alanını doldurun.',
    Email: 'Geçerli bir e-posta adresi girin.',
    PhoneNumber: 'Telefon numaranızı girin.',
    TcNo: 'T.C. Kimlik Numarası 11 rakam olmalıdır.',
    StudentStaffNo: 'Öğrenci/Personel numaranızı girin.',
    Facility: 'Başvurmak istediğiniz tesisi seçin.',
    Consent: 'Başvuru bilgilerinin doğruluğunu onaylayın.'
  });
});

test('valid filled fields do not create required validation errors', () => {
  const errors = validateApplicationForm(fakeForm(), { id: 17, type: 'Yurt' });
  assert.deepEqual(errors, {});
});

function fakeForm(overrides = {}) {
  const file = new Blob(['%PDF-1.4'], { type: 'application/pdf' });
  file.name = 'belge.pdf';
  file.size = file.size || 8;

  const values = {
    FullName: ' Ayşe Yılmaz ',
    Email: 'ayse@example.test',
    TcNo: '123 456 789 01',
    PhoneNumber: '+90 (555) 111-22-33',
    StudentStaffNo: ' OGR-42 ',
    ApplicantRole: 'Ogrenci',
    AccommodationType: 'Yurt',
    ApplicantNote: ' Sessiz oda tercihi ',
    IdempotencyKey: 'from-form',
    Consent: true,
    Document: file,
    ...overrides
  };

  return {
    elements: {
      FullName: { value: values.FullName },
      Email: { value: values.Email },
      TcNo: { value: values.TcNo },
      PhoneNumber: { value: values.PhoneNumber },
      StudentStaffNo: { value: values.StudentStaffNo },
      ApplicantRole: { value: values.ApplicantRole },
      AccommodationType: { value: values.AccommodationType },
      ApplicantNote: { value: values.ApplicantNote },
      IdempotencyKey: { value: values.IdempotencyKey },
      Consent: { checked: values.Consent },
      Document: { files: values.Document ? [values.Document] : [] }
    }
  };
}
