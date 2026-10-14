document.addEventListener('change', function (e) {
    const input = e.target;
    if (!(input instanceof HTMLInputElement) || input.type !== 'file' || input.dataset.pdfOnly !== 'true') return;

    const file = input.files && input.files[0];
    let message = '';
    if (file) {
        const maxBytes = Number(input.dataset.maxBytes || 0);
        if (!file.name.toLowerCase().endsWith('.pdf')) {
            message = 'Only .pdf files are allowed.';
        } else if (file.size === 0) {
            message = 'The selected file is empty.';
        } else if (maxBytes > 0 && file.size > maxBytes) {
            message = 'The file is too large (max ' + Math.round(maxBytes / 1048576) + ' MB).';
        }
    }

    input.setCustomValidity(message);
    input.classList.toggle('is-invalid', message !== '');
    if (message) {
        input.reportValidity();
        input.value = '';
    }
});
