window.capBrand = {
  apply: function (appName, shortName, accentWord, logoUrl, theme, accent) {
    try {
      if (appName) document.title = appName + (appName.indexOf('ERP') >= 0 ? '' : ' ERP');

      function applyTo(root, accentInline) {
        if (!root) return;
        var nameHtml = (shortName || 'App') + ' <span' +
          (accentInline ? ' style="color:#f5a623"' : '') + '>' + (accentWord || '') + '</span>';
        var textEl = root.querySelector('.cap-brand-text');
        if (textEl) textEl.innerHTML = nameHtml;

        var img = root.querySelector('img.cap-brand-logo');
        if (logoUrl) {
          if (!img) {
            img = document.createElement('img');
            img.className = 'cap-brand-logo';
            img.alt = '';
            root.insertBefore(img, root.firstChild);
          }
          if (img.getAttribute('src') !== logoUrl) img.src = logoUrl;
        } else if (img) {
          img.remove();
        }
      }

      applyTo(document.querySelector('.cap-brand'), false);
      applyTo(document.querySelector('.cap-login-brand'), true);

      var splash = document.querySelector('#app .login-panel strong');
      if (splash) {
        splash.innerHTML = (shortName || 'App') + ' <span style="color:#f5a623">' + (accentWord || '') + '</span>';
      }

      if (logoUrl) {
        var link = document.querySelector("link[rel*='icon']");
        if (link) link.href = logoUrl;
      }
      // Do NOT set data-theme / data-accent here — user preference (capTheme) owns those.
    } catch (e) { /* ignore */ }
  }
};
