const tokenKey = 'token';

const state = {
  stats: null,
  assignedFacilities: [],
  students: { items: [], page: 1, pageSize: 10, totalCount: 0 },
  applications: [],
  availableRooms: [],
  studentsWithRooms: [],
  manageRooms: [],
  requests: [],
  staffAssignments: [],
  faultReports: [],
  userFacilityAssignments: [],
  announcements: [],
  fallbackMode: false,
  listPageSize: 10,
  pages: {
    applications: 1,
    availableRooms: 1,
    studentsWithRooms: 1,
    manageRooms: 1,
    requests: 1,
    staffAssignments: 1,
    faultReports: 1,
    userFacilityAssignments: 1,
    announcements: 1
  }
};

const REFRESH_INTERVAL = 15000;
let activeSection = 'dashboard';

const sectionTitles = {
  dashboard: 'Kontrol Paneli',
  applications: 'Başvuru Yönetimi',
  students: 'Öğrenci Yönetimi',
  manage: 'Yerleşim & Oda Düzenleme',
  operations: 'Operasyon & Görevlendirme',
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
  document.getElementById('roomTypeFilter').addEventListener('change', () => { state.pages.availableRooms = 1; loadAvailableRooms(); });
  document.getElementById('requestOpenOnlyFilter').addEventListener('change', () => { state.pages.requests = 1; loadRequests(); });

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
  await Promise.allSettled([loadAssignedFacilities(), loadStudents(1), loadApplications(), loadAvailableRooms(), loadManage(), loadOperations(), loadAnnouncements()]);
  await loadDashboard();
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
    applications: loadApplications,
    students: () => loadStudents(state.students.page || 1),
    manage: loadManage,
    operations: loadOperations,
    announcements: loadAnnouncements,
    rooms: loadAvailableRooms
  };
  refreshers[activeSection]?.();
}

async function loadDashboard() {
  try {
    state.stats = await api('/api/yetkili/dashboard-stats');
    state.fallbackMode = false;
  } catch {
    state.fallbackMode = true;
    state.stats = null;
  }
  renderDashboard();
}

function renderDashboard() {
  const stats = state.stats || {};
  const facilities = state.assignedFacilities || [];
  const cards = [
    ['Atanmış Tesis', facilities.length, '🏢'],
    ['Doluluk Oranı', `${stats.occupancyRate ?? 0}%`, '📊'],
    ['Toplam Sakin', stats.currentOccupancy ?? 0, '👨‍🎓'],
    ['Bekleyen Başvuru', stats.pendingApplicationCount ?? state.applications.length ?? 0, '📝']
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

  document.getElementById('recentApplications').innerHTML = emptyOr(state.applications.slice(0, 5), application => `
    <div class="activity-item">
      <div>
        <strong>${escapeHtml(application.fullName)}</strong>
        <small>${escapeHtml(application.accommodationType)} · ${date(application.createdAt)}</small>
      </div>
      ${getStatusBadge(application.status)}
    </div>
  `);
}

async function loadAssignedFacilities() {
  try {
    state.assignedFacilities = state.fallbackMode ? [] : await api('/api/yetkili/facilities');
  } catch {
    state.assignedFacilities = [];
  }
  syncRoomTypeFilter();
}

function syncRoomTypeFilter() {
  const select = document.getElementById('roomTypeFilter');
  if (!select) return;
  const current = select.value;
  const types = [...new Set((state.assignedFacilities || []).map(facility => facility.type))];
  select.innerHTML = types.length
    ? types.map(type => `<option value="${type}" ${type === current ? 'selected' : ''}>${type}</option>`).join('')
    : '<option value="Yurt">Yurt</option>';
  if (types.length && !types.includes(select.value)) {
    select.value = types[0];
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
      <td>${escapeHtml(roleDisplay(item.role))}</td>
      <td>${item.isActive ? '<span class="badge badge-success">Aktif</span>' : '<span class="badge badge-muted">Pasif</span>'}</td>
      <td>
        ${item.role === 'Ogrenci' ? `<button class="row-btn" onclick="editStudent('${item.id}')">Düzenle</button>
        <button class="row-btn danger" onclick="deleteStudent('${item.id}')">Sil</button>` : '-'}
      </td>
    </tr>
  `);

  const totalPages = Math.max(1, Math.ceil((state.students.totalCount || 0) / (state.students.pageSize || 10)));
  renderPager('studentPager', state.students.page || 1, totalPages, loadStudents);
}

async function loadApplications() {
  try {
    state.applications = state.fallbackMode ? [] : await api('/api/yetkili/applications');
  } catch {
    state.applications = [];
  }
  renderApplications();
}

function renderApplications() {
  const page = paginateItems(state.applications, 'applications');
  document.getElementById('applicationRows').innerHTML = emptyTable(page.items, item => `
    <tr>
      <td><strong>${escapeHtml(item.fullName)}</strong></td>
      <td>${escapeHtml(item.tcNo)}</td>
      <td>${escapeHtml(item.studentStaffNo || '-')}</td>
      <td>${escapeHtml(item.accommodationType)}</td>
      <td>${getStatusBadge(item.status)}</td>
      <td>${date(item.createdAt)}</td>
      <td>
        <button class="row-btn" onclick="openAssignModal(${item.id}, '${item.userId}', '${escapeAttr(item.fullName)}', '${item.accommodationType}')">Odaya Yerleştir</button>
        <button class="row-btn danger" onclick="rejectApplication(${item.id})">Reddet</button>
      </td>
    </tr>
  `);
  renderPager('applicationPager', page.page, page.totalPages, next => { state.pages.applications = next; renderApplications(); });
}

function facilitiesForAccommodationType(accommodationType) {
  return (state.assignedFacilities || []).filter(facility => facility.type === accommodationType && facility.isActive !== false);
}

function parseFacilityKey(value) {
  const [type, rawId] = String(value || '').split(':');
  const id = Number(rawId);
  if (!type || !id) return null;
  return {
    type,
    id,
    dormitoryId: type === 'Yurt' ? id : null,
    housingUnitId: type === 'Lojman' ? id : null
  };
}

function roomsForAssignment(accommodationType, facilityKey = null) {
  return (state.manageRooms || [])
    .filter(room => room.facilityType === accommodationType)
    .filter(room => !facilityKey || (room.facilityType === facilityKey.type && room.facilityId === facilityKey.id))
    .filter(room => room.status !== 'Maintenance' && Number(room.currentOccupancy || 0) < Number(room.capacity || 0))
    .sort((a, b) => String(a.facilityName || '').localeCompare(String(b.facilityName || ''), 'tr')
      || String(a.blockName || '').localeCompare(String(b.blockName || ''), 'tr')
      || Number(a.floorNumber || 0) - Number(b.floorNumber || 0)
      || String(a.roomNumber || '').localeCompare(String(b.roomNumber || ''), 'tr'));
}

function assignmentRoomLabel(room) {
  const freeBeds = Math.max(0, Number(room.capacity || 0) - Number(room.currentOccupancy || 0));
  return `${room.facilityName} / ${room.blockName} / Kat ${room.floorNumber} / Oda ${room.roomNumber} - ${room.capacity} yatak, ${room.currentOccupancy} dolu / ${freeBeds} boş`;
}

function assignmentRoomCard(room) {
  const freeBeds = Math.max(0, Number(room.capacity || 0) - Number(room.currentOccupancy || 0));
  return `
    <div class="assignment-room-card">
      <div>
        <strong>${escapeHtml(room.facilityName)} / ${escapeHtml(room.blockName)}</strong>
        <small>Kat ${escapeHtml(room.floorNumber)} - Oda ${escapeHtml(room.roomNumber)}</small>
      </div>
      <div class="assignment-room-meta">
        <span>${room.capacity} yatak</span>
        <span>${room.currentOccupancy} dolu</span>
        <span>${freeBeds} boş</span>
        ${getStatusBadge(room.status)}
      </div>
    </div>
  `;
}

function assignForm(applicationId, userId, accommodationType) {
  const facilities = facilitiesForAccommodationType(accommodationType);
  const facilityOptions = facilities.map(facility => `<option value="${facility.type}:${facility.id}">${escapeHtml(facility.name)} (${escapeHtml(facility.type)})</option>`).join('');
  return {
    title: 'Odaya Ata',
    html: `<form id="assignForm" class="form-grid">
      <input type="hidden" name="applicationId" value="${escapeAttr(applicationId)}">
      <input type="hidden" name="userId" value="${escapeAttr(userId)}">
      <input type="hidden" name="accommodationType" value="${escapeAttr(accommodationType)}">
      <label class="full">Atama Tipi<select name="allocationMode" id="allocationMode">
        <option value="manual">Manuel oda seçimi</option>
        <option value="auto">Otomatik uygun oda ata</option>
      </select></label>
      <label class="full">Tesis<select name="facilityKey" id="assignmentFacility" required>${facilityOptions || '<option value="">Size atanmış uygun tesis yok</option>'}</select></label>
      <label class="full" id="manualRoomField">Oda<select name="roomId" id="assignmentRoom"></select></label>
      <div id="assignmentRoomPreview" class="assignment-preview full"></div>
      <button class="primary-btn full" type="submit">Atamayı Tamamla</button>
    </form>`,
    bind: () => {
      const form = document.getElementById('assignForm');
      bindAssignmentControls(form, accommodationType);
      form.addEventListener('submit', submitApplicationAssign);
    }
  };
}

function bindAssignmentControls(form, accommodationType) {
  const modeSelect = form.querySelector('#allocationMode');
  const facilitySelect = form.querySelector('#assignmentFacility');
  const roomSelect = form.querySelector('#assignmentRoom');
  const roomField = form.querySelector('#manualRoomField');
  const preview = form.querySelector('#assignmentRoomPreview');

  const refreshRooms = () => {
    const facilityKey = parseFacilityKey(facilitySelect.value);
    const isAuto = modeSelect.value === 'auto';
    const rooms = roomsForAssignment(accommodationType, facilityKey);
    roomField.style.display = isAuto ? 'none' : '';
    roomSelect.required = !isAuto;
    roomSelect.disabled = isAuto || rooms.length === 0;

    if (isAuto) {
      preview.innerHTML = facilityKey
        ? `<div class="assignment-room-card"><strong>${rooms.length} uygun oda bulundu</strong><small>Sistem seçilen tesiste boş yatağı olan uygun bir odayı otomatik seçecek.</small></div>`
        : '<p class="muted">Otomatik atama için tesis seçin.</p>';
      return;
    }

    roomSelect.innerHTML = rooms.length
      ? rooms.map(room => `<option value="${room.id}">${escapeHtml(assignmentRoomLabel(room))}</option>`).join('')
      : '<option value="">Bu tesiste uygun oda yok</option>';
    refreshPreview();
  };

  const refreshPreview = () => {
    const selectedRoom = state.manageRooms.find(room => room.id === Number(roomSelect.value));
    preview.innerHTML = selectedRoom ? assignmentRoomCard(selectedRoom) : '<p class="muted">Uygun oda seçin.</p>';
  };

  modeSelect.addEventListener('change', refreshRooms);
  facilitySelect.addEventListener('change', refreshRooms);
  roomSelect.addEventListener('change', refreshPreview);
  refreshRooms();
}

function buildAssignmentPayload(data) {
  const isAuto = data.allocationMode === 'auto';
  const facilityKey = parseFacilityKey(data.facilityKey);
  if (isAuto && !facilityKey) {
    toast('Otomatik atama için tesis seçin.', true);
    return null;
  }
  if (!isAuto && !data.roomId) {
    toast('Manuel atama için uygun bir oda seçin.', true);
    return null;
  }
  return {
    approved: true,
    reason: null,
    roomId: isAuto ? null : Number(data.roomId),
    autoPlace: isAuto,
    dormitoryId: isAuto && facilityKey?.type === 'Yurt' ? facilityKey.id : null,
    housingUnitId: isAuto && facilityKey?.type === 'Lojman' ? facilityKey.id : null
  };
}

async function openAssignModal(applicationId, userId, fullName, accommodationType) {
  if (!state.manageRooms.length) {
    await loadManageRooms();
  }
  const form = assignForm(applicationId, userId, accommodationType);
  openModal(`Odaya Ata: ${escapeHtml(fullName)}`, form.html, form.bind);
}

async function submitApplicationAssign(event) {
  event.preventDefault();
  const data = Object.fromEntries(new FormData(event.currentTarget).entries());
  const payload = buildAssignmentPayload(data);
  if (!payload) return;
  try {
    const result = await api(`/api/yetkili/applications/${Number(data.applicationId)}/assign`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    });
    closeModalIfOpen();
    state.applications = state.applications.filter(item => item.id !== Number(data.applicationId));
    renderApplications();
    showApplicationFeedback(result?.message || 'Başvuru başarıyla onaylandı ve yerleştirildi.');
    await Promise.allSettled([loadManage(), loadAvailableRooms(), loadDashboard()]);
  } catch (error) {
    toast(error.message || 'Başvuru onaylanamadı.', true);
  }
}

async function rejectApplication(id) {
  if (!confirm('Bu başvuruyu reddetmek istediğinize emin misiniz?')) return;
  try {
    const result = await api(`/api/yetkili/applications/${id}/reject`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ approved: false, reason: 'Yetkili panelinden reddedildi.', roomId: null, autoPlace: false, dormitoryId: null, housingUnitId: null })
    });
    state.applications = state.applications.filter(item => item.id !== id);
    renderApplications();
    showApplicationFeedback(result?.message || 'Başvuru reddedildi.');
    await loadDashboard();
  } catch (error) {
    toast(error.message || 'Başvuru reddedilemedi.', true);
  }
}

function showApplicationFeedback(message) {
  const host = document.getElementById('applicationFeedback');
  if (!host) return;
  host.textContent = message;
  host.classList.add('success');
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
  const page = paginateItems(state.availableRooms, 'availableRooms');
  document.getElementById('availableRoomRows').innerHTML = emptyTable(page.items, item => `
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
  renderPager('availableRoomPager', page.page, page.totalPages, next => { state.pages.availableRooms = next; renderAvailableRooms(); });
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
    staffAssignmentModal: staffAssignmentForm(),
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
  const page = paginateItems(state.studentsWithRooms, 'studentsWithRooms');
  document.getElementById('studentRoomRows').innerHTML = emptyTable(page.items, item => `
    <tr>
      <td><strong>${escapeHtml(item.fullName)}</strong></td>
      <td>${escapeHtml(item.email)}</td>
      <td>${escapeHtml(item.tcNo)}</td>
      <td>${escapeHtml(item.studentStaffNo || '-')}</td>
      <td>${escapeHtml(item.facilityName)}</td>
      <td>${escapeHtml(item.blockName)}</td>
      <td>${escapeHtml(item.floorNumber)}</td>
      <td><strong>${escapeHtml(item.roomNumber)}</strong></td>
      <td>${date(item.checkInDate)}</td>
      <td>
        <button class="row-btn" onclick="openChangeRoomModal(${item.placementId})">Oda Değiştir</button>
        <button class="row-btn danger" onclick="checkoutPlacement(${item.placementId})">Çıkış Yap</button>
      </td>
    </tr>
  `);
  renderPager('studentRoomPager', page.page, page.totalPages, next => { state.pages.studentsWithRooms = next; renderStudentsWithRooms(); });
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
  const page = paginateItems(state.manageRooms, 'manageRooms');
  document.getElementById('manageRoomRows').innerHTML = emptyTable(page.items, item => `
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
  renderPager('manageRoomPager', page.page, page.totalPages, next => { state.pages.manageRooms = next; renderManageRooms(); });
}

function openRoomEdit(id) {
  const item = state.manageRooms.find(x => x.id === id);
  if (!item) return;
  const form = roomEditForm(item);
  openModal(form.title, form.html, form.bind);
}

function openChangeRoomModal(placementId) {
  const resident = state.studentsWithRooms.find(item => item.placementId === placementId);
  if (!resident) return;
  const rooms = roomsForAssignment(resident.facilityType).filter(room => room.id !== resident.roomId);
  const options = rooms.map(room => `<option value="${room.id}">${escapeHtml(assignmentRoomLabel(room))}</option>`).join('');
  openModal(`Oda Değiştir: ${escapeHtml(resident.fullName)}`, `
    <form id="changeRoomForm" class="form-grid" data-placement-id="${placementId}">
      <div class="assignment-preview full">
        <div class="assignment-room-card">
          <strong>${escapeHtml(resident.facilityName)} / ${escapeHtml(resident.blockName)}</strong>
          <small>Mevcut oda: Kat ${escapeHtml(resident.floorNumber)} - Oda ${escapeHtml(resident.roomNumber)}</small>
        </div>
      </div>
      <label class="full">Yeni Oda<select name="roomId" required>${options || '<option value="">Uygun boş oda yok</option>'}</select></label>
      <button class="primary-btn full" type="submit">Odayı Değiştir</button>
    </form>
  `, () => document.getElementById('changeRoomForm').addEventListener('submit', submitChangeRoom));
}

async function submitChangeRoom(event) {
  event.preventDefault();
  const form = event.currentTarget;
  const data = Object.fromEntries(new FormData(form).entries());
  if (!data.roomId) {
    toast('Yeni oda seçin.', true);
    return;
  }
  try {
    const result = await api(`/api/yetkili/placements/${form.dataset.placementId}/change-room`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ roomId: Number(data.roomId) })
    });
    closeModalIfOpen();
    toast(result?.message || 'Oda değişikliği tamamlandı.');
    await Promise.allSettled([loadManage(), loadAvailableRooms(), loadDashboard()]);
  } catch (error) {
    toast(error.message || 'Oda değiştirilemedi.', true);
  }
}

async function checkoutPlacement(placementId) {
  if (!confirm('Bu sakinin yerleşimini sonlandırmak istediğinize emin misiniz?')) return;
  try {
    const result = await api(`/api/yetkili/placements/${placementId}/checkout`, { method: 'POST' });
    state.studentsWithRooms = state.studentsWithRooms.filter(item => item.placementId !== placementId);
    renderStudentsWithRooms();
    toast(result?.message || 'Yerleşim sonlandırıldı.');
  await Promise.allSettled([loadStudents(state.students.page || 1), loadManageRooms(), loadAvailableRooms(), loadDashboard()]);
  } catch (error) {
    toast(error.message || 'Çıkış işlemi tamamlanamadı.', true);
  }
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

// ============ OPERASYONLAR ============
function loadOperations() {
  return Promise.allSettled([loadRequests(), loadStaffAssignments(), loadFaultReports(), loadUserFacilityAssignments()]);
}

async function loadRequests() {
  try {
    const openOnly = document.getElementById('requestOpenOnlyFilter')?.value === 'open';
    state.requests = await api(`/api/yetkili/requests?openOnly=${openOnly}`);
  } catch {
    state.requests = [];
  }
  renderRequests();
}

function renderRequests() {
  const page = paginateItems(state.requests, 'requests');
  document.getElementById('requestRows').innerHTML = emptyTable(page.items, item => `
    <tr>
      <td><strong>${escapeHtml(item.fullName)}</strong></td>
      <td>${escapeHtml(item.roomNumber)}</td>
      <td>${escapeHtml(item.category)}</td>
      <td class="desc-cell">${escapeHtml(item.description)}</td>
      <td>${date(item.createdAt)}</td>
      <td>${getStatusBadge(item.status)}</td>
      <td>
        <select onchange="setRequestStatus(${item.id}, this.value)">
          ${['Open', 'InProgress', 'Resolved', 'Rejected'].map(status => `<option value="${status}" ${status === item.status ? 'selected' : ''}>${requestStatusDisplay(status)}</option>`).join('')}
        </select>
      </td>
    </tr>
  `);
  renderPager('requestPager', page.page, page.totalPages, next => { state.pages.requests = next; renderRequests(); });
}

async function setRequestStatus(id, status) {
  try {
    const result = await api(`/api/yetkili/requests/${id}/status`, {
      method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ status })
    });
    const item = state.requests.find(request => request.id === id);
    if (item) item.status = status;
    renderRequests();
    toast(result?.message || 'Talep durumu güncellendi.');
    await loadDashboard();
  } catch (error) {
    toast(error.message || 'Talep durumu güncellenemedi.', true);
    await loadRequests();
  }
}

function staffAssignmentForm() {
  const facilities = state.assignedFacilities || [];
  const facilityOptions = facilities.map(facility => `<option value="${facility.type}:${facility.id}">${escapeHtml(facility.name)} (${escapeHtml(facility.type)})</option>`).join('');
  return {
    title: 'Personele Görev Ata',
    html: `<form id="staffAssignmentForm" class="form-grid">
      <label class="full">Tesis<select name="facilityKey" required>${facilityOptions || '<option value="">Size atanmış tesis yok</option>'}</select></label>
      <label>Rol<select name="assignedRole" required>
        <option value="TeknikPersonel">Teknik Personel</option>
        <option value="TemizlikPersoneli">Temizlik Personeli</option>
      </select></label>
      <label>Öncelik<select name="priority" required>
        <option value="Normal">Normal</option>
        <option value="Yüksek">Yüksek</option>
        <option value="Acil">Acil</option>
      </select></label>
      <label class="full">Başlık<input name="title" required maxlength="120" placeholder="Görev başlığı"></label>
      <label class="full">Konum<input name="location" required maxlength="160" placeholder="Blok, kat, oda veya ortak alan"></label>
      <label class="full">Detay<textarea name="details" maxlength="1000" placeholder="Görev detayı"></textarea></label>
      <label>Termin<input name="dueDate" type="date"></label>
      <label class="inline-check full"><input name="isMaintenanceRequest" type="checkbox"> Arıza iş emri olarak işaretle</label>
      <button class="primary-btn full" type="submit">Görevi Ata</button>
    </form>`,
    bind: () => document.getElementById('staffAssignmentForm').addEventListener('submit', submitStaffAssignment)
  };
}

async function submitStaffAssignment(event) {
  event.preventDefault();
  const data = Object.fromEntries(new FormData(event.currentTarget).entries());
  const facility = parseFacilityKey(data.facilityKey);
  if (!facility) {
    toast('Görev için tesis seçin.', true);
    return;
  }

  try {
    const result = await api('/api/yetkili/staff-assignments', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        assignedRole: data.assignedRole,
        dormitoryId: facility.type === 'Yurt' ? facility.id : null,
        housingUnitId: facility.type === 'Lojman' ? facility.id : null,
        title: data.title,
        location: data.location,
        details: data.details || null,
        priority: data.priority,
        isMaintenanceRequest: data.isMaintenanceRequest === 'on',
        dueDate: data.dueDate || null
      })
    });
    closeModalIfOpen();
    if (result?.assignment) {
      state.staffAssignments.unshift(result.assignment);
      renderStaffAssignments();
    } else {
      await loadStaffAssignments();
    }
    toast(result?.message || 'Görev personele atandı.');
  } catch (error) {
    toast(error.message || 'Görev atanamadı.', true);
  }
}

async function loadStaffAssignments() {
  try {
    state.staffAssignments = await api('/api/yetkili/staff-assignments');
  } catch {
    state.staffAssignments = [];
  }
  renderStaffAssignments();
}

function renderStaffAssignments() {
  const page = paginateItems(state.staffAssignments, 'staffAssignments');
  document.getElementById('assignmentRows').innerHTML = emptyTable(page.items, item => `
    <tr>
      <td><strong>${escapeHtml(item.title)}</strong><br><small>${escapeHtml(item.details || '-')}</small></td>
      <td>${roleDisplay(item.assignedRole)}${item.isMaintenanceRequest ? '<br><small>Arıza iş emri</small>' : ''}</td>
      <td>${escapeHtml(item.dormitoryName || item.housingUnitName || '-')}</td>
      <td>${escapeHtml(item.location)}</td>
      <td>${escapeHtml(item.priority)}</td>
      <td>${item.dueDate ? date(item.dueDate) : '-'}</td>
      <td>${item.isCompleted ? '<span class="badge badge-success">Tamamlandı</span>' : '<span class="badge badge-warning">Bekliyor</span>'}</td>
    </tr>
  `);
  renderPager('assignmentPager', page.page, page.totalPages, next => { state.pages.staffAssignments = next; renderStaffAssignments(); });
}

async function loadFaultReports() {
  try {
    state.faultReports = await api('/api/yetkili/fault-reports');
  } catch {
    state.faultReports = [];
  }
  renderFaultReports();
}

function renderFaultReports() {
  const page = paginateItems(state.faultReports, 'faultReports');
  document.getElementById('faultReportList').innerHTML = emptyOr(page.items, item => `
    <div class="activity-item">
      <div>
        <strong>${escapeHtml(item.category)} · ${escapeHtml(item.dormitoryName || item.housingUnitName || '-')}</strong>
        <small>${escapeHtml(item.location)} - ${escapeHtml(item.description)}</small>
      </div>
      <small>${date(item.createdAt)}</small>
    </div>
  `);
  renderPager('faultReportPager', page.page, page.totalPages, next => { state.pages.faultReports = next; renderFaultReports(); });
}

async function loadUserFacilityAssignments() {
  try {
    state.userFacilityAssignments = await api('/api/yetkili/facility-assignments');
  } catch {
    state.userFacilityAssignments = [];
  }
  renderUserFacilityAssignments();
}

function renderUserFacilityAssignments() {
  const page = paginateItems(state.userFacilityAssignments, 'userFacilityAssignments');
  document.getElementById('facilityAssignmentRows').innerHTML = emptyTable(page.items, item => `
    <tr>
      <td><strong>${escapeHtml(item.userFullName)}</strong></td>
      <td>${roleDisplay(item.userRole)}</td>
      <td>${escapeHtml(item.dormitoryName || item.housingUnitName || '-')}</td>
      <td>${escapeHtml(item.assignedByName)}</td>
      <td>${date(item.assignedAt)}</td>
      <td>${item.isActive ? '<span class="badge badge-success">Aktif</span>' : '<span class="badge badge-muted">Pasif</span>'}</td>
    </tr>
  `);
  renderPager('facilityAssignmentPager', page.page, page.totalPages, next => { state.pages.userFacilityAssignments = next; renderUserFacilityAssignments(); });
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
  const page = paginateItems(state.announcements, 'announcements');
  document.getElementById('announcementRows').innerHTML = emptyTable(page.items, item => `
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
  renderPager('announcementPager', page.page, page.totalPages, next => { state.pages.announcements = next; renderAnnouncements(); });
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
  if (response.status === 401 || response.status === 403) {
    clearStoredTokens();
    window.location.href = '/index.html';
    throw new Error('Yetkisiz oturum. Lütfen tekrar giriş yapın.');
  }
  if (!response.ok) {
    const text = await response.text();
    let message = text || response.statusText;
    try {
      const payload = text ? JSON.parse(text) : null;
      message = payload?.message || payload?.title || message;
    } catch {
      // Text response is already usable.
    }
    throw new Error(message || response.statusText);
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

function requestStatusDisplay(status) {
  const map = {
    Open: 'Açık',
    InProgress: 'İşlemde',
    Resolved: 'Çözüldü',
    Rejected: 'Reddedildi'
  };
  return map[status] || status;
}

function roleDisplay(role) {
  const map = {
    Ogrenci: 'Öğrenci',
    Personel: 'Personel',
    Yetkili: 'Yetkili',
    Admin: 'Admin',
    TeknikPersonel: 'Teknik Personel',
    TemizlikPersoneli: 'Temizlik Personeli'
  };
  return map[role] || role;
}

function emptyTable(items, renderer) {
  return (items || []).length ? (items || []).map(renderer).join('') : '<tr><td colspan="10">Kayıt bulunamadı.</td></tr>';
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
  if (!host) return;
  host.innerHTML = `
    <button class="ghost-btn" ${page <= 1 ? 'disabled' : ''}>Önceki</button>
    <span class="badge badge-muted">${page} / ${totalPages}</span>
    <button class="ghost-btn" ${page >= totalPages ? 'disabled' : ''}>Sonraki</button>
  `;
  const [prev, next] = host.querySelectorAll('button');
  prev.addEventListener('click', () => onChange(page - 1));
  next.addEventListener('click', () => onChange(page + 1));
}

function paginateItems(items, key, pageSize = state.listPageSize) {
  const source = items || [];
  const totalPages = Math.max(1, Math.ceil(source.length / pageSize));
  const requestedPage = Number(state.pages[key] || 1);
  const page = Math.min(Math.max(1, requestedPage), totalPages);
  state.pages[key] = page;
  const start = (page - 1) * pageSize;
  return {
    items: source.slice(start, start + pageSize),
    page,
    totalPages
  };
}
