// جداسازی سه‌رقمی مبلغ هنگام تایپ + تبدیل ارقام فارسی/عربی به لاتین
// استفاده: dashboardPriceInput.attach(elementReference) — از AppPriceField.razor صدا زده می‌شود
window.dashboardPriceInput = {
    format: function (el) {
        var raw = el.value;
        var caret = el.selectionStart ?? raw.length;

        var digitsBefore = 0, dotSeen = false, out = '';
        for (var i = 0; i < raw.length; i++) {
            var code = raw.charCodeAt(i), c = raw[i];
            if (code >= 0x06F0 && code <= 0x06F9) c = String.fromCharCode(code - 0x06F0 + 48); // ۰-۹
            else if (code >= 0x0660 && code <= 0x0669) c = String.fromCharCode(code - 0x0660 + 48); // ٠-٩
            if (c >= '0' && c <= '9') { out += c; if (i < caret) digitsBefore++; }
            else if (c === '.' && !dotSeen) { dotSeen = true; out += c; if (i < caret) digitsBefore++; }
            // کاما، فاصله و حروف حذف می‌شوند
        }

        // جداسازی فقط روی بخش صحیح اعمال می‌شود
        var dotIndex = out.indexOf('.');
        var intPart = dotIndex === -1 ? out : out.substring(0, dotIndex);
        var decPart = dotIndex === -1 ? '' : out.substring(dotIndex);
        var grouped = '';
        for (var j = intPart.length - 1, count = 0; j >= 0; j--) {
            grouped = intPart[j] + grouped;
            if (++count % 3 === 0 && j > 0) grouped = ',' + grouped;
        }

        var formatted = grouped + decPart;
        if (formatted !== raw) {
            el.value = formatted;
            // مکان‌نما بعد از همان تعداد رقمِ قبلی قرار می‌گیرد تا وسط تایپ نپرد
            var pos = 0, seen = 0;
            while (pos < formatted.length && seen < digitsBefore) {
                var ch = formatted[pos];
                if ((ch >= '0' && ch <= '9') || ch === '.') seen++;
                pos++;
            }
            try { el.setSelectionRange(pos, pos); } catch (e) { }
        }
    },
    attach: function (el) {
        el.addEventListener('input', function () { dashboardPriceInput.format(el); });
        // بعد از خروج از فیلد، صفرهای انتهایی اعشار حذف می‌شوند: 1,000.00 → 1,000
        el.addEventListener('blur', function () {
            if (el.value.indexOf('.') === -1) return;
            var trimmed = el.value.replace(/0+$/, '').replace(/\.$/, '');
            if (trimmed !== el.value) el.value = trimmed;
        });
    }
};
