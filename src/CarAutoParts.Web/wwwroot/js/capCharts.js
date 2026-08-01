/**
 * Theme-aware Apache ECharts helpers for Car Auto Parts Blazor UI.
 * Public API: render / update / destroy / rethemeAll / setFrame / exportPng / onClick
 */
window.capCharts = (function () {
  const ECHARTS_CDN = 'https://cdn.jsdelivr.net/npm/echarts@5.5.1/dist/echarts.min.js';
  const ECHARTS_GL_CDN = 'https://cdn.jsdelivr.net/npm/echarts-gl@2.0.9/dist/echarts-gl.min.js';

  /** @type {Record<string, { chart: any, spec: any, frameIndex: number, clickHandler?: Function }>} */
  const charts = {};
  let echartsPromise = null;
  let glPromise = null;

  function cssVar(name, fallback) {
    const v = getComputedStyle(document.documentElement).getPropertyValue(name).trim();
    return v || fallback;
  }

  function themeColors() {
    return {
      accent: cssVar('--cap-accent', '#f5a623'),
      accent2: cssVar('--cap-accent-2', '#ffcf70'),
      text: cssVar('--cap-text', '#e8eef6'),
      muted: cssVar('--cap-muted', '#94a3b8'),
      border: cssVar('--cap-border', 'rgba(255,255,255,0.08)'),
      surface: cssVar('--cap-surface', '#1a2332')
    };
  }

  function reduceMotion() {
    try {
      return window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    } catch {
      return false;
    }
  }

  function withAlpha(color, alpha) {
    if (!color) return `rgba(245,166,35,${alpha})`;
    if (color.startsWith('#')) {
      let h = color.slice(1);
      if (h.length === 3) h = h.split('').map(c => c + c).join('');
      const r = parseInt(h.slice(0, 2), 16);
      const g = parseInt(h.slice(2, 4), 16);
      const b = parseInt(h.slice(4, 6), 16);
      return `rgba(${r},${g},${b},${alpha})`;
    }
    if (color.startsWith('rgb(')) return color.replace('rgb(', 'rgba(').replace(')', `,${alpha})`);
    return color;
  }

  function seriesPalette(t) {
    return [
      t.accent,
      t.accent2,
      '#5b8def',
      '#3dd68c',
      '#ef6b7b',
      '#2ec4b6',
      '#fbbf24',
      '#94a3b8',
      '#fb7185',
      '#38bdf8'
    ];
  }

  function resolveColor(role, index, t, palette) {
    if (role === 'accent') return t.accent;
    if (role === 'accent2') return t.accent2;
    if (role === 'muted') return t.muted;
    return palette[index % palette.length];
  }

  function loadScript(src) {
    return new Promise((resolve, reject) => {
      const existing = document.querySelector(`script[data-cap-src="${src}"]`);
      if (existing) {
        if (existing.dataset.loaded === '1') resolve();
        else existing.addEventListener('load', () => resolve(), { once: true });
        return;
      }
      const s = document.createElement('script');
      s.src = src;
      s.async = true;
      s.dataset.capSrc = src;
      s.onload = () => { s.dataset.loaded = '1'; resolve(); };
      s.onerror = () => reject(new Error('Failed to load ' + src));
      document.head.appendChild(s);
    });
  }

  function ensureEcharts() {
    if (window.echarts) return Promise.resolve(window.echarts);
    if (!echartsPromise) {
      echartsPromise = loadScript(ECHARTS_CDN).then(() => {
        if (!window.echarts) throw new Error('echarts missing after CDN load');
        return window.echarts;
      });
    }
    return echartsPromise;
  }

  function ensureGl() {
    return ensureEcharts().then(() => {
      if (window.echarts && window.echarts.graphic && window.__capEchartsGl) return;
      if (!glPromise) {
        glPromise = loadScript(ECHARTS_GL_CDN).then(() => { window.__capEchartsGl = true; });
      }
      return glPromise;
    });
  }

  function animOpts() {
    if (reduceMotion()) return { animation: false };
    return {
      animation: true,
      animationDuration: 750,
      animationEasing: 'cubicOut',
      animationDurationUpdate: 500,
      animationEasingUpdate: 'cubicInOut'
    };
  }

  function baseTextStyle(t) {
    return { color: t.muted, fontFamily: 'Outfit, system-ui, sans-serif', fontSize: 12 };
  }

  function applyFrame(spec, frameIndex) {
    if (!spec || !spec.frames || !spec.frames.length) return spec;
    const idx = Math.max(0, Math.min(frameIndex, spec.frames.length - 1));
    const frame = spec.frames[idx];
    return Object.assign({}, spec, {
      labels: frame.labels && frame.labels.length ? frame.labels : (spec.labels || []),
      datasets: frame.datasets && frame.datasets.length ? frame.datasets : (spec.datasets || []),
      points3D: frame.points3D && frame.points3D.length ? frame.points3D : spec.points3D,
      xCategories: frame.xCategories && frame.xCategories.length ? frame.xCategories : spec.xCategories,
      yCategories: frame.yCategories && frame.yCategories.length ? frame.yCategories : spec.yCategories,
      _frameLabel: frame.label || ''
    });
  }

  function buildCartesian(spec, t, palette) {
    const type = (spec.type || 'bar').toLowerCase();
    const horizontal = type === 'horizontalbar' || spec.indexAxis === 'y';
    const stacked = !!spec.stacked;
    const isArea = type === 'stackedarea' || type === 'area';
    const isLine = type === 'line' || type === 'timeline' || isArea;
    const labels = spec.labels || [];
    const datasets = (spec.datasets || []).map((ds, i) => {
      const color = resolveColor(ds.colorRole, i, t, palette);
      const fill = !!(ds.fill || isArea);
      const seriesType = isLine ? 'line' : 'bar';
      return {
        name: ds.label || `Series ${i + 1}`,
        type: seriesType,
        data: ds.data || [],
        stack: stacked || isArea ? (spec.stackId || 'total') : undefined,
        smooth: ds.tension === false ? false : 0.35,
        showSymbol: isLine,
        symbolSize: 6,
        itemStyle: { color: color, borderRadius: seriesType === 'bar' ? [4, 4, 0, 0] : 0 },
        lineStyle: { width: 2.5, color: color },
        areaStyle: fill ? { color: withAlpha(color, 0.22) } : undefined,
        emphasis: { focus: 'series' },
        barMaxWidth: 42
      };
    });

    return Object.assign({
      backgroundColor: 'transparent',
      textStyle: baseTextStyle(t),
      color: palette,
      tooltip: {
        trigger: 'axis',
        backgroundColor: withAlpha(t.surface, 0.95),
        borderColor: t.border,
        textStyle: { color: t.text }
      },
      legend: {
        show: datasets.length > 1,
        textStyle: baseTextStyle(t),
        top: 0
      },
      grid: { left: 48, right: 20, top: datasets.length > 1 ? 40 : 24, bottom: 32, containLabel: true },
      xAxis: {
        type: horizontal ? 'value' : 'category',
        data: horizontal ? undefined : labels,
        axisLabel: { color: t.muted },
        axisLine: { lineStyle: { color: t.border } },
        splitLine: { lineStyle: { color: withAlpha(t.border, 0.55) } }
      },
      yAxis: {
        type: horizontal ? 'category' : 'value',
        data: horizontal ? labels : undefined,
        axisLabel: { color: t.muted },
        axisLine: { lineStyle: { color: t.border } },
        splitLine: { lineStyle: { color: withAlpha(t.border, 0.55) } }
      },
      series: datasets
    }, animOpts());
  }

  function buildDoughnut(spec, t, palette) {
    const labels = spec.labels || [];
    const values = (spec.datasets && spec.datasets[0] && spec.datasets[0].data) || [];
    return Object.assign({
      backgroundColor: 'transparent',
      textStyle: baseTextStyle(t),
      color: palette,
      tooltip: {
        trigger: 'item',
        backgroundColor: withAlpha(t.surface, 0.95),
        borderColor: t.border,
        textStyle: { color: t.text }
      },
      legend: {
        orient: 'vertical',
        right: 4,
        top: 'middle',
        textStyle: baseTextStyle(t)
      },
      series: [{
        name: (spec.datasets && spec.datasets[0] && spec.datasets[0].label) || 'Share',
        type: 'pie',
        radius: ['52%', '72%'],
        center: ['38%', '50%'],
        avoidLabelOverlap: true,
        itemStyle: { borderColor: t.surface, borderWidth: 2 },
        label: { color: t.muted, formatter: '{b}' },
        data: labels.map((name, i) => ({
          name,
          value: values[i] || 0,
          itemStyle: { color: withAlpha(palette[i % palette.length], 0.9) }
        }))
      }]
    }, animOpts());
  }

  function buildBubble(spec, t, palette) {
    const bubbles = spec.bubbles || [];
    return Object.assign({
      backgroundColor: 'transparent',
      textStyle: baseTextStyle(t),
      tooltip: {
        trigger: 'item',
        backgroundColor: withAlpha(t.surface, 0.95),
        borderColor: t.border,
        textStyle: { color: t.text },
        formatter: p => {
          const d = p.data || [];
          return `${d[3] || p.seriesName}<br/>X ${d[0]} · Y ${d[1]} · Size ${d[2]}`;
        }
      },
      grid: { left: 48, right: 20, top: 24, bottom: 32 },
      xAxis: { type: 'value', axisLabel: { color: t.muted }, splitLine: { lineStyle: { color: withAlpha(t.border, 0.55) } } },
      yAxis: { type: 'value', axisLabel: { color: t.muted }, splitLine: { lineStyle: { color: withAlpha(t.border, 0.55) } } },
      series: [{
        type: 'scatter',
        symbolSize: v => Math.max(8, Math.min(56, Math.sqrt(Math.abs(v[2] || 1)) * 2.2)),
        data: bubbles.map(b => [b.x, b.y, b.r, b.name || '']),
        itemStyle: { color: withAlpha(t.accent, 0.75), borderColor: t.accent2 }
      }]
    }, animOpts());
  }

  function buildBar3d(spec, t, palette) {
    const xCats = spec.xCategories || spec.labels || [];
    const yCats = spec.yCategories || [];
    const points = (spec.points3D || []).map(p => [p.x, p.y, p.z]);
    return Object.assign({
      backgroundColor: 'transparent',
      tooltip: {},
      visualMap: {
        max: Math.max(1, ...points.map(p => p[2] || 0)),
        inRange: { color: [withAlpha(t.accent, 0.35), t.accent, t.accent2] },
        textStyle: { color: t.muted },
        calculable: true
      },
      xAxis3D: { type: 'category', data: xCats, name: '', axisLabel: { color: t.muted } },
      yAxis3D: { type: 'category', data: yCats, name: '', axisLabel: { color: t.muted } },
      zAxis3D: { type: 'value', name: 'Sales', axisLabel: { color: t.muted } },
      grid3D: {
        boxWidth: 180,
        boxDepth: 100,
        viewControl: {
          autoRotate: !!(spec.autoRotate),
          autoRotateSpeed: 8,
          distance: 220,
          alpha: 22,
          beta: 35
        },
        light: { main: { intensity: 1.15 }, ambient: { intensity: 0.35 } }
      },
      series: [{
        type: 'bar3D',
        data: points,
        shading: 'lambert',
        itemStyle: { opacity: 0.9 },
        emphasis: { label: { show: false } }
      }]
    }, animOpts());
  }

  function buildOption(rawSpec, frameIndex) {
    const t = themeColors();
    const palette = seriesPalette(t);
    const spec = applyFrame(rawSpec || {}, frameIndex || 0);
    const type = (spec.type || 'bar').toLowerCase();

    if (type === 'doughnut' || type === 'pie') return buildDoughnut(spec, t, palette);
    if (type === 'bubble') return buildBubble(spec, t, palette);
    if (type === 'bar3d') return buildBar3d(spec, t, palette);
    return buildCartesian(spec, t, palette);
  }

  function needsGl(spec) {
    const type = ((spec && spec.type) || '').toLowerCase();
    if (type === 'bar3d') return true;
    if (spec && spec.frames && spec.frames.some(f => (f.type || '').toLowerCase() === 'bar3d')) return true;
    return false;
  }

  async function ensureLibs(spec) {
    await ensureEcharts();
    if (needsGl(spec)) await ensureGl();
  }

  function destroy(hostId) {
    const entry = charts[hostId];
    if (!entry) return;
    try {
      if (entry.clickHandler) entry.chart.off('click', entry.clickHandler);
      entry.chart.dispose();
    } catch { /* ignore */ }
    delete charts[hostId];
  }

  async function render(hostId, spec) {
    const el = document.getElementById(hostId);
    if (!el) return false;
    try {
      await ensureLibs(spec);
    } catch (err) {
      console.warn('capCharts: failed to load ECharts', err);
      return false;
    }

    destroy(hostId);
    const chart = window.echarts.init(el, null, { renderer: 'canvas' });
    const frameIndex = (spec && spec.playback && typeof spec.playback.currentIndex === 'number')
      ? spec.playback.currentIndex
      : 0;
    chart.setOption(buildOption(spec, frameIndex), true);
    charts[hostId] = { chart, spec: spec || {}, frameIndex };
    return true;
  }

  async function update(hostId, spec) {
    const entry = charts[hostId];
    if (!entry) return render(hostId, spec);
    try {
      await ensureLibs(spec);
    } catch {
      return false;
    }
    entry.spec = spec || {};
    const frameIndex = entry.frameIndex || 0;
    entry.chart.setOption(buildOption(entry.spec, frameIndex), true);
    return true;
  }

  function setFrame(hostId, index) {
    const entry = charts[hostId];
    if (!entry) return false;
    const frames = entry.spec && entry.spec.frames;
    if (!frames || !frames.length) return false;
    entry.frameIndex = Math.max(0, Math.min(index, frames.length - 1));
    entry.chart.setOption(buildOption(entry.spec, entry.frameIndex), true);
    return true;
  }

  function rethemeAll() {
    Object.keys(charts).forEach(id => {
      const entry = charts[id];
      if (!entry) return;
      entry.chart.setOption(buildOption(entry.spec, entry.frameIndex || 0), true);
    });
  }

  function exportPng(hostId, pixelRatio) {
    const entry = charts[hostId];
    if (!entry) return null;
    try {
      return entry.chart.getDataURL({
        type: 'png',
        pixelRatio: pixelRatio || 2,
        backgroundColor: themeColors().surface
      });
    } catch {
      return null;
    }
  }

  function onClick(hostId, dotNetRef) {
    const entry = charts[hostId];
    if (!entry || !dotNetRef) return false;
    if (entry.clickHandler) entry.chart.off('click', entry.clickHandler);
    entry.clickHandler = function (params) {
      const name = params.name || (params.data && params.data.name) || '';
      const value = Array.isArray(params.value) ? (params.value[params.value.length - 1] || 0) : (params.value || 0);
      const seriesName = params.seriesName || '';
      try {
        dotNetRef.invokeMethodAsync('OnChartClick', name, seriesName, Number(value) || 0);
      } catch { /* ignore */ }
    };
    entry.chart.on('click', entry.clickHandler);
    return true;
  }

  function resize(hostId) {
    const entry = charts[hostId];
    if (entry) entry.chart.resize();
  }

  window.addEventListener('resize', () => {
    Object.keys(charts).forEach(resize);
  });

  return {
    render,
    update,
    destroy,
    rethemeAll,
    setFrame,
    exportPng,
    onClick,
    resize,
    themeColors,
    reduceMotion
  };
})();
