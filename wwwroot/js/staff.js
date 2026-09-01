const token = localStorage.getItem('token');
const user = JSON.parse(localStorage.getItem('currentUser') || '{}');
const role = String(user.role || claim('http://schemas.microsoft.com/ws/2008/06/identity/claims/role') || '').toLowerCase();
const technical = role === 'teknikpersonel';
const cleaning = role === 'temizlikpersoneli';
const state = { requests: [], periodic: [], tasks: [], assignments: [] };

const pageCopy = {
  dashboard: ['Operasyon Özeti', 'Vardiya durumu, bekleyen görevler ve öncelikli kayıtlar.'],
  assignments: ['Görevlendirmeler', 'Yurt yönetimi tarafından size atanan saha görevleri.'],
  requests: ['Arıza Talepleri', 'Sakinlerden ve yönetimden gelen teknik iş emirleri.'],
  periodic: ['Periyodik Bakımlar', 'Planlı bakım döngüleri ve yaklaşan kontroller.'],
  tasks: ['Görevlerim', 'Temizlik ve düzenleme görevleriniz.'],
  faultReport: ['Arıza Bildir', 'Görev sırasında tespit ettiğiniz arızaları yönetime iletin.']
};

document.addEventListener('DOMContentLoaded', () => {
  if (!token || (!technical && !cleaning)) {
    localStorage.clear();
    location.replace('/index.html');
    return;
  }

  by('staffName').textContent = user.fullName || 'Personel';
  by('staffRole').textContent = technical ? 'Teknik Personel' : 'Temizlik Personeli';
  by('roleLabel').textContent = technical ? 'Teknik Personel Paneli' : 'Temizlik Personeli Paneli';
  buildNav();
  bindShell();
  load();
  loadDutyLocation();
  setInterval(() => {
    const modalOpen = by('modalBackdrop').style.display !== 'none';
    if (!document.hidden && !modalOpen) load();
  }, 15000);
});

function bindShell() {
  by('logoutBtn')?.addEventListener('click', () => {
    localStorage.clear();
    location.replace('/index.html');
  });
  by('closeModal').addEventListener('click', closeModal);
  by('modalBackdrop').addEventListener('click', event => {
    if (event.target.id === 'modalBackdrop') closeModal();
  });
  by('faultForm').addEventListener('submit', reportFault);
  by('addMaintenanceBtn')?.addEventListener('click', maintenanceForm);
}

function buildNav() {
  const items = technical
    ? [
        ['dashboard', '📊', 'Operasyon Özeti'],
        ['assignments', '📋', 'Görevlendirmeler'],
        ['requests', '🛠️', 'Arıza Talepleri'],
        ['periodic', '🔄', 'Periyodik Bakımlar']
      ]
    : [
        ['dashboard', '📊', 'Bugünün Özeti'],
        ['assignments', '📋', 'Görevlendirmeler'],
        ['tasks', '✨', 'Görevlerim'],
        ['faultReport', '⚑', 'Arıza Bildir']
      ];

  by('staffNav').innerHTML = `${items.map(([id, iconValue, title], index) => `
    <button class="nav-item ${index === 0 ? 'active' : ''}" type="button" data-page="${id}">
      <span>${iconValue}</span>
      <span class="nav-label">${title}</span>
    </button>
  `).join('')}
    <button class="nav-item danger" id="logoutBtn" type="button">
      <span>🚪</span>
      <span class="nav-label">Çıkış Yap</span>
    </button>`;

  document.querySelectorAll('[data-page]').forEach(button => {
    button.addEventListener('click', () => openPage(button.dataset.page));
  });
  applyPageCopy('dashboard');
}

function openPage(id) {
  document.querySelectorAll('.page-section').forEach(section => section.classList.toggle('active', section.id === id));
  document.querySelectorAll('.nav-item[data-page]').forEach(button => button.classList.toggle('active', button.dataset.page === id));
  applyPageCopy(id);
}

function applyPageCopy(id) {
  const [title, description] = pageCopy[id] || ['Personel Paneli', 'Günlük operasyon akışı.'];
  by('sectionTitle').textContent = title;
  by('welcomeLabel').textContent = description;
}

async function load() {
  try {
    const assignments = api('/api/staff/assignments');
    if (technical) {
      [state.requests, state.periodic, state.assignments] = await Promise.all([
        api('/api/staff/maintenance-requests'),
        api('/api/staff/periodic-maintenance'),
        assignments
      ]);
    } else {
      [state.tasks, state.assignments] = await Promise.all([
        api('/api/staff/cleaning-tasks'),
        assignments
      ]);
    }
    render();
  } catch (error) {
    toast(error.message || 'Veriler yüklenemedi.', true);
  }
}

function render() {
  technical ? renderTechnical() : renderCleaning();
  renderAssignments();
}

function renderTechnical() {
  const activeRequests = state.requests.filter(item => item.status !== 'Resolved');
  const dueMaintenance = state.periodic.filter(item => new Date(item.nextMaintenanceDate) <= new Date(Date.now() + 2592e5));
  const openAssignments = state.assignments.filter(item => !item.isCompleted);

  renderOverview([
    ['Açık İş Emri', activeRequests.length, '🛠️'],
    ['Yaklaşan Bakım', dueMaintenance.length, '🔄'],
    ['Yönetici Ataması', openAssignments.length, '📋']
  ]);
  by('dashboardContent').innerHTML = summaryPanel('Öncelikli Arızalar', activeRequests.slice(0, 4).map(item => `${item.roomNumber} · ${item.category}`), 'İş emri yok.')
    + summaryPanel('Yaklaşan Bakım', dueMaintenance.slice(0, 4).map(item => `${item.systemName} · ${item.location}`), 'Yakın bakım yok.');
  by('requestList').innerHTML = state.requests.length ? state.requests.map(requestCard).join('') : emptyState('Arıza kaydı yok.');
  by('periodicList').innerHTML = state.periodic.length ? state.periodic.map(periodicCard).join('') : emptyState('Bakım planı yok.');
}

function renderCleaning() {
  const openTasks = state.tasks.filter(item => !item.isCompleted);
  const openAssignments = state.assignments.filter(item => !item.isCompleted);
  const completedToday = state.tasks.filter(item => item.isCompleted && sameDay(item.completedAt));

  renderOverview([
    ['Yönetimden Bekleyen', openAssignments.length, '📋'],
    ['Günlük Görev', openTasks.length, '✨'],
    ['Bugün Tamamlanan', completedToday.length, '✅']
  ]);
  by('dashboardContent').innerHTML = summaryPanel('Yönetimden Öncelikli İşler', openAssignments.slice(0, 4).map(item => `${item.title} · ${item.location}`), 'Yönetim ataması yok.')
    + summaryPanel('Günlük Görevler', openTasks.slice(0, 4).map(item => `${item.taskType} · ${item.location}`), 'Bekleyen görev yok.');
  by('taskList').innerHTML = state.tasks.length ? state.tasks.map(taskCard).join('') : emptyState('Günlük görev yok.');
}

function renderAssignments() {
  const items = [...state.assignments].sort((a, b) => rank(a.priority) - rank(b.priority) || new Date(a.dueDate || '9999') - new Date(b.dueDate || '9999'));
  by('assignmentList').innerHTML = items.length ? items.map(assignmentCard).join('') : emptyState('Yurt yönetimi tarafından atanmış görev yok.');
}

function renderOverview(items) {
  by('overview').innerHTML = items.map(([label, value, iconValue]) => `
    <article class="kpi-card">
      <div class="kpi-top">
        <span class="kpi-label">${label}</span>
        <span class="kpi-icon">${iconValue}</span>
      </div>
      <div class="kpi-value">${value}</div>
    </article>
  `).join('');
}

function summaryPanel(title, rows, emptyText) {
  return `
    <article class="panel staff-summary-panel">
      <div class="panel-head"><h2>${title}</h2></div>
      <div class="activity-list">
        ${rows.length ? rows.map(row => `<div class="activity-item"><strong>${esc(row)}</strong><span class="badge badge-muted">Öncelik</span></div>`).join('') : emptyState(emptyText)}
      </div>
    </article>`;
}

function requestCard(item) {
  const statusBadge = item.status === 'Resolved'
    ? badge('Çözüldü', 'success')
    : item.status === 'InProgress'
      ? badge('İşlemde', 'warning')
      : badge('Açık', 'info');
  const target = item.targetRepairDate
    ? `Hedef: ${date(item.targetRepairDate)}${item.repairPeriodDays ? ` (${item.repairPeriodDays} gün)` : ''}`
    : 'Onarım süresi bekliyor';
  const action = item.status === 'Resolved'
    ? ''
    : item.isManagerAssignment
      ? `<button class="row-btn" type="button" onclick="completeAssignment(${Math.abs(item.id)})">Tamamlandı İşaretle</button>`
      : `<button class="row-btn" type="button" onclick="scheduleRepair(${item.id})">Süre Belirle</button><button class="row-btn" type="button" onclick="resolveRepair(${item.id})">Tamir Edildi</button>`;

  return activityItem({
    icon: '🛠️',
    title: `${item.isManagerAssignment ? 'Yönetici görevi · ' : `Oda ${esc(item.roomNumber)} · `}${esc(item.category)}`,
    detail: item.description,
    meta: `${target}${item.isManagerAssignment ? ` · ${esc(item.priority)} öncelik` : ''}`,
    badges: `${statusBadge}${item.isManagerAssignment ? badge('Yurt Yönetimi', 'info') : ''}`,
    action
  });
}

function periodicCard(item) {
  const isLate = new Date(item.nextMaintenanceDate) < new Date();
  return activityItem({
    icon: '🔄',
    title: `${esc(item.systemName)} · ${esc(item.location)}`,
    detail: `${esc(item.notes || 'Not eklenmemiş')} · ${item.intervalDays} günde bir`,
    meta: `Sonraki bakım: ${date(item.nextMaintenanceDate)}`,
    badges: isLate ? badge('Gecikmiş', 'danger') : badge('Planlı', 'info'),
    action: `<button class="row-btn" type="button" onclick="completeMaintenance(${item.id})">Bakım Yapıldı</button>`
  });
}

function taskCard(item) {
  return activityItem({
    icon: taskIcon(item.taskType),
    title: `${esc(item.taskType)} · ${esc(item.location)}`,
    detail: item.notes || 'Açıklama eklenmemiş',
    meta: item.isCompleted ? `Tamamlandı: ${date(item.completedAt)}` : `Kaydedildi: ${date(item.createdAt)}`,
    badges: item.isCompleted ? badge('Tamamlandı', 'success') : badge('Bekliyor', 'info'),
    action: item.isCompleted ? '' : `<button class="row-btn" type="button" onclick="completeTask(${item.id})">Tamamlandı İşaretle</button>`
  });
}

function assignmentCard(item) {
  const tone = { Acil: 'danger', Yüksek: 'warning', Normal: 'info', Düşük: 'muted' }[item.priority] || 'info';
  return activityItem({
    icon: item.isMaintenanceRequest ? '🛠️' : '📋',
    title: `${esc(item.title)} · ${esc(item.location)}`,
    detail: item.details || 'Yönetim notu eklenmemiş',
    meta: item.dueDate ? `Termin: ${date(item.dueDate)}` : 'Termin belirtilmedi',
    badges: `${badge('Yurt Yönetimi', 'info')}${badge(`${esc(item.priority)} öncelik`, tone)}${item.isCompleted ? badge('Tamamlandı', 'success') : badge('Bekliyor', 'warning')}`,
    action: item.isCompleted ? '' : `<button class="row-btn" type="button" onclick="completeAssignment(${item.id})">Tamamlandı İşaretle</button>`
  });
}

function activityItem({ icon: iconValue, title, detail, meta, badges, action }) {
  return `
    <div class="activity-item staff-activity-item">
      <span class="staff-item-icon">${iconValue}</span>
      <div class="staff-item-main">
        <strong>${title}</strong>
        <small>${esc(detail)}</small>
      </div>
      <div class="item-meta staff-item-meta">
        <small>${esc(meta)}</small>
        <div class="staff-badges">${badges}</div>
        ${action ? `<div class="staff-actions">${action}</div>` : ''}
      </div>
    </div>`;
}

async function scheduleRepair(id) {
  openModal('Onarım Süresi Belirle', `
    <form id="repairScheduleForm" class="form-grid">
      <label class="full">Onarım süresi (gün)
        <input type="number" min="1" name="repairPeriodDays" value="3" required>
      </label>
      <div class="form-actions">
        <button class="ghost-btn" type="button" data-modal-close>Vazgeç</button>
        <button class="primary-btn" type="submit">Süreyi Kaydet</button>
      </div>
    </form>`, () => {
      by('repairScheduleForm').addEventListener('submit', async event => {
        event.preventDefault();
        const data = Object.fromEntries(new FormData(event.currentTarget).entries());
        try {
          await api(`/api/staff/maintenance-requests/${id}/schedule`, {
            method: 'PATCH',
            body: JSON.stringify({ repairPeriodDays: Number(data.repairPeriodDays) })
          });
          closeModal();
          toast('Onarım süresi kaydedildi.');
          load();
        } catch (error) {
          toast(error.message, true);
        }
      });
    });
}

function resolveRepair(id) {
  confirmAction('Arızayı Tamir Edildi İşaretle', 'Bu arıza kaydını çözüldü olarak kapatmak istiyor musunuz?', async () => {
    await api(`/api/staff/maintenance-requests/${id}/resolve`, { method: 'PATCH' });
    toast('Arıza çözüldü.');
    load();
  });
}

function completeMaintenance(id) {
  confirmAction('Bakımı Tamamla', 'Bu periyodik bakımın tamamlandığını kaydetmek istiyor musunuz?', async () => {
    await api(`/api/staff/periodic-maintenance/${id}/complete`, { method: 'PATCH' });
    toast('Bakım tamamlandı.');
    load();
  });
}

function completeTask(id) {
  confirmAction('Görevi Tamamla', 'Bu görevi tamamlandı olarak işaretlemek istiyor musunuz?', async () => {
    await api(`/api/staff/cleaning-tasks/${id}/complete`, { method: 'PATCH' });
    toast('Görev tamamlandı.');
    load();
  });
}

function completeAssignment(id) {
  confirmAction('Görevlendirmeyi Tamamla', 'Yönetici tarafından atanan bu görevi tamamlandı olarak işaretlemek istiyor musunuz?', async () => {
    await api(`/api/staff/assignments/${id}/complete`, { method: 'PATCH' });
    toast('Yönetici görevi tamamlandı.');
    load();
  });
}

function maintenanceForm() {
  openModal('Periyodik Bakım Planla', `
    <form id="maintenanceForm" class="form-grid two">
      <label>Sistem<input name="systemName" required></label>
      <label>Konum<input name="location" required></label>
      <label>Periyot (gün)<input type="number" min="1" name="intervalDays" value="30" required></label>
      <label>Sonraki bakım<input type="date" name="nextMaintenanceDate" required></label>
      <label class="full">Not<textarea name="notes"></textarea></label>
      <div class="form-actions">
        <button class="ghost-btn" type="button" data-modal-close>Vazgeç</button>
        <button class="primary-btn" type="submit">Planı Kaydet</button>
      </div>
    </form>`, () => {
      by('maintenanceForm').addEventListener('submit', async event => {
        event.preventDefault();
        const values = Object.fromEntries(new FormData(event.currentTarget).entries());
        values.intervalDays = Number(values.intervalDays);
        try {
          await api('/api/staff/periodic-maintenance', { method: 'POST', body: JSON.stringify(values) });
          closeModal();
          toast('Bakım planı eklendi.');
          load();
        } catch (error) {
          toast(error.message, true);
        }
      });
    });
}

async function reportFault(event) {
  event.preventDefault();
  try {
    await api('/api/staff/fault-reports', {
      method: 'POST',
      body: JSON.stringify(Object.fromEntries(new FormData(event.currentTarget).entries()))
    });
    event.currentTarget.reset();
    toast('Arıza bildirimi yurt yönetimine iletildi.');
  } catch (error) {
    toast(error.message, true);
  }
}

function confirmAction(title, message, onConfirm) {
  openModal(title, `
    <div class="confirm-copy">
      <p>${esc(message)}</p>
    </div>
    <div class="form-actions">
      <button class="ghost-btn" type="button" data-modal-close>Vazgeç</button>
      <button class="primary-btn" type="button" id="confirmActionBtn">Onayla</button>
    </div>`, () => {
      by('confirmActionBtn').addEventListener('click', async () => {
        try {
          await onConfirm();
          closeModal();
        } catch (error) {
          toast(error.message || 'İşlem gerçekleştirilemedi.', true);
        }
      });
    });
}

function openModal(title, html, bind) {
  by('modalTitle').textContent = title;
  by('modalBody').innerHTML = html;
  by('modalBody').querySelectorAll('[data-modal-close]').forEach(button => button.addEventListener('click', closeModal));
  const backdrop = by('modalBackdrop');
  backdrop.style.display = 'grid';
  setTimeout(() => backdrop.classList.add('show'), 20);
  bind?.();
}

function closeModal() {
  const backdrop = by('modalBackdrop');
  backdrop.classList.remove('show');
  setTimeout(() => {
    backdrop.style.display = 'none';
    by('modalBody').innerHTML = '';
  }, 180);
}

function api(path, options = {}) {
  return fetch(path, {
    ...options,
    headers: {
      Authorization: `Bearer ${token}`,
      'Content-Type': 'application/json',
      ...(options.headers || {})
    }
  }).then(async response => {
    if (response.status === 401 || response.status === 403) {
      localStorage.clear();
      location.replace('/index.html');
      throw new Error('Oturum yetkisi bulunamadı.');
    }
    if (!response.ok) throw new Error(await response.text() || 'İşlem gerçekleştirilemedi.');
    return response.status === 204 ? null : response.json();
  });
}

async function loadDutyLocation() {
  try {
    const assignment = await api('/api/staff/duty-location');
    const locationName = assignment?.dormitoryName || assignment?.housingUnitName;
    by('dutyLocation').textContent = locationName ? `Görev Yeri: ${locationName}` : 'Görev yeri atanmamış';
  } catch {
    by('dutyLocation').textContent = 'Görev yeri atanmamış';
  }
}

function by(id) {
  return document.getElementById(id);
}

function claim(key) {
  try {
    let payload = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
    while (payload.length % 4) payload += '=';
    return JSON.parse(decodeURIComponent(atob(payload).split('').map(char => `%${(`00${char.charCodeAt(0).toString(16)}`).slice(-2)}`).join('')))[key];
  } catch {
    return null;
  }
}

function emptyState(text) {
  return `<p class="muted staff-empty">${esc(text)}</p>`;
}

function badge(label, tone) {
  const tones = { success: 'badge-success', warning: 'badge-warning', danger: 'badge-danger', info: 'badge-info', muted: 'badge-muted' };
  return `<span class="badge ${tones[tone] || 'badge-muted'}">${label}</span>`;
}

function date(value) {
  return value ? new Date(value).toLocaleDateString('tr-TR') : '-';
}

function sameDay(value) {
  return value && new Date(value).toDateString() === new Date().toDateString();
}

function rank(value) {
  return ({ Acil: 0, Yüksek: 1, Normal: 2, Düşük: 3 }[value] ?? 2);
}

function esc(value) {
  return String(value ?? '').replace(/[&<>"']/g, char => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#039;' }[char]));
}

function taskIcon(value) {
  const task = String(value || '');
  if (task.includes('Çöp')) return '♻️';
  if (task.toLocaleLowerCase('tr-TR').includes('düzen')) return '↔️';
  return '✨';
}

function toast(message, isError = false) {
  const element = document.createElement('div');
  element.className = `toast${isError ? ' error' : ''}`;
  element.textContent = message;
  by('toastHost').appendChild(element);
  setTimeout(() => element.remove(), 3600);
}
