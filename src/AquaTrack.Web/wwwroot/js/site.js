// AquaTrack UI helpers. Author: Attique
(function () {
    // Mobile sidebar drawer
    document.addEventListener('click', function (e) {
        if (e.target.closest('[data-sidebar-open]')) document.body.classList.add('sidebar-open');
        if (e.target.closest('[data-sidebar-close]')) document.body.classList.remove('sidebar-open');
    });

    // Show / hide password
    document.querySelectorAll('[data-pw-toggle]').forEach(function (btn) {
        btn.addEventListener('click', function () {
            var input = document.getElementById(btn.getAttribute('data-pw-toggle'));
            if (!input) return;
            var show = input.type === 'password';
            input.type = show ? 'text' : 'password';
            btn.querySelector('i').className = show ? 'bi bi-eye-slash' : 'bi bi-eye';
        });
    });

    // Disable submit buttons after first click to avoid double submits
    document.querySelectorAll('form[data-once]').forEach(function (form) {
        form.addEventListener('submit', function () {
            if (form.checkValidity && !form.checkValidity()) return;
            form.querySelectorAll('button[type=submit]').forEach(function (b) { b.disabled = true; });
        });
    });
})();
