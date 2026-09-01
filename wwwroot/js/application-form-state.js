(function exposeApplicationFormState(global) {
  const allCampusesValue = '';

  function normalizeText(value) {
    return String(value || '').trim().toLocaleLowerCase('tr-TR');
  }

  function responseItems(response) {
    if (Array.isArray(response)) return response;
    if (Array.isArray(response?.items)) return response.items;
    return [];
  }

  function usableFacilities(response) {
    return responseItems(response).filter(item => item && item.isApplicationOpen !== false);
  }

  function campusOptions(facilities, type) {
    const byKey = new Map();
    usableFacilities(facilities)
      .filter(item => item.type === type)
      .forEach(item => {
        const display = String(item.campusLocation || '').trim();
        const key = normalizeText(display);
        if (key && !byKey.has(key)) byKey.set(key, display);
      });

    return Array.from(byKey.values()).sort((a, b) => a.localeCompare(b, 'tr-TR'));
  }

  function filterFacilities(facilities, type, campus) {
    const campusKey = normalizeText(campus);
    return usableFacilities(facilities).filter(item => item.type === type)
      .filter(item => !campusKey || normalizeText(item.campusLocation) === campusKey);
  }

  function selectedIsVisible(selected, visibleFacilities) {
    if (!selected) return false;
    return visibleFacilities.some(item => item.type === selected.type && String(item.id) === String(selected.id));
  }

  function modalActions(mode, hasReference) {
    return {
      showCancel: mode !== 'success',
      showSubmit: mode === 'ready' || mode === 'error' || mode === 'submitting',
      disableSubmit: mode === 'loading-facilities' || mode === 'submitting',
      showCopy: mode === 'success' && hasReference,
      showTrack: mode === 'success' && hasReference,
      showClose: mode === 'success'
    };
  }

  const api = {
    allCampusesValue,
    normalizeText,
    responseItems,
    usableFacilities,
    campusOptions,
    filterFacilities,
    selectedIsVisible,
    modalActions
  };

  global.ApplicationFormState = api;
  if (typeof module !== 'undefined' && module.exports) {
    module.exports = api;
  }
})(typeof window !== 'undefined' ? window : globalThis);
