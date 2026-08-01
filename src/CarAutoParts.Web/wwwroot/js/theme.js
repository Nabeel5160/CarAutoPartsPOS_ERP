window.capTheme = {
  storageKey: 'cap.theme.raw',

  apply: function (mode, accent) {
    document.documentElement.setAttribute('data-theme', mode || 'dark');
    document.documentElement.setAttribute('data-accent', accent || 'amber');
  },

  setStored: function (value) {
    try { localStorage.setItem(window.capTheme.storageKey, value || 'dark:amber'); } catch (e) { /* private mode */ }
  },

  getStored: function () {
    try { return localStorage.getItem(window.capTheme.storageKey); } catch (e) { return null; }
  },

  /** Apply saved preference before Blazor boots (no flash of wrong theme). */
  bootFromStorage: function () {
    try {
      var raw = localStorage.getItem(window.capTheme.storageKey);
      if (!raw) {
        // Migrate legacy Blazored key if present as plain "mode:accent"
        var legacy = localStorage.getItem('cap.theme');
        if (legacy && legacy.indexOf(':') > 0 && legacy.indexOf('{') < 0) {
          raw = legacy.replace(/^"|"$/g, '');
          localStorage.setItem(window.capTheme.storageKey, raw);
        }
      }
      if (raw && raw.indexOf(':') > 0) {
        var parts = raw.replace(/^"|"$/g, '').split(':');
        window.capTheme.apply(parts[0], parts[1] || 'amber');
      }
    } catch (e) { /* ignore */ }
  }
};

window.capDownload = function (base64, fileName, contentType) {
  const bytes = Uint8Array.from(atob(base64), c => c.charCodeAt(0));
  const blob = new Blob([bytes], { type: contentType || 'application/octet-stream' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = fileName || 'download.bin';
  a.click();
  URL.revokeObjectURL(url);
};

window.capSetLocale = function (culture, dir) {
  document.documentElement.lang = culture || 'en';
  document.documentElement.dir = dir || 'ltr';
};
