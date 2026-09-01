const tokenKey = 'token';

const state = {
  stats: null,
  assignedFacilities: [],
  students: { items: [], page: 1, pageSize: 10, totalCount: 0 },
  availableRooms: [],
  studentsWithRooms: [],
  manageRooms: [],
  announcements: [],
  fallbackMode: false
};

const REFRESH_INTERVAL = 15000;
let activeSection = 'dashboard';

const sectionTitles = {
  dashboard: 'Kontrol Paneli',
  students: 'Öğrenci Yönetimi',
  manage: 'Yerleşim & Oda Düzenleme',
  announcements: 'Duyuru Yönetimi',
  rooms: 'Boş Odalar'
};

document.addEventListener('DOMContentLoaded', () => {
  const token = getStoredToken();
  if (!isValidYetkiliToken(token)) {
    clearStoredTokens();
    window.location.href = '/index.html';
    return;
  }

  bindShell();
  openApp(token);
});

function getStoredToken() {
  return localStorage.getItem(tokenKey) || localStorage.getItem('admin_token');
}

function isValidYetkiliToken(token) {
  if (!token) return false;
  const claims = parseJwt(token);
  const role = String(claims['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] || claims.role || '').toLowerCase();
  return Boolean(claims.sub) && (!claims.exp || claims.exp * 1000 > Date.now()) && role === 'yetkili';
}

function clearStoredTokens() {
  localStorage.removeItem('token');
  localStorage.removeItem('admin_token');
}

function bindShell() {
  document.getElementById('logoutBtn').addEventListener('click', logout);
  document.getElementById('closeModal').addEventListener('click', closeModal);
  document.getElementById('modalBackdrop').addEventListener('click', event => {
    if (event.target.id === 'modalBackdrop') closeModal();
  });

  document.querySelectorAll('[data-section]').forEach(button => {
    button.addEventListener('click', () => switchSection(button.dataset.section));
  });
  document.querySelectorAll('[data-section-jump]').forEach(button => {
    button.addEventListener('click', () => switchSection(button.dataset.sectionJump));
  });
  document.querySelectorAll('[data-modal]').forEach(button => {
    button.addEventListener('click', () => openNamedModal(button.dataset.modal));
  });

  document.getElementById('studentSearch').addEventListener('input', debounce(() => loadStudents(1), 350));
  document.getElementById('roomTypeFilter').addEventListener('change', loadAvailableRooms);

  setInterval(() => {
    if (document.hidden) return;
    if (document.getElementById('modalBackdrop').style.display !== 'none') return;
    refreshActiveSection();
  }, REFRESH_INTERVAL);
}

async function openApp(token) {
  const claims = parseJwt(token);
  document.getElementById('yetkiliName').textContent = claims.fullName || claims.name || 'Yetkili';
  document.getElementById('yetkiliRole').textContent = 'Yetkili';
  switchSection('dashboard');
  await Promise.allSettled([loadDashboard(), loadAssignedFacilities(), loadStudents(1), loadAvailableRooms(), loadManage(), loadAnnouncements()]);
}

function logout() {
  clearStoredTokens();
  window.location.href = '/index.html';
}

function switchSection(id) {
  activeSection = id;
  document.querySelectorAll('.page-section').forEach(section => section.classList.toggle('active', section.id === id));
  document.querySelectorAll('.nav-item[data-section]').forEach(button => button.classList.toggle('active', button.dataset.section === id));
  document.getElementById('sectionTitle').textContent = sectionTitles[id] || 'Yetkili Paneli';
}

function refreshActiveSection() {
  const refreshers = {
    dashboard: loadDashboard,
    students: () => loadStudents(state.students.page || 1),
    manage: loadManage,
    announcements: loadAnnouncements,
    rooms: loadAvailableRooms
  };
  refreshers[activeSection]?.();
}

async function loadDashboard() {
  try {
    state.stats = await api('/api/yetkili/facilities');
  } catch {
    state.fallbackMode = true;
    state.stats = [];
  }
  renderDashboard();
}

function renderDashboard() {
  const facilities = state.stats || [];
  const cards = [
    ['Atanmış Yurt', facilities.length, '🏢'],
    ['Öğrenci Sayısı', state.students.totalCount ?? 0, '👨‍🎓'],
    ['Müsait Oda', state.availableRooms?.filter(r => r.currentOccupancy < r.capacity).length ?? 0, '🚪']
  ];

  document.getElementById('kpiGrid').innerHTML = cards.map(([label, value, icon]) => `
    <article class="kpi-card">
      <div class="kpi-top">
        <span class="kpi-label">${label}</span>
        <span class="kpi-icon">${icon}</span>
      </div>
      <div class="kpi-value">${value}</div>
    </article>
  `).join('');

  document.getElementById('facilityInfo').textContent = facilities.length > 0
    ? facilities.map(f => f.name).join(', ')
    : 'Henüz bir yurta atanmamışsınız.';

  document.getElementById('assignedFacilities').innerHTML = emptyOr(facilities, f => `
    <div class="activity-item">
      <div>
        <strong>${escapeHtml(f.name)}</strong>
        <small>${escapeHtml(f.campusLocation)} · Kapasite: ${f.totalCapacity}</small>
      </div>
      <span class="badge ${f.isActive ? 'badge-success' : 'badge-muted'}">${f.isActive ? 'Aktif' : 'Pasif'}</span>
    </div>
  `);

  document.getElementById('recentStudents').innerHTML = emptyOr(state.students.items.slice(0, 5), s => `
    <div class="activity-item">
      <div>
        <strong>${escapeHtml(s.fullName)}</strong>
        <small>${escapeHtml(s.email)}</small>
      </div>
      <span class="badge ${s.isActive ? 'badge-success' : 'badge-muted'}">${s.isActive ? 'Aktif' : 'Pasif'}</span>
    </div>
  `);
}

async function loadAssignedFacilities() {
  try {
    state.assignedFacilities = state.fallbackMode ? [] : await api('/api/yetkili/facilities');
  } catch {
    state.assignedFacilities = [];
  }
}

async function loadStudents(page = state.students.page) {
  try {
    const search = encodeURIComponent(document.getElementById('studentSearch').value);
    state.students = state.fallbackMode
      ? { items: [], page, pageSize: 10, totalCount: 0 }
      : await api(`/api/yetkili/students?page=${page}&pageSize=10&search=${search}`);
  } catch {
    state.students = { items: [], page, pageSize: 10, totalCount: 0 };
  }
  renderStudents();
}

function renderStudents() {
  document.getElementById('studentRows').innerHTML = emptyTable(state.students.items || [], item => `
    <tr>
      <td><strong>${escapeHtml(item.fullName)}</strong></td>
      <td>${escapeHtml(item.email)}</td>
      <td>${escapeHtml(item.tcNo)}</td>
      <td>${escapeHtml(item.studentStaffNo || '-')}</td>
      <td>${item.isActive ? '<span class="badge badge-success">Aktif</span>' : '<span class="badge badge-muted">Pasif</span>'}</td>
      <td>
        <button class="row-btn" onclick="editStudent('${item.id}')">Düzenle</button>
        <button class="row-btn danger" onclick="deleteStudent('${item.id}')">Sil</button>
      </td>
    </tr>
  `);

  const totalPages = Math.max(1, Math.ceil((state.students.totalCount || 0) / (state.students.pageSize || 10)));
  renderPager('studentPager', state.students.page || 1, totalPages, loadStudents);
}

async function loadAvailableRooms() {
  try {
    const type = document.getElementById('roomTypeFilter').value;
    state.availableRooms = state.fallbackMode ? [] : await api(`/api/yetkili/rooms/available?type=${type}`);
  } catch {
    state.availableRooms = [];
  }
  renderAvailableRooms();
}

function renderAvailableRooms() {
  document.getElementById('availableRoomRows').innerHTML = emptyTable(state.availableRooms, item => `
    <tr>
      <td><strong>${escapeHtml(item.roomNumber)}</strong></td>
      <td>${escapeHtml(item.facilityName)}</td>
      <td>${escapeHtml(item.blockName)} / ${item.floorNumber}</td>
      <td>${item.capacity}</td>
      <td>${item.currentOccupancy} / ${item.capacity}</td>
      <td>${money(item.price)}</td>
      <td>${getStatusBadge(item.status)}</td>
    </tr>
  `);
}

function studentForm(item = null) {
  const isEdit = !!item;
  return {
    title: isEdit ? 'Öğrenci Düzenle' : 'Yeni Öğrenci',
    html: `<form id="studentForm" class="form-grid two" data-edit="${isEdit}" data-id="${item?.id || ''}">
      <label>Ad Soyad<input name="fullName" value="${escapeAttr(item?.fullName)}" required></label>
      <label>E-posta<input name="email" type="email" value="${escapeAttr(item?.email)}" required></label>
      <label>TC Kimlik No<input name="tcNo" value="${escapeAttr(item?.tcNo)}" required minlength="11" maxlength="11"></label>
      <label>Öğrenci No<input name="studentStaffNo" value="${escapeAttr(item?.studentStaffNo)}"></label>
      <label>Telefon<input name="phoneNumber" value="${escapeAttr(item?.phoneNumber)}"></label>
      <label>${isEdit ? 'Yeni Şifre (boş bırakılırsa değişmez)' : 'Şifre'}<input name="password" type="password" ${isEdit ? '' : 'required'} minlength="6"></label>
      <button class="primary-btn full" type="submit">${isEdit ? 'Güncelle' : 'Kaydet'}</button>
    </form>`,
    bind: () => document.getElementById('studentForm').addEventListener('submit', submitStudent)
  };
}

async function submitStudent(event) {
  event.preventDefault();
  const form = event.currentTarget;
  const isEdit = form.dataset.edit === 'true';
  const id = form.dataset.id;

  const data = Object.fromEntries(new FormData(form).entries());
  const body = {
    fullName: data.fullName,
    email: data.email,
    tcNo: data.tcNo,
    studentStaffNo: data.studentStaffNo || null,
    phoneNumber: data.phoneNumber || null
  };
  if (data.password) body.password = data.password;

  try {
    if (isEdit) {
      await api(`/api/yetkili/students/${id}`, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
      toast('Öğrenci güncellendi.');
    } else {
      await api('/api/yetkili/students', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
      toast('Öğrenci eklendi.');
    }
    closeModalIfOpen();
    await loadStudents(state.students.page || 1);
    await loadDashboard();
  } catch (error) {
    toast(error.message || 'İşlem başarısız.', true);
  }
}

async function editStudent(id) {
  try {
    const students = state.students.items;
    const student = students.find(s => s.id === id);
    if (!student) {
      const resp = await api(`/api/yetkili/students?page=1&pageSize=100`);
      const found = resp.items.find(s => s.id === id);
      if (found) {
        openNamedModal('studentModal');
        setTimeout(() => {
          document.querySelector('[name="fullName"]').value = found.fullName;
          document.querySelector('[name="email"]').value = found.email;
          document.querySelector('[name="tcNo"]').value = found.tcNo;
          document.querySelector('[name="studentStaffNo"]').value = found.studentStaffNo || '';
          document.querySelector('[name="phoneNumber"]').value = found.phoneNumber || '';
          document.getElementById('studentForm').dataset.edit = 'true';
          document.getElementById('studentForm').dataset.id = found.id;
          document.getElementById('modalTitle').textContent = 'Öğrenci Düzenle';
          document.querySelector('#studentForm button[type="submit"]').textContent = 'Güncelle';
        }, 50);
      }
    } else {
      openNamedModal('studentModal');
      setTimeout(() => {
        document.querySelector('[name="fullName"]').value = student.fullName;
        document.querySelector('[name="email"]').value = student.email;
        document.querySelector('[name="tcNo"]').value = student.tcNo;
        document.querySelector('[name="studentStaffNo"]').value = student.studentStaffNo || '';
        document.querySelector('[name="phoneNumber"]').value = student.phoneNumber || '';
        document.getElementById('studentForm').dataset.edit = 'true';
        document.getElementById('studentForm').dataset.id = student.id;
        document.getElementById('modalTitle').textContent = 'Öğrenci Düzenle';
        document.querySelector('#studentForm button[type="submit"]').textContent = 'Güncelle';
      }, 50);
    }
  } catch (error) {
    toast(error.message);
  }
}

async function deleteStudent(id) {
  if (!confirm('Bu öğrenciyi silmek istiyor musunuz?')) return;
  try {
    await api(`/api/yetkili/students/${id}`, { method: 'DELETE' });
    toast('Öğrenci silindi.');
    await loadStudents(state.students.page || 1);
    await loadDashboard();
  } catch (error) {
    toast(error.message, true);
  }
}

function openNamedModal(name) {
  const forms = {
    studentModal: studentForm(),
    roomEditModal: roomEditForm(),
    announcementModal: announcementForm()
  };
  if (!forms[name]) return;
  openModal(forms[name].title, forms[name].html, forms[name].bind);
}

// ============ YERLESIM DUZENLEME ============
function loadManage() {
  return Promise.allSettled([loadStudentsWithRooms(), loadManageRooms()]);
}

async function loadStudentsWithRooms() {
  try {
    state.studentsWithRooms = await api('/api/yetkili/students-with-rooms');
  } catch {
    state.studentsWithRooms = [];
  }
  renderStudentsWithRooms();
}

function renderStudentsWithRooms() {
  document.getElementById('studentRoomRows').innerHTML = emptyTable(state.studentsWithRooms, item => `
    <tr>
      <td><strong>${escapeHtml(item.fullName)}</strong></td>
      <td>${escapeHtml(item.email)}</td>
      <td>${escapeHtml(item.tcNo)}</td>
      <td>${escapeHtml(item.studentStaffNo || '-')}</td>
      <td>${escapeHtml(item.facilityName)}</td>
      <td>${escapeHtml(item.blockName)}</td>
      <td><strong>${escapeHtml(item.roomNumber)}</strong></td>
      <td>${date(item.checkInDate)}</td>
    </tr>
  `);
}

async function loadManageRooms() {
  try {
    state.manageRooms = await api('/api/yetkili/rooms');
  } catch {
    state.manageRooms = [];
  }
  renderManageRooms();
}

function renderManageRooms() {
  document.getElementById('manageRoomRows').innerHTML = emptyTable(state.manageRooms, item => `
    <tr>
      <td><strong>${escapeHtml(item.roomNumber)}</strong></td>
      <td>${escapeHtml(item.facilityName)}</td>
      <td>${escapeHtml(item.blockName)} / ${item.floorNumber}</td>
      <td>${item.capacity}</td>
      <td>${item.currentOccupancy} / ${item.capacity}</td>
      <td>${money(item.price)}</td>
      <td>${getStatusBadge(item.status)}</td>
      <td><button class="row-btn" onclick="openRoomEdit(${item.id})">Düzenle</button></td>
    </tr>
  `);
}

function openRoomEdit(id) {
  const item = state.manageRooms.find(x => x.id === id);
  if (!item) return;
  const form = roomEditForm(item);
  openModal(form.title, form.html, form.bind);
}

function roomEditForm(item = null) {
  return {
    title: item ? `Oda ${item.roomNumber} Düzenle` : 'Oda Düzenle',
    html: `<form id="roomEditForm" class="form-grid two" data-id="${item?.id || ''}">
      <label>Oda No<input name="roomNumber" value="${escapeAttr(item?.roomNumber)}" required maxlength="10"></label>
      <label>Kapasite<input name="capacity" type="number" min="1" max="50" value="${item?.capacity ?? 1}" required></label>
      <label>Fiyat (TL)<input name="price" type="number" min="0" step="0.01" value="${item?.price ?? 0}" required></label>
      <label>Durum<select name="status">
        ${['Empty', 'PartiallyFull', 'Full', 'Maintenance'].map(s => `<option value="${s}" ${s === item?.status ? 'selected' : ''}>${roomDisplayStatus(s)}</option>`).join('')}
      </select></label>
      <small class="full muted">Not: Kapasite mevcut doluluktan (${item?.currentOccupancy ?? 0} kişi) düşük olamaz. "Bakımda" seçilirse oda yerleştirmeye kapanır.</small>
      <button class="primary-btn full" type="submit">Kaydet</button>
    </form>`,
    bind: () => document.getElementById('roomEditForm').addEventListener('submit', submitRoomEdit)
  };
}

async function submitRoomEdit(event) {
  event.preventDefault();
  const form = event.currentTarget;
  const id = form.dataset.id;
  const data = Object.fromEntries(new FormData(form).entries());
  try {
    await api(`/api/yetkili/rooms/${id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        roomNumber: data.roomNumber,
        capacity: Number(data.capacity),
        price: Number(data.price),
        status: data.status
      })
    });
    closeModalIfOpen();
    toast('Oda güncellendi.');
    await loadManageRooms();
  } catch (error) {
    toast(error.message || 'Oda güncellenemedi.', true);
  }
}

// ============ DUYURULAR ============
async function loadAnnouncements() {
  try {
    state.announcements = await api('/api/announcements');
  } catch {
    state.announcements = [];
  }
  renderAnnouncements();
}

function targetRoleDisplay(role) {
  const map = { All: 'Herkes', Student: 'Öğrenci', Staff: 'Personel' };
  return map[role] || role;
}

function renderAnnouncements() {
  document.getElementById('announcementRows').innerHTML = emptyTable(state.announcements, item => `
    <tr>
      <td><strong>${escapeHtml(item.title)}</strong></td>
      <td class="desc-cell">${escapeHtml(item.content)}</td>
      <td>${escapeHtml(targetRoleDisplay(item.targetRole))}</td>
      <td>${date(item.createdAt)}</td>
      <td>${item.isActive ? '<span class="badge badge-success">Yayında</span>' : '<span class="badge badge-muted">Yayın dışı</span>'}</td>
      <td>
        ${item.isActive ? `<button class="row-btn warn" onclick="unpublishAnnouncement(${item.id})">Yayından Kaldır</button>` : '-'}
      </td>
    </tr>
  `);
}

function announcementForm() {
  return {
    title: 'Yeni Duyuru',
    html: `<form id="announcementForm" class="form-grid">
      <label class="full">Başlık<input name="title" required maxlength="180" placeholder="Duyuru başlığı"></label>
      <label class="full">İçerik<textarea name="content" required maxlength="4000" placeholder="Duyuru içeriği"></textarea></label>
      <label>Hedef<select name="targetRole">
        <option value="All">Herkes</option>
        <option value="Student">Öğrenciler</option>
        <option value="Staff">Personel</option>
      </select></label>
      <label>Durum<select name="isActive"><option value="true" selected>Yayında</option><option value="false">Yayın dışı</option></select></label>
      <button class="primary-btn full" type="submit">Duyuruyu Yayınla</button>
    </form>`,
    bind: () => document.getElementById('announcementForm').addEventListener('submit', submitAnnouncement)
  };
}

async function submitAnnouncement(event) {
  event.preventDefault();
  const data = Object.fromEntries(new FormData(event.currentTarget).entries());
  try {
    await api('/api/announcements', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        title: data.title,
        content: data.content,
        targetRole: data.targetRole,
        isActive: data.isActive === 'true'
      })
    });
    closeModalIfOpen();
    toast('Duyuru yayınlandı. Öğrenciler anında görebilir.');
    await loadAnnouncements();
  } catch (error) {
    toast(error.message || 'Duyuru yayınlanamadı.', true);
  }
}

async function unpublishAnnouncement(id) {
  const item = state.announcements.find(x => x.id === id);
  if (!item || !confirm('Bu duyuruyu yayından kaldırmak istiyor musunuz?')) return;
  try {
    await api(`/api/announcements/${id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        title: item.title,
        content: item.content,
        targetRole: item.targetRole,
        isActive: false
      })
    });
    toast('Duyuru yayından kaldırıldı.');
    await loadAnnouncements();
  } catch (error) {
    toast(error.message, true);
  }
}

function openModal(title, html, bind) {
  document.getElementById('modalTitle').textContent = title;
  document.getElementById('modalBody').innerHTML = html;
  const backdrop = document.getElementById('modalBackdrop');
  backdrop.style.display = 'grid';
  setTimeout(() => backdrop.classList.add('show'), 20);
  bind();
}

function closeModal() {
  const backdrop = document.getElementById('modalBackdrop');
  backdrop.classList.remove('show');
  setTimeout(() => {
    backdrop.style.display = 'none';
    document.getElementById('modalBody').innerHTML = '';
  }, 180);
}

function closeModalIfOpen() {
  const backdrop = document.getElementById('modalBackdrop');
  if (backdrop.style.display !== 'none') closeModal();
}

async function api(path, options = {}) {
  const headers = { ...(options.headers || {}) };
  const token = getStoredToken();
  if (!token) throw new Error('Oturum bulunamadı.');
  headers.Authorization = `Bearer ${token}`;

  const response = await fetch(path, { ...options, headers });
  if (response.status === 401) {
    clearStoredTokens();
    window.location.href = '/index.html';
    throw new Error('Yetkisiz oturum. Lütfen tekrar giriş yapın.');
  }
  if (response.status === 403) {
    throw new Error('Bu işlem için yetkiniz yok.');
  }
  if (!response.ok) {
    const text = await response.text();
    throw new Error(text || response.statusText);
  }
  if (response.status === 204) return null;
  const contentType = response.headers.get('content-type') || '';
  return contentType.includes('application/json') ? response.json() : null;
}

function toast(message, isError = false) {
  const el = document.createElement('div');
  el.className = `toast${isError ? ' error' : ''}`;
  el.textContent = message;
  document.getElementById('toastHost').appendChild(el);
  setTimeout(() => el.remove(), 3600);
}

function getStatusBadge(status) {
  const map = {
    Pending: ['Beklemede', 'badge-warning'],
    Approved: ['Onaylandı', 'badge-success'],
    Rejected: ['Reddedildi', 'badge-danger'],
    Open: ['Açık', 'badge-info'],
    InProgress: ['İşlemde', 'badge-warning'],
    Resolved: ['Çözüldü', 'badge-success'],
    Empty: ['Boş', 'badge-muted'],
    PartiallyFull: ['Kısmen Dolu', 'badge-warning'],
    Full: ['Dolu', 'badge-success'],
    Maintenance: ['Bakımda', 'badge-danger'],
    Aktif: ['Aktif', 'badge-success'],
    Pasif: ['Pasif', 'badge-muted']
  };
  const normalized = String(status || '');
  const [label, tone] = map[normalized] || [normalized, 'badge-muted'];
  return `<span class="badge ${tone}">${escapeHtml(label)}</span>`;
}

function roomDisplayStatus(status) {
  const map = { Empty: 'Boş', PartiallyFull: 'Kısmen Dolu', Full: 'Dolu', Maintenance: 'Bakımda' };
  return map[status] || status;
}

function emptyTable(items, renderer) {
  return (items || []).length ? (items || []).map(renderer).join('') : '<tr><td colspan="8">Kayıt bulunamadı.</td></tr>';
}

function emptyOr(items, renderer) {
  return (items || []).length ? (items || []).map(renderer).join('') : '<p class="muted">Kayıt bulunamadı.</p>';
}

function parseJwt(token) {
  try {
    let payload = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
    while (payload.length % 4) payload += '=';
    return JSON.parse(decodeURIComponent(atob(payload).split('').map(c => `%${(`00${c.charCodeAt(0).toString(16)}`).slice(-2)}`).join('')));
  } catch { return {}; }
}

function clone(value) { return JSON.parse(JSON.stringify(value)); }

function date(value) { return value ? new Date(value).toLocaleDateString('tr-TR') : '-'; }

function money(value) { return Number(value || 0).toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' }); }

function escapeHtml(value) {
  return String(value ?? '').replace(/[&<>"']/g, char => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#039;' }[char]));
}

function escapeAttr(value) { return escapeHtml(value ?? '').replace(/"/g, '&quot;'); }

function debounce(fn, wait) {
  let timer;
  return (...args) => { clearTimeout(timer); timer = setTimeout(() => fn(...args), wait); };
}

function renderPager(id, page, totalPages, onChange) {
  const host = document.getElementById(id);
  host.innerHTML = `
    <button class="ghost-btn" ${page <= 1 ? 'disabled' : ''}>Önceki</button>
    <span class="badge badge-muted">${page} / ${totalPages}</span>
    <button class="ghost-btn" ${page >= totalPages ? 'disabled' : ''}>Sonraki</button>
  `;
  const [prev, next] = host.querySelectorAll('button');
  prev.addEventListener('click', () => onChange(page - 1));
  next.addEventListener('click', () => onChange(page + 1));
}