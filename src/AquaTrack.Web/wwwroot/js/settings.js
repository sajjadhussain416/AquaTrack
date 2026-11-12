/* AquaTrack - plant settings: payment methods (bank dropdown, IBAN check, on/off badges). */
(function () {
    'use strict';

    var select = document.querySelector('[data-bank-select]');
    var otherBox = document.querySelector('[data-bank-other]');
    var iban = document.querySelector('[data-iban]');
    var hint = document.querySelector('[data-iban-hint]');

    function clean(v) { return (v || '').replace(/[^0-9a-z]/gi, '').toUpperCase(); }

    // ISO 13616 mod-97 check, same as the server.
    function ibanOk(n) {
        if (!/^PK\d{2}[A-Z]{4}[0-9A-Z]{16}$/.test(n)) return false;
        var r = n.slice(4) + n.slice(0, 4), rem = 0;
        for (var i = 0; i < r.length; i++) {
            var c = r.charCodeAt(i);
            rem = c >= 65 ? (rem * 100 + (c - 55)) % 97 : (rem * 10 + (c - 48)) % 97;
        }
        return rem === 1;
    }

    function pretty(n) { return n.replace(/(.{4})(?=.)/g, '$1 '); }

    function toggleOther() {
        if (!select || !otherBox) return;
        var isOther = select.value === 'other';
        otherBox.hidden = !isOther;
        if (isOther) { var t = otherBox.querySelector('input'); if (t && !t.value) t.focus(); }
    }

    function pickBankFromIban(n) {
        if (!select || n.length < 8) return;
        var code = n.substr(4, 4);
        var opt = select.querySelector('option[data-code="' + code + '"]');
        if (opt && select.value !== opt.value) {
            select.value = opt.value;
            toggleOther();
            select.classList.add('flash-pick');
            setTimeout(function () { select.classList.remove('flash-pick'); }, 900);
        }
    }

    function checkIban() {
        if (!iban || !hint) return;
        var n = clean(iban.value);
        hint.className = 'iban-hint';
        hint.textContent = '';
        if (!n) return;

        if (n.indexOf('PK') === 0) {
            pickBankFromIban(n);
            if (n.length < 24) { hint.textContent = hint.dataset.short + ' (' + n.length + '/24)'; hint.classList.add('wait'); }
            else if (ibanOk(n)) { hint.textContent = '✓ ' + hint.dataset.ok; hint.classList.add('ok'); }
            else { hint.textContent = hint.dataset.bad; hint.classList.add('bad'); }
        } else if (/^\d+$/.test(n)) {
            hint.textContent = hint.dataset.acct; hint.classList.add('wait');
        } else {
            hint.textContent = hint.dataset.bad; hint.classList.add('bad');
        }
    }

    // "On / Off" badge on each wallet / bank card, from its main field.
    function refreshStatus() {
        document.querySelectorAll('[data-pm]').forEach(function (card) {
            var key = card.querySelector('[data-pm-key]');
            var badge = card.querySelector('[data-pm-status]');
            if (!key || !badge) return;
            var on = key.value.trim().length > 0;
            badge.textContent = on ? badge.dataset.on : badge.dataset.off;
            badge.classList.toggle('active', on);
            badge.classList.toggle('neutral', !on);
            card.classList.toggle('is-on', on);
        });
    }

    if (select) select.addEventListener('change', toggleOther);
    if (iban) {
        iban.addEventListener('input', checkIban);
        iban.addEventListener('blur', function () {
            var n = clean(iban.value);
            if (n.indexOf('PK') === 0) iban.value = pretty(n);
        });
    }
    document.querySelectorAll('[data-pm-key]').forEach(function (el) { el.addEventListener('input', refreshStatus); });

    toggleOther();
    checkIban();
    refreshStatus();
})();
