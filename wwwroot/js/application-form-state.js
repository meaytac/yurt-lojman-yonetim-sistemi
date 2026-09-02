(function exposeApplicationFormState(global) {
  const allCampusesValue = '';
  const maxDocumentBytes = 8 * 1024 * 1024;
  const allowedDocumentTypes = ['application/pdf', 'image/jpeg', 'image/png'];

  function normalizeText(value) {
    return String(value || '').trim().toLocaleLowerCase('tr-TR');
  }

  function normalizeTc(value) {
    return String(value || '').replace(/\D/g, '');
  }

  function normalizePhone(value) {
    const text = String(value || '').trim();
    if (!text) return '';
    return text.startsWith('+')
      ? `+${text.slice(1).replace(/\D/g, '')}`
      : text.replace(/\D/g, '');
  }

  function responseItems(response) {
    if (Array.isArray(response)) return response;
    if (Array.isArray(response?.items)) return response.items;
    return [];
  }

  function usableFacilities(response) {
    return responseItems(response).filter(item => item && item.isApplicationOpen !== false);
  }

  function campusOptions(facilities, type) {
    const byKey = new Map();
    usableFacilities(facilities)
      .filter(item => item.type === type)
      .forEach(item => {
        const display = String(item.campusLocation || '').trim();
        const key = normalizeText(display);
        if (key && !byKey.has(key)) byKey.set(key, display);
      });

    return Array.from(byKey.values()).sort((a, b) => a.localeCompare(b, 'tr-TR'));
  }

  function filterFacilities(facilities, type, campus) {
    const campusKey = normalizeText(campus);
    return usableFacilities(facilities).filter(item => item.type === type)
      .filter(item => !campusKey || normalizeText(item.campusLocation) === campusKey);
  }

  function selectedIsVisible(selected, visibleFacilities) {
    if (!selected) return false;
    return visibleFacilities.some(item => item.type === selected.type && String(item.id) === String(selected.id));
  }

  function modalActions(mode, hasReference) {
    return {
      showCancel: mode !== 'success',
      showSubmit: mode === 'ready' || mode === 'error' || mode === 'submitting',
      disableSubmit: mode === 'loading-facilities' || mode === 'submitting',
      showCopy: mode === 'success' && hasReference,
      showTrack: mode === 'success' && hasReference,
      showClose: mode === 'success'
    };
  }

  function formValue(form, name) {
    return String(form?.elements?.[name]?.value || '');
  }

  function formChecked(form, name) {
    return Boolean(form?.elements?.[name]?.checked);
  }

  function selectedFile(form) {
    return form?.elements?.Document?.files?.[0] || null;
  }

  function validateApplicationForm(form, selected) {
    const fieldErrors = {};
    const fullName = formValue(form, 'FullName').trim();
    const email = formValue(form, 'Email').trim();
    const phoneNumber = normalizePhone(formValue(form, 'PhoneNumber'));
    const tcNo = normalizeTc(formValue(form, 'TcNo'));
    const studentStaffNo = formValue(form, 'StudentStaffNo').trim();
    const applicantRole = formValue(form, 'ApplicantRole');
    const accommodationType = formValue(form, 'AccommodationType');
    const document = selectedFile(form);

    if (!applicantRole || !['Ogrenci', 'Personel'].includes(applicantRole)) {
      fieldErrors.ApplicantRole = 'Başvuru türünü seçin.';
    }
    if (!fullName) fieldErrors.FullName = 'Ad soyad alanını doldurun.';
    if (!email) fieldErrors.Email = 'E-posta adresinizi girin.';
    else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) fieldErrors.Email = 'Geçerli bir e-posta adresi girin.';
    if (!phoneNumber) fieldErrors.PhoneNumber = 'Telefon numaranızı girin.';
    if (!/^\d{11}$/.test(tcNo)) fieldErrors.TcNo = 'T.C. Kimlik Numarası 11 rakam olmalıdır.';
    if (!studentStaffNo) fieldErrors.StudentStaffNo = 'Öğrenci/Personel numaranızı girin.';
    if (!['Yurt', 'Lojman'].includes(accommodationType)) fieldErrors.AccommodationType = 'Konaklama türünü seçin.';
    if (!selected) fieldErrors.Facility = 'Başvurmak istediğiniz tesisi seçin.';
    if (document && !allowedDocumentTypes.includes(document.type)) fieldErrors.Document = 'Belge PDF, JPG veya PNG formatında olmalıdır.';
    if (document && document.size > maxDocumentBytes) fieldErrors.Document = 'Belge izin verilen boyut sınırını aşıyor.';
    if (!formChecked(form, 'Consent')) fieldErrors.Consent = 'Başvuru bilgilerinin doğruluğunu onaylayın.';

    return fieldErrors;
  }

  function buildApplicationFormData(form, selected, idempotencyKey) {
    const formData = new FormData();
    const accommodationType = formValue(form, 'AccommodationType');
    const document = selectedFile(form);

    formData.append('FullName', formValue(form, 'FullName').trim());
    formData.append('Email', formValue(form, 'Email').trim());
    formData.append('TcNo', normalizeTc(formValue(form, 'TcNo')));
    formData.append('PhoneNumber', normalizePhone(formValue(form, 'PhoneNumber')));
    formData.append('StudentStaffNo', formValue(form, 'StudentStaffNo').trim());
    formData.append('ApplicantRole', formValue(form, 'ApplicantRole'));
    formData.append('AccommodationType', accommodationType);
    formData.append('ApplicantNote', formValue(form, 'ApplicantNote').trim());
    formData.append('IdempotencyKey', idempotencyKey || formValue(form, 'IdempotencyKey'));
    formData.append('Consent', formChecked(form, 'Consent') ? 'true' : 'false');

    if (selected?.type === 'Yurt' && accommodationType === 'Yurt') {
      formData.append('DormitoryId', String(selected.id));
    }
    if (selected?.type === 'Lojman' && accommodationType === 'Lojman') {
      formData.append('HousingUnitId', String(selected.id));
    }
    if (document) {
      formData.append('Document', document, document.name);
    }

    return formData;
  }

  const api = {
    allCampusesValue,
    maxDocumentBytes,
    allowedDocumentTypes,
    normalizeText,
    normalizeTc,
    normalizePhone,
    responseItems,
    usableFacilities,
    campusOptions,
    filterFacilities,
    selectedIsVisible,
    modalActions,
    validateApplicationForm,
    buildApplicationFormData
  };

  global.ApplicationFormState = api;
  if (typeof module !== 'undefined' && module.exports) {
    module.exports = api;
  }
})(typeof window !== 'undefined' ? window : globalThis);
