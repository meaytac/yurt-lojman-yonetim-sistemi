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
    setStatus(state, `${data.referenceCode} başvurusu: ${data.status}`, 'success');
    result.innerHTML = `
      <h2>${publicEscape(data.applicantFullName)}</h2>
      <p>${publicEscape(data.facilityName || data.accommodationType)} için başvuru durumu: <strong>${publicEscape(data.status)}</strong></p>
      <ul class="timeline">${(data.history || []).map(item => `<li><strong>${publicEscape(item.status)}</strong><br><span>${new Date(item.createdAt).toLocaleString('tr-TR')}</span><br>${publicEscape(item.note || '')}</li>`).join('')}</ul>`;
  } catch (error) {
    setStatus(state, error.message, 'error');
    result.innerHTML = '';
  }
}

document.addEventListener('DOMContentLoaded', () => {
  verifyApplicationFromQuery();
  const ref = queryValue('ref');
  const token = queryValue('token');
  if (ref) document.getElementById('trackReference')?.setAttribute('value', ref);
  if (token) document.getElementById('trackToken')?.setAttribute('value', token);
  document.getElementById('trackForm')?.addEventListener('submit', trackApplication);
});
