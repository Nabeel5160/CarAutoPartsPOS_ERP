window.capTheme = {
  apply: function (mode, accent) {
    document.documentElement.setAttribute('data-theme', mode || 'dark');
    document.documentElement.setAttribute('data-accent', accent || 'amber');
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
