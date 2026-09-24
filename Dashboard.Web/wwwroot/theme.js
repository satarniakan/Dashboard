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
