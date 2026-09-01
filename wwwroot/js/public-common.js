async function publicApi(path, options = {}) {
  const response = await fetch(path, options);
  const text = await response.text();
  const data = text ? JSON.parse(text) : null;
  if (!response.ok) {
    throw new Error(data?.message || text || response.statusText);
  }
  return data;
}

function publicEscape(value) {
  return String(value ?? '').replace(/[&<>"']/g, char => ({
    '&': '&amp;',
    '<': '&lt;',
    '>': '&gt;',
    '"': '&quot;',
    "'": '&#039;'
  }[char]));
}

function queryValue(name) {
  return new URLSearchParams(window.location.search).get(name) || '';
}

function setStatus(element, message, kind = '') {
  if (!element) return;
  element.textContent = message;
  element.className = `status ${kind}`.trim();
}
