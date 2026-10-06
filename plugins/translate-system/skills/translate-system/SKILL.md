---
name: translate-system
description: Traduce un sistema MEAX (Blazor Web App, .NET 8+) a Español, English y 日本語. Instala el motor de idiomas con archivos Resources/es.json, en.json y ja.json; toma el idioma del claim "Idioma" del JWT de MEAX One; agrega el selector de idioma con banderas en el menú de usuario; recorre todas las pantallas para reemplazar los textos fijos por L["..."] y genera las tres traducciones. Úsala cuando el usuario diga "translate-system", "traduce el sistema", "agrega idiomas", "multi-idioma", "i18n", "japonés", o pida actualizar o completar las traducciones de un sistema que ya la usa.
---

# translate-system

Deja un sistema Blazor de MEAX en tres idiomas (ES / EN / JA), igual que el PRC (Purchase
Requisition), pero adaptado a Blazor. Todo lo que se instala está en `templates/blazor/` junto
a este archivo. El script `scripts/i18n_check.py` valida el resultado y `glossary/` trae las
traducciones aprobadas del PRC para usar los mismos términos.

`SKILL_DIR` en este documento es la carpeta donde está este `SKILL.md`. Con el plugin
instalado está dentro de `~/.claude/plugins/`; búscala si no la conoces, por ejemplo con
Glob `**/translate-system/SKILL.md` en la carpeta `.claude` del usuario.

## Cómo funciona lo que se instala

- **Diccionarios**: `Resources/es.json`, `Resources/en.json` y `Resources/ja.json` son JSON
  planos `{ "texto en inglés": "traducción" }`. **La llave siempre es el texto en inglés**,
  aunque el sistema esté escrito en español. `en.json` también se llena (llave = valor) para
  que los tres idiomas estén completos y el inglés se pueda corregir sin tocar código.
- **Búsqueda**: el idioma pedido, luego `en.json` y al final la llave. Un texto sin traducir
  se ve en inglés y nunca truena. Los JSON se recargan solos si se editan en el servidor.
- **Idioma de cada persona** (`MeaxCultureProvider`):
  1. Lo que eligió en el menú de usuario (cookie `.MEAX.Idioma`, `Path=/`, compartida por los
     sistemas del mismo servidor), mientras su idioma en MEAX One siga siendo el mismo que
     cuando eligió.
  2. El claim `Idioma` del JWT de MEAX One (`"es"`, `"en"` o `"ja"`).
  3. Español.
- **Formatos fijos**: solo cambia `CurrentUICulture` (los textos). `CurrentCulture` (fechas,
  números, moneda) se queda en la cultura que ya usaba el proceso, o en la que diga
  `Localization:FormattingCulture` en appsettings. Cambiar de idioma no cambia ningún formato.
- **Selector**: `LanguageMenu.razor` va dentro del menú de usuario (`#mo-user-dropdown`). Es
  HTML con JS (`mo-language.js`), así que funciona con render estático o interactivo. Al
  elegir un idioma llama a `GET culture/set?culture=ja&returnUrl=…`, que guarda la cookie y
  recarga la página, porque un circuito de Blazor fija su idioma al abrirse.
- **Uso en código**:
  - Razor: `@L["New request"]`, `placeholder="@L["Search..."]"`, `Title="@L["My requests"]"`.
  - Con valores: `@L["Request {0} was approved", folio]`.
  - En `@code`: `L["Saved"].Value`.
  - En clases estáticas o servicios: `Loc.T("Draft")` y `Loc.T("{0} rows", n)`.
  - En JavaScript: `moT('Delete this row?')`.

## Flujo

Sigue los pasos en orden y no te saltes la validación. Avísale al usuario en una línea al
empezar cada paso.

### 0. Revisión previa

1. Ubica el proyecto web: el `.csproj` con `Sdk="Microsoft.NET.Sdk.Web"` y
   `Components/App.razor`. **Si no es Blazor Web App** (MVC, Razor Pages, WebForms, React…),
   detente y dile al usuario que por ahora la skill solo cubre Blazor y que lo de MVC se puede
   tomar del PRC.
2. Revisa `TargetFramework`; se necesita net8.0 o mayor.
3. Respaldo:
   - Si es repo git, trabaja en la rama actual pero avisa si hay cambios sin commit.
   - Si **no** es repo git, antes de tocar nada copia la carpeta de la solución a
     `<carpeta>.respaldo-<yyyyMMdd-HHmm>`, sin `bin`, `obj` ni `.vs`, y dile al usuario dónde
     quedó. Por ejemplo, desde la carpeta padre:
     ```powershell
     robocopy "<Sol>" "<Sol>.respaldo-<fecha>" /E /XD bin obj .vs node_modules /NFL /NDL /NJH /NJS
     ```
     Corre robocopy desde **PowerShell**: Git Bash convierte `\\servidor\ruta` en `\servidor\ruta`
     (C:\servidor\…). Los códigos de salida **menores a 8 son éxito** (1 = copió archivos).
4. **Modo actualización**: si ya existe `Localization/LocalizationSetup.cs` o
   `Resources/es.json`, la skill ya se corrió antes. Sáltate el paso 2 y solo haz los pasos 3
   a 6 sobre lo que falte, para traducir pantallas nuevas y llenar huecos.

### 1. Reconocimiento (sin editar)

Lee y anota:

- `Program.cs`: dónde están `AddRazorComponents`, `UseAuthentication`, `MapRazorComponents`.
- `App.razor`, `Routes.razor`, `_Imports.razor` (y si hay más de un `_Imports.razor`).
- Los modos de render (`@rendermode` por página o global).
- El layout con el menú de usuario (`#mo-user-dropdown`) y cómo se abre (JS o Blazor).
- La autenticación: clase de claims (p. ej. `MeaxClaims`), JWT del hub y login de
  desarrollo, si lo hay.
- El idioma base de los textos actuales (inglés o español).
- La lista de componentes `.razor` con sus líneas, y las clases `.cs` que arman texto para la
  pantalla: etiquetas de estados o enums, mensajes de validación, excepciones que se muestran
  al usuario, toasts.
- Textos de JS visibles para el usuario (`alert`, `confirm`, textos en el DOM).

Corre el inventario:

```bash
python "SKILL_DIR/scripts/i18n_check.py" "<carpeta del proyecto>" --hardcoded --out "<scratchpad>/inventario.txt"
```

Lee el reporte desde el archivo; en pantalla sale cortado.

Con más de unos 40 componentes, planea repartir el paso 3 en subagentes por carpeta (ver
"Sistemas grandes").

### 2. Instalar la infraestructura

1. Copia `templates/blazor/Localization/*.cs` (6 archivos, incluido `LocalizedMarkup.cs`) a
   `<Proyecto>/Localization/`.
   - Cambia `__NAMESPACE__` por `<RootNamespace>.Localization`. El RootNamespace sale del
     `.csproj` o del nombre del proyecto.
   - Si ya existe una carpeta o clase con esos nombres, avisa antes de sobrescribir.
2. Copia `templates/blazor/LanguageMenu.razor` a `<Proyecto>/Components/Layout/` con el mismo
   reemplazo de `__NAMESPACE__`.
3. Copia `templates/blazor/wwwroot/mo-language.js` a `<Proyecto>/wwwroot/js/`.
4. CSS:
   - Si el CSS de la plantilla (`meaxone-template.css` o el que use) **ya** tiene
     `#mo-lang-panel`, agrega solo el bloque "Extras para Blazor" de
     `templates/blazor/wwwroot/mo-language.css` al final del CSS propio del sistema
     (p. ej. `app.css`).
   - Si no lo tiene, agrega el archivo completo.
5. `Program.cs`, tres cambios:
   ```csharp
   using <RootNamespace>.Localization;
   builder.Services.AddMeaxLocalization(builder.Environment, builder.Configuration);   // con los demás servicios
   app.UseMeaxLocalization();      // JUSTO después de app.UseAuthentication(); siempre antes de Map*
   app.MapCultureEndpoint();       // antes de app.MapRazorComponents<App>()
   ```
   `UseMeaxLocalization` **tiene** que ir después de `UseAuthentication`, porque el idioma
   sale de un claim.
6. `.csproj`: para que los JSON se publiquen, agrega
   ```xml
   <ItemGroup>
     <Content Update="Resources\*.json" CopyToOutputDirectory="PreserveNewest" CopyToPublishDirectory="PreserveNewest" />
   </ItemGroup>
   ```
7. `Components/_Imports.razor` (y cualquier otro `_Imports.razor` con componentes):
   ```razor
   @using Microsoft.Extensions.Localization
   @using <RootNamespace>.Localization
   @inject IStringLocalizer<SharedResource> L
   ```
   Antes, busca componentes que ya tengan un miembro o variable llamado `L`; renómbralos para
   que no choquen.
8. `App.razor`:
   - `<html lang="@SupportedCultures.Current.Code">`.
   - `<script src="@Assets["js/mo-language.js"]"></script>` después de `blazor.web.js`. Si
     el proyecto no usa `@Assets` (.NET 8), usa `src="js/mo-language.js"`.
   - **Solo si hay textos de JS por traducir**: antes de los scripts, agrega
     `<script type="application/json" id="mo-i18n">@((MarkupString)Loc.ClientJson())</script>`.
9. Layout: pon `<LanguageMenu />` dentro de `#mo-user-dropdown`, después de los enlaces de
   navegación ("Back to MEAX One", "Switch user") y antes del divisor que precede a "Sign
   out". Respeta el orden y los divisores que ya tenga.
10. Claims:
    - Si existe una clase de nombres de claims (`MeaxClaims`), agrega
      `public const string Idioma = "Idioma";` con un comentario de una línea.
    - Si hay un login de desarrollo que imita los claims del hub, agrégale
      `new(MeaxClaims.Idioma, "es")` para que se comporte igual que producción.
11. `dotnet build` del proyecto. Corrige hasta que compile antes de seguir.

### 3. Reemplazar los textos fijos

Recorre **todos** los `.razor` y las clases `.cs` que producen texto visible. Hazlo archivo por
archivo, leyendo cada uno completo. La lista de `--hardcoded` sirve de guía, pero no es
exhaustiva y tiene falsos positivos.

**Qué se traduce**

- Texto entre etiquetas.
- `<PageTitle>`.
- Atributos `placeholder`, `title`, `aria-label` y `alt`.
- Parámetros de componentes que son texto (`Title=`, `Subtitle=`, `Label=`, `Text=`…).
- Opciones de `<select>` (solo lo que se ve, nunca el `value`).
- Encabezados de tablas.
- Mensajes de éxito, error, validación y estados vacíos.
- Textos de los helpers de etiquetas (estados, urgencias, enums mostrados), usando `Loc.T`.
- Excepciones cuyo mensaje se le muestra al usuario, traducidas donde se lanzan con `Loc.T`.
- `confirm`, `alert` y textos de JS con `moT`.
- El modal de reconexión y la barra de error de Blazor.
- Atributos `data-*` que el CSS muestra con `attr()` (p. ej. la etiqueta de columna en móvil).
- Valores por defecto de `[Parameter]`: no pueden usar `L`. Cámbialos a `string?` sin valor y
  resuelve al pintar: `@(Titulo ?? L["Default title"])`.
- `const string` con mensajes: vuélvelos propiedades (`static string X => Loc.T("…")`).
- Textos en arreglos o tuplas dentro del markup (pestañas, opciones de zoom): `L[...]` en cada
  elemento.

**Qué NO se traduce** (anótalo para el reporte final)

- Datos de la base o capturados por el usuario: nombres, departamentos, líneas, comentarios,
  catálogos.
- Folios, códigos, números de parte, claves de formato (`MX-7104-F1`), siglas (PCS, PL, MC,
  FG/SFG, PR, PO, IT).
- Nombres de productos (MEAX One).
- Valores de `<option value>`, rutas, clases CSS, nombres de enums usados como valor, llaves
  de configuración, logs y excepciones técnicas (configuración faltante, errores internos).
- **Formatos oficiales impresos o exportados** (hojas que replican un documento controlado,
  plantillas de Excel o PDF): se quedan exactamente como el documento oficial.
- **Correos**: no se sabe el idioma del destinatario. Se quedan como están; menciónalo en el
  reporte.
- **Bitácoras o historiales que el sistema guarda en la base** (acción, detalle, motivo): son
  datos, se guardan en el idioma base. Menciónalo en el reporte; si el usuario lo quiere
  traducido, lo correcto es guardar un código y traducirlo al mostrarlo (cambio aparte).
- Proyectos de pruebas y migraciones.

**Reglas para las llaves**

1. La llave es el texto en **inglés**, con mayúsculas y puntuación como se ven en pantalla.
   Si el texto original está en español, escribe la llave en inglés y guarda el español
   original como valor de `es.json` (respeta su redacción).
2. Antes de crear una llave, busca si ya existe una igual o equivalente en los JSON del
   sistema o en `glossary/es.json`. Reusa la misma llave exacta, porque las llaves distinguen
   mayúsculas y minúsculas. No crees "Save" y "Save changes" para lo mismo.
3. Frases completas, nunca pedazos: `L["Showing {0} of {1}"]` y no `L["Showing"] + n + L["of"]`.
   Los valores van como placeholders `{0}`, `{1}`, porque el orden cambia en japonés.
4. **Nunca** `L[$"..."]` ni llaves armadas con variables. Usa placeholders, o un `switch` que
   devuelva llaves literales.
5. Sin HTML dentro de las llaves. Si una frase tiene un `<strong>`, un enlace o un `<span>` en
   medio, usa placeholders y `LocalizedMarkup.Con`, que respeta el orden de cada idioma:
   `@L["Folio {0} assigned."].Con(@<b>@folio</b>)`. Nunca `MarkupString` con texto traducido.
6. No uses texto traducido como valor de lógica: filtros, comparaciones, `switch`, claves de
   diccionarios. La lógica trabaja con valores fijos y solo se traduce lo que se muestra.
7. Las etiquetas de estados se traducen en el helper (`Loc.T("Draft")`), no en cada pantalla.
   **Antes** de traducir un helper, busca todos sus usos. Si también alimenta correos,
   exportaciones o lo que se guarda en la base, deja el helper como está (idioma base) y crea
   una versión aparte para pantalla (p. ej. `Nombre` fijo y `NombreTexto => Loc.T(...)`, con un
   `switch` de llaves literales). Así el idioma de quien actúa no se mete en un correo ni en la
   base.
8. Las siglas y unidades que no se traducen pueden quedar fuera de la llave:
   `@L["Quantity"] (PCS)`.
9. No cambies el diseño ni la lógica. Solo cambia el texto por su `L[...]`.
10. Si un literal se queda a propósito en una línea que `--hardcoded` sigue marcando (`.cs`),
    puedes agregar al final `// i18n-ignore` para que deje de salir. No lo uses en el markup.

Después de cada carpeta, corre `dotnet build` para detectar comillas mal anidadas pronto.

### 4. Generar las traducciones

1. Llena `en.json`:
   ```bash
   python "SKILL_DIR/scripts/i18n_check.py" "<proyecto>" --fix-en
   ```
2. Toma del glosario todo lo que ya existe (mismas traducciones que los demás sistemas):
   ```bash
   python "SKILL_DIR/scripts/i18n_check.py" "<proyecto>" --from-glossary "SKILL_DIR/glossary"
   ```
3. Saca lo que falta:
   ```bash
   python "SKILL_DIR/scripts/i18n_check.py" "<proyecto>" --missing-json "<scratchpad>/faltantes.json"
   ```
4. Traduce las llaves de `es` y `ja` y escríbelas en uno o varios lotes
   `{"es": {...}, "ja": {...}}`. Antes, revisa en `glossary/` cómo se tradujeron términos
   parecidos. Intégralos con:
   ```bash
   python "SKILL_DIR/scripts/i18n_check.py" "<proyecto>" --merge lote1.json lote2.json
   ```
   El script guarda los tres archivos ordenados y con el mismo formato (UTF-8 sin BOM,
   2 espacios).
5. **No modifiques `SKILL_DIR/glossary`.** Esta copia se reemplaza en cada actualización del
   plugin, y el glosario lo mantiene solo Fabian Gonzalez en el repo
   `MEAX-Development/Skills-For-Meax`. En el reporte final, di cuántas llaves nuevas tiene
   este sistema que no están en el glosario. Si el usuario es el mantenedor y tiene el repo
   clonado, ofrécele agregarlas ahí (sin cambiar las que ya estaban) y hacer push:
   ```bash
   python "SKILL_DIR/scripts/i18n_check.py" "<proyecto>" --to-glossary "<clon>/plugins/translate-system/skills/translate-system/glossary"
   ```

**Estilo de traducción**

- **Español**: de México, profesional, de **tú** (como el PRC: "Puedes…", "No tienes…").
  - Botones en infinitivo: "Guardar", "Enviar a aprobación".
  - Estatus en masculino, como en el PRC: "Borrador", "Devuelto", "Rechazado", "Cancelado",
    "Cerrado".
  - "Error." con punto es una llave distinta de "Error": no armes `@L["Error"].`, porque en
    japonés queda "エラー.".
  - Términos fijos del glosario: "Solicitud", "Aprobación", "Estatus", "Folio", "Cotización",
    "Proveedor".
- **Japonés**: lenguaje de empresa japonesa.
  - Botones y etiquetas cortos con sustantivos o verbos (保存, 送信, 承認待ち).
  - Mensajes en forma です/ます (保存しました。).
  - Puntuación 、。 y comillas 「」.
  - Siglas y códigos sin traducir.
  - Usa los mismos términos que `glossary/ja.json` (申請, 承認, 却下, 差戻し…).
- Respeta los placeholders `{0}` exactamente.
- En los dos idiomas cuida la longitud: un menú lateral o un botón no debe crecer mucho
  (busca la forma corta natural).

### 5. Validar

1. `python "SKILL_DIR/scripts/i18n_check.py" "<proyecto>"` debe terminar en **RESULTADO: OK**:
   - ninguna llave faltante en los tres idiomas;
   - placeholders iguales;
   - sin llaves dinámicas ni valores vacíos.
2. Revisa los avisos: "ja.json sigue en inglés" solo es válido para siglas o nombres. Las
   llaves sin uso se borran con `--prune`, pero antes confirma que ninguna se use con una
   variable.
3. `python ... --hardcoded --out "<scratchpad>/pendientes.txt"`: lo que quede en la lista debe
   estar en la categoría "NO se traduce". Corrige el resto.
4. `dotnet build` sin errores y sin advertencias nuevas. Si hay proyecto de pruebas, corre
   `dotnet test`.
5. Prueba en el navegador, si se puede arrancar (`.claude/launch.json` con `preview_start`, o
   `dotnet run`). Si el sistema tiene login de desarrollo, úsalo. Verifica:
   - El menú de usuario muestra "Idioma / Language / 言語" con las 3 banderas y la palomita en
     el idioma activo, y el panel abre sin cerrar el menú.
   - Al cambiar a ES, EN y JA la página recarga en la misma ruta, con todo traducido,
     incluyendo el menú lateral y los estados.
   - Los textos más largos (ES y JA) no rompen botones, menú lateral ni encabezados de tabla.
   - Las fechas y los números se ven igual en los tres idiomas.
   - Una página interactiva (formulario, búsqueda) sigue respondiendo después de cambiar de
     idioma.
   - En la consola del navegador no aparecen errores nuevos.
   No captures credenciales reales. Si para entrar se requiere el SSO de producción, deja la
   prueba visual al usuario.

### 6. Reporte final

Breve y en español:

- Número de llaves por idioma y archivos modificados.
- Dónde quedó el respaldo, si se hizo.
- Lo que se quedó sin traducir **a propósito** y por qué (formatos oficiales, correos, datos).
- Pendientes o dudas: términos que convenga revisar con alguien de Japón, textos ambiguos.
- Cómo corregir una traducción: editar `Resources/<idioma>.json`. En el servidor el cambio se
  toma solo, sin reiniciar.

## Sistemas grandes

Con más de unos 40 componentes, reparte el paso 3 con subagentes, uno por carpeta o grupo de
unos 10 archivos. A cada uno pásale:

- la lista exacta de archivos;
- las reglas de "Qué se traduce", "Qué NO se traduce" y "Reglas para las llaves";
- las llaves que ya existen.

Los subagentes **solo editan código** y devuelven la lista de llaves nuevas. Los JSON los
llenas tú en el paso 4, para que no haya dos escribiendo el mismo archivo. Para traducir
muchas llaves también puedes repartir lotes del `faltantes.json` en paralelo e integrarlos
con `--merge`.

## Problemas conocidos

- **El idioma no cambia en páginas interactivas**: `UseMeaxLocalization()` quedó antes de
  `UseAuthentication()` o después de `MapRazorComponents`. También puede faltar la recarga
  completa: el enlace debe llevar `data-enhance-nav="false"`.
- **`L` no existe en un componente**: el componente está fuera de la carpeta de
  `_Imports.razor`. Agrega el `@inject` a ese `_Imports` o al componente.
- **`CS0104 'L' is ambiguous` o choques de nombre**: hay otro miembro `L`. Renómbralo.
- **Los JSON no llegan al servidor**: falta el `<Content Update="Resources\*.json" …>` del
  `.csproj`.
- **Comillas en Razor**: `title="@L["Text"]"` funciona. Dentro de un string de C# en
  `@code` usa `L["Text"].Value`.
- **CS8602 dentro de `@<b>…</b>`**: al mover una expresión a una plantilla Razor (lambda) se
  pierde el análisis de nulos. Usa `!` sobre la variable que ya se validó afuera.
- **Pruebas**: sin `AddMeaxLocalization`, `Loc.T` regresa la llave (inglés). Las pruebas que
  comparan contra el texto en inglés siguen pasando.
- **`<PageTitle>` vacío** en páginas `InteractiveServer` con `prerender: false`: el
  `HeadOutlet` estático no lo recibe. Ya pasaba antes; no es por la traducción.
- **Banderas como "MX / US / JP"**: Windows no tiene emojis de banderas y muestra las letras.
  Es igual en el PRC.

## Publicar (solo si el usuario lo pide)

1. Revisa a dónde publica el perfil (`Properties/PublishProfiles/*.pubxml`). Si es un
   servidor de producción, confirma con el usuario qué se sube.
   - En repos git, revisa si hay otros cambios sin commit que se subirían junto.
   - Sin git, compara las fechas contra el `.dll` del servidor.
2. Compara `appsettings*.json` y `web.config` del servidor contra los locales. Si difieren,
   no los sobrescribas sin preguntar.
3. `dotnet publish -c Release -o <scratchpad>/publish`. Revisa que traiga `Resources/*.json`
   y `wwwroot/js/mo-language.js`, y que no traiga `appsettings.Development.json` ni datos de
   prueba.
4. Todo desde **PowerShell** (rutas UNC):
   - respalda la carpeta del servidor con robocopy;
   - crea `app_offline.htm` en la carpeta del sitio y espera unos segundos;
   - copia con `robocopy <publish> <servidor> /E /R:5 /W:3` (sin `/MIR`);
   - borra `app_offline.htm`.

   Haz cada paso en su propia llamada.
5. Verifica:
   - que la `.dll` del servidor sea la nueva;
   - que el sitio responda (con SSO, un 302 al hub es lo normal);
   - que `<url>/culture/set?culture=ja&returnUrl=<pathbase>/` regrese 302 con la cookie
     `.MEAX.Idioma`.
6. Si el sistema ya estaba publicado con la skill y solo cambiaron traducciones, basta con
   copiar `Resources/*.json` al servidor: se toman solos, sin reiniciar.
