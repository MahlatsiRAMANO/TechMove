// Live USD -> ZAR preview on the "New service request" page.
// The server always recalculates with the live rate on save; this is only a preview.
(function () {
    const card = document.getElementById('fxCard');
    const input = document.getElementById('CostUsd');
    const rateEl = document.getElementById('fxRate');
    const zarEl = document.getElementById('fxZar');
    const noteEl = document.getElementById('fxNote');
    if (!card || !input) return;

    let rate = null;
    const zar = new Intl.NumberFormat('en-ZA', { style: 'currency', currency: 'ZAR' });

    function update() {
        const usd = parseFloat(input.value);
        if (rate === null || isNaN(usd) || usd < 0) {
            zarEl.textContent = 'R 0.00';
            return;
        }
        // Round to cents the same way as the server (away from zero).
        const value = Math.round((usd * rate + Number.EPSILON) * 100) / 100;
        zarEl.textContent = zar.format(value);
    }

    fetch(card.dataset.rateUrl, { headers: { 'Accept': 'application/json' } })
        .then(r => r.json())
        .then(data => {
            if (data.success) {
                rate = Number(data.rate);
                rateEl.textContent = '1 USD = R ' + rate.toFixed(4);
                update();
            } else {
                rateEl.textContent = 'Unavailable';
                noteEl.textContent = data.error || 'The currency service is unavailable. You can try saving again later.';
                noteEl.classList.add('text-danger');
            }
        })
        .catch(() => {
            rateEl.textContent = 'Unavailable';
            noteEl.textContent = 'Could not reach the currency service.';
            noteEl.classList.add('text-danger');
        });

    input.addEventListener('input', update);
})();
