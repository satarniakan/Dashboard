// سرویس‌ورکر سبک فروشگاه:
// - ناوبری‌ها همیشه network-first (صفحات SSR هرگز کش نمی‌شوند)؛ فقط هنگام آفلاین‌بودن صفحه‌ی جایگزین
// - فایل‌های استاتیک (css/js/فونت/تصویر) stale-while-revalidate
// - endpoint های JSON و درخواست‌های POST هرگز کش نمی‌شوند
const CACHE = 'dashboard-store-v1';
const OFFLINE_URL = '/offline.html';

const PRECACHE = [
    OFFLINE_URL,
    '/app.css?v=20260926c',
    '/store.css?v=20260926b',
    '/icons/icon-192.png',
    '/icons/icon-512.png'
];

self.addEventListener('install', e => {
    e.waitUntil(
        caches.open(CACHE)
            .then(c => c.addAll(PRECACHE))
            .then(() => self.skipWaiting())
    );
});

self.addEventListener('activate', e => {
    e.waitUntil(
        caches.keys()
            .then(keys => Promise.all(keys.filter(k => k !== CACHE).map(k => caches.delete(k))))
            .then(() => self.clients.claim())
    );
});

function isStaticAsset(url) {
    return url.pathname.startsWith('/_content')
        || url.pathname.startsWith('/_framework')
        || url.pathname.startsWith('/icons/')
        || /\.(css|js|png|jpe?g|svg|gif|webp|woff2?|ttf|ico)$/.test(url.pathname);
}

self.addEventListener('fetch', e => {
    const req = e.request;
    if (req.method !== 'GET') return;

    const url = new URL(req.url);
    if (url.origin !== self.location.origin) return;

    if (req.mode === 'navigate') {
        e.respondWith(fetch(req).catch(() => caches.match(OFFLINE_URL)));
        return;
    }

    if (isStaticAsset(url)) {
        e.respondWith(
            caches.match(req).then(hit =>
                fetch(req).then(res => {
                    const copy = res.clone();
                    caches.open(CACHE).then(c => c.put(req, copy));
                    return res;
                }).catch(() => hit)
            )
        );
    }
});
