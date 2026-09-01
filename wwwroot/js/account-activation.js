async function activateAccount(event) {
  event.preventDefault();
  const state = document.getElementById('activationState');
  const form = new FormData(event.currentTarget);
  try {
    const data = await publicApi('/api/public/applications/activate', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        referenceCode: form.get('referenceCode'),
        token: form.get('token'),
        password: form.get('password')
      })
    });
    setStatus(state, data.message, 'success');
  } catch (error) {
    setStatus(state, error.message, 'error');
  }
}

document.addEventListener('DOMContentLoaded', () => {
  const ref = queryValue('ref');
  const token = queryValue('token');
  if (ref) document.getElementById('activationReference')?.setAttribute('value', ref);
  if (token) document.getElementById('activationToken')?.setAttribute('value', token);
  document.getElementById('activationForm')?.addEventListener('submit', activateAccount);
});
