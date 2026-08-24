const token = localStorage.getItem('token');
const user = JSON.parse(localStorage.getItem('currentUser') || '{}');
const role = String(user.role || getClaim('http://schemas.microsoft.com/ws/2008/06/identity/claims/role') || '').toLowerCase();
const technical = role === 'teknikpersonel';
const cleaning = role === 'temizlikpersoneli';
const state = { requests: [], periodic: [], tasks: [] };

document.addEventListener('DOMContentLoaded', () => {
  if (!token || (!technical && !cleaning)) { localStorage.clear(); location.replace('/index.html'); return; }
  document.getElementById('staffName').textContent = user.fullName || 'Personel';
  document.getElementById('staffRole').textContent = technical ? 'Teknik personel' : 'Temizlik görevlisi';
  document.getElementById('roleLabel').textContent = technical ? 'Teknik Personel Paneli' : 'Temizlik Görev Paneli';
  buildNav(); document.getElementById('logoutBtn').onclick = () => { localStorage.clear(); location.replace('/index.html'); };
  document.getElementById('closeModal').onclick = closeModal;
  document.getElementById('modalBackdrop').onclick = e => { if (e.target.id === 'modalBackdrop') closeModal(); };
  document.getElementById('taskForm').onsubmit = createTask;
  const add = document.getElementById('addMaintenanceBtn'); if (add) add.onclick = showMaintenanceForm;
  load();
});

function buildNav() {
  const items = technical ? [['dashboard','◫','Operasyon özeti'],['requests','⚒','Arıza talepleri'],['periodic','◌','Periyodik bakımlar']] : [['dashboard','◫','Bugünün özeti'],['tasks','✦','Görevlerim'],['newTask','＋','Görev / arıza bildir']];
  document.getElementById('staffNav').innerHTML = items.map(([id, icon, label], i) => `<button class="nav-btn ${i === 0 ? 'active' : ''}" data-page="${id}"><span class="nav-icon">${icon}</span>${label}</button>`).join('');
  document.querySelectorAll('[data-page]').forEach(b => b.onclick = () => openPage(b.dataset.page));
}
function openPage(id) { document.querySelectorAll('.page').forEach(x => x.classList.toggle('active', x.id === id)); document.querySelectorAll('[data-page]').forEach(x => x.classList.toggle('active', x.dataset.page === id)); document.getElementById('sectionTitle').textContent = ({dashboard:'Bugünün iş akışı',requests:'Arıza talepleri',periodic:'Periyodik bakımlar',tasks:'Görevlerim',newTask:'Görev kaydı'})[id]; }
async function load() { try { if (technical) { [state.requests, state.periodic] = await Promise.all([api('/api/staff/maintenance-requests'), api('/api/staff/periodic-maintenance')]); } else state.tasks = await api('/api/staff/cleaning-tasks'); render(); } catch (e) { toast(e.message || 'Veriler yüklenemedi.'); } }
function render() { technical ? renderTechnical() : renderCleaning(); }
function renderTechnical() {
  const active = state.requests.filter(x => x.status !== 'Resolved'); const due = state.periodic.filter(x => new Date(x.nextMaintenanceDate) <= new Date(Date.now()+3*864e5));
  overview([['Açık iş emri', active.length],['Yaklaşan bakım', due.length],['Bugün tamamlanan', state.requests.filter(x=>x.status==='Resolved' && sameDay(x.createdAt)).length]]);
  document.getElementById('dashboardContent').innerHTML = mini('Öncelikli arızalar', active.slice(0,4).map(x=>`${x.roomNumber} · ${x.category}`, 'İş emri yok.')) + mini('Yaklaşan bakım', due.slice(0,4).map(x=>`${x.systemName} · ${x.location}`, 'Yakın bakım yok.'));
  document.getElementById('requestList').innerHTML = state.requests.length ? state.requests.map(requestCard).join('') : empty('Açık ya da geçmiş arıza kaydı yok.');
  document.getElementById('periodicList').innerHTML = state.periodic.length ? state.periodic.map(periodicCard).join('') : empty('Henüz periyodik bakım planı yok.');
}
function renderCleaning() {
  const open = state.tasks.filter(x=>!x.isCompleted); overview([['Bekleyen görev',open.length],['Bugün tamamlanan',state.tasks.filter(x=>x.isCompleted&&sameDay(x.completedAt)).length],['Toplam kayıt',state.tasks.length]]);
  document.getElementById('dashboardContent').innerHTML = mini('Sıradaki görevler',open.slice(0,5).map(x=>`${x.taskType} · ${x.location}`),'Bekleyen görev yok.') + mini('Tamamlanan işler',state.tasks.filter(x=>x.isCompleted).slice(0,5).map(x=>`${x.taskType} · ${x.location}`),'Henüz tamamlanan görev yok.');
  document.getElementById('taskList').innerHTML = state.tasks.length ? state.tasks.map(taskCard).join('') : empty('Henüz görev kaydı yok.');
}
function overview(items){document.getElementById('overview').innerHTML=items.map(([a,b])=>`<article class="metric"><small>${a}</small><strong>${b}</strong></article>`).join('');}
function mini(title, items, none){return `<article class="mini-card"><h3>${title}</h3>${items.length?items.map(x=>`<div class="mini-row"><span>${esc(x)}</span><strong>→</strong></div>`).join(''):`<p>${none}</p>`}</article>`;}
function requestCard(x) { const status=x.status==='Resolved'?'done':x.status==='InProgress'?'progress':'open'; const target=x.targetRepairDate?`Hedef: ${date(x.targetRepairDate)} (${x.repairPeriodDays} gün)`: 'Onarım süresi bekliyor'; return `<article class="work-item"><div class="work-icon">⚒</div><div class="work-main"><strong>Oda ${esc(x.roomNumber)} · ${esc(x.category)}</strong><small>${esc(x.description)}</small></div><div class="work-meta"><small>${target}</small><span class="badge ${status}">${x.status==='Resolved'?'Çözüldü':x.status==='InProgress'?'İşlemde':'Açık'}</span>${x.status!=='Resolved'?`<div class="actions"><button class="secondary" onclick="scheduleRepair(${x.id})">Süre belirle</button><button class="primary" onclick="resolveRepair(${x.id})">Tamir edildi</button></div>`:''}</div></article>`; }
function periodicCard(x) { const late = new Date(x.nextMaintenanceDate) < new Date(); return `<article class="work-item"><div class="work-icon">◌</div><div class="work-main"><strong>${esc(x.systemName)} · ${esc(x.location)}</strong><small>${esc(x.notes || 'Not eklenmemiş')} · ${x.intervalDays} günde bir</small></div><div class="work-meta"><small>Sonraki bakım: ${date(x.nextMaintenanceDate)}</small><span class="badge ${late?'overdue':'open'}">${late?'Gecikmiş':'Planlı'}</span><div class="actions"><button class="primary" onclick="completeMaintenance(${x.id})">Bakım yapıldı</button></div></div></article>`; }
function taskCard(x) { return `<article class="work-item"><div class="work-icon">${iconFor(x.taskType)}</div><div class="work-main"><strong>${esc(x.taskType)} · ${esc(x.location)}</strong><small>${esc(x.notes || 'Açıklama eklenmemiş')}</small></div><div class="work-meta"><small>${x.isCompleted?'Tamamlandı: '+date(x.completedAt):'Kaydedildi: '+date(x.createdAt)}</small><span class="badge ${x.isCompleted?'done':'open'}">${x.isCompleted?'Tamamlandı':'Bekliyor'}</span>${!x.isCompleted?`<div class="actions"><button class="primary" onclick="completeTask(${x.id})">Tamamlandı işaretle</button></div>`:''}</div></article>`; }
async function scheduleRepair(id) { const days=prompt('Bu arıza kaç gün içinde onarılacak?', '3'); if (!days) return; try { await api(`/api/staff/maintenance-requests/${id}/schedule`,{method:'PATCH',body:JSON.stringify({repairPeriodDays:Number(days)})}); toast('Onarım süresi kaydedildi.'); load(); }catch(e){toast(e.message)} }
async function resolveRepair(id) { if(!confirm('Arızayı tamir edildi olarak işaretlemek istiyor musunuz?'))return; await api(`/api/staff/maintenance-requests/${id}/resolve`,{method:'PATCH'});toast('Arıza çözüldü olarak işaretlendi.');load(); }
async function completeMaintenance(id) { await api(`/api/staff/periodic-maintenance/${id}/complete`,{method:'PATCH'});toast('Bakım tamamlandı; sonraki tarih güncellendi.');load(); }
async function completeTask(id) { await api(`/api/staff/cleaning-tasks/${id}/complete`,{method:'PATCH'});toast('Görev tamamlandı.');load(); }
function showMaintenanceForm(){document.getElementById('modalBody').innerHTML=`<p class="eyebrow">YENİ PLAN</p><h2>Periyodik bakım planla</h2><form id="maintenanceForm" class="form-grid"><label>Sistem<input name="systemName" placeholder="Örn. Yangın sistemi" required></label><label>Konum<input name="location" placeholder="Örn. A Blok" required></label><label>Periyot (gün)<input type="number" min="1" name="intervalDays" value="30" required></label><label>Sonraki bakım<input type="date" name="nextMaintenanceDate" required></label><label class="full">Not<textarea name="notes"></textarea></label><button class="primary">Planı kaydet</button></form>`;document.getElementById('maintenanceForm').onsubmit=async e=>{e.preventDefault();const v=Object.fromEntries(new FormData(e.target));v.intervalDays=Number(v.intervalDays);await api('/api/staff/periodic-maintenance',{method:'POST',body:JSON.stringify(v)});closeModal();toast('Bakım planı eklendi.');load()};document.getElementById('modalBackdrop').classList.add('show');}
async function createTask(e){e.preventDefault();try{await api('/api/staff/cleaning-tasks',{method:'POST',body:JSON.stringify(Object.fromEntries(new FormData(e.target)))});e.target.reset();toast('Görev kaydedildi.');load();openPage('tasks')}catch(err){toast(err.message)}}
function api(path,options={}){return fetch(path,{...options,headers:{Authorization:`Bearer ${token}`,'Content-Type':'application/json',...(options.headers||{})}}).then(async r=>{if(!r.ok)throw new Error(await r.text()||'İşlem gerçekleştirilemedi.');return r.status===204?null:r.json()})}
function getClaim(key){try{return JSON.parse(atob(token.split('.')[1].replace(/-/g,'+').replace(/_/g,'/')))[key]}catch{return null}} function closeModal(){document.getElementById('modalBackdrop').classList.remove('show')}function empty(t){return `<p>${t}</p>`}function date(x){return x?new Date(x).toLocaleDateString('tr-TR'): '-'}function sameDay(x){return x&&new Date(x).toDateString()===new Date().toDateString()}function esc(x){return String(x??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#039;'}[c]))}function iconFor(x){return x.includes('Çöp')?'♻':x.includes('düzen')?'↔':x.includes('Arıza')?'⚑':'✦'}function toast(t){const e=document.createElement('div');e.className='toast';e.textContent=t;document.getElementById('toastHost').append(e);setTimeout(()=>e.remove(),3200)}
