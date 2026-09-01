const tokenKey = 'token';

const state = {
  stats: null,
  facilities: [],
  rooms: [],
  users: { items: [], page: 1, pageSize: 10, totalCount: 0 },
  applications: [],
  placements: [],
  requests: [],
  staffAssignments: [],
  faultReports: [],
  userFacilityAssignments: [],
  announcements: [],
  roomPage: 1,
  roomPageSize: 10,
  fallbackMode: false
};

const REFRESH_INTERVAL = 15000;
let activeSection = 'dashboard';

const sectionTitles = {
  dashboard: 'Kontrol Paneli',
  facilities: 'Tesisler',
  rooms: 'Oda & Kat Yönetimi',
  applications: 'Başvuru Yönetimi',
  users: 'Kullanıcılar & Roller',
  placements: 'Yerleşim Takibi',
  requests: 'Arıza Talepleri',
  operations: 'Operasyon & Görevlendirme',
  announcements: 'Duyuru Yönetimi'
};

const mock = createMockStore();

document.addEventListener('DOMContentLoaded', () => {
  const token = getStoredToken();
  const claims = token ? parseJwt(token) : null;
  const valid = isValidAdminToken(token);
  if (!valid) {
    const info = { token: token ? token.substring(0, 40) + '...' : null, claims };
    console.error('[admin] token gecersiz, login sayfasina yonlendiriliyor', info);
    try { localStorage.setItem('lastAdminTokenError', JSON.stringify(info)); } catch {}
    // Kisa sure bekle ki log kopyalanabilsin, sonra yonlendir
    setTimeout(() => {
      clearStoredTokens();
      window.location.href = '/index.html';
    }, 3000);
    document.body.innerHTML = '<div style="padding:2rem;font-family:sans-serif;max-width:800px;margin:auto"><h2 style="color:#b91c1c">Yetki dogrulanamadi — 3 saniye icinde giris sayfasina donuluyor</h2><p>Asagidaki bilgi gelistiriciye iletilmeli:</p><pre style="background:#fef2f2;border:1px solid #fecaca;padding:1rem;overflow:auto;white-space:pre-wrap;word-break:break-all">' + escapeHtml(JSON.stringify(info, null, 2)) + '</pre><p><a href="/index.html">Hemen don</a> — F12 Console da ayni bilgi var (Preserve log acik tutun).</p></div>';
    return;
  }

  try {
    bindShell();
    openApp(token);
  } catch (e) {
    console.error('[admin] sayfa yuklenirken hata', e);
    document.body.innerHTML = '<div style="padding:2rem;font-family:sans-serif"><h2>Panel yuklenemedi</h2><p style="color:#b91c1c">' + String(e.message || e) + '</p><p>Konsolu (F12) kontrol edin ve sayfayi Ctrl+F5 ile yenileyin. Sorun surerse tokeni silip tekrar giris yapin.</p><a href="/index.html">Giris sayfasina don</a></div>';
  }
});

function getStoredToken() {
  return localStorage.getItem(tokenKey) || localStorage.getItem('admin_token');
}

function isValidAdminToken(token) {
  if (!token) return false;

  let claims;
  try { claims = parseJwt(token); } catch { return false; }
  const rawRole = claims['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] ?? claims.role ?? '';
  const roles = Array.isArray(rawRole) ? rawRole : [rawRole];
  const lowerRoles = roles.map(r => String(r).toLowerCase());
  const hasAllowedRole = lowerRoles.includes('admin') || lowerRoles.includes('yetkili');
  const hasSub = Boolean(claims.sub || claims['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier']);
  const notExpired = !claims.exp || claims.exp * 1000 > Date.now();
  return hasSub && notExpired && hasAllowedRole;
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

  document.getElementById('facilitySearch').addEventListener('input', renderFacilities);
  document.getElementById('roomSearch').addEventListener('input', () => { state.roomPage = 1; renderRooms(); });
  document.getElementById('roomFacilityFilter').addEventListener('change', () => { state.roomPage = 1; renderRooms(); });
  document.getElementById('roomStatusFilter').addEventListener('change', () => { state.roomPage = 1; renderRooms(); });
  document.getElementById('applicationStatusFilter').addEventListener('change', loadApplications);
  document.getElementById('userSearch').addEventListener('input', debounce(() => loadUsers(1), 350));
  document.getElementById('roleFilter').addEventListener('change', () => loadUsers(1));
  document.getElementById('activePlacementsOnly').addEventListener('change', loadPlacements);
  document.getElementById('requestOpenOnlyFilter').addEventListener('change', loadRequests);

  setInterval(() => {
    if (document.hidden) return;
    if (document.getElementById('modalBackdrop').style.display !== 'none') return;
    refreshActiveSection();
  }, REFRESH_INTERVAL);
}

let isYetkili = false;

async function openApp(token) {
  const claims = parseJwt(token);
  const role = claims['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] || claims.role || 'Admin';
  isYetkili = String(role).toLowerCase() === 'yetkili';
  document.getElementById('adminName').textContent = claims.fullName || claims.name || 'Sistem Yöneticisi';
  document.getElementById('adminRole').textContent = role;
  if (isYetkili) applyYetkiliRestrictions();
  switchSection(isYetkili && activeSection === 'facilities' ? 'dashboard' : 'dashboard');
  await Promise.allSettled([loadDashboard(), loadFacilities(), loadRooms(), loadApplications(), loadUsers(1), loadPlacements(), loadRequests(), loadOperations(), loadAnnouncements()]);
}

function applyYetkiliRestrictions() {
  document.querySelector('[data-section="facilities"]')?.classList.add('hidden-by-role');
  document.getElementById('facilities')?.classList.add('hidden-by-role');
  document.querySelector('[data-modal="userFacilityAssignmentModal"]')?.classList.add('hidden-by-role');
  document.getElementById('facilityAssignmentPanel')?.classList.add('hidden-by-role');
  document.querySelector('[data-modal="floorModal"]')?.classList.add('hidden-by-role');
  document.querySelector('[data-modal="roomModal"]')?.classList.add('hidden-by-role');
}

function logout() {
  clearStoredTokens();
  window.location.href = '/index.html';
}

function switchSection(id) {
  if (isYetkili && id === 'facilities') id = 'dashboard';
  activeSection = id;
  document.querySelectorAll('.page-section').forEach(section => section.classList.toggle('active', section.id === id));
  document.querySelectorAll('.nav-item[data-section]').forEach(button => button.classList.toggle('active', button.dataset.section === id));
  document.getElementById('sectionTitle').textContent = sectionTitles[id] || 'Yönetim Paneli';
}

function refreshActiveSection() {
  const refreshers = {
    dashboard: loadDashboard,
    facilities: loadFacilities,
    rooms: loadRooms,
    applications: loadApplications,
    users: () => loadUsers(state.users.page || 1),
    placements: loadPlacements,
    requests: loadRequests,
    operations: loadOperations,
    announcements: loadAnnouncements
  };
  refreshers[activeSection]?.();
}

async function loadDashboard() {
  try {
    state.stats = await api('/api/admin/dashboard-stats');
  } catch {
    state.fallbackMode = true;
    state.stats = getMockStats();
    toast('Sunucu yanıt vermediği için demo veri yüklendi.', true);
  }
  renderDashboard();
}

function renderDashboard() {
  const s = state.stats || getMockStats();
  const cards = [
    ['Doluluk Oranı', `${s.occupancyRate ?? 0}%`, '📊'],
    ['Bekleyen Başvuru', s.pendingApplicationCount ?? 0, '📝'],
    ['Açık Arıza', s.openRequestCount ?? 0, '🔧'],
    ['Ödenmemiş Borç', money(s.totalUnpaidAndOverdueDebt ?? 0), '💰']
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

  document.getElementById('recentApplications').innerHTML = emptyOr(s.recentApplications || [], item => `
    <div class="activity-item">
      <div>
        <strong>${escapeHtml(item.fullName)}</strong>
        <small>${escapeHtml(item.accommodationType)} - ${getStatusBadge(item.status)}</small>
      </div>
      <small>${date(item.createdAt)}</small>
    </div>
  `);

  document.getElementById('recentRequests').innerHTML = emptyOr(s.recentRequests || [], item => `
    <div class="activity-item">
      <div>
        <strong>${escapeHtml(item.category)} / Oda ${escapeHtml(item.roomNumber)}</strong>
        <small>${escapeHtml(item.fullName)} - ${getStatusBadge(item.status)}</small>
      </div>
      <small>${date(item.createdAt)}</small>
    </div>
  `);
}

async function loadFacilities() {
  try {
    state.facilities = state.fallbackMode ? clone(mock.facilities) : await api('/api/admin/facilities');
  } catch {
    state.fallbackMode = true;
    state.facilities = clone(mock.facilities);
  }
  renderFacilities();
}

function renderFacilities() {
  const term = document.getElementById('facilitySearch').value.toLowerCase();
  const rows = state.facilities.filter(x => {
    const name = String(x.name || '').toLowerCase();
    const campus = String(x.campusLocation || '').toLowerCase();
    const type = String(x.type || '').toLowerCase();
    return !term || name.includes(term) || campus.includes(term) || type.includes(term);
  });

  document.getElementById('facilityRows').innerHTML = emptyTable(rows, item => `
    <tr>
      <td><strong>${escapeHtml(item.name)}</strong></td>
      <td>${escapeHtml(item.type)}</td>
      <td>${escapeHtml(item.campusLocation)}</td>
      <td>${item.totalCapacity}</td>
      <td>${item.buildingCount ?? 0}</td>
      <td>${getStatusBadge(item.isActive ? 'Aktif' : 'Pasif')}</td>
      <td>
        <button class="row-btn" onclick="editFacility('${item.type}', ${item.id})">Düzenle</button>
        <button class="row-btn warn" onclick="toggleFacility('${item.type}', ${item.id}, ${!item.isActive})">${item.isActive ? 'Pasifleştir' : 'Aktifleştir'}</button>
        <button class="row-btn danger" onclick="deleteFacility('${item.type}', ${item.id})">Sil</button>
      </td>
    </tr>
  `);

  // Oda sekmesindeki tesis filtresini guncelle
  const facilityFilter = document.getElementById('roomFacilityFilter');
  if (facilityFilter) {
    const currentVal = facilityFilter.value;
    const facilityNames = [...new Set(state.facilities.map(x => x.name))].sort((a, b) => a.localeCompare(b, 'tr'));
    facilityFilter.innerHTML = '<option value="">Tüm tesisler</option>' + facilityNames.map(name => `<option value="${escapeAttr(name)}" ${name === currentVal ? 'selected' : ''}>${escapeHtml(name)}</option>`).join('');
  }
}

async function loadRooms() {
  try {
    state.rooms = state.fallbackMode ? clone(mock.rooms) : await api('/api/admin/rooms-detail');
  } catch {
    state.fallbackMode = true;
    state.rooms = clone(mock.rooms);
  }
  renderRooms();
}

function renderRooms() {
  const term = document.getElementById('roomSearch').value.toLowerCase();
  const status = document.getElementById('roomStatusFilter').value;
  const facility = document.getElementById('roomFacilityFilter').value;
  const filtered = state.rooms.filter(x => {
    const roomText = String(x.roomNumber || '').toLowerCase();
    const facilityText = String(x.facilityName || '').toLowerCase();
    const blockText = String(x.blockName || '').toLowerCase();
    return (!term || roomText.includes(term) || facilityText.includes(term) || blockText.includes(term)) && (!status || x.status === status) && (!facility || x.facilityName === facility);
  });

  const totalPages = Math.max(1, Math.ceil(filtered.length / state.roomPageSize));
  state.roomPage = Math.min(state.roomPage, totalPages);
  const start = (state.roomPage - 1) * state.roomPageSize;
  const pageItems = filtered.slice(start, start + state.roomPageSize);

  document.getElementById('roomRows').innerHTML = emptyTable(pageItems, item => `
    <tr>
      <td><strong>${escapeHtml(item.roomNumber)}</strong></td>
      <td>${escapeHtml(item.facilityName)}</td>
      <td>${escapeHtml(item.blockName)} / ${item.floorNumber}</td>
      <td>${item.capacity}</td>
      <td>${item.currentOccupancy} / ${item.capacity}</td>
      <td>${money(item.price)}</td>
      <td>${getStatusBadge(item.status)}</td>
      <td>
        <button class="row-btn" onclick="editRoom(${item.id})">Düzenle</button>
        <button class="row-btn" onclick="showOccupants(${item.id})">Detay</button>
        ${isYetkili ? '' : `<button class="row-btn danger" onclick="deleteRoom(${item.id})">Sil</button>`}
      </td>
    </tr>
  `);

  renderPager('roomPager', state.roomPage, totalPages, next => { state.roomPage = next; renderRooms(); });
}

async function loadUsers(page = state.users.page) {
  try {
    const search = encodeURIComponent(document.getElementById('userSearch').value);
    const role = encodeURIComponent(document.getElementById('roleFilter').value);
    state.users = state.fallbackMode
      ? getMockUsers(page)
      : await api(`/api/admin/users?page=${page}&pageSize=10&search=${search}&role=${role}`);
  } catch {
    state.fallbackMode = true;
    state.users = getMockUsers(page);
  }
  renderUsers();
}

function renderUsers() {
  document.getElementById('userRows').innerHTML = emptyTable(state.users.items || [], item => `
    <tr>
      <td><strong>${escapeHtml(item.fullName)}</strong></td>
      <td>${escapeHtml(item.email)}</td>
      <td>${escapeHtml(item.tcNo)}</td>
      <td>${escapeHtml(item.studentStaffNo || '-')}</td>
      <td>
        ${isYetkili
          ? `<span>${escapeHtml(item.role)}</span>`
          : `<select onchange="changeUserRole('${item.id}', this.value)">
          ${['Ogrenci', 'TeknikPersonel', 'TemizlikPersoneli', 'Personel', 'Yetkili', 'Admin'].map(role => `<option value="${role}" ${role === item.role ? 'selected' : ''}>${role}</option>`).join('')}
        </select>`}
      </td>
      <td>${getStatusBadge(item.isActive ? 'Aktif' : 'Pasif')}</td>
      <td>${isYetkili ? '<span class="muted">-</span>' : `<button class="row-btn warn" onclick="setUserStatus('${item.id}', ${!item.isActive})">${item.isActive ? 'Dondur' : 'Aktif Et'}</button>`}</td>
    </tr>
  `);

  const totalPages = Math.max(1, Math.ceil((state.users.totalCount || 0) / (state.users.pageSize || 10)));
  renderPager('userPager', state.users.page || 1, totalPages, loadUsers);
}

async function loadPlacements() {
  try {
    const activeOnly = document.getElementById('activePlacementsOnly').checked;
    state.placements = state.fallbackMode
      ? clone(mock.placements).filter(x => !activeOnly || x.isActive)
      : await api(`/api/admin/placements?activeOnly=${activeOnly}`);
  } catch {
    state.fallbackMode = true;
    const activeOnly = document.getElementById('activePlacementsOnly').checked;
    state.placements = clone(mock.placements).filter(x => !activeOnly || x.isActive);
  }
  renderPlacements();
}

function renderPlacements() {
  document.getElementById('placementRows').innerHTML = emptyTable(state.placements || [], item => `
    <tr>
      <td><strong>${escapeHtml(item.fullName)}</strong></td>
      <td>${escapeHtml(item.facilityName || '-')}</td>
      <td>${escapeHtml(item.blockName || '-')}</td>
      <td><strong>${escapeHtml(item.roomNumber)}</strong></td>
      <td>${date(item.checkInDate)}</td>
      <td>${item.checkOutDate ? date(item.checkOutDate) : '-'}</td>
      <td>${getStatusBadge(item.isActive ? 'Aktif' : 'Çıkış Yaptı')}</td>
      <td>${item.isActive ? `<button class="row-btn warn" onclick="checkout(${item.id})">Çıkış Yap</button>` : '-'}</td>
    </tr>
  `);
}

async function loadApplications() {
  try {
    const status = document.getElementById('applicationStatusFilter').value;
    const statusParam = status ? `?status=${status}` : '';
    state.applications = state.fallbackMode
      ? clone(mock.recentApplications).filter(x => !status || x.status === status)
      : await api(`/api/admin/applications${statusParam}`);
  } catch {
    state.fallbackMode = true;
    const status = document.getElementById('applicationStatusFilter').value;
    state.applications = clone(mock.recentApplications).filter(x => !status || x.status === status);
  }
  renderApplications();
}

function parseApplicationDoc(docUrl){
  if(!docUrl) return { fileUrl: null, note: null };
  const m = String(docUrl).match(/^(.*)\s*\[Not:\s*(.*)\]$/);
  if(m) return { fileUrl: m[1].trim() || null, note: m[2].replace(/\]$/, '').trim() || null };
  return { fileUrl: docUrl, note: null };
}
function openApplicationDetail(id){
  const item = (state.applications || []).find(x => x.id === id);
  if(!item) return;
  const doc = parseApplicationDoc(item.documentUrl);
  openModal(`Başvuru Detayı — ${escapeHtml(item.fullName)}`, `
    <div class="form-grid" style="gap:0.8rem;">
      <div class="list-row"><span class="info-label">Ad Soyad</span><span class="info-value"><strong>${escapeHtml(item.fullName)}</strong></span></div>
      <div class="list-row"><span class="info-label">TC</span><span class="info-value">${escapeHtml(item.tcNo)}</span></div>
      <div class="list-row"><span class="info-label">No</span><span class="info-value">${escapeHtml(item.studentStaffNo || '-')}</span></div>
      <div class="list-row"><span class="info-label">Tür</span><span class="info-value">${escapeHtml(item.accommodationType)}</span></div>
      <div class="list-row"><span class="info-label">Durum</span><span class="info-value">${getStatusBadge(item.status)}</span></div>
      <div class="list-row"><span class="info-label">Tarih</span><span class="info-value">${date(item.createdAt)}</span></div>
      ${doc.note ? `<div class="list-row"><span class="info-label">Ek Metin</span><span class="info-value">${escapeHtml(doc.note)}</span></div>` : ''}
      <div class="list-row"><span class="info-label">Yüklenen Dosya</span><span class="info-value">${doc.fileUrl ? `<a href="${escapeAttr(doc.fileUrl)}" target="_blank" rel="noopener" class="row-btn">Dosyayı Görüntüle / İndir</a> <small class="muted" style="margin-left:0.5rem; word-break:break-all;">${escapeHtml(doc.fileUrl)}</small>` : '<span class="muted">Dosya eklenmemiş</span>'}</span></div>
    </div>
  `, () => {});
}
function renderApplications() {
  document.getElementById('applicationRows').innerHTML = emptyTable(state.applications || [], item => `
    <tr style="cursor:pointer;" onclick="openApplicationDetail(${item.id})">
      <td><strong>${escapeHtml(item.fullName)}</strong></td>
      <td>${escapeHtml(item.tcNo)}</td>
      <td>${escapeHtml(item.studentStaffNo || '-')}</td>
      <td>${escapeHtml(item.accommodationType)}</td>
      <td>${getStatusBadge(item.status)}</td>
      <td>${date(item.createdAt)}</td>
      <td onclick="event.stopPropagation();">
        ${item.status === 'Pending' ? `<button class="row-btn" onclick="openAssignModal(${item.id}, '${item.userId}', '${escapeAttr(item.fullName)}', '${item.accommodationType}')">Odaya Yerleştir</button>` : '-'}
      </td>
    </tr>
  `);
}

function openNamedModal(name) {
  const forms = {
    facilityModal: facilityForm(),
    buildingModal: buildingForm(),
    floorModal: floorForm(),
    roomModal: roomForm(),
    assignModal: assignForm(),
    staffAssignmentModal: staffAssignmentForm(),
    userFacilityAssignmentModal: userFacilityAssignmentForm(),
    announcementModal: announcementFormAdmin()
  };
  if (!forms[name]) return;
  openModal(forms[name].title, forms[name].html, forms[name].bind);
}

function loadOperations() {
  return Promise.allSettled([loadStaffAssignments(), loadFaultReports(), loadUserFacilityAssignments()]);
}

async function loadRequests() {
  try {
    const openOnly = document.getElementById('requestOpenOnlyFilter').value === 'open';
    state.requests = state.fallbackMode ? [] : await api(`/api/admin/requests?openOnly=${openOnly}`);
  } catch {
    state.requests = [];
  }
  renderRequests();
}

function renderRequests() {
  const rows = state.requests || [];
  if (!rows.length) {
    document.getElementById('requestRows').innerHTML = '<tr><td colspan="9">Kayıt bulunamadı.</td></tr>';
    return;
  }
  document.getElementById('requestRows').innerHTML = rows.map(item => `
    <tr>
      <td><strong>${escapeHtml(item.fullName)}</strong></td>
      <td>${escapeHtml(item.facilityName || '-')}</td>
      <td>${escapeHtml(item.blockName || '-')} / Kat ${item.floorNumber ?? '-'}</td>
      <td><strong>${escapeHtml(item.roomNumber)}</strong></td>
      <td>${escapeHtml(item.category)}</td>
      <td class="desc-cell">${escapeHtml(item.description)}</td>
      <td>${date(item.createdAt)}</td>
      <td>${getStatusBadge(item.status)}</td>
      <td>
        <select onchange="updateRequestStatus(${item.id}, this.value)">
          ${['Open', 'InProgress', 'Resolved', 'Rejected'].map(s => `<option value="${s}" ${s === item.status ? 'selected' : ''}>${requestStatusDisplay(s)}</option>`).join('')}
        </select>
      </td>
    </tr>
  `).join('');
}

async function updateRequestStatus(id, status) {
  await saveAndRefresh(`/api/admin/requests/${id}/status`, 'PATCH', { status }, loadOperations);
}

async function loadStaffAssignments() {
  try {
    state.staffAssignments = state.fallbackMode ? [] : await api('/api/admin/staff-assignments');
  } catch {
    state.staffAssignments = [];
  }
  renderStaffAssignments();
}

function renderStaffAssignments() {
  document.getElementById('assignmentRows').innerHTML = emptyTable(state.staffAssignments, item => `
    <tr>
      <td><strong>${escapeHtml(item.title)}</strong><br><small>${escapeHtml(item.details || '-')}</small></td>
      <td>${staffRoleDisplay(item.assignedRole)}${item.isMaintenanceRequest ? '<br><small>⚒ Arıza iş emri</small>' : ''}</td>
      <td>${escapeHtml(item.location)}</td>
      <td>${escapeHtml(item.priority)}</td>
      <td>${item.dueDate ? date(item.dueDate) : '-'}</td>
      <td>${item.isCompleted ? getStatusBadge('Completed') : `<span class="badge badge-warning">Bekliyor</span>`}</td>
    </tr>
  `);
}

function staffAssignmentForm() {
  const openRequests = (state.requests || []).filter(r => r.status === 'Open' || r.status === 'InProgress');
  return {
    title: 'Personele Görev Ata',
    html: `<form id="staffAssignmentForm" class="form-grid two">
      <label class="full">Arıza Talebi (isteğe bağlı — açık taleplerden seçerek görev oluştur)
        <select name="requestId" id="requestSelect">
          <option value="">Manuel görev (arıza seçilmedi)</option>
          ${openRequests.map(r => `<option value="${r.id}">${escapeHtml(r.category)} — ${escapeHtml(r.facilityName)} / ${escapeHtml(r.blockName)} / Oda ${escapeHtml(r.roomNumber)} — ${escapeHtml(r.fullName)}: ${escapeHtml((r.description || '').substring(0, 50))}</option>`).join('')}
        </select>
      </label>
      <label>Görevli Rolü<select name="assignedRole">
        <option value="TeknikPersonel">Teknik Personel</option>
        <option value="TemizlikPersoneli">Temizlik Personeli</option>
      </select></label>
      <label>Öncelik<select name="priority">
        ${['Acil', 'Yüksek', 'Normal', 'Düşük'].map(p => `<option ${p === 'Normal' ? 'selected' : ''}>${p}</option>`).join('')}
      </select></label>
      <label>Başlık<input name="title" required placeholder="Örn. Wi-Fi ağ düzenlemesi"></label>
      <label>Termin<input name="dueDate" type="date"></label>
      <label class="full">Konum<input name="location" required placeholder="Örn. B Blok / 1. kat"></label>
      <label class="full">Detay<textarea name="details"></textarea></label>
      <label class="inline-check full"><input type="checkbox" name="isMaintenanceRequest" value="true"> Arıza / onarım işidir (teknik personelin arıza talepleri ekranında da listelenir)</label>
      <button class="primary-btn full" type="submit">Görevi Kaydet</button>
    </form>`,
    bind: () => {
      const form = document.getElementById('staffAssignmentForm');
      const requestSelect = form.querySelector('#requestSelect');
      const titleInput = form.querySelector('[name="title"]');
      const locationInput = form.querySelector('[name="location"]');
      const detailsInput = form.querySelector('[name="details"]');
      const isMaintenanceCheck = form.querySelector('[name="isMaintenanceRequest"]');
      requestSelect.addEventListener('change', () => {
        const req = state.requests.find(r => String(r.id) === requestSelect.value);
        if (req) {
          titleInput.value = `${req.category} — Oda ${req.roomNumber} (${req.facilityName} / ${req.blockName})`;
          locationInput.value = `${req.facilityName} / ${req.blockName} / Kat ${req.floorNumber} / Oda ${req.roomNumber}`;
          detailsInput.value = req.description;
          isMaintenanceCheck.checked = true;
        }
      });
      form.addEventListener('submit', submitStaffAssignment);
    }
  };
}

async function submitStaffAssignment(event) {
  event.preventDefault();
  const data = Object.fromEntries(new FormData(event.currentTarget).entries());
  await saveAndRefresh('/api/admin/staff-assignments', 'POST', {
    assignedRole: data.assignedRole,
    title: data.title,
    location: data.location,
    details: data.details || null,
    priority: data.priority,
    isMaintenanceRequest: data.isMaintenanceRequest === 'true',
    dueDate: data.dueDate ? `${data.dueDate}T00:00:00Z` : null
  }, loadStaffAssignments);
}

async function loadFaultReports() {
  try {
    state.faultReports = state.fallbackMode ? [] : await api('/api/admin/fault-reports');
  } catch {
    state.faultReports = [];
  }
  renderFaultReports();
}

function renderFaultReports() {
  document.getElementById('faultReportList').innerHTML = emptyOr(state.faultReports, item => `
    <div class="activity-item">
      <div>
        <strong>${escapeHtml(item.category)} / ${escapeHtml(item.location)}</strong>
        <small>${escapeHtml(item.description)}</small>
      </div>
      <small>${date(item.createdAt)}</small>
    </div>
  `);
}

async function loadUserFacilityAssignments() {
  if (isYetkili) {
    state.userFacilityAssignments = [];
    renderUserFacilityAssignments();
    return;
  }
  try {
    state.userFacilityAssignments = state.fallbackMode ? [] : await api('/api/admin/user-facility-assignments');
  } catch {
    state.userFacilityAssignments = [];
  }
  renderUserFacilityAssignments();
}

function renderUserFacilityAssignments() {
  document.getElementById('facilityAssignmentRows').innerHTML = emptyTable(state.userFacilityAssignments, item => `
    <tr>
      <td><strong>${escapeHtml(item.userFullName)}</strong><br><small>${escapeHtml(item.userRole)}</small></td>
      <td>${item.dormitoryName ? `<strong>${escapeHtml(item.dormitoryName)}</strong>` : `<strong>${escapeHtml(item.housingUnitName)}</strong>`}</td>
      <td>${escapeHtml(item.assignedByName)}</td>
      <td>${date(item.assignedAt)}</td>
      <td>${item.unassignedAt ? date(item.unassignedAt) : '-'}</td>
      <td>${item.isActive ? '<span class="badge badge-success">Aktif</span>' : '<span class="badge badge-muted">Pasif</span>'}</td>
      <td>
        <button class="row-btn" onclick="editUserFacilityAssignment(${item.id})">Düzenle</button>
        <button class="row-btn danger" onclick="deleteUserFacilityAssignment(${item.id})">Sil</button>
      </td>
    </tr>
  `);
}

const ASSIGNABLE_ROLES = ['Yetkili', 'Personel', 'TeknikPersonel', 'TemizlikPersoneli'];

async function fetchUsersByRole(role) {
  try {
    return await api(`/api/admin/users-by-role/${role}`);
  } catch {
    return [];
  }
}

function userFacilityAssignmentForm() {
  const dormitories = state.facilities.filter(x => x.type === 'Yurt');
  const housingUnits = state.facilities.filter(x => x.type === 'Lojman');

  return {
    title: 'Yetkili / Personel Ata',
    html: `<form id="userFacilityAssignmentForm" class="form-grid" data-assignment-id="">
      <label class="full">Hesap<select name="accountMode" id="accountMode">
        <option value="existing">Mevcut kullanıcı seç</option>
        <option value="new">Yeni hesap oluştur ve ata</option>
      </select></label>
      <div id="existingAccountFields" class="form-grid two">
        <label>Rol<select name="assignRole" id="assignRoleSelect">
          ${ASSIGNABLE_ROLES.map(r => `<option value="${r}">${staffRoleDisplay(r)}</option>`).join('')}
        </select></label>
        <label class="full">Kullanıcı<select name="userId" id="assignUserSelect"><option value="">Yükleniyor...</option></select></label>
      </div>
      <div id="newAccountFields" class="form-grid two" style="display:none;">
        <label>Rol<select name="newRole">
          ${ASSIGNABLE_ROLES.map(r => `<option value="${r}">${staffRoleDisplay(r)}</option>`).join('')}
        </select></label>
        <label>Ad Soyad<input name="fullName" placeholder="Ad Soyad"></label>
        <label>E-posta<input name="email" type="email" placeholder="ornek@ozal.edu.tr"></label>
        <label>TC Kimlik No<input name="tcNo" minlength="11" maxlength="11" placeholder="11 haneli TC"></label>
        <label>Telefon<input name="phoneNumber" placeholder="+90... (isteğe bağlı)"></label>
        <label class="full">Şifre<input name="password" type="password" minlength="6" placeholder="En az 6 karakter"></label>
      </div>
      <label>Yurt<select name="dormitoryId">
        <option value="">Yurt seçilmedi</option>
        ${dormitories.map(x => `<option value="${x.id}">${escapeHtml(x.name)}</option>`).join('')}
      </select></label>
      <label>Lojman<select name="housingUnitId">
        <option value="">Lojman seçilmedi</option>
        ${housingUnits.map(x => `<option value="${x.id}">${escapeHtml(x.name)}</option>`).join('')}
      </select></label>
      <button class="primary-btn full" type="submit">Atamayı Kaydet</button>
    </form>`,
    bind: () => {
      const form = document.getElementById('userFacilityAssignmentForm');
      const modeSel = form.querySelector('#accountMode');
      const roleSel = form.querySelector('#assignRoleSelect');
      const userSel = form.querySelector('#assignUserSelect');
      const existingFields = form.querySelector('#existingAccountFields');
      const newFields = form.querySelector('#newAccountFields');

      const toggleMode = () => {
        const isNew = modeSel.value === 'new';
        existingFields.style.display = isNew ? 'none' : '';
        newFields.style.display = isNew ? '' : 'none';
        newFields.querySelectorAll('input').forEach(input => { input.required = isNew && input.name !== 'phoneNumber'; });
        userSel.required = !isNew;
      };
      modeSel.addEventListener('change', toggleMode);

      const loadUsers = async () => {
        userSel.innerHTML = '<option value="">Yükleniyor...</option>';
        const users = await fetchUsersByRole(roleSel.value);
        userSel.innerHTML = users.length
          ? users.map(u => `<option value="${u.id}">${escapeHtml(u.fullName)} - ${escapeHtml(u.email)}</option>`).join('')
          : '<option value="">Bu rolde kayıtlı kullanıcı yok</option>';
      };
      roleSel.addEventListener('change', loadUsers);

      form._loadUsers = loadUsers;
      form._toggleMode = toggleMode;

      loadUsers();
      toggleMode();
      form.addEventListener('submit', submitUserFacilityAssignment);
    }
  };
}

async function submitUserFacilityAssignment(event) {
  event.preventDefault();
  const form = event.currentTarget;
  const data = Object.fromEntries(new FormData(form).entries());
  const assignmentId = form.dataset.assignmentId;
  const isNewAccount = data.accountMode === 'new';
  const dormitoryId = data.dormitoryId ? Number(data.dormitoryId) : null;
  const housingUnitId = data.housingUnitId ? Number(data.housingUnitId) : null;

  if (!dormitoryId && !housingUnitId) {
    toast('Yurt veya lojmandan biri seçilmelidir.', true);
    return;
  }

  try {
    let userId;
    if (assignmentId) {
      await api(`/api/admin/user-facility-assignments/${assignmentId}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ dormitoryId, housingUnitId, isActive: true })
      });
      toast('Atama güncellendi.');
    } else {
      if (isNewAccount) {
        const created = await api('/api/admin/users', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            fullName: data.fullName,
            email: data.email,
            password: data.password,
            tcNo: data.tcNo,
            phoneNumber: data.phoneNumber || null,
            role: data.newRole
          })
        });
        userId = created.id;
        toast(`${created.fullName} hesabı sisteme kaydedildi.`);
      } else {
        userId = data.userId;
        if (!userId) {
          toast('Kullanıcı seçin.', true);
          return;
        }
      }
      await api('/api/admin/user-facility-assignments', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ userId, dormitoryId, housingUnitId })
      });
      toast('Atama kaydedildi.');
    }
    closeModalIfOpen();
    await loadUserFacilityAssignments();
  } catch (error) {
    toast(error.message || 'Atama kaydedilemedi.', true);
  }
}

async function editUserFacilityAssignment(id) {
  const item = state.userFacilityAssignments.find(x => x.id === id);
  if (!item) return;
  const form = userFacilityAssignmentForm();
  form.html = form.html.replace('<form id="userFacilityAssignmentForm"', `<form id="userFacilityAssignmentForm" data-assignment-id="${id}"`);
  form.html = form.html.replace('<button class="primary-btn full" type="submit">Atamayı Kaydet</button>', '<button class="primary-btn full" type="submit">Atamayı Güncelle</button>');
  openModal(form.title, form.html, () => {
    form.bind();
    const formEl = document.getElementById('userFacilityAssignmentForm');
    const modeSel = formEl.querySelector('#accountMode');
    const roleSel = formEl.querySelector('#assignRoleSelect');
    const userSel = formEl.querySelector('#assignUserSelect');
    const newFields = formEl.querySelector('#newAccountFields');
    modeSel.value = 'existing';
    modeSel.disabled = true;
    newFields.style.display = 'none';
    formEl._toggleMode();
    (async () => {
      if (ASSIGNABLE_ROLES.includes(item.userRole)) {
        roleSel.value = item.userRole;
        await formEl._loadUsers();
      }
      userSel.value = item.userId;
      userSel.disabled = true;
      roleSel.disabled = true;
      if (item.dormitoryId) formEl.querySelector('[name="dormitoryId"]').value = String(item.dormitoryId);
      if (item.housingUnitId) formEl.querySelector('[name="housingUnitId"]').value = String(item.housingUnitId);
    })();
  });
}

async function deleteUserFacilityAssignment(id) {
  if (!confirm('Bu atamayı silmek istiyor musunuz?')) return;
  await saveAndRefresh(`/api/admin/user-facility-assignments/${id}`, 'DELETE', null, loadUserFacilityAssignments);
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

function announcementTargetDisplay(role) {
  const map = { All: 'Herkes', Student: 'Öğrenci', Staff: 'Personel' };
  return map[role] || role;
}

function renderAnnouncements() {
  document.getElementById('announcementRowsAdmin').innerHTML = emptyTable(state.announcements, item => `
    <tr>
      <td><strong>${escapeHtml(item.title)}</strong></td>
      <td class="desc-cell">${escapeHtml(item.content)}</td>
      <td>${escapeHtml(announcementTargetDisplay(item.targetRole))}</td>
      <td>${date(item.createdAt)}</td>
      <td>${item.isActive ? '<span class="badge badge-success">Yayında</span>' : '<span class="badge badge-muted">Yayın dışı</span>'}</td>
      <td>
        ${item.isActive ? `<button class="row-btn warn" onclick="unpublishAnnouncement(${item.id})">Yayından Kaldır</button>` : '-'}
      </td>
    </tr>
  `);
}

function announcementFormAdmin() {
  return {
    title: 'Yeni Duyuru',
    html: `<form id="announcementFormAdmin" class="form-grid">
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
    bind: () => document.getElementById('announcementFormAdmin').addEventListener('submit', submitAnnouncementAdmin)
  };
}

async function submitAnnouncementAdmin(event) {
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
    toast('Duyuru yayınlandı.');
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
      body: JSON.stringify({ title: item.title, content: item.content, targetRole: item.targetRole, isActive: false })
    });
    toast('Duyuru yayından kaldırıldı.');
    await loadAnnouncements();
  } catch (error) {
    toast(error.message, true);
  }
}

function facilityForm(item = null, type = 'Yurt') {
  return {
    title: item ? 'Tesis Düzenle' : 'Yeni Tesis',
    html: `<form id="facilityForm" class="form-grid two">
      <label>Tür<select name="type" ${item ? 'disabled' : ''}>
        <option ${type === 'Yurt' ? 'selected' : ''}>Yurt</option>
        <option ${type === 'Lojman' ? 'selected' : ''}>Lojman</option>
      </select></label>
      <label>Durum<select name="isActive"><option value="true" ${item?.isActive !== false ? 'selected' : ''}>Aktif</option><option value="false" ${item?.isActive === false ? 'selected' : ''}>Pasif</option></select></label>
      <label class="full">Ad<input name="name" value="${escapeAttr(item?.name)}" required></label>
      <label class="full">Kampus<input name="campusLocation" value="${escapeAttr(item?.campusLocation)}" required></label>
      <label>Kapasite<input name="totalCapacity" type="number" min="0" value="${item?.totalCapacity ?? 0}"></label>
      <button class="primary-btn full" type="submit">Kaydet</button>
    </form>`,
    bind: () => bindFacilitySubmit(item)
  };
}

function buildingForm() {
  const dorms = state.facilities.filter(x => x.type === 'Yurt');
  const units = state.facilities.filter(x => x.type === 'Lojman');
  return {
    title: 'Blok / Bina Ekle',
    html: `<form id="buildingForm" class="form-grid">
      <label>Bağlı Tesis<select name="facility">
        ${dorms.map(x => `<option value="d-${x.id}">Yurt - ${escapeHtml(x.name)}</option>`).join('')}
        ${units.map(x => `<option value="h-${x.id}">Lojman - ${escapeHtml(x.name)}</option>`).join('')}
      </select></label>
      <label>Blok Adı<input name="blockName" required placeholder="A Blok"></label>
      <button class="primary-btn" type="submit">Kaydet</button>
    </form>`,
    bind: () => document.getElementById('buildingForm').addEventListener('submit', submitBuilding)
  };
}

function floorForm() {
  return {
    title: 'Kat Ekle',
    html: `<form id="floorForm" class="form-grid">
      <label>Bina / Blok Id<input name="buildingId" type="number" min="1" value="1" required></label>
      <label>Kat No<input name="floorNumber" type="number" min="0" value="1" required></label>
      <button class="primary-btn" type="submit">Kaydet</button>
    </form>`,
    bind: () => document.getElementById('floorForm').addEventListener('submit', submitFloor)
  };
}

function roomForm(item = null) {
  return {
    title: item ? 'Oda Düzenle' : 'Yeni Oda',
    html: `<form id="roomForm" class="form-grid two">
      <label>Kat Id<input name="blockFloorId" type="number" min="1" value="${item?.blockFloorId ?? 1}" required></label>
      <label>Oda No<input name="roomNumber" value="${escapeAttr(item?.roomNumber)}" required></label>
      <label>Kapasite<input name="capacity" type="number" min="1" value="${item?.capacity ?? 1}" required></label>
      <label>Fiyat<input name="price" type="number" min="0" step="0.01" value="${item?.price ?? 0}" required></label>
      <label class="full">Durum<select name="status">
        ${['Empty', 'PartiallyFull', 'Full', 'Maintenance'].map(x => `<option value="${x}" ${x === item?.status ? 'selected' : ''}>${roomDisplayStatus(x)}</option>`).join('')}
      </select></label>
      <button class="primary-btn full" type="submit">Kaydet</button>
    </form>`,
    bind: () => bindRoomSubmit(item)
  };
}

function assignForm() {
  return {
    title: 'Kullanıcıyı Odaya Ata',
    html: `<form id="assignForm" class="form-grid">
      <label>Kullanıcı Ara<input type="search" id="assignUserSearch" placeholder="İsim, e-posta veya TC ile ara" autocomplete="off"></label>
      <label>Kullanıcı<select name="userId" id="assignUserSelect" required><option value="">Arama yapın...</option></select></label>
      <label>Oda<select name="roomId">${state.rooms.filter(x => x.status !== 'Maintenance' && x.currentOccupancy < x.capacity).map(x => `<option value="${x.id}">${escapeHtml(x.roomNumber)} - ${escapeHtml(x.facilityName)} / ${escapeHtml(x.blockName)}</option>`).join('')}</select></label>
      <label>Konaklama Türü<select name="accommodationType"><option value="Yurt">Yurt</option><option value="Lojman">Lojman</option></select></label>
      <button class="primary-btn" type="submit">Ata</button>
    </form>`,
    bind: () => {
      const form = document.getElementById('assignForm');
      const searchInput = form.querySelector('#assignUserSearch');
      const userSelect = form.querySelector('#assignUserSelect');
      let debounceTimer;
      const doSearch = async () => {
        const term = searchInput.value.trim();
        if (term.length < 2) {
          userSelect.innerHTML = '<option value="">En az 2 karakter girin...</option>';
          return;
        }
        userSelect.innerHTML = '<option value="">Aranıyor...</option>';
        try {
          const result = await api(`/api/admin/users?search=${encodeURIComponent(term)}&page=1&pageSize=20`);
          const users = result.items || [];
          if (!users.length) {
            userSelect.innerHTML = '<option value="">Sonuç bulunamadı</option>';
          } else {
            userSelect.innerHTML = users.map(x => `<option value="${x.id}">${escapeHtml(x.fullName)} - ${escapeHtml(x.email)} - ${escapeHtml(x.role)}</option>`).join('');
          }
        } catch {
          userSelect.innerHTML = '<option value="">Arama hatası</option>';
        }
      };
      searchInput.addEventListener('input', () => {
        clearTimeout(debounceTimer);
        debounceTimer = setTimeout(doSearch, 300);
      });
      form.addEventListener('submit', submitAssign);
    }
  };
}

function openAssignModal(applicationId, userId, fullName, accommodationType) {
  const facilityMap = new Map(state.facilities.map(f => [f.name, f.type]));
  const availableRooms = state.rooms.filter(x => x.status !== 'Maintenance' && x.currentOccupancy < x.capacity && facilityMap.get(x.facilityName) === accommodationType);
  const form = {
    title: `${fullName} - Odaya Yerleştir (${escapeHtml(accommodationType)})`,
    html: `<form id="assignForm" class="form-grid">
      <input type="hidden" name="applicationId" value="${applicationId}">
      <input type="hidden" name="userId" value="${userId}">
      <input type="hidden" name="accommodationType" value="${accommodationType}">
      ${availableRooms.length === 0
        ? `<p class="muted">Seçili tesis türünde (${escapeHtml(accommodationType)}) müsait oda bulunmuyor.</p>`
        : `<label>Oda Seç<select name="roomId" required>${availableRooms.map(x => `<option value="${x.id}">${escapeHtml(x.roomNumber)} - ${escapeHtml(x.facilityName)} / ${escapeHtml(x.blockName)} (${x.currentOccupancy}/${x.capacity}) - ${money(x.price)}</option>`).join('')}</select></label>
           <button class="primary-btn" type="submit">Odaya Yerleştir</button>`}
    </form>`,
    bind: () => {
      const formEl = document.getElementById('assignForm');
      if (formEl) formEl.addEventListener('submit', submitApplicationAssign);
    }
  };
  openModal(form.title, form.html, form.bind);
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

function bindFacilitySubmit(item) {
  document.getElementById('facilityForm').addEventListener('submit', async event => {
    event.preventDefault();
    const data = Object.fromEntries(new FormData(event.currentTarget).entries());
    const type = item ? typeFromFacility(item) : data.type;
    const body = {
      name: data.name,
      campusLocation: data.campusLocation,
      totalCapacity: Number(data.totalCapacity),
      isActive: data.isActive === 'true'
    };
    const base = type === 'Yurt' ? '/api/admin/dormitories' : '/api/admin/housing-units';
    await saveAndRefresh(item ? `${base}/${item.id}` : base, item ? 'PUT' : 'POST', body, loadFacilities);
  });
}

function typeFromFacility(item) {
  return item.type === 'Lojman' ? 'Lojman' : 'Yurt';
}

async function submitBuilding(event) {
  event.preventDefault();
  const data = Object.fromEntries(new FormData(event.currentTarget).entries());
  const [kind, id] = data.facility.split('-');
  await saveAndRefresh('/api/admin/buildings', 'POST', {
    dormitoryId: kind === 'd' ? Number(id) : null,
    housingUnitId: kind === 'h' ? Number(id) : null,
    blockName: data.blockName
  }, loadFacilities);
}

async function submitFloor(event) {
  event.preventDefault();
  const data = Object.fromEntries(new FormData(event.currentTarget).entries());
  await saveAndRefresh('/api/admin/floors', 'POST', {
    buildingId: Number(data.buildingId),
    floorNumber: Number(data.floorNumber)
  }, loadRooms);
}

function bindRoomSubmit(item) {
  document.getElementById('roomForm').addEventListener('submit', async event => {
    event.preventDefault();
    const data = Object.fromEntries(new FormData(event.currentTarget).entries());
    await saveAndRefresh(item ? `/api/admin/rooms/${item.id}` : '/api/admin/rooms', item ? 'PUT' : 'POST', {
      blockFloorId: Number(data.blockFloorId),
      roomNumber: data.roomNumber,
      capacity: Number(data.capacity),
      status: data.status,
      price: Number(data.price)
    }, loadRooms);
  });
}

async function submitAssign(event) {
  event.preventDefault();
  const data = Object.fromEntries(new FormData(event.currentTarget).entries());
  await saveAndRefresh('/api/admin/placements/assign', 'POST', {
    userId: data.userId,
    roomId: Number(data.roomId),
    accommodationType: data.accommodationType
  }, async () => Promise.all([loadRooms(), loadPlacements(), loadDashboard()]));
}

async function submitApplicationAssign(event) {
  event.preventDefault();
  const data = Object.fromEntries(new FormData(event.currentTarget).entries());
  await saveAndRefresh(`/api/admin/applications/${data.applicationId}/assign`, 'POST', {
    roomId: Number(data.roomId)
  }, async () => Promise.all([loadApplications(), loadRooms(), loadPlacements(), loadDashboard()]));
}

async function saveAndRefresh(url, method, body, refresh) {
  try {
    const options = { method, headers: { 'Content-Type': 'application/json' } };
    if (body !== null && body !== undefined) {
      options.body = JSON.stringify(body);
    }
    await api(url, options);
    closeModalIfOpen();
    toast('İşlem başarılı.');
    await refresh();
  } catch (error) {
    state.fallbackMode = true;
    applyMockMutation(url, method, body);
    closeModalIfOpen();
    toast(error.message || 'Demo modda işlem uygulandı.', true);
    await refresh();
  }
}

function closeModalIfOpen() {
  const backdrop = document.getElementById('modalBackdrop');
  if (backdrop.style.display !== 'none') {
    closeModal();
  }
}

function editFacility(type, id) {
  const item = state.facilities.find(x => x.type === type && x.id === id);
  if (!item) return;
  const form = facilityForm(item, type);
  openModal(form.title, form.html, form.bind);
}

async function toggleFacility(type, id, isActive) {
  const url = type === 'Yurt' ? `/api/admin/dormitories/${id}/active` : `/api/admin/housing-units/${id}/active`;
  await saveAndRefresh(url, 'PATCH', { isActive }, loadFacilities);
}

async function deleteFacility(type, id) {
  if (!confirm('Bu tesisi silmek istiyor musunuz?')) return;
  const url = type === 'Yurt' ? `/api/admin/dormitories/${id}` : `/api/admin/housing-units/${id}`;
  await saveAndRefresh(url, 'DELETE', null, loadFacilities);
}

function editRoom(id) {
  const item = state.rooms.find(x => x.id === id);
  if (!item) return;
  openModal('Oda Düzenle', roomForm(item).html, roomForm(item).bind);
}

async function deleteRoom(id) {
  if (!confirm('Bu odayı silmek istiyor musunuz?')) return;
  await saveAndRefresh(`/api/admin/rooms/${id}`, 'DELETE', null, loadRooms);
}

async function showOccupants(id) {
  try {
    const data = state.fallbackMode ? getMockOccupants(id) : await api(`/api/admin/rooms/${id}/occupants`);
    openModal(`Oda ${data.roomNumber} Sakinleri`, `
      <div class="activity-list">
        ${emptyOr(data.occupants || [], x => `
          <div class="activity-item">
            <div>
              <strong>${escapeHtml(x.fullName)}</strong>
              <small>${escapeHtml(x.role)} - ${escapeHtml(x.tcNo)}</small>
            </div>
            <small>${date(x.checkInDate)}</small>
          </div>
        `)}
      </div>
    `, () => {});
  } catch {
    const data = getMockOccupants(id);
    openModal(`Oda ${data.roomNumber} Sakinleri`, `<div class="activity-list">${emptyOr(data.occupants || [], x => `<div class="activity-item"><div><strong>${escapeHtml(x.fullName)}</strong><small>${escapeHtml(x.role)} - ${escapeHtml(x.tcNo)}</small></div><small>${date(x.checkInDate)}</small></div>`)}</div>`, () => {});
  }
}

async function changeUserRole(id, role) {
  await saveAndRefresh(`/api/admin/users/${id}/role`, 'PATCH', { role }, () => loadUsers(state.users.page || 1));
}

async function setUserStatus(id, isActive) {
  await saveAndRefresh(`/api/admin/users/${id}/status`, 'PATCH', { isActive }, () => loadUsers(state.users.page || 1));
}

async function checkout(id) {
  await saveAndRefresh(`/api/admin/placements/${id}/checkout`, 'POST', {}, async () => Promise.all([loadPlacements(), loadRooms(), loadDashboard()]));
}

async function api(path, options = {}, attachToken = true) {
  const headers = { ...(options.headers || {}) };
  if (attachToken) {
    const token = getStoredToken();
    if (!token) throw new Error('Oturum bulunamadı.');
    headers.Authorization = `Bearer ${token}`;
  }

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

function applyMockMutation(url, method, body) {
  const id = Number((url.match(/\/(\d+)(?:\/|$)/) || [])[1]);

  if (url.includes('/rooms') && method === 'POST' && body) {
    const nextId = Math.max(...mock.rooms.map(x => x.id), 0) + 1;
    mock.rooms.push({
      id: nextId,
      blockFloorId: body.blockFloorId,
      facilityName: 'Demo Tesis',
      blockName: 'Demo Blok',
      floorNumber: 1,
      roomNumber: body.roomNumber,
      capacity: body.capacity,
      currentOccupancy: 0,
      status: body.status,
      price: body.price
    });
  } else if (url.includes('/rooms') && method === 'PUT' && body) {
    const room = mock.rooms.find(x => x.id === id);
    if (room) Object.assign(room, body);
  } else if (url.includes('/rooms') && method === 'DELETE') {
    mock.rooms = mock.rooms.filter(x => x.id !== id);
  } else if (url.includes('/placements/assign') && method === 'POST' && body) {
    const user = mock.users.find(x => x.id === body.userId);
    const room = mock.rooms.find(x => x.id === body.roomId);
    if (user && room) {
      const placementId = Math.max(...mock.placements.map(x => x.id), 0) + 1;
      mock.placements.unshift({
        id: placementId,
        userId: user.id,
        fullName: user.fullName,
        roomId: room.id,
        roomNumber: room.roomNumber,
        checkInDate: new Date().toISOString(),
        checkOutDate: null,
        isActive: true
      });
      room.currentOccupancy = Math.min(room.capacity, room.currentOccupancy + 1);
      room.status = room.currentOccupancy >= room.capacity ? 'Full' : 'PartiallyFull';
    }
  } else if (url.includes('/placements/') && url.endsWith('/checkout')) {
    const placement = mock.placements.find(x => x.id === id);
    if (placement && placement.isActive) {
      placement.isActive = false;
      placement.checkOutDate = new Date().toISOString();
      const room = mock.rooms.find(x => x.id === placement.roomId);
      if (room) {
        room.currentOccupancy = Math.max(0, room.currentOccupancy - 1);
        room.status = room.currentOccupancy === 0 ? 'Empty' : 'PartiallyFull';
      }
    }
  } else if (url.includes('/applications/') && url.endsWith('/assign') && body) {
    const application = mock.recentApplications.find(x => x.id === id);
    if (application && application.status === 'Pending') {
      application.status = 'Approved';
      const room = mock.rooms.find(x => x.id === body.roomId);
      if (room) {
        room.currentOccupancy = Math.min(room.capacity, room.currentOccupancy + 1);
        room.status = room.currentOccupancy >= room.capacity ? 'Full' : 'PartiallyFull';
        const user = mock.users.find(x => x.id === application.userId);
        if (user) {
          const placementId = Math.max(...mock.placements.map(x => x.id), 0) + 1;
          mock.placements.unshift({
            id: placementId,
            userId: user.id,
            fullName: user.fullName,
            roomId: room.id,
            roomNumber: room.roomNumber,
            checkInDate: new Date().toISOString(),
            checkOutDate: null,
            isActive: true
          });
        }
      }
    }
  } else if (url.includes('/users/') && url.endsWith('/role') && body) {
    const matches = url.match(/\/users\/([^\/]+)/);
    const user = mock.users.find(x => x.id === matches?.[1]);
    if (user) user.role = body.role;
  } else if (url.includes('/users/') && url.endsWith('/status') && body) {
    const matches = url.match(/\/users\/([^\/]+)/);
    const user = mock.users.find(x => x.id === matches?.[1]);
    if (user) user.isActive = body.isActive;
  } else if ((url.includes('/dormitories') || url.includes('/housing-units')) && method === 'POST' && body) {
    const type = url.includes('/dormitories') ? 'Yurt' : 'Lojman';
    mock.facilities.push({ id: Math.max(...mock.facilities.map(x => x.id), 0) + 1, type, buildingCount: 0, ...body });
  } else if ((url.includes('/dormitories') || url.includes('/housing-units')) && method === 'PUT' && body) {
    const type = url.includes('/dormitories') ? 'Yurt' : 'Lojman';
    const facility = mock.facilities.find(x => x.id === id && x.type === type);
    if (facility) Object.assign(facility, body);
  } else if ((url.includes('/dormitories') || url.includes('/housing-units')) && method === 'PATCH' && body) {
    const facility = mock.facilities.find(x => x.id === id);
    if (facility) facility.isActive = body.isActive;
  } else if ((url.includes('/dormitories') || url.includes('/housing-units')) && method === 'DELETE') {
    mock.facilities = mock.facilities.filter(x => x.id !== id);
  }
}

function getMockStats() {
  const totalCapacity = mock.rooms.reduce((sum, room) => sum + room.capacity, 0);
  const currentOccupancy = mock.rooms.reduce((sum, room) => sum + room.currentOccupancy, 0);
  return {
    dormitoryCount: mock.facilities.filter(x => x.type === 'Yurt').length,
    housingUnitCount: mock.facilities.filter(x => x.type === 'Lojman').length,
    totalRoomCount: mock.rooms.length,
    totalCapacity,
    currentOccupancy,
    emptyRoomCount: mock.rooms.filter(x => x.status === 'Empty').length,
    occupiedRoomCount: mock.rooms.filter(x => x.status === 'PartiallyFull' || x.status === 'Full').length,
    maintenanceRoomCount: mock.rooms.filter(x => x.status === 'Maintenance').length,
    occupancyRate: totalCapacity === 0 ? 0 : Math.round((currentOccupancy / totalCapacity) * 10000) / 100,
    pendingApplicationCount: 3,
    openRequestCount: mock.recentRequests.filter(x => x.status === 'Open' || x.status === 'InProgress').length,
    totalUnpaidAndOverdueDebt: 10500,
    recentApplications: clone(mock.recentApplications),
    recentRequests: clone(mock.recentRequests)
  };
}

function getMockUsers(page = 1) {
  const term = document.getElementById('userSearch').value.toLowerCase();
  const role = document.getElementById('roleFilter').value;
  const items = mock.users.filter(x =>
    (!term || x.fullName.toLowerCase().includes(term) || x.email.toLowerCase().includes(term) || x.tcNo.includes(term)) &&
    (!role || x.role === role));
  const pageSize = 10;
  return { items: clone(items.slice((page - 1) * pageSize, page * pageSize)), page, pageSize, totalCount: items.length };
}

function getMockOccupants(roomId) {
  const room = mock.rooms.find(x => x.id === roomId) || mock.rooms[0];
  const occupants = mock.placements
    .filter(x => x.roomId === room.id && x.isActive)
    .map(x => {
      const user = mock.users.find(u => u.id === x.userId);
      return {
        placementId: x.id,
        userId: x.userId,
        fullName: x.fullName,
        tcNo: user?.tcNo || '-',
        studentStaffNo: user?.studentStaffNo || '-',
        role: user?.role || 'Ogrenci',
        checkInDate: x.checkInDate
      };
    });

  return { roomId: room.id, roomNumber: room.roomNumber, capacity: room.capacity, currentOccupancy: room.currentOccupancy, status: room.status, occupants };
}

function createMockStore() {
  const now = new Date();
  const isoDaysAgo = days => new Date(now.getTime() - days * 86400000).toISOString();
  return {
    facilities: [
      { id: 1, name: 'MTÜ Merkez Öğrenci Yurdu', type: 'Yurt', campusLocation: 'Battalgazi Yerleşkesi', totalCapacity: 11, isActive: true, buildingCount: 1 },
      { id: 2, name: 'Yeşilyurt Kız Öğrenci Yurdu', type: 'Yurt', campusLocation: 'Yeşilyurt Yerleşkesi', totalCapacity: 8, isActive: true, buildingCount: 1 },
      { id: 3, name: 'MTÜ Personel Lojmanları', type: 'Lojman', campusLocation: 'Battalgazi Yerleşkesi', totalCapacity: 2, isActive: true, buildingCount: 1 }
    ],
    rooms: [
      { id: 1, blockFloorId: 1, facilityName: 'MTÜ Merkez Öğrenci Yurdu', blockName: 'A Blok', floorNumber: 1, roomNumber: '101', capacity: 4, currentOccupancy: 1, status: 'PartiallyFull', price: 2500 },
      { id: 2, blockFloorId: 1, facilityName: 'MTÜ Merkez Öğrenci Yurdu', blockName: 'A Blok', floorNumber: 1, roomNumber: '102', capacity: 4, currentOccupancy: 1, status: 'PartiallyFull', price: 2500 },
      { id: 3, blockFloorId: 2, facilityName: 'MTÜ Merkez Öğrenci Yurdu', blockName: 'A Blok', floorNumber: 2, roomNumber: '201', capacity: 3, currentOccupancy: 0, status: 'Empty', price: 2700 },
      { id: 4, blockFloorId: 3, facilityName: 'Yeşilyurt Kız Öğrenci Yurdu', blockName: 'B Blok', floorNumber: 1, roomNumber: 'B-103', capacity: 4, currentOccupancy: 0, status: 'Empty', price: 2400 },
      { id: 5, blockFloorId: 3, facilityName: 'Yeşilyurt Kız Öğrenci Yurdu', blockName: 'B Blok', floorNumber: 1, roomNumber: 'B-104', capacity: 4, currentOccupancy: 0, status: 'Maintenance', price: 2400 },
      { id: 6, blockFloorId: 4, facilityName: 'MTÜ Personel Lojmanları', blockName: 'L Blok', floorNumber: 1, roomNumber: 'L101', capacity: 1, currentOccupancy: 1, status: 'Full', price: 5500 },
      { id: 7, blockFloorId: 4, facilityName: 'MTÜ Personel Lojmanları', blockName: 'L Blok', floorNumber: 1, roomNumber: 'L102', capacity: 1, currentOccupancy: 0, status: 'Empty', price: 5750 }
    ],
    users: [
      { id: '11111111-1111-1111-1111-111111111111', fullName: 'Sistem Yöneticisi', email: 'admin@ozal.edu.tr', phoneNumber: '+904220000001', tcNo: '11111111111', studentStaffNo: 'ADMIN-001', role: 'Admin', isActive: true, createdAt: isoDaysAgo(20) },
      { id: '22222222-2222-2222-2222-222222222222', fullName: 'Yurt İşleri Yetkilisi', email: 'yetkili@ozal.edu.tr', phoneNumber: '+904220000002', tcNo: '22222222222', studentStaffNo: 'PER-100', role: 'Yetkili', isActive: true, createdAt: isoDaysAgo(18) },
      { id: '33333333-3333-3333-3333-333333333333', fullName: 'Ayşe Yılmaz', email: 'ayse.yilmaz@ogr.ozal.edu.tr', phoneNumber: '+905550000001', tcNo: '33333333333', studentStaffNo: 'OGR-2026-001', role: 'Ogrenci', isActive: true, createdAt: isoDaysAgo(15) },
      { id: '44444444-4444-4444-4444-444444444444', fullName: 'Mehmet Kaya', email: 'mehmet.kaya@ogr.ozal.edu.tr', phoneNumber: '+905550000002', tcNo: '44444444444', studentStaffNo: 'OGR-2026-002', role: 'Ogrenci', isActive: true, createdAt: isoDaysAgo(15) },
      { id: '55555555-5555-5555-5555-555555555555', fullName: 'Zeynep Demir', email: 'zeynep.demir@ogr.ozal.edu.tr', phoneNumber: '+905550000003', tcNo: '55555555555', studentStaffNo: 'OGR-2026-003', role: 'Ogrenci', isActive: true, createdAt: isoDaysAgo(12) },
      { id: '66666666-6666-6666-6666-666666666666', fullName: 'Ali Çelik', email: 'ali.celik@ozal.edu.tr', phoneNumber: '+905550000004', tcNo: '66666666666', studentStaffNo: 'PRS-2026-014', role: 'Personel', isActive: true, createdAt: isoDaysAgo(10) },
      { id: '77777777-7777-7777-7777-777777777777', fullName: 'Elif Şahin', email: 'elif.sahin@ozal.edu.tr', phoneNumber: '+905550000005', tcNo: '77777777777', studentStaffNo: 'PRS-2026-019', role: 'Personel', isActive: true, createdAt: isoDaysAgo(9) }
    ],
    placements: [
      { id: 1, userId: '44444444-4444-4444-4444-444444444444', fullName: 'Mehmet Kaya', roomId: 1, roomNumber: '101', facilityName: 'MTU Merkez Ogrenci Yurdu', blockName: 'A Blok', checkInDate: isoDaysAgo(30), checkOutDate: null, isActive: true },
      { id: 2, userId: '55555555-5555-5555-5555-555555555555', fullName: 'Zeynep Demir', roomId: 2, roomNumber: '102', facilityName: 'MTU Merkez Ogrenci Yurdu', blockName: 'A Blok', checkInDate: isoDaysAgo(18), checkOutDate: null, isActive: true },
      { id: 3, userId: '66666666-6666-6666-6666-666666666666', fullName: 'Ali Çelik', roomId: 6, roomNumber: 'L101', facilityName: 'MTU Personel Lojmanlari', blockName: 'L Blok', checkInDate: isoDaysAgo(45), checkOutDate: null, isActive: true }
    ],
    recentApplications: [
      { id: 5, userId: '55555555-5555-5555-5555-555555555555', fullName: 'Zeynep Demir', tcNo: '55555555555', accommodationType: 'Yurt', status: 'Pending', createdAt: isoDaysAgo(1) },
      { id: 3, userId: '66666666-6666-6666-6666-666666666666', fullName: 'Ali Çelik', tcNo: '66666666666', accommodationType: 'Lojman', status: 'Pending', createdAt: isoDaysAgo(2) },
      { id: 1, userId: '33333333-3333-3333-3333-333333333333', fullName: 'Ayşe Yılmaz', tcNo: '33333333333', accommodationType: 'Yurt', status: 'Pending', createdAt: isoDaysAgo(3) },
      { id: 2, userId: '44444444-4444-4444-4444-444444444444', fullName: 'Mehmet Kaya', tcNo: '44444444444', accommodationType: 'Yurt', status: 'Approved', createdAt: isoDaysAgo(5) },
      { id: 4, userId: '77777777-7777-7777-7777-777777777777', fullName: 'Elif Şahin', tcNo: '77777777777', accommodationType: 'Lojman', status: 'Rejected', createdAt: isoDaysAgo(8) }
    ],
    recentRequests: [
      { id: 1, userId: '44444444-4444-4444-4444-444444444444', fullName: 'Mehmet Kaya', roomNumber: '101', category: 'Elektrik', status: 'Open', createdAt: isoDaysAgo(0) },
      { id: 2, userId: '55555555-5555-5555-5555-555555555555', fullName: 'Zeynep Demir', roomNumber: '102', category: 'Isıtma', status: 'InProgress', createdAt: isoDaysAgo(1) },
      { id: 3, userId: '66666666-6666-6666-6666-666666666666', fullName: 'Ali Çelik', roomNumber: 'L101', category: 'Su Tesisatı', status: 'Open', createdAt: isoDaysAgo(2) },
      { id: 4, userId: '44444444-4444-4444-4444-444444444444', fullName: 'Mehmet Kaya', roomNumber: '101', category: 'Mobilya', status: 'Resolved', createdAt: isoDaysAgo(5) }
    ]
  };
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
    Beklemede: ['Beklemede', 'badge-warning'],
    Bekleyen: ['Beklemede', 'badge-warning'],
    Approved: ['Onaylandı', 'badge-success'],
    Onaylandı: ['Onaylandı', 'badge-success'],
    Rejected: ['Reddedildi', 'badge-danger'],
    Reddedildi: ['Reddedildi', 'badge-danger'],
    Open: ['Açık', 'badge-info'],
    Açık: ['Açık', 'badge-info'],
    InProgress: ['İşlemde', 'badge-warning'],
    İşlemde: ['İşlemde', 'badge-warning'],
    Resolved: ['Çözüldü', 'badge-success'],
    Completed: ['Çözüldü', 'badge-success'],
    Çözüldü: ['Çözüldü', 'badge-success'],
    Cancelled: ['İptal Edildi', 'badge-muted'],
    'İptal Edildi': ['İptal Edildi', 'badge-muted'],
    Empty: ['Boş', 'badge-muted'],
    Boş: ['Boş', 'badge-muted'],
    PartiallyFull: ['Kısmen Dolu', 'badge-warning'],
    'Kısmen Dolu': ['Kısmen Dolu', 'badge-warning'],
    Full: ['Dolu', 'badge-success'],
    Dolu: ['Dolu', 'badge-success'],
    Maintenance: ['Bakımda', 'badge-danger'],
    Bakımda: ['Bakımda', 'badge-danger'],
    Aktif: ['Aktif', 'badge-success'],
    Pasif: ['Pasif', 'badge-muted'],
    'Çıkış Yaptı': ['Çıkış Yaptı', 'badge-muted']
  };
  const normalized = String(status || '');
  const [label, tone] = map[normalized] || [normalized, 'badge-muted'];
  return `<span class="badge ${tone}">${escapeHtml(label)}</span>`;
}

function roomDisplayStatus(status) {
  const map = {
    Empty: 'Boş',
    PartiallyFull: 'Kısmen Dolu',
    Full: 'Dolu',
    Maintenance: 'Bakımda'
  };
  return map[status] || status;
}

function applicationStatusDisplay(status) {
  const map = {
    Pending: 'Bekleyen',
    Approved: 'Onaylandı',
    Rejected: 'Reddedildi'
  };
  return map[status] || status;
}

function requestStatusDisplay(status) {
  const map = {
    Open: 'Açık',
    InProgress: 'İşlemde',
    Resolved: 'Çözüldü',
    Rejected: 'Reddedildi'
  };
  return map[status] || status;
}

function staffRoleDisplay(role) {
  const map = {
    TeknikPersonel: 'Teknik Personel',
    TemizlikPersoneli: 'Temizlik Personeli'
  };
  return map[role] || role;
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
    // Add padding if needed
    while (payload.length % 4) payload += '=';
    return JSON.parse(decodeURIComponent(atob(payload).split('').map(c => `%${(`00${c.charCodeAt(0).toString(16)}`).slice(-2)}`).join('')));
  } catch {
    return {};
  }
}

function clone(value) {
  return JSON.parse(JSON.stringify(value));
}

function date(value) {
  return value ? new Date(value).toLocaleDateString('tr-TR') : '-';
}

function money(value) {
  return Number(value || 0).toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' });
}

function escapeHtml(value) {
  return String(value ?? '').replace(/[&<>"']/g, char => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#039;' }[char]));
}

function escapeAttr(value) {
  return escapeHtml(value ?? '').replace(/"/g, '&quot;');
}

function debounce(fn, wait) {
  let timer;
  return (...args) => {
    clearTimeout(timer);
    timer = setTimeout(() => fn(...args), wait);
  };
}
