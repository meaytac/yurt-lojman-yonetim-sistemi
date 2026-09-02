(() => {
  const helpers = window.ApplicationFormState;
  const state = {
    facilities: [],
    selected: null,
    mode: 'idle',
    idempotencyKey: '',
    opener: null,
    lastReference: ''
  };

  document.addEventListener('DOMContentLoaded', () => {
    bind();
    refreshIdempotencyKey();
    setMode('idle');
    if (new URLSearchParams(window.location.search).get('application') === '1') {
      openModal();
    }
  });

  function bind() {
    byId('openApplicationModal')?.addEventListener('click', openModal);
    byId('closeApplicationModal')?.addEventListener('click', () => closeModal());
    byId('cancelApplicationButton')?.addEventListener('click', () => closeModal());
    byId('closeApplicationResult')?.addEventListener('click', () => closeModal(true));
    byId('applicationModalBackdrop')?.addEventListener('click', event => {
      if (event.target?.id === 'applicationModalBackdrop' && state.mode !== 'submitting') closeModal();
    });
    document.addEventListener('keydown', handleKeydown);
    byId('preRegistrationApplicationForm')?.addEventListener('submit', submitApplication);
    byId('applicationAccommodationType')?.addEventListener('change', handleTypeChange);
    byId('campusSelect')?.addEventListener('change', handleCampusChange);
    byId('copyReferenceButton')?.addEventListener('click', copyReference);
    byId('applicationDocument')?.addEventListener('change', updateDocumentName);
    document.querySelectorAll('#preRegistrationApplicationForm input, #preRegistrationApplicationForm select, #preRegistrationApplicationForm textarea')
      .forEach(input => input.addEventListener('input', () => clearFieldError(fieldNameForElement(input))));
  }

  async function openModal(event) {
    state.opener = event?.currentTarget || document.activeElement;
    resetForm();
    const backdrop = byId('applicationModalBackdrop');
    backdrop.hidden = false;
    await loadFacilities();
    focusFirst();
  }

  function closeModal(force = false) {
    if (state.mode === 'submitting' && !force) return;
    const backdrop = byId('applicationModalBackdrop');
    if (!backdrop) return;
    resetSensitiveFields();
    backdrop.hidden = true;
    state.opener?.focus?.();
  }

  async function loadFacilities() {
    state.facilities = [];
    setMode('loading-facilities');
    setFacilityListMessage('Tesisler yükleniyor...');
    setCampusDisabled(true);

    try {
      const response = await publicApi('/api/public/facilities');
      state.facilities = helpers.usableFacilities(response);
      rebuildCampusOptions();
      setCampusDisabled(false);
      setMode('ready');
      renderFacilities();
    } catch {
      setMode('error');
      setCampusDisabled(true);
      setFacilityListMessage('Tesis bilgileri yüklenemedi. Lütfen tekrar deneyin.', true);
    }
  }

  function handleTypeChange() {
    state.selected = null;
    rebuildCampusOptions({ keepIfAvailable: true });
    clearFacilityIds();
    renderFacilities();
    updateSelectedSummary();
  }

  function handleCampusChange() {
    const visible = currentVisibleFacilities();
    if (!helpers.selectedIsVisible(state.selected, visible)) {
      state.selected = null;
      clearFacilityIds();
      updateSelectedSummary();
    }
    renderFacilities();
  }

  function rebuildCampusOptions(options = {}) {
    const select = byId('campusSelect');
    if (!select) return;

    const previous = options.keepIfAvailable ? select.value : helpers.allCampusesValue;
    const type = selectedType();
    const campuses = helpers.campusOptions(state.facilities, type);
    select.innerHTML = `<option value="${helpers.allCampusesValue}">Tüm kampüsler</option>${campuses
      .map(campus => `<option value="${escapeAttr(campus)}">${escapeHtml(campus)}</option>`)
      .join('')}`;

    select.value = campuses.some(campus => helpers.normalizeText(campus) === helpers.normalizeText(previous))
      ? campuses.find(campus => helpers.normalizeText(campus) === helpers.normalizeText(previous))
      : helpers.allCampusesValue;
  }

  function renderFacilities() {
    const host = byId('applicationFacilityList');
    if (!host) return;
    host.setAttribute('aria-busy', state.mode === 'loading-facilities' ? 'true' : 'false');

    if (state.mode === 'loading-facilities') {
      setFacilityListMessage('Tesisler yükleniyor...');
      return;
    }

    if (state.mode === 'error') {
      setFacilityListMessage('Tesis bilgileri yüklenemedi. Lütfen tekrar deneyin.', true);
      return;
    }

    const allForType = helpers.filterFacilities(state.facilities, selectedType(), helpers.allCampusesValue);
    if (!allForType.length) {
      setFacilityListMessage('Bu konaklama türünde başvuruya açık tesis bulunmuyor.');
      return;
    }

    const items = currentVisibleFacilities();
    if (!items.length) {
      setFacilityListMessage('Seçilen kampüste başvuruya açık tesis bulunmuyor. Başka bir kampüs seçebilirsiniz.');
      return;
    }

    host.innerHTML = items.map(item => `
      <button class="application-facility ${state.selected?.id === item.id && state.selected?.type === item.type ? 'selected' : ''}" type="button" data-type="${escapeAttr(item.type)}" data-id="${escapeAttr(item.id)}" aria-pressed="${state.selected?.id === item.id && state.selected?.type === item.type}">
        <strong>${escapeHtml(item.name)}</strong>
        <small>${escapeHtml(String(item.campusLocation || '').trim())} · ${escapeHtml(item.type)}</small>
        <small>${Number(item.totalCapacity || 0)} kapasite · ${Number(item.availableCapacity || 0)} müsait</small>
        <small>${escapeHtml(item.applicationConditions || 'Başvuru koşulu belirtilmemiş.')}</small>
      </button>`).join('');

    host.querySelectorAll('.application-facility').forEach(button => {
      button.addEventListener('click', () => selectFacility(button.dataset.type, button.dataset.id));
    });
  }

  function selectFacility(type, id) {
    state.selected = state.facilities.find(item => item.type === type && String(item.id) === String(id)) || null;
    byId('applicationDormitoryId').value = state.selected?.type === 'Yurt' ? state.selected.id : '';
    byId('applicationHousingUnitId').value = state.selected?.type === 'Lojman' ? state.selected.id : '';
    renderFacilities();
    updateSelectedSummary();
    clearFieldError('Facility');
    setStatus('', '');
  }

  async function submitApplication(event) {
    event.preventDefault();
    if (state.mode === 'submitting') return;
    const form = event.currentTarget;
    clearFieldErrors();
    const fieldErrors = helpers.validateApplicationForm(form, state.selected);
    if (Object.keys(fieldErrors).length) {
      applyFieldErrors(fieldErrors);
      setStatus('Başvuru gönderilemedi. Lütfen işaretli alanları kontrol edin.', 'error');
      focusFirstError(fieldErrors);
      return;
    }

    setMode('submitting');
    setStatus('', '');

    try {
      const formData = helpers.buildApplicationFormData(form, state.selected, state.idempotencyKey);
      const data = await publicApi('/api/public/applications', { method: 'POST', body: formData });
      state.lastReference = data.referenceCode;
      showSuccess(data.referenceCode, data.securityCode || data.trackingToken || data.token || '');
      setMode('success');
      refreshIdempotencyKey();
    } catch (error) {
      setMode('error');
      if (error.fieldErrors && Object.keys(error.fieldErrors).length) {
        applyFieldErrors(error.fieldErrors);
        focusFirstError(error.fieldErrors);
      }
      setStatus(error.message || 'Başvuru gönderilemedi. Lütfen bilgileri kontrol edin.', 'error');
    }
  }

  function showSuccess(referenceCode, securityCode = '') {
    byId('applicationReferenceCode').textContent = referenceCode || '';
    const params = new URLSearchParams();
    if (referenceCode) params.set('ref', referenceCode);
    if (securityCode) params.set('code', securityCode);
    byId('goTrackButton').href = `/track-application.html${params.toString() ? `?${params}` : ''}`;
    const result = byId('applicationResultPanel');
    result.hidden = false;
    result.focus();
    setStatus('', '');
  }

  async function copyReference() {
    if (!state.lastReference) return;
    const button = byId('copyReferenceButton');
    const original = button.textContent;
    try {
      await navigator.clipboard.writeText(state.lastReference);
    } catch {
      const input = document.createElement('input');
      input.value = state.lastReference;
      input.style.position = 'fixed';
      input.style.opacity = '0';
      document.body.appendChild(input);
      input.select();
      document.execCommand('copy');
      input.remove();
    }
    button.textContent = 'Kopyalandı';
    setStatus('Başvuru numarası kopyalandı.', 'success');
    setTimeout(() => { button.textContent = original; }, 1600);
  }

  function updateSelectedSummary() {
    const summary = byId('selectedFacilitySummary');
    const output = byId('applicationSummary');
    if (!state.selected) {
      summary.textContent = state.mode === 'ready' ? 'Başvurmak istediğiniz tesisi seçin.' : 'Tesis seçilmedi.';
      output.textContent = '';
      return;
    }

    const lines = [
      `Seçilen tesis: ${state.selected.name}`,
      `Kampüs: ${String(state.selected.campusLocation || '').trim()}`,
      `Konaklama türü: ${state.selected.type}`,
      `Müsaitlik: ${Number(state.selected.availableCapacity || 0)}`
    ];
    const conditions = state.selected.applicationConditions ? `Koşullar: ${state.selected.applicationConditions}` : 'Koşullar: Belirtilmemiş.';
    summary.textContent = [...lines, conditions].join('\n');
    output.textContent = lines.join('\n');
  }

  function setMode(mode) {
    state.mode = mode;
    const submit = byId('submitApplicationButton');
    const actions = helpers.modalActions(mode, Boolean(state.lastReference));
    byId('cancelApplicationButton').hidden = !actions.showCancel;
    byId('copyReferenceButton').hidden = !actions.showCopy;
    byId('goTrackButton').hidden = !actions.showTrack;
    byId('closeApplicationResult').hidden = !actions.showClose;
    submit.hidden = !actions.showSubmit;
    submit.disabled = actions.disableSubmit;
    submit.textContent = mode === 'submitting' ? 'Başvuru gönderiliyor...' : 'Başvuruyu Gönder';

    const formFields = byId('preRegistrationApplicationForm')?.querySelectorAll('fieldset input, fieldset select, fieldset textarea, .application-facility');
    formFields?.forEach(element => {
      element.disabled = mode === 'submitting' || mode === 'success';
    });

    if (mode !== 'success') {
      byId('applicationResultPanel').hidden = true;
    }
    updateSelectedSummary();
  }

  function resetForm() {
    const form = byId('preRegistrationApplicationForm');
    form?.reset();
    state.selected = null;
    state.lastReference = '';
    state.facilities = [];
    clearFacilityIds();
    clearFieldErrors();
    updateDocumentName();
    byId('applicationReferenceCode').textContent = '';
    setStatus('', '');
    refreshIdempotencyKey();
    setMode('idle');
    rebuildCampusOptions();
    updateSelectedSummary();
  }

  function resetSensitiveFields() {
    const form = byId('preRegistrationApplicationForm');
    form?.reset();
    state.selected = null;
    state.facilities = [];
    state.lastReference = '';
    clearFacilityIds();
    clearFieldErrors();
    updateDocumentName();
    refreshIdempotencyKey();
    setMode('idle');
  }

  function setFacilityListMessage(message, canRetry = false) {
    const host = byId('applicationFacilityList');
    if (!host) return;
    host.innerHTML = `<div class="application-empty-state"><p>${escapeHtml(message)}</p>${canRetry ? '<button class="btn-secondary" id="retryFacilitiesButton" type="button">Tekrar Dene</button>' : ''}</div>`;
    byId('retryFacilitiesButton')?.addEventListener('click', loadFacilities);
  }

  function currentVisibleFacilities() {
    return helpers.filterFacilities(state.facilities, selectedType(), byId('campusSelect')?.value || helpers.allCampusesValue);
  }

  function selectedType() {
    return byId('applicationAccommodationType')?.value || 'Yurt';
  }

  function setCampusDisabled(disabled) {
    const select = byId('campusSelect');
    if (select) select.disabled = disabled;
  }

  function clearFacilityIds() {
    byId('applicationDormitoryId').value = '';
    byId('applicationHousingUnitId').value = '';
  }

  function refreshIdempotencyKey() {
    state.idempotencyKey = crypto.randomUUID();
    const input = byId('applicationIdempotencyKey');
    if (input) input.value = state.idempotencyKey;
  }

  function updateDocumentName() {
    const file = byId('applicationDocument')?.files?.[0];
    const name = byId('applicationDocumentName');
    if (name) name.textContent = file?.name || 'Dosya seçilmedi';
    clearFieldError('Document');
  }

  const fieldElements = {
    FullName: 'applicationFullName',
    Email: 'applicationEmail',
    TcNo: 'applicationTcNo',
    PhoneNumber: 'applicationPhoneNumber',
    StudentStaffNo: 'applicationStudentStaffNo',
    ApplicantRole: 'applicantRole',
    AccommodationType: 'applicationAccommodationType',
    Document: 'applicationDocument',
    ApplicantNote: 'applicationApplicantNote',
    Consent: 'applicationConsent',
    Facility: 'applicationFacilityList',
    DormitoryId: 'applicationFacilityList',
    HousingUnitId: 'applicationFacilityList'
  };

  const fieldErrorElements = {
    FullName: 'applicationFullNameError',
    Email: 'applicationEmailError',
    TcNo: 'applicationTcNoError',
    PhoneNumber: 'applicationPhoneNumberError',
    StudentStaffNo: 'applicationStudentStaffNoError',
    ApplicantRole: 'applicantRoleError',
    AccommodationType: 'applicationAccommodationTypeError',
    Document: 'applicationDocumentError',
    ApplicantNote: 'applicationApplicantNoteError',
    Consent: 'applicationConsentError',
    Facility: 'applicationFacilityError',
    DormitoryId: 'applicationFacilityError',
    HousingUnitId: 'applicationFacilityError'
  };

  function applyFieldErrors(fieldErrors) {
    Object.entries(fieldErrors).forEach(([field, message]) => {
      const targetField = fieldElements[field] ? field : canonicalFacilityField(field);
      const input = byId(fieldElements[targetField]);
      const error = byId(fieldErrorElements[targetField]);
      if (input) input.setAttribute('aria-invalid', 'true');
      if (error) error.textContent = message;
    });
  }

  function clearFieldErrors() {
    Object.values(fieldElements).forEach(id => byId(id)?.removeAttribute('aria-invalid'));
    Object.values(fieldErrorElements).forEach(id => {
      const error = byId(id);
      if (error) error.textContent = '';
    });
  }

  function clearFieldError(field) {
    const targetField = canonicalFacilityField(field);
    byId(fieldElements[targetField])?.removeAttribute('aria-invalid');
    const error = byId(fieldErrorElements[targetField]);
    if (error) error.textContent = '';
  }

  function fieldNameForElement(element) {
    if (!element) return '';
    if (element.id === 'applicationDormitoryId' || element.id === 'applicationHousingUnitId') return 'Facility';
    return element.name || '';
  }

  function canonicalFacilityField(field) {
    return field === 'DormitoryId' || field === 'HousingUnitId' ? 'Facility' : field;
  }

  function focusFirstError(fieldErrors) {
    const order = ['ApplicantRole', 'FullName', 'Email', 'PhoneNumber', 'TcNo', 'StudentStaffNo', 'AccommodationType', 'Facility', 'Document', 'ApplicantNote', 'Consent'];
    const first = order.find(field => fieldErrors[field] || (field === 'Facility' && (fieldErrors.DormitoryId || fieldErrors.HousingUnitId)));
    const element = byId(fieldElements[first]);
    element?.scrollIntoView?.({ behavior: 'smooth', block: 'center' });
    element?.focus?.({ preventScroll: true });
  }

  function handleKeydown(event) {
    const backdrop = byId('applicationModalBackdrop');
    if (!backdrop || backdrop.hidden) return;
    if (event.key === 'Escape' && state.mode !== 'submitting') {
      event.preventDefault();
      closeModal();
      return;
    }
    if (event.key === 'Tab') trapFocus(event);
  }

  function trapFocus(event) {
    const focusable = Array.from(byId('applicationModalBackdrop').querySelectorAll('button, [href], input, select, textarea'))
      .filter(element => !element.disabled && !element.hidden && element.offsetParent !== null);
    if (!focusable.length) return;
    const first = focusable[0];
    const last = focusable[focusable.length - 1];
    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first.focus();
    }
  }

  function focusFirst() {
    byId('applicantRole')?.focus();
  }

  function setStatus(message, type) {
    const element = byId('applicationModalState');
    if (!element) return;
    element.textContent = message;
    element.className = `login-message ${type || ''}`.trim();
  }

  function byId(id) {
    return document.getElementById(id);
  }

  function escapeHtml(value) {
    return String(value ?? '').replace(/[&<>"']/g, char => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#039;' }[char]));
  }

  function escapeAttr(value) {
    return escapeHtml(value ?? '').replace(/"/g, '&quot;');
  }
})();
