async function verifyApplicationFromQuery() {
  const state = document.getElementById('verifyState');
  if (!state) return;
  const referenceCode = queryValue('ref');
  const token = queryValue('token');
  if (!referenceCode || !token) {
    setStatus(state, 'Doğrulama bağlantısı eksik veya geçersiz.', 'error');
    return;
  }

  try {
    const data = await publicApi('/api/public/applications/verify-email', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ referenceCode, token })
    });
    setStatus(state, data.message, 'success');
  } catch (error) {
    setStatus(state, error.message, 'error');
  }
}

async function trackApplication(event) {
  event.preventDefault();
  const state = document.getElementById('trackState');
  const result = document.getElementById('trackResult');
  const missingForm = document.getElementById('missingInfoForm');
  try {
    const form = new FormData(event.currentTarget);
    const data = await publicApi('/api/public/applications/track', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        referenceCode: form.get('referenceCode'),
        token: form.get('token')
      })
    });
    const statusText = applicationStatusText(data.status);
    setStatus(state, `${data.referenceCode} başvurusu: ${statusText}`, 'success');
    result.innerHTML = `
      <h2>${publicEscape(data.applicantFullName)}</h2>
      <dl class="track-summary">
        <div><dt>Referans</dt><dd>${publicEscape(data.referenceCode)}</dd></div>
        <div><dt>E-posta</dt><dd>${publicEscape(data.maskedEmail || '-')}</dd></div>
        <div><dt>Rol</dt><dd>${roleText(data.applicantRole)}</dd></div>
        <div><dt>Tesis</dt><dd>${publicEscape(data.facilityName || data.accommodationType)}</dd></div>
        <div><dt>Durum</dt><dd><strong>${publicEscape(statusText)}</strong></dd></div>
      </dl>
      <ul class="timeline">${(data.history || []).map(item => `<li><strong>${publicEscape(applicationStatusText(item.status))}</strong><br><span>${new Date(item.createdAt).toLocaleString('tr-TR')}</span><br>${publicEscape(item.note || '')}</li>`).join('')}</ul>`;
    if (missingForm) {
      missingForm.hidden = data.status !== 'MissingInformation';
      document.getElementById('missingReference').value = form.get('referenceCode') || '';
      document.getElementById('missingToken').value = form.get('token') || '';
    }
  } catch (error) {
    setStatus(state, error.message, 'error');
    result.innerHTML = '';
    if (missingForm) missingForm.hidden = true;
  }
}

async function submitMissingInformation(event) {
  event.preventDefault();
  const state = document.getElementById('trackState');
  const form = event.currentTarget;
  try {
    const response = await publicApi('/api/public/applications/update-missing-information', {
      method: 'POST',
      body: new FormData(form)
    });
    setStatus(state, response.message, 'success');
    form.hidden = true;
    form.reset();
  } catch (error) {
    setStatus(state, error.message, 'error');
  }
}

function applicationStatusText(status) {
  const map = {
    EmailVerificationPending: 'E-posta doğrulaması bekleniyor',
    Pending: 'İnceleme kuyruğunda',
    UnderReview: 'İnceleniyor',
    MissingInformation: 'Ek bilgi gerekiyor',
    ApprovedAwaitingActivation: 'Onaylandı, hesap aktivasyonu bekleniyor',
    Approved: 'Onaylandı',
    Rejected: 'Reddedildi',
    Cancelled: 'İptal edildi'
  };
  return map[status] || status || '-';
}

function roleText(role) {
  const map = { Ogrenci: 'Öğrenci', Personel: 'Personel' };
  return publicEscape(map[role] || role || '-');
}

document.addEventListener('DOMContentLoaded', () => {
  verifyApplicationFromQuery();
  const ref = queryValue('ref');
  const token = queryValue('token');
  if (ref) document.getElementById('trackReference')?.setAttribute('value', ref);
  if (token) document.getElementById('trackToken')?.setAttribute('value', token);
  document.getElementById('trackForm')?.addEventListener('submit', trackApplication);
  document.getElementById('missingInfoForm')?.addEventListener('submit', submitMissingInformation);
});
