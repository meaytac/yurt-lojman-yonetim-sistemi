async function loadPublicFacilities() {
  const host = document.getElementById('facilityList');
  if (!host) return;
  try {
    const facilities = await publicApi('/api/public/facilities');
    host.innerHTML = facilities.length ? facilities.map(facility => `
      <button class="facility-item" type="button" data-type="${facility.type}" data-id="${facility.id}" data-open="${facility.isApplicationOpen}">
        <strong>${publicEscape(facility.name)}</strong>
        <span>${publicEscape(facility.publicDescription || facility.campusLocation)}</span>
        <span class="facility-meta">
          <span>${publicEscape(facility.type)}</span>
          <span>${facility.availableCapacity} boş kapasite</span>
          <span>${facility.isApplicationOpen ? 'Başvuru açık' : 'Başvuru kapalı'}</span>
        </span>
      </button>`).join('') : '<p class="status">Yayında tesis bulunamadı.</p>';

    host.querySelectorAll('.facility-item').forEach(button => {
      button.addEventListener('click', () => selectFacility(button));
    });
  } catch (error) {
    host.innerHTML = `<p class="status error">${publicEscape(error.message)}</p>`;
  }
}

function selectFacility(button) {
  document.querySelectorAll('.facility-item').forEach(item => item.classList.remove('is-selected'));
  button.classList.add('is-selected');
  const type = button.dataset.type;
  const id = button.dataset.id;
  document.getElementById('accommodationType').value = type;
  document.getElementById('dormitoryId').value = type === 'Yurt' ? id : '';
  document.getElementById('housingUnitId').value = type === 'Lojman' ? id : '';
}

async function submitPublicApplication(event) {
  event.preventDefault();
  const state = document.getElementById('visitorState');
  if (!document.getElementById('accommodationType').value) {
    setStatus(state, 'Lütfen başvuru yapılacak tesisi seçin.', 'error');
    return;
  }

  try {
    const data = await publicApi('/api/public/applications', {
      method: 'POST',
      body: new FormData(event.currentTarget)
    });
    event.currentTarget.reset();
    document.querySelectorAll('.facility-item').forEach(item => item.classList.remove('is-selected'));
    setStatus(state, `${data.message} Referans kodunuz: ${data.referenceCode}`, 'success');
  } catch (error) {
    setStatus(state, error.message, 'error');
  }
}

document.addEventListener('DOMContentLoaded', () => {
  loadPublicFacilities();
  document.getElementById('visitorApplicationForm')?.addEventListener('submit', submitPublicApplication);
});
