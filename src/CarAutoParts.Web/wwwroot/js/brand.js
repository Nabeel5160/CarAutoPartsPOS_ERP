window.capBrand = {
  apply: function (appName, shortName, accentWord, logoUrl, theme, accent) {
    try {
      if (appName) document.title = appName + (appName.indexOf('ERP') >= 0 ? '' : ' ERP');
      var brand = document.querySelector('.cap-brand');
      if (brand) {
        brand.innerHTML = (shortName || 'App') + ' <span>' + (accentWord || '') + '</span>';
      }
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
