(() => {
  const state = {
    facilities: [],
    selected: null,
    idempotencyKey: '',
    isSubmitting: false,
    opener: null,
    lastReference: ''
  };

  document.addEventListener('DOMContentLoaded', () => {
    bind();
    refreshIdempotencyKey();
    if (new URLSearchParams(window.location.search).get('application') === '1') {
      openModal();
    }
  });

  function bind() {
    byId('openApplicationModal')?.addEventListener('click', openModal);
    byId('closeApplicationModal')?.addEventListener('click', () => closeModal());
    byId('closeApplicationResult')?.addEventListener('click', () => closeModal(true));
    byId('applicationModalBackdrop')?.addEventListener('click', event => {
      if (event.target?.id === 'applicationModalBackdrop' && !state.isSubmitting) closeModal();
    });
    document.addEventListener('keydown', handleKeydown);
    byId('preRegistrationApplicationForm')?.addEventListener('submit', submitApplication);
    byId('applicationAccommodationType')?.addEventListener('change', () => {
      state.selected = null;
      renderFacilities();
      updateSelectedSummary();
    });
    byId('campusFilter')?.addEventListener('input', renderFacilities);
    byId('copyReferenceButton')?.addEventListener('click', async () => {
      if (!state.lastReference) return;
      await navigator.clipboard?.writeText(state.lastReference);
      setStatus('Başvuru numarası kopyalandı.', 'success');
    });
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
    if (state.isSubmitting && !force) return;
    const backdrop = byId('applicationModalBackdrop');
    if (!backdrop) return;
    resetSensitiveFields();
    backdrop.hidden = true;
    state.opener?.focus?.();
  }

  async function loadFacilities() {
    const host = byId('applicationFacilityList');
    host.innerHTML = '<p class="muted">Tesisler yükleniyor...</p>';
    try {
      state.facilities = await publicApi('/api/public/facilities');
      renderFacilities();
    } catch (error) {
      host.innerHTML = `<p class="login-message error">${escapeHtml(error.message)}</p>`;
    }
  }

  function renderFacilities() {
    const host = byId('applicationFacilityList');
    if (!host) return;
    const type = byId('applicationAccommodationType')?.value || 'Yurt';
    const campus = (byId('campusFilter')?.value || '').toLocaleLowerCase('tr-TR');
    const items = state.facilities.filter(item => item.type === type && item.isApplicationOpen !== false)
      .filter(item => !campus || String(item.campusLocation || '').toLocaleLowerCase('tr-TR').includes(campus));

    host.innerHTML = items.length ? items.map(item => `
      <button class="application-facility ${state.selected?.id === item.id && state.selected?.type === item.type ? 'selected' : ''}" type="button" data-type="${item.type}" data-id="${item.id}">
        <strong>${escapeHtml(item.name)}</strong>
        <small>${escapeHtml(item.campusLocation)} · ${item.availableCapacity} müsait kapasite</small>
        <small>${escapeHtml(item.amenities || 'İmkân bilgisi girilmemiş.')}</small>
      </button>`).join('') : '<p class="muted">Seçilebilir tesis bulunamadı.</p>';

    host.querySelectorAll('.application-facility').forEach(button => {
      button.addEventListener('click', () => {
        state.selected = state.facilities.find(item => item.type === button.dataset.type && String(item.id) === button.dataset.id);
        byId('applicationDormitoryId').value = state.selected.type === 'Yurt' ? state.selected.id : '';
        byId('applicationHousingUnitId').value = state.selected.type === 'Lojman' ? state.selected.id : '';
        renderFacilities();
        updateSelectedSummary();
      });
    });
  }

  async function submitApplication(event) {
    event.preventDefault();
    if (state.isSubmitting) return;
    const form = event.currentTarget;
    if (!state.selected) {
      setStatus('Lütfen başvuru yapılacak tesisi seçin.', 'error');
      return;
    }

    state.isSubmitting = true;
    const submit = byId('submitApplicationButton');
    submit.disabled = true;
    submit.textContent = 'Gönderiliyor...';
    setStatus('', '');

    try {
      const data = await publicApi('/api/public/applications', { method: 'POST', body: new FormData(form) });
      state.lastReference = data.referenceCode;
      setStatus(`Başvurunuz oluşturuldu. Devam edebilmek için e-posta adresinize gönderilen doğrulama bağlantısını kullanın. Başvuru Numaranız: ${data.referenceCode}`, 'success');
      byId('copyReferenceButton').hidden = false;
      byId('goTrackButton').hidden = false;
      byId('closeApplicationResult').hidden = false;
      submit.hidden = true;
      refreshIdempotencyKey();
    } catch (error) {
      setStatus(error.message, 'error');
      refreshIdempotencyKey();
    } finally {
      state.isSubmitting = false;
      submit.disabled = false;
      submit.textContent = 'Başvuruyu Gönder';
    }
  }

  function updateSelectedSummary() {
    const summary = byId('selectedFacilitySummary');
    const output = byId('applicationSummary');
    if (!state.selected) {
      summary.textContent = 'Tesis seçilmedi.';
      output.textContent = '';
      return;
    }
    const text = `${state.selected.name} · ${state.selected.type} · ${state.selected.availableCapacity} müsait kapasite. Koşullar: ${state.selected.applicationConditions || 'Belirtilmemiş.'}`;
    summary.textContent = text;
    output.textContent = `Başvuru özeti: ${text}`;
  }

  function resetForm() {
    const form = byId('preRegistrationApplicationForm');
    form?.reset();
    state.selected = null;
    state.lastReference = '';
    byId('applicationDormitoryId').value = '';
    byId('applicationHousingUnitId').value = '';
    byId('copyReferenceButton').hidden = true;
    byId('goTrackButton').hidden = true;
    byId('closeApplicationResult').hidden = true;
    byId('submitApplicationButton').hidden = false;
    setStatus('', '');
    refreshIdempotencyKey();
    updateSelectedSummary();
  }

  function resetSensitiveFields() {
    const form = byId('preRegistrationApplicationForm');
    form?.reset();
    state.selected = null;
    refreshIdempotencyKey();
  }

  function refreshIdempotencyKey() {
    state.idempotencyKey = crypto.randomUUID();
    const input = byId('applicationIdempotencyKey');
    if (input) input.value = state.idempotencyKey;
  }

  function handleKeydown(event) {
    const backdrop = byId('applicationModalBackdrop');
    if (!backdrop || backdrop.hidden) return;
    if (event.key === 'Escape' && !state.isSubmitting) {
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
})();
