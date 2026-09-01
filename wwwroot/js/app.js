const tokenKey = 'token';

function authHeaders(extra = {}) {
  const token = localStorage.getItem(tokenKey);
  return token ? { ...extra, Authorization: `Bearer ${token}` } : extra;
}

async function api(path, options = {}) {
  const response = await fetch(path, {
    ...options,
    headers: authHeaders(options.headers || {})
  });

  if (!response.ok) {
    const message = await response.text();
    throw new Error(message || response.statusText);
  }

  if (response.status === 204) return null;
  return response.json();
}

async function login() {
  const email = document.getElementById('email').value;
  const password = document.getElementById('password').value;
  const state = document.getElementById('loginState');
  try {
    const data = await api('/api/auth/login', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, password })
    });
    localStorage.setItem('token', data.token);
    localStorage.setItem('currentUser', JSON.stringify({
      userId: data.userId,
      fullName: data.fullName,
      email: data.email,
      role: data.role,
      phoneNumber: data.phoneNumber || ''
    }));
    state.textContent = `${data.fullName} olarak giriş yapıldı.`;
    state.className = 'login-message success';

const role = String(data.role || '').trim().toLowerCase();
    const isAdmin = role === 'admin';
    const isYetkili = role === 'yetkili';
    const isStaff = role === 'teknikpersonel' || role === 'temizlikpersoneli';
    const target = isAdmin ? '/admin.html' : isYetkili ? '/yetkili.html' : isStaff ? '/staff.html' : '/application.html';
    window.setTimeout(() => {
      window.location.href = target;
    }, 250);
  } catch (error) {
    state.textContent = error.message;
    state.className = 'login-message error';
  }
}

async function loadDashboard() {
  const ids = ['totalCapacity', 'occupancyRate', 'pendingApplications', 'openRequests', 'unpaidDebts'];
  if (!document.getElementById(ids[0])) return;
  try {
    const data = await api('/api/admin/dashboard-stats');
    document.getElementById('totalCapacity').textContent = data.totalCapacity;
    document.getElementById('occupancyRate').textContent = `${data.occupancyRate}%`;
    document.getElementById('pendingApplications').textContent = data.pendingApplicationCount;
    document.getElementById('openRequests').textContent = data.openRequestCount;
    document.getElementById('unpaidDebts').textContent = `${data.totalUnpaidAndOverdueDebt} TL`;
  } catch {
    ids.forEach(id => document.getElementById(id).textContent = 'Giriş gerekli');
  }
}

async function loadAnnouncements() {
  const root = document.getElementById('announcements');
  if (!root) return;
  try {
    const items = (await api('/api/announcements'))
      .sort((left, right) => new Date(right.createdAt) - new Date(left.createdAt))
      .slice(0, 5);
    root.innerHTML = items.map(x => {
      const date = new Date(x.createdAt || Date.now());
      return `
      <article class="announcement-item">
        <time class="announcement-date" datetime="${date.toISOString()}">
          <span>${date.toLocaleDateString('tr-TR', { day: '2-digit' })}</span>
          <small>${date.toLocaleDateString('tr-TR', { month: 'short' }).replace('.', '')}</small>
        </time>
        <div class="announcement-copy">
          <strong>${escapeHtml(x.title)}</strong>
          <span>${escapeHtml(x.content)}</span>
        </div>
        <span class="announcement-arrow" aria-hidden="true">→</span>
      </article>`;
    }).join('');
  } catch (error) {
    root.innerHTML = `<p class="muted">${escapeHtml(error.message)}</p>`;
  }
}

async function loadRooms() {
  const tbody = document.getElementById('roomsTable');
  if (!tbody) return;
  const rooms = await api('/api/admin/rooms');
  tbody.innerHTML = rooms.map(x => `<tr><td>${escapeHtml(x.roomNumber)}</td><td>${x.capacity}</td><td>${x.currentOccupancy}</td><td>${x.status}</td><td>${x.price}</td></tr>`).join('');
}

async function loadMyApplications() {
  const root = document.getElementById('myApplications');
  if (!root) return;
  try {
    const items = await api('/api/applications/mine');
    root.innerHTML = items.map(x => `<article class="item"><strong>${x.accommodationType} - ${x.status}</strong><span>${new Date(x.createdAt).toLocaleString('tr-TR')}</span></article>`).join('');
  } catch (error) {
    root.innerHTML = `<p class="muted">${escapeHtml(error.message)}</p>`;
  }
}

function bindJsonForm(formId, path, afterSave) {
  const form = document.getElementById(formId);
  if (!form) return;
  form.addEventListener('submit', async event => {
    event.preventDefault();
    const data = Object.fromEntries(new FormData(form).entries());
    form.querySelectorAll('input[type="number"]').forEach(input => data[input.name] = Number(input.value));
    form.querySelectorAll('input[type="checkbox"]').forEach(input => data[input.name] = input.checked);
    try {
      await api(path, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(data) });
      form.reset();
      if (afterSave) await afterSave();
    } catch (error) {
      alert(error.message);
    }
  });
}

function bindMultipartForm(formId, path, afterSave) {
  const form = document.getElementById(formId);
  if (!form) return;
  form.addEventListener('submit', async event => {
    event.preventDefault();
    try {
      await api(path, { method: 'POST', body: new FormData(form) });
      form.reset();
      if (afterSave) await afterSave();
    } catch (error) {
      alert(error.message);
    }
  });
}

function escapeHtml(value) {
  return String(value ?? '').replace(/[&<>"']/g, char => ({
    '&': '&amp;',
    '<': '&lt;',
    '>': '&gt;',
    '"': '&quot;',
    "'": '&#039;'
  }[char]));
}

document.addEventListener('DOMContentLoaded', () => {
  const loginForm = document.getElementById('loginForm');
  if (loginForm) {
    loginForm.addEventListener('submit', event => {
      event.preventDefault();
      login();
    });
  }
  loadDashboard();
  loadAnnouncements();
});
