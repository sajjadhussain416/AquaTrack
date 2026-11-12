// AquaTrack delivery route screen. Saves each card without reloading the page.
// Author: Attique
(function () {
    var list = document.getElementById('routeStops');
    if (!list) return;

    var filter = 'pending';
    var offlineMsg = list.getAttribute('data-msg-offline') || 'Could not save.';

    function allStops() { return Array.prototype.slice.call(document.querySelectorAll('article.stop')); }
    function int(v) { var n = parseInt(v, 10); return isNaN(n) ? 0 : n; }
    function num(v) { var n = parseFloat(v); return isNaN(n) ? 0 : n; }

    // ---- +/- steppers, full amount, open/close card ----
    document.addEventListener('click', function (e) {
        var step = e.target.closest('[data-step]');
        if (step) {
            var input = step.parentElement.querySelector('input');
            var next = Math.min(200, Math.max(0, int(input.value) + int(step.getAttribute('data-step'))));
            input.value = next;
            return;
        }

        var full = e.target.closest('[data-full-cash]');
        if (full) {
            var form = full.closest('form');
            var filled = int(form.querySelector('[name=Filled]').value);
            form.querySelector('[name=Cash]').value = Math.round(filled * num(form.getAttribute('data-rate')));
            return;
        }

        var head = e.target.closest('[data-stop-toggle]');
        if (head && !e.target.closest('a')) {
            head.closest('article.stop').classList.toggle('open');
        }
    });

    // ---- Payment method: show TID box and where to send money ----
    function syncMethod(form) {
        var checked = form.querySelector('input[name=Method]:checked');
        var method = checked ? checked.value : 'Cash';
        var row = form.querySelector('[data-tid-row]');
        if (!row) return;
        row.hidden = method === 'Cash';
        row.querySelectorAll('[data-for]').forEach(function (s) { s.hidden = s.getAttribute('data-for') !== method; });
    }

    document.addEventListener('change', function (e) {
        if (e.target.name === 'Method') syncMethod(e.target.closest('form'));
    });

    // ---- Owner's payment accounts: keep them current without a reload ----
    // Fetched when the screen comes back into view and every minute, so a bank / Easypaisa change in
    // Settings reaches the delivery staff's phone on its own.
    var payToBusy = false;
    function refreshPayTo() {
        if (payToBusy || document.hidden || !document.querySelector('[data-pay-to]')) return;
        payToBusy = true;
        fetch('/Delivery/PayTo', { headers: { 'X-Requested-With': 'fetch' }, cache: 'no-store' })
            .then(function (res) { return res.ok ? res.text() : null; })
            .then(function (html) {
                if (html === null) return;
                document.querySelectorAll('[data-pay-to]').forEach(function (box) {
                    if (box.innerHTML.trim() !== html.trim()) box.innerHTML = html;
                });
                document.querySelectorAll('[data-stop-form]').forEach(syncMethod);
            })
            .catch(function () { /* offline: keep what is shown */ })
            .then(function () { payToBusy = false; });
    }
    document.addEventListener('visibilitychange', function () { if (!document.hidden) refreshPayTo(); });
    window.addEventListener('focus', refreshPayTo);
    setInterval(refreshPayTo, 60000);

    document.querySelectorAll('[data-stop-form]').forEach(syncMethod);

    // ---- Save a card ----
    document.addEventListener('submit', function (e) {
        var form = e.target.closest('[data-stop-form]');
        if (!form || !window.fetch) return;
        e.preventDefault();

        var card = form.closest('article.stop');
        var error = form.querySelector('.stop-error');
        var data = new FormData(form);
        data.set('Status', (e.submitter && e.submitter.value) || 'Done');

        var buttons = form.querySelectorAll('button');
        buttons.forEach(function (b) { b.disabled = true; });
        error.hidden = true;

        fetch(form.action, { method: 'POST', body: data, headers: { 'X-Requested-With': 'fetch' }, credentials: 'same-origin' })
            .then(function (res) {
                if (res.ok) return res.text().then(function (html) { replace(card, html); });
                return res.json().then(function (j) { throw new Error(j.message || offlineMsg); });
            })
            .catch(function (err) {
                error.textContent = err && err.message && err.message !== 'Failed to fetch' ? err.message : offlineMsg;
                error.hidden = false;
                buttons.forEach(function (b) { b.disabled = false; });
            });
    });

    function replace(card, html) {
        var tmp = document.createElement('div');
        tmp.innerHTML = html.trim();
        var fresh = tmp.firstElementChild;
        updateCashInHand(cashOf(card), cashOf(fresh));
        fresh.classList.add('just-saved');
        card.replaceWith(fresh);
        var f = fresh.querySelector('[data-stop-form]');
        if (f) syncMethod(f);
        recount();
        setTimeout(function () { fresh.classList.remove('just-saved'); apply(); }, 900);
    }

    // Cash this stop put in the staff member's hand (digital money never does).
    function cashOf(stop) {
        if (!stop || stop.getAttribute('data-state') === 'pending') return 0;
        return stop.getAttribute('data-method') === 'Cash' ? num(stop.getAttribute('data-cash')) : 0;
    }

    // "Cash in your hand" box: move it by the change this save made, without reloading.
    function updateCashInHand(before, after) {
        var el = document.querySelector('[data-in-hand]');
        if (!el || before === after) return;
        var v = num(el.getAttribute('data-in-hand')) + (after - before);
        el.setAttribute('data-in-hand', v);
        el.textContent = (v < 0 ? '-' : '') + 'Rs ' + Math.round(Math.abs(v)).toLocaleString('en-US');
    }

    // ---- Totals and counters ----
    function recount() {
        var stops = 0, done = 0, filled = 0, empties = 0, cash = 0, digital = 0, pending = 0, saved = 0;

        allStops().forEach(function (s) {
            var inRoute = list.contains(s);
            var state = s.getAttribute('data-state');
            if (inRoute || state !== 'pending') stops++;
            if (state === 'pending') { if (inRoute) pending++; return; }
            saved++;
            if (state === 'done') done++;
            filled += int(s.getAttribute('data-filled'));
            empties += int(s.getAttribute('data-empties'));
            if (s.getAttribute('data-method') === 'Cash') cash += num(s.getAttribute('data-cash'));
            else digital += num(s.getAttribute('data-cash'));
        });

        set('[data-total=stops]', stops);
        set('[data-total=done]', done);
        set('[data-total=filled]', filled);
        set('[data-total=empties]', empties);
        set('[data-total=cash]', Math.round(cash).toLocaleString('en-US'));
        set('[data-total=digital]', Math.round(digital).toLocaleString('en-US'));
        set('[data-count=pending]', pending);
        set('[data-count=saved]', saved);
    }

    function set(sel, v) { var el = document.querySelector(sel); if (el) el.textContent = v; }

    // ---- Filter tabs and search ----
    var searchBox = document.querySelector('[data-route-search]');

    function apply() {
        var q = (searchBox && searchBox.value || '').trim().toLowerCase();
        var visibleInRoute = 0;

        allStops().forEach(function (s) {
            var state = s.getAttribute('data-state');
            var matchText = !q || (s.getAttribute('data-search') || '').indexOf(q) >= 0;
            var inRoute = list.contains(s);
            var matchTab = !inRoute || filter === 'all'
                || (filter === 'pending' && state === 'pending')
                || (filter === 'saved' && state !== 'pending');
            var show = matchText && matchTab;
            s.hidden = !show;
            if (show && inRoute) visibleInRoute++;
        });

        var empty = list.querySelector('.route-empty');
        if (empty) empty.hidden = visibleInRoute > 0;

        if (q) {
            var others = document.querySelector('details.others');
            if (others) others.open = true;
        }
    }

    document.querySelectorAll('[data-filter]').forEach(function (b) {
        b.addEventListener('click', function () {
            filter = b.getAttribute('data-filter');
            document.querySelectorAll('[data-filter]').forEach(function (x) { x.classList.toggle('on', x === b); });
            apply();
        });
    });

    if (searchBox) searchBox.addEventListener('input', apply);

    apply();
})();
