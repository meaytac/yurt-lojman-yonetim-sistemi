const test = require('node:test');
const assert = require('node:assert/strict');
const {
  parsePublicApiError,
  mapValidationErrors,
  safeMessage
} = require('../wwwroot/js/public-common.js');

test('ValidationProblemDetails maps fields to short Turkish messages', () => {
  const parsed = parsePublicApiError({
    title: 'One or more validation errors occurred.',
    status: 400,
    errors: {
      FullName: ['The FullName field is required.'],
      Email: ['The Email field is not a valid e-mail address.'],
      TcNo: [
        'The TcNo field is required.',
        'The field TcNo must be a string with a minimum length of 11 and a maximum length of 11.'
      ],
      traceId: ['abc']
    },
    traceId: '00-test'
  }, '', 400, 'Bad Request');

  assert.equal(parsed.message, 'Başvuru gönderilemedi. Lütfen işaretli alanları kontrol edin.');
  assert.equal(parsed.fieldErrors.FullName, 'Ad soyad alanını doldurun.');
  assert.equal(parsed.fieldErrors.Email, 'Geçerli bir e-posta adresi girin.');
  assert.equal(parsed.fieldErrors.TcNo, 'T.C. Kimlik Numarası 11 rakam olmalıdır.');
  assert.equal(JSON.stringify(parsed).includes('traceId'), false);
  assert.equal(JSON.stringify(parsed).includes('The TcNo field is required'), false);
});

test('validation mapper emits one message per field', () => {
  const errors = mapValidationErrors({
    Email: ['The Email field is required.', 'The Email field is not a valid e-mail address.']
  });

  assert.deepEqual(errors, { Email: 'Geçerli bir e-posta adresi girin.' });
});

test('custom api error preserves safe Turkish message', () => {
  const parsed = parsePublicApiError({ success: false, code: 'FACILITY_CLOSED', message: 'Seçilen yurt başvuruya açık değil.' }, '', 400, 'Bad Request');
  assert.equal(parsed.message, 'Seçilen yurt başvuruya açık değil.');
  assert.deepEqual(parsed.fieldErrors, {});
});

test('raw json and trace id text are hidden behind safe fallback', () => {
  assert.equal(safeMessage('{"traceId":"abc","errors":{"TcNo":["bad"]}}', 400), 'İşlem tamamlanamadı. Lütfen bilgileri kontrol edin.');
});

test('network or old text fallback is controlled', () => {
  const parsed = parsePublicApiError(null, 'Servis geçici olarak kullanılamıyor.', 503, 'Service Unavailable');
  assert.equal(parsed.message, 'Servis geçici olarak kullanılamıyor.');
});
