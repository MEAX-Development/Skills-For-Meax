// Latido a MEAX One (Login/Docs/Satelites.md §4.4).
//
// Cada 20 min: POST <hub>/auth/heartbeat?system=<CODIGO> con credentials:'include'.
// Renueva la cookie .MEAX.JWT, mantiene viva la sesion del hub (el siguiente SSO no pide
// contrasena) y cuenta la permanencia en el Dashboard. El ?system= es obligatorio en
// meax.one: sin el, el hub no distingue a las sub-aplicaciones y descarta el latido.
//
// El layout publica window.meaxHeartbeatUrl SOLO cuando la sesion vino del SSO; con el
// login de desarrollo no existe y aqui no se hace nada. Si el hub no responde, el sistema
// sigue con la sesion que tiene.
(function () {
    var url = window.meaxHeartbeatUrl;
    if (!url) return;

    function latir() {
        fetch(url, { method: 'POST', credentials: 'include', cache: 'no-store' }).catch(function () { });
    }

    latir();
    setInterval(latir, 20 * 60 * 1000);
})();
