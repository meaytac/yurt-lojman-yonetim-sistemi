async function publicApi(path, options = {}) {
  const response = await fetch(path, options);
  const text = await response.text();
  const data = parseJsonOrNull(text);
  if (!response.ok) {
    throw toPublicApiError(data, text, response.status, response.statusText);
  }
  return data;
}

const publicFieldLabels = {
  FullName: 'Ad Soyad',
  Email: 'E-posta',
  TcNo: 'T.C. Kimlik Numarası',
  PhoneNumber: 'Telefon',
  StudentStaffNo: 'Öğrenci/Personel Numarası',
  ApplicantRole: 'Başvuru Türü',
  AccommodationType: 'Konaklama Türü',
  DormitoryId: 'Yurt',
  HousingUnitId: 'Lojman',
  Document: 'Belge',
  ApplicantNote: 'Açıklama',
  Note: 'Açıklama',
  Consent: 'Onay'
};

function parseJsonOrNull(text) {
  if (!text) return null;
  try {
    return JSON.parse(text);
  } catch {
    return null;
  }
}

function toPublicApiError(data, text, status, statusText) {
  const parsed = parsePublicApiError(data, text, status, statusText);
  const error = new Error(parsed.message);
  error.status = status;
  error.fieldErrors = parsed.fieldErrors;
  return error;
}

function parsePublicApiError(data, text = '', status = 0, statusText = '') {
  if (data?.errors && typeof data.errors === 'object') {
    return {
      message: 'Başvuru gönderilemedi. Lütfen işaretli alanları kontrol edin.',
      fieldErrors: mapValidationErrors(data.errors)
    };
  }

  if (data?.message && typeof data.message === 'string') {
    return { message: safeMessage(data.message, status), fieldErrors: {} };
  }

  if (data?.title && typeof data.title === 'string' && status >= 500) {
    return { message: 'Sunucu hatası oluştu. Lütfen daha sonra tekrar deneyin.', fieldErrors: {} };
  }

  if (status === 429) {
    return { message: 'Çok fazla deneme yapıldı. Lütfen kısa bir süre sonra tekrar deneyin.', fieldErrors: {} };
  }

  if (text && !looksLikeJson(text)) {
    return { message: safeMessage(text, status), fieldErrors: {} };
  }

  return {
    message: status >= 500
      ? 'Sunucu hatası oluştu. Lütfen daha sonra tekrar deneyin.'
      : statusText || 'İşlem tamamlanamadı. Lütfen tekrar deneyin.',
    fieldErrors: {}
  };
}

function mapValidationErrors(errors) {
  const fieldErrors = {};
  Object.entries(errors).forEach(([field, messages]) => {
    const key = canonicalFieldName(field);
    if (!key || fieldErrors[key]) return;
    fieldErrors[key] = friendlyValidationMessage(key, Array.isArray(messages) ? messages : [String(messages || '')]);
  });
  return fieldErrors;
}

function canonicalFieldName(field) {
  const clean = String(field || '').split('.').pop();
  if (clean.toLowerCase() === 'traceid') return '';
  const exact = Object.keys(publicFieldLabels).find(key => key.toLocaleLowerCase('tr-TR') === clean.toLocaleLowerCase('tr-TR'));
  return exact || clean;
}

function friendlyValidationMessage(field, messages) {
  const joined = messages.join(' ').toLocaleLowerCase('tr-TR');
  if (field === 'FullName') return 'Ad soyad alanını doldurun.';
  if (field === 'Email') return joined.includes('valid') || joined.includes('geçerli')
    ? 'Geçerli bir e-posta adresi girin.'
    : 'E-posta adresinizi girin.';
  if (field === 'TcNo') return 'T.C. Kimlik Numarası 11 rakam olmalıdır.';
  if (field === 'PhoneNumber') return 'Telefon numaranızı girin.';
  if (field === 'StudentStaffNo') return 'Öğrenci/Personel numaranızı girin.';
  if (field === 'ApplicantRole') return 'Başvuru türünü seçin.';
  if (field === 'AccommodationType') return 'Konaklama türünü seçin.';
  if (field === 'DormitoryId' || field === 'HousingUnitId' || field === 'Facility') return 'Başvurmak istediğiniz tesisi seçin.';
  if (field === 'Document') {
    if (joined.includes('size') || joined.includes('boyut') || joined.includes('mb')) return 'Belge izin verilen boyut sınırını aşıyor.';
    return 'Belge PDF, JPG veya PNG formatında olmalıdır.';
  }
  if (field === 'Consent') return 'Başvuru bilgilerinin doğruluğunu onaylayın.';
  return 'Lütfen bu alanı kontrol edin.';
}

function safeMessage(message, status) {
  const clean = String(message || '').trim();
  if (!clean || looksLikeJson(clean) || clean.includes('traceId') || clean.includes('System.')) {
    return status >= 500
      ? 'Sunucu hatası oluştu. Lütfen daha sonra tekrar deneyin.'
      : 'İşlem tamamlanamadı. Lütfen bilgileri kontrol edin.';
  }
  if (/required|valid e-mail|string with a minimum|field is/i.test(clean)) {
    return 'Başvuru gönderilemedi. Lütfen işaretli alanları kontrol edin.';
  }
  return clean;
}

function looksLikeJson(value) {
  const text = String(value || '').trim();
  return (text.startsWith('{') && text.endsWith('}')) || (text.startsWith('[') && text.endsWith(']'));
}

function publicEscape(value) {
  return String(value ?? '').replace(/[&<>"']/g, char => ({
    '&': '&amp;',
    '<': '&lt;',
    '>': '&gt;',
    '"': '&quot;',
    "'": '&#039;'
  }[char]));
}

function queryValue(name) {
  return new URLSearchParams(window.location.search).get(name) || '';
}

function setStatus(element, message, kind = '') {
  if (!element) return;
  element.textContent = message;
  element.className = `status ${kind}`.trim();
}

if (typeof module !== 'undefined' && module.exports) {
  module.exports = {
    parsePublicApiError,
    mapValidationErrors,
    friendlyValidationMessage,
    safeMessage
  };
}
