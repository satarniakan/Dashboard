// تم روشن/تاریک — باید در <head> و قبل از رندر بدنه بارگذاری شود تا صفحه بدون پرش رنگ بالا بیاید
(function () {
    const KEY = 'dashboard-theme';

    function apply(theme) {
        document.documentElement.setAttribute('data-theme', theme);
        document.documentElement.setAttribute('data-bs-theme', theme);
    }

    // اگر کاربر انتخابی ذخیره نکرده باشد، پیش‌فرض تم سیستم‌عامل است
// و تا وقتی دستی انتخاب نکرده، تغییر تم سیستم به‌صورت زنده هم اعمال می‌شود
const saved = localStorage.getItem(KEY);
const systemDark = window.matchMedia('(prefers-color-scheme: dark)');
apply(saved === 'dark' || saved === 'light' ? saved : (systemDark.matches ? 'dark' : 'light'));

if (saved !== 'dark' && saved !== 'light') {
    systemDark.addEventListener('change', (e) => {
        if (localStorage.getItem(KEY) !== 'dark' && localStorage.getItem(KEY) !== 'light') {
            apply(e.matches ? 'dark' : 'light');
        }
    });
}

    window.themeInterop = {
        get: () => document.documentElement.getAttribute('data-theme') || 'light',
        set: (theme) => {
            localStorage.setItem(KEY, theme);
            apply(theme);
            return theme;
        },
        toggle: () => {
            const next = window.themeInterop.get() === 'dark' ? 'light' : 'dark';
            return window.themeInterop.set(next);
        }
    };
})();

// خواندن کوکی با نام — برای کامپوننت‌های تعاملی که به کوکی سبد نیاز دارند
window.getCookie = (name) =>
    document.cookie.split('; ').find(c => c.startsWith(name + '='))?.split('=')[1] ?? null;

// شمارنده‌ی سبد خرید — بَج را با fetch از endpoint سبک به‌روز می‌کند
window.dashboardCartBadge = {
    refresh: async function (el) {
        try {
            const r = await fetch('/shop/cart/count');
            const d = await r.json();
            el.textContent = d.count > 0 ? d.count : '';
            el.style.display = d.count > 0 ? '' : 'none';
        } catch (e) { /*offline*/ }
    }
};

// فراخوانی ساده‌ی endpoint های JSON برای کامپوننت‌های تعاملی
window.dashboardApi = {
    get: async (url) => (await fetch(url)).json(),
    post: async (url) => { await fetch(url, { method: 'POST' }); }
};
