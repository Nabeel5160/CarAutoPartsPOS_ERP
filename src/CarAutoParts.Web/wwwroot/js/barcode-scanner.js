// Program C1 — thin camera barcode scanner for mobile stock check.
// Uses the browser BarcodeDetector API where available (modern Chrome/Edge/Android WebView).
// Falls back to "unsupported" so the UI can hide the scan button gracefully (e.g. iOS Safari, older browsers).
window.capBarcodeScanner = (function () {
  let stream = null;
  let detector = null;
  let videoEl = null;
  let scanning = false;
  let dotNetRef = null;
  let rafId = null;

  function supported() {
    return 'BarcodeDetector' in window && !!(navigator.mediaDevices && navigator.mediaDevices.getUserMedia);
  }

  async function start(videoElementId, dotNetHelper) {
    if (!supported()) return false;
    videoEl = document.getElementById(videoElementId);
    if (!videoEl) return false;
    dotNetRef = dotNetHelper;

    try {
      stream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: 'environment' } });
      videoEl.srcObject = stream;
      await videoEl.play();
      detector = new window.BarcodeDetector({
        formats: ['ean_13', 'ean_8', 'code_128', 'code_39', 'upc_a', 'upc_e', 'qr_code']
      });
      scanning = true;
      loop();
      return true;
    } catch (e) {
      console.error('Barcode scan start failed', e);
      stop();
      return false;
    }
  }

  async function loop() {
    if (!scanning || !videoEl || !detector) return;
    try {
      const codes = await detector.detect(videoEl);
      if (codes && codes.length > 0 && dotNetRef) {
        const value = codes[0].rawValue;
        const ref = dotNetRef;
        stop();
        if (value) await ref.invokeMethodAsync('OnBarcodeDetected', value);
        return;
      }
    } catch (e) {
      // transient detect errors (e.g. video not ready) — ignore and keep scanning
    }
    rafId = requestAnimationFrame(loop);
  }

  function stop() {
    scanning = false;
    if (rafId) { cancelAnimationFrame(rafId); rafId = null; }
    if (stream) { stream.getTracks().forEach(t => t.stop()); stream = null; }
    if (videoEl) { videoEl.srcObject = null; }
    dotNetRef = null;
  }

  return { supported, start, stop };
})();
