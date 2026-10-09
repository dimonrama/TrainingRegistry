// галочка "Кто изменил / Дата изменения": запоминаем выбор в браузере, чтобы не включать на каждой странице
(function () {
    document.querySelectorAll('[data-toggle-audit]').forEach(function (checkbox) {
        var table = document.querySelector(checkbox.dataset.target);
        if (!table) return;
        var key = 'showAuditColumns';
        try { checkbox.checked = localStorage.getItem(key) === '1'; } catch (e) { }
        var apply = function () { table.classList.toggle('show-audit', checkbox.checked); };
        apply();
        checkbox.addEventListener('change', function () {
            apply();
            try { localStorage.setItem(key, checkbox.checked ? '1' : '0'); } catch (e) { }
        });
    });
})();
