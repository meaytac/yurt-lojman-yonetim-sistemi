async function activateAccount(event) {
  event.preventDefault();
  const state = document.getElementById('activationState');
  const form = new FormData(event.currentTarget);
  const password = String(form.get('password') || '');
  const confirmPassword = String(form.get('confirmPassword') || '');
  if (password !== confirmPassword) {
    setStatus(state, 'Şifre ve tekrar alanı aynı olmalıdır.', 'error');
    return;
  }

  try {
    const data = await publicApi('/api/public/applications/activate', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        referenceCode: form.get('referenceCode'),
        token: form.get('token'),
        password,
        confirmPassword
      })
    });
    setStatus(state, data.message, 'success');
    setTimeout(() => {
      window.location.href = '/index.html';
    }, 1200);
  } catch (error) {
    setStatus(state, error.message, 'error');
  }
}

document.addEventListener('DOMContentLoaded', () => {
  const ref = queryValue('ref');
  const token = queryValue('token');
  if (ref) document.getElementById('activationReference')?.setAttribute('value', ref);
  if (token) document.getElementById('activationToken')?.setAttribute('value', token);
  document.querySelectorAll('[data-toggle-password]').forEach(button => {
    button.addEventListener('click', () => {
      const input = document.getElementById(button.dataset.togglePassword);
      if (!input) return;
      const show = input.type === 'password';
      input.type = show ? 'text' : 'password';
      button.textContent = show ? 'Gizle' : 'Göster';
    });
  });
  document.getElementById('activationForm')?.addEventListener('submit', activateAccount);
});
