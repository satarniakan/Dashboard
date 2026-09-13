window.persianDatePickerInterop = {
    instances: {},

    init: function (elementId, dotNetRef, mode, initialDisplay) {
        var el = document.getElementById(elementId);
        if (!el) return;

        if (initialDisplay) {
            el.value = initialDisplay;
        }

        var dp = new AzarDatepicker({
            selector: '#' + elementId,
            calendar: 'jalali',
            mode: mode, // 'date' یا 'datetime'
            inputFormat: mode === 'datetime' ? 'YYYY/MM/DD HH:mm' : 'YYYY/MM/DD',
            showClearButton: true,
            onSelect: function (data) {
                dotNetRef.invokeMethodAsync('OnDateSelected', data.iso);
            }
        });

        window.persianDatePickerInterop.instances[elementId] = dp;
    },

    destroy: function (elementId) {
        var dp = window.persianDatePickerInterop.instances[elementId];
        if (dp) {
            dp.destroy();
            delete window.persianDatePickerInterop.instances[elementId];
        }
    }
};