/* ═══════════════════════════════════════════════════════════════════
   Sistema __CODE__ en MEAX One — base meax_db del hub.
   Produccion: 10.228.25.66. Pruebas: 10.228.25.13. Son independientes y los
   id NO coinciden: todo se busca por code.
   En produccion lo normal es hacerlo desde el hub:
     Gestion de Sistemas → Nuevo / Editar   y   Gestion de sistemas → accesos.
   Ver Login/Docs/Satelites.md §3.
   ═══════════════════════════════════════════════════════════════════ */

DECLARE @code NVARCHAR(100) = N'__CODE__';

/* 1) ¿Como esta registrado hoy? (solo lectura) */
SELECT id, code, name, url, url_nv, canal_activo, is_active, is_public_visible, icon, department
FROM dbo.meax_all_system
WHERE code = @code;

/* 2) Si NO existe, registrarlo (idempotente). Oculto (is_public_visible = 0) mientras se prueba.
      icon: doc, box, truck, wrench, key, monitor, clipboard, dollar… (Helpers/IconSet.cs)
      department: produccion, calidad, rh, ingenieria, ventas, compras, finanzas, it, legal, otros */
IF NOT EXISTS (SELECT 1 FROM dbo.meax_all_system WHERE code = @code)
    INSERT INTO dbo.meax_all_system
        (code, name, name_en, name_ja, url, url_nv, nv_label, canal_activo,
         is_active, is_public_visible, icon, department)
    VALUES
        (@code, N'__NOMBRE__', N'__NOMBRE_EN__', NULL,
         N'http://meax.one/__ALIAS__/', NULL, NULL, 'EST',
         1, 0, N'doc', N'otros');

/* 3) La URL DEBE ser exactamente el alias de IIS y terminar en "/": el SSO acepta un
      returnUrl si EMPIEZA con url (sin "/" final, /PR tambien empata con /PRC). */
-- UPDATE dbo.meax_all_system SET url = N'http://meax.one/__ALIAS__/' WHERE code = @code;

/* 4) Quien entra hoy. */
SELECT a.nomina, a.role, a.created_by
FROM dbo.meax_system_access a
JOIN dbo.meax_all_system s ON s.id = a.system_id
WHERE s.code = @code
ORDER BY a.nomina;

/* 5) Dar acceso a una persona (idempotente). El rol nunca NULL: sin rol el token no trae
      SystemRole. Se compara sensible a mayusculas ("Admin" <> "admin"). */
DECLARE @nomina INT = 0, @rol NVARCHAR(50) = N'Admin';

INSERT INTO dbo.meax_system_access (nomina, system_id, role, created_by)
SELECT @nomina, s.id, @rol, N'script'
FROM dbo.meax_all_system s
WHERE s.code = @code AND @nomina > 0
  AND NOT EXISTS (SELECT 1 FROM dbo.meax_system_access a
                  WHERE a.nomina = @nomina AND a.system_id = s.id);
