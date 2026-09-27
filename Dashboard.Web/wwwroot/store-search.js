// جست‌وجوی زنده‌ی محصولات — بهبود تدریجی روی فرم GET موجود (بدون JS هم کار می‌کند)
// با تایپ در #q یا تغییر #cat، همان URL را fetch می‌کند و فقط ناحیه‌ی #product-results را جایگزین می‌کند.
(function () {
    'use strict';

    var DEBOUNCE_MS = 400;
    var controller = null;
    var timer = null;

    function init() {
        if (!window.location.pathname.startsWith('/shop/products')) return;

        var results = document.getElementById('product-results');
        var q = document.getElementById('q');
        var cat = document.getElementById('cat');
        if (!results || !q) return;

        q.addEventListener('input', schedule);
        if (cat) cat.addEventListener('change', schedule);
    }

    function schedule() {
        clearTimeout(timer);
        timer = setTimeout(run, DEBOUNCE_MS);
    }

    function buildUrl() {
        var q = document.getElementById('q');
        var cat = document.getElementById('cat');
        var params = new URLSearchParams();
        if (q && q.value.trim()) params.set('q', q.value.trim());
        if (cat && cat.value) params.set('cat', cat.value);
        var qs = params.toString();
        return '/shop/products' + (qs ? '?' + qs : '');
    }

    function run() {
        var url = buildUrl();
        if (controller) controller.abort();
        controller = new AbortController();

        var results = document.getElementById('product-results');
        if (!results) return;
        results.setAttribute('aria-busy', 'true');
        results.style.opacity = '0.55';

        fetch(url, {
            signal: controller.signal,
            headers: { 'Accept': 'text/html' },
            credentials: 'same-origin'
        })
            .then(function (res) {
                if (!res.ok) throw new Error('HTTP ' + res.status);
                return res.text();
            })
            .then(function (html) {
                var doc = new DOMParser().parseFromString(html, 'text/html');
                var fresh = doc.getElementById('product-results');
                if (!fresh) return;

                var current = document.getElementById('product-results');
                current.innerHTML = fresh.innerHTML;
                current.removeAttribute('aria-busy');
                current.style.opacity = '';

                history.replaceState(null, '', url);
                controller = null;
            })
            .catch(function (err) {
                if (err && err.name === 'AbortError') return;
                var current = document.getElementById('product-results');
                if (current) {
                    current.removeAttribute('aria-busy');
                    current.style.opacity = '';
                }
            });
    }

    document.addEventListener('DOMContentLoaded', init);
    // ناوبری تقویت‌شده‌ی Blazor: پس از هر بار load شدن صفحه دوباره وصل شو
    document.addEventListener('enhancedload', init);
})();
