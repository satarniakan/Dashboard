// دانلود فایل خروجی گزارش‌ها (اکسل و PDF).
// فایل به‌صورت DotNetStreamReference می‌آید (نه base64) تا فایل‌های بزرگ
// حافظهٔ دوبرابر مصرف نکنند.
function downloadReportFile(fileName, contentReference, mimeType) {
    const arrayBuffer = new Uint8Array(await contentReference.arrayBuffer());
    const blob = new Blob([arrayBuffer], { type: mimeType });
    const url = URL.createObjectURL(blob);

    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    document.body.appendChild(anchor);
    anchor.click();
    document.body.removeChild(anchor);

    // آزادسازی حافظهٔ مرورگر
    URL.revokeObjectURL(url);
}

window.downloadExcelFile = function (fileName, contentReference) {
    downloadReportFile(fileName, contentReference,
        'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet');
};

window.downloadPdfFile = function (fileName, contentReference) {
    downloadReportFile(fileName, contentReference, 'application/pdf');
};
