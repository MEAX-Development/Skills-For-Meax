// Idiomas de MEAX One (ES / EN / JA): selector del menú de usuario y textos de JavaScript.
// Lo instala la skill translate-system; el componente que lo usa es LanguageMenu.razor.

(function () {
    'use strict';

    function panel() { return document.getElementById('mo-lang-panel'); }

    function cerrar() {
        var p = panel();
        if (!p || !p.classList.contains('open')) return;
        p.classList.remove('open');
        var ch = document.getElementById('mo-lang-chevron');
        if (ch) ch.style.transform = '';
        var btn = document.querySelector('[aria-controls="mo-lang-panel"]');
        if (btn) btn.setAttribute('aria-expanded', 'false');
    }

    // Abre la lista sin cerrar el menú de usuario (el clic no llega al document).
    window.moToggleLangPanel = function (e) {
        e.preventDefault();
        e.stopPropagation();
        var p = panel();
        if (!p) return;
        var abierto = p.classList.toggle('open');
        var ch = document.getElementById('mo-lang-chevron');
        if (ch) ch.style.transform = abierto ? 'rotate(180deg)' : '';
        if (e.currentTarget) e.currentTarget.setAttribute('aria-expanded', abierto ? 'true' : 'false');
    };

    // Agrega la página actual como returnUrl: el servidor guarda la cookie y regresa aquí.
    window.moSetLanguage = function (e, code) {
        e.preventDefault();
        e.stopPropagation();
        var link = e.currentTarget;
        var url = new URL(link.getAttribute('href'), document.baseURI);
        url.searchParams.set('culture', code);
        url.searchParams.set('returnUrl', location.pathname + location.search + location.hash);
        location.href = url.toString();
        return false;
    };

    document.addEventListener('click', cerrar);

    // Textos de JavaScript: moT('Delete this row?') o moT('Saved {0} rows', n).
    // El diccionario lo escribe App.razor en <script type="application/json" id="mo-i18n">.
    var dict = null;
    window.moT = function (key) {
        if (dict === null) {
            var el = document.getElementById('mo-i18n');
            try { dict = el ? JSON.parse(el.textContent) : {}; } catch (_) { dict = {}; }
        }
        var texto = Object.prototype.hasOwnProperty.call(dict, key) ? dict[key] : key;
        var args = Array.prototype.slice.call(arguments, 1);
        return args.length
            ? texto.replace(/\{(\d+)\}/g, function (m, i) { return i < args.length ? String(args[i]) : m; })
            : texto;
    };
})();
