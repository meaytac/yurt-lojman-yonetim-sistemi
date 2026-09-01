(() => {
  const PAGE_META = {
    profile: ['Profilim', 'Kişisel bilgileriniz, konaklama durumunuz ve hesap güvenliğiniz.'],
    borc: ['Borç Takip ve Ödeme', 'Dönem borçlarınızı ve vadesi gelmiş ödemelerinizi takip edin.'],
    ariza: ['Arıza Talebi', 'Konaklama alanınızla ilgili arıza kayıtlarını oluşturun ve izleyin.'],
    basvuru: ['Oda Başvurusu', 'Konaklama başvurularınızın güncel durumunu görüntüleyin.'],
    degisim: ['Oda Değişimi', 'Oda değişim taleplerinizi tek alandan takip edin.'],
    duyurular: ['Duyurular', 'Yurt ve lojman yönetiminden gelen duyuruları görüntüleyin.'],
    sikayet: ['Şikayet & Öneri', 'Geri bildirimlerinizi yönetime iletin ve geçmiş kayıtları izleyin.']
  };

  const state = {
    token: localStorage.getItem('token'),
    currentUser: readJson('currentUser') || {},
    payments: [],
    requests: [],
    applications: [],
    announcements: [],
    accommodation: null,
    feedback: []
  };

  document.addEventListener('DOMContentLoaded', init);

  function init() {
    bindLogin();
    bindNavigation();
    bindProfileActions();
    bindForms();
    bindById('logoutBtn', 'click', logout);

    if (!state.token) {
      showLogin();
      return;
    }

    if (redirectIfWrongRole()) return;

    showApp();
    hydrateUser();
    switchPage('profile');
    loadAll();
    checkMustChangePassword();

    window.setInterval(() => {
      if (document.hidden) return;
      loadAll(false);
    }, 15000);
  }

  function bindLogin() {
    bindById('loginButton', 'click', async event => {
      event.preventDefault();
      const email = valueOf('loginEmail');
      const password = valueOf('loginPassword');
      if (!email || !password) {
        toast('E-posta ve şifre alanlarını doldurun.', true);
        return;
      }

      try {
        const data = await api('/api/auth/login', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ email, password })
        }, false);

        localStorage.setItem('token', data.token);
        localStorage.setItem('currentUser', JSON.stringify({
          userId: data.userId,
          fullName: data.fullName,
          email: data.email,
          role: data.role,
          phoneNumber: data.phoneNumber || '',
          mustChangePassword: data.mustChangePassword
        }));

        state.token = data.token;
        state.currentUser = readJson('currentUser') || {};
        if (redirectIfWrongRole()) return;

        showApp();
        hydrateUser();
        switchPage('profile');
        loadAll();
        if (data.mustChangePassword) showForceChangePasswordModal();
      } catch (error) {
        toast(error.message || 'Giriş yapılamadı.', true);
      }
    });
  }

  function bindNavigation() {
    document.querySelectorAll('.nav-item[data-page]').forEach(item => {
      item.addEventListener('click', () => switchPage(item.dataset.page));
    });
  }

  function bindProfileActions() {
    bindById('fillPersonalInfoBtn', 'click', openPersonalInfoForm);
    bindById('editPersonalInfoBtn', 'click', openPersonalInfoForm);
    bindById('cancelPersonalInfoBtn', 'click', closePersonalInfoForm);
    bindById('savePersonalInfoBtn', 'click', savePersonalInfo);
    bindById('changeEmailBtn', 'click', openEmailEdit);
    bindById('cancelEmailBtn', 'click', closeEmailEdit);
    bindById('saveEmailBtn', 'click', saveEmail);
    bindById('changePasswordBtn', 'click', openPasswordEdit);
    bindById('cancelPasswordBtn', 'click', closePasswordEdit);
    bindById('savePasswordBtn', 'click', savePassword);
    bindById('submitSikayetBtn', 'click', submitFeedback);
  }

  function bindForms() {
    bindById('requestForm', 'submit', submitRequest);
    bindById('applicationForm', 'submit', submitApplication);
    bindById('quickPaymentButton', 'click', payLatestDue);
  }

  function showLogin() {
    byId('loginScreen')?.style.setProperty('display', 'flex');
    byId('mainApp')?.classList.remove('active');
  }

  function showApp() {
    byId('loginScreen')?.style.setProperty('display', 'none');
    byId('mainApp')?.classList.add('active');
    document.querySelectorAll('.nav-item.disabled').forEach(item => item.classList.remove('disabled'));
    byId('profileIncompleteBanner')?.style.setProperty('display', 'none');
  }

  function redirectIfWrongRole() {
    const role = normalizeRole(state.currentUser.role || claim('role') || claim('http://schemas.microsoft.com/ws/2008/06/identity/claims/role'));
    if (!role || role === 'ogrenci' || role === 'personel') return false;

    const target = role === 'admin'
      ? '/admin.html'
      : role === 'yetkili'
        ? '/yetkili.html'
        : role === 'teknikpersonel' || role === 'temizlikpersoneli'
          ? '/staff.html'
          : '/index.html';
    window.location.replace(target);
    return true;
  }

  function hydrateUser() {
    const claims = decodeToken(state.token);
    const fullName = state.currentUser.fullName || claims.fullName || 'Öğrenci';
    const email = state.currentUser.email || claims.email || claim('email') || '-';
    const role = state.currentUser.role || claim('role') || 'Öğrenci';
    const studentNo = state.currentUser.studentStaffNo || state.currentUser.studentId || '-';
    const department = state.currentUser.department || '-';
    const phone = state.currentUser.phoneNumber || state.currentUser.phone || '-';
    const initials = fullName.split(/\s+/).filter(Boolean).slice(0, 2).map(part => part[0]).join('').toUpperCase() || '?';

    setText('sidebarName', fullName);
    setText('sidebarEmail', email);
    setText('sidebarAvatar', initials);
    setText('topbarName', fullName);
    setText('topbarRole', roleDisplay(role));
    setText('displayFullName', fullName);
    setText('displayEmail', email);
    setText('currentEmailLabel', email);
    setText('displayStudentId', studentNo);
    setText('displayDepartment', department);
    setText('displayPhone', phone);
    setValue('editFullName', fullName);
    setValue('editStudentId', studentNo === '-' ? '' : studentNo);
    setValue('editDepartment', department === '-' ? '' : department);
    setValue('editPhone', phone === '-' ? '' : phone);
    setValue('profileEmail', email === '-' ? '' : email);
    setText('personalInfoBadge', 'Hesabınız');
    byId('personalInfoBadge')?.classList.add('approved');
    byId('personalInfoDisplay')?.style.setProperty('display', 'block');
    byId('personalInfoEdit')?.style.setProperty('display', 'none');

    state.feedback = readJson(feedbackStorageKey()) || [];
    renderFeedback();
  }

  function switchPage(pageId = 'profile') {
    document.querySelectorAll('.page-section').forEach(section => section.classList.remove('active'));
    byId(`page-${pageId}`)?.classList.add('active');
    document.querySelectorAll('.nav-item[data-page]').forEach(item => {
      item.classList.toggle('active', item.dataset.page === pageId);
    });
    const [title, subtitle] = PAGE_META[pageId] || PAGE_META.profile;
    setText('sectionTitle', title);
    setText('sectionSubtitle', subtitle);
  }

  async function loadAll(showErrors = true) {
    const jobs = [
      loadPayments(showErrors),
      loadRequests(showErrors),
      loadApplications(showErrors),
      loadAnnouncements(showErrors),
      loadAccommodationInfo(showErrors)
    ];
    await Promise.allSettled(jobs);
  }

  async function loadPayments(showErrors) {
    const root = byId('paymentList');
    const quickPaymentInfo = byId('quickPaymentInfo');
    const quickPaymentButton = byId('quickPaymentButton');
    if (!root) return;

    try {
      state.payments = await api('/api/payments/mine');
      root.className = 'student-list';
      root.innerHTML = state.payments.length
        ? state.payments.map(renderPayment).join('')
        : empty('Kayıtlı borç bulunmuyor.');

      const latestDue = state.payments
        .filter(item => String(item.status).toLowerCase() !== 'paid' && new Date(item.dueDate) <= new Date())
        .sort((left, right) => new Date(right.dueDate) - new Date(left.dueDate))[0];

      if (latestDue) {
        setText('quickPaymentInfo', `Seçili borç: ${latestDue.description} - ${money(latestDue.amount)}`);
        if (quickPaymentButton) quickPaymentButton.disabled = false;
      } else {
        setText('quickPaymentInfo', 'Vadesi gelmiş ödenmemiş borç bulunmuyor.');
        if (quickPaymentButton) quickPaymentButton.disabled = true;
      }
    } catch (error) {
      root.innerHTML = empty(error.message);
      if (quickPaymentInfo) quickPaymentInfo.textContent = 'Borç bilgisi yüklenemedi.';
      if (quickPaymentButton) quickPaymentButton.disabled = true;
      if (showErrors) toast(error.message || 'Borçlar yüklenemedi.', true);
    }
  }

  async function loadRequests(showErrors) {
    const root = byId('requestsList');
    if (!root) return;
    try {
      state.requests = await api('/api/requests/mine');
      root.className = 'student-list';
      root.innerHTML = state.requests.length
        ? state.requests.map(renderRequest).join('')
        : empty('Henüz arıza talebiniz bulunmuyor.');
    } catch (error) {
      root.innerHTML = empty(error.message);
      if (showErrors) toast(error.message || 'Arıza talepleri yüklenemedi.', true);
    }
  }

  async function loadApplications(showErrors) {
    const root = byId('myApplications');
    if (!root) return;
    try {
      state.applications = await api('/api/applications/mine');
      root.className = 'student-list';
      root.innerHTML = state.applications.length
        ? state.applications.map(renderApplication).join('')
        : empty('Henüz başvurunuz bulunmuyor.');
    } catch (error) {
      root.innerHTML = empty(error.message);
      if (showErrors) toast(error.message || 'Başvurular yüklenemedi.', true);
    }
  }

  async function loadAnnouncements(showErrors) {
    const root = byId('announcementsList');
    if (!root) return;
    try {
      state.announcements = await api('/api/announcements');
      root.className = 'student-list';
      root.innerHTML = state.announcements.length
        ? state.announcements.map(renderAnnouncement).join('')
        : empty('Yayınlanmış duyuru bulunmuyor.');
    } catch (error) {
      root.innerHTML = empty(error.message);
      if (showErrors) toast(error.message || 'Duyurular yüklenemedi.', true);
    }
  }

  async function loadAccommodationInfo() {
    const root = byId('accommodationInfo');
    if (!root) return;
    try {
      state.accommodation = await api('/api/placements/mine');
      root.className = 'student-list';
      root.innerHTML = `
        <div class="list-row"><span class="info-label">Tesis</span><span class="info-value"><strong>${esc(state.accommodation.facilityName)}</strong> (${esc(state.accommodation.facilityType)})</span></div>
        <div class="list-row"><span class="info-label">Blok</span><span class="info-value"><strong>${esc(state.accommodation.blockName)}</strong></span></div>
        <div class="list-row"><span class="info-label">Kat</span><span class="info-value"><strong>${esc(state.accommodation.floorNumber)}</strong></span></div>
        <div class="list-row"><span class="info-label">Oda No</span><span class="info-value"><strong>${esc(state.accommodation.roomNumber)}</strong></span></div>
        <div class="list-row"><span class="info-label">Giriş Tarihi</span><span class="info-value"><strong>${date(state.accommodation.checkInDate)}</strong></span></div>`;
    } catch {
      state.accommodation = null;
      root.className = 'empty-state';
      root.innerHTML = '<i class="fas fa-info-circle"></i><p>Henüz bir odaya yerleştirilmemişsiniz.</p>';
    }
  }

  function renderPayment(item) {
    const [label, badge] = status(item.status);
    return `<div class="list-row"><span>${esc(item.description)}<br><small>${date(item.dueDate)}</small></span><span class="amount">${money(item.amount)}</span><span class="badge ${badge}">${label}</span></div>`;
  }

  function renderRequest(item) {
    const [label, badge] = status(item.status);
    return `<article class="ticket-card"><div><strong><i class="fas fa-wrench"></i> ${esc(item.category)}</strong><p>${esc(item.description)}</p><small>${date(item.createdAt)} - Oda ${esc(item.roomNumber || item.roomId)}</small></div><span class="badge ${badge}">${label}</span></article>`;
  }

  function renderApplication(item) {
    const [label, badge] = status(item.status);
    return `<div class="list-row"><span>${esc(item.accommodationType)} başvurusu<br><small>${date(item.createdAt)}</small></span><strong>#${esc(item.id)}</strong><span class="badge ${badge}">${label}</span></div>`;
  }

  function renderAnnouncement(item) {
    return `<article class="announcement-item"><i class="fas fa-circle-info"></i><span><strong>${esc(item.title)}</strong> - ${esc(item.content)}<br><small>${date(item.createdAt)}</small></span></article>`;
  }

  async function submitRequest(event) {
    event.preventDefault();
    const form = event.currentTarget;
    try {
      const created = await api('/api/requests', { method: 'POST', body: new FormData(form) });
      state.requests = [created, ...state.requests];
      form.reset();
      await loadRequests(false);
      toast('Arıza talebiniz kaydedildi.');
    } catch (error) {
      toast(error.message || 'Arıza talebi kaydedilemedi.', true);
    }
  }

  async function submitApplication(event) {
    event.preventDefault();
    const form = event.currentTarget;
    try {
      const created = await api('/api/applications', { method: 'POST', body: new FormData(form) });
      state.applications = [created, ...state.applications];
      form.reset();
      await loadApplications(false);
      toast('Başvurunuz kaydedildi.');
    } catch (error) {
      toast(error.message || 'Başvuru kaydedilemedi.', true);
    }
  }

  async function payLatestDue(event) {
    const button = event.currentTarget;
    button.disabled = true;
    try {
      await api('/api/payments/mine/pay-latest-due', { method: 'POST' });
      await loadPayments(false);
      toast('Borç ödendi olarak işaretlendi.');
    } catch (error) {
      toast(error.message || 'Ödeme işlemi yapılamadı.', true);
      await loadPayments(false);
    }
  }

  function openPersonalInfoForm() {
    byId('personalInfoDisplay')?.style.setProperty('display', 'none');
    byId('personalInfoEdit')?.style.setProperty('display', 'block');
    byId('emptyState')?.style.setProperty('display', 'none');
    byId('personalInfoForm')?.style.setProperty('display', 'block');
  }

  function closePersonalInfoForm() {
    byId('personalInfoDisplay')?.style.setProperty('display', 'block');
    byId('personalInfoEdit')?.style.setProperty('display', 'none');
  }

  async function savePersonalInfo() {
    const fullName = valueOf('editFullName');
    const studentId = valueOf('editStudentId');
    const department = valueOf('editDepartment');
    const phone = valueOf('editPhone');
    if (!fullName || !phone) {
      showInlineAlert('Ad soyad ve telefon alanları zorunludur.', 'error');
      return;
    }

    try {
      await api('/api/auth/update-phone', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ phoneNumber: phone })
      });

      state.currentUser = { ...state.currentUser, fullName, studentStaffNo: studentId, department, phoneNumber: phone };
      localStorage.setItem('currentUser', JSON.stringify(state.currentUser));
      hydrateUser();
      closePersonalInfoForm();
      showInlineAlert('Profil bilgileriniz güncellendi.', 'success');
      toast('Profil bilgileriniz güncellendi.');
    } catch (error) {
      showInlineAlert(error.message || 'Profil güncellenemedi.', 'error');
    }
  }

  function openEmailEdit() {
    byId('emailDisplay')?.style.setProperty('display', 'none');
    byId('emailEditForm')?.style.setProperty('display', 'block');
  }

  function closeEmailEdit() {
    byId('emailDisplay')?.style.setProperty('display', 'block');
    byId('emailEditForm')?.style.setProperty('display', 'none');
  }

  function saveEmail() {
    const nextEmail = valueOf('profileEmail');
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(nextEmail)) {
      showInlineAlert('Geçerli bir e-posta adresi girin.', 'error');
      return;
    }

    state.currentUser = { ...state.currentUser, email: nextEmail };
    localStorage.setItem('currentUser', JSON.stringify(state.currentUser));
    hydrateUser();
    closeEmailEdit();
    showInlineAlert('E-posta görünümü güncellendi.', 'success');
  }

  function openPasswordEdit() {
    byId('passwordDisplay')?.style.setProperty('display', 'none');
    byId('passwordEditForm')?.classList.add('active');
    setValue('currentPassword', '');
    setValue('newPassword', '');
    setValue('confirmPassword', '');
  }

  function closePasswordEdit() {
    byId('passwordDisplay')?.style.setProperty('display', 'block');
    byId('passwordEditForm')?.classList.remove('active');
  }

  async function savePassword() {
    const currentPassword = valueOf('currentPassword');
    const newPassword = valueOf('newPassword');
    const confirmPassword = valueOf('confirmPassword');
    if (!currentPassword || !newPassword || !confirmPassword) {
      showInlineAlert('Tüm şifre alanlarını doldurun.', 'error');
      return;
    }
    if (newPassword.length < 6) {
      showInlineAlert('Yeni şifre en az 6 karakter olmalıdır.', 'error');
      return;
    }
    if (newPassword !== confirmPassword) {
      showInlineAlert('Yeni şifreler eşleşmiyor.', 'error');
      return;
    }

    try {
      await api('/api/auth/change-password', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ currentPassword, newPassword })
      });
      closePasswordEdit();
      showInlineAlert('Şifreniz başarıyla güncellendi.', 'success');
      toast('Şifreniz başarıyla güncellendi.');
    } catch (error) {
      showInlineAlert(error.message || 'Şifre güncellenemedi.', 'error');
    }
  }

  function submitFeedback() {
    const type = valueOf('sikayetType') || 'sikayet';
    const subject = valueOf('sikayetSubject');
    const description = valueOf('sikayetDescription');
    if (subject.length < 5 || description.length < 10) {
      showFeedbackAlert('Konu en az 5, açıklama en az 10 karakter olmalıdır.', 'error');
      return;
    }

    state.feedback.unshift({
      id: Date.now(),
      type,
      subject,
      description,
      date: new Date().toISOString(),
      status: 'pending'
    });
    localStorage.setItem(feedbackStorageKey(), JSON.stringify(state.feedback));
    setValue('sikayetSubject', '');
    setValue('sikayetDescription', '');
    setValue('sikayetType', 'sikayet');
    renderFeedback();
    showFeedbackAlert('Bildiriminiz kaydedildi.', 'success');
    toast('Bildiriminiz kaydedildi.');
  }

  function renderFeedback() {
    const root = byId('sikayetList');
    if (!root) return;
    if (!state.feedback.length) {
      root.innerHTML = empty('Henüz şikayet veya öneri bildirimi yapılmamış.');
      return;
    }

    root.innerHTML = state.feedback.map(item => {
      const typeLabel = item.type === 'sikayet' ? 'Şikayet' : 'Öneri';
      const typeClass = item.type === 'sikayet' ? 'complaint' : 'suggestion';
      const [label, badge] = status(item.status);
      return `<article class="complaint-card pending">
        <div class="complaint-header">
          <div class="complaint-title"><span class="type-badge ${typeClass}">${typeLabel}</span><span>${esc(item.subject)}</span></div>
          <span class="complaint-date">${date(item.date)}</span>
        </div>
        <div class="complaint-description">${esc(item.description)}</div>
        <span class="badge ${badge}">${label}</span>
      </article>`;
    }).join('');
  }

  async function checkMustChangePassword() {
    try {
      const mustChange = await api('/api/auth/must-change-password');
      if (mustChange) showForceChangePasswordModal();
    } catch {
      // Bu kontrol panel açılışını engellememeli.
    }
  }

  function showForceChangePasswordModal() {
    if (byId('forceChangeModal')) return;
    const modal = document.createElement('div');
    modal.id = 'forceChangeModal';
    modal.className = 'modal-backdrop show';
    modal.innerHTML = `
      <section class="modal student-password-modal">
        <header>
          <h2>İlk Giriş - Şifre Değiştirme Zorunluluğu</h2>
        </header>
        <div id="modalBody">
          <p class="modal-note">Güvenliğiniz için atanan ilk şifrenizi değiştirmeniz gerekmektedir.</p>
          <form id="forceChangeForm" class="form-grid">
            <label>Mevcut Şifre<input type="password" name="currentPassword" required autocomplete="current-password"></label>
            <label>Yeni Şifre<input type="password" name="newPassword" required minlength="6" autocomplete="new-password"></label>
            <label>Yeni Şifre (Tekrar)<input type="password" name="confirmPassword" required autocomplete="new-password"></label>
            <div class="form-actions">
              <button class="primary-btn" type="submit">Şifreyi Değiştir ve Devam Et</button>
            </div>
          </form>
        </div>
      </section>`;
    document.body.appendChild(modal);
    bindById('forceChangeForm', 'submit', async event => {
      event.preventDefault();
      const form = event.currentTarget;
      const data = Object.fromEntries(new FormData(form).entries());
      if (data.newPassword !== data.confirmPassword) {
        toast('Şifreler eşleşmiyor.', true);
        return;
      }
      try {
        await api('/api/auth/change-password', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ currentPassword: data.currentPassword, newPassword: data.newPassword })
        });
        modal.remove();
        toast('Şifreniz başarıyla değiştirildi.');
      } catch (error) {
        toast(error.message || 'Şifre değiştirilemedi.', true);
      }
    });
  }

  async function api(path, options = {}, requireAuth = true) {
    const headers = { ...(options.headers || {}) };
    if (requireAuth && state.token) headers.Authorization = `Bearer ${state.token}`;
    const response = await fetch(path, { ...options, headers });
    if (!response.ok) {
      throw new Error(await errorMessage(response));
    }
    if (response.status === 204) return null;
    const contentType = response.headers.get('content-type') || '';
    return contentType.includes('application/json') ? response.json() : response.text();
  }

  async function errorMessage(response) {
    const contentType = response.headers.get('content-type') || '';
    if (contentType.includes('application/json')) {
      const data = await response.json();
      if (data?.message) return data.message;
      if (Array.isArray(data)) return data.map(item => item.description || item.code || String(item)).join(', ');
      if (data?.errors) return Object.values(data.errors).flat().join(', ');
      return JSON.stringify(data);
    }
    return await response.text() || response.statusText || 'İşlem gerçekleştirilemedi.';
  }

  function logout() {
    localStorage.removeItem('token');
    localStorage.removeItem('currentUser');
    window.location.replace('/index.html');
  }

  function bindById(id, eventName, handler) {
    byId(id)?.addEventListener(eventName, handler);
  }

  function byId(id) {
    return document.getElementById(id);
  }

  function setText(id, value) {
    const element = byId(id);
    if (element) element.textContent = value ?? '';
  }

  function setValue(id, value) {
    const element = byId(id);
    if (element) element.value = value ?? '';
  }

  function valueOf(id) {
    return byId(id)?.value?.trim() || '';
  }

  function showInlineAlert(message, type) {
    const element = byId('alertMessage');
    if (!element) return;
    element.textContent = message;
    element.className = `alert-message alert-${type}`;
    element.style.display = 'block';
    window.setTimeout(() => { element.style.display = 'none'; }, 4200);
  }

  function showFeedbackAlert(message, type) {
    const element = byId('sikayetAlert');
    if (!element) return;
    element.textContent = message;
    element.className = `alert-message alert-${type}`;
    element.style.display = 'block';
    window.setTimeout(() => { element.style.display = 'none'; }, 4200);
  }

  function toast(message, isError = false) {
    const host = byId('toastHost');
    if (!host) return;
    const item = document.createElement('div');
    item.className = `toast${isError ? ' error' : ''}`;
    item.textContent = message;
    host.append(item);
    window.setTimeout(() => item.remove(), 3600);
  }

  function empty(message) {
    return `<div class="empty-state"><i class="fas fa-inbox"></i><p>${esc(message)}</p></div>`;
  }

  function status(value) {
    const key = String(value || '').toLowerCase();
    const values = {
      paid: ['Ödendi', 'badge-success'],
      unpaid: ['Planlandı', 'badge-info'],
      overdue: ['Beklemede', 'badge-warning'],
      pending: ['Beklemede', 'badge-warning'],
      approved: ['Onaylandı', 'badge-success'],
      rejected: ['Reddedildi', 'badge-danger'],
      open: ['Açık', 'badge-warning'],
      inprogress: ['İşlemde', 'badge-info'],
      completed: ['Tamamlandı', 'badge-success'],
      resolved: ['Çözüldü', 'badge-success']
    };
    return values[key] || [esc(value || 'Beklemede'), 'badge-muted'];
  }

  function roleDisplay(role) {
    const key = normalizeRole(role);
    if (key === 'personel') return 'Personel Paneli';
    return 'Öğrenci Paneli';
  }

  function normalizeRole(role) {
    return String(role || '').trim().toLocaleLowerCase('tr-TR');
  }

  function feedbackStorageKey() {
    return `studentFeedback:${state.currentUser.userId || state.currentUser.email || 'local'}`;
  }

  function readJson(key) {
    try {
      return JSON.parse(localStorage.getItem(key) || 'null');
    } catch {
      return null;
    }
  }

  function decodeToken(token) {
    if (!token) return {};
    try {
      const base64 = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
      return JSON.parse(window.atob(base64));
    } catch {
      return {};
    }
  }

  function claim(name) {
    return decodeToken(state.token)[name];
  }

  function money(value) {
    return Number(value || 0).toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' });
  }

  function date(value) {
    return value ? new Date(value).toLocaleDateString('tr-TR') : '-';
  }

  function esc(value) {
    return String(value ?? '').replace(/[&<>"']/g, char => ({
      '&': '&amp;',
      '<': '&lt;',
      '>': '&gt;',
      '"': '&quot;',
      "'": '&#039;'
    }[char]));
  }
})();
