/**
 * Shell helpers: fullscreen toggle + change notifications for MainLayout.
 */
window.capShell = {
  _dotNetRef: null,
  _listening: false,

  bind: function (dotNetRef) {
    this._dotNetRef = dotNetRef;
    if (!this._listening) {
      var self = this;
      var onChange = function () { self._notify(); };
      document.addEventListener('fullscreenchange', onChange);
      document.addEventListener('webkitfullscreenchange', onChange);
      this._listening = true;
    }
    this._notify();
  },

  unbind: function () {
    this._dotNetRef = null;
  },

  isFullscreen: function () {
    return !!(document.fullscreenElement || document.webkitFullscreenElement || document.msFullscreenElement);
  },

  toggleFullscreen: async function (selector) {
    try {
      if (this.isFullscreen()) {
        if (document.exitFullscreen) await document.exitFullscreen();
        else if (document.webkitExitFullscreen) document.webkitExitFullscreen();
        else if (document.msExitFullscreen) document.msExitFullscreen();
      } else {
        var el = (selector && document.querySelector(selector)) || document.documentElement;
        if (el.requestFullscreen) await el.requestFullscreen();
        else if (el.webkitRequestFullscreen) el.webkitRequestFullscreen();
        else if (el.msRequestFullscreen) el.msRequestFullscreen();
      }
    } catch (e) {
      console.warn('capShell.toggleFullscreen', e);
    }
    return this.isFullscreen();
  },

  _notify: function () {
    if (!this._dotNetRef) return;
    try {
      this._dotNetRef.invokeMethodAsync('OnFullscreenChanged', this.isFullscreen());
    } catch (e) { /* disposed */ }
  }
};
