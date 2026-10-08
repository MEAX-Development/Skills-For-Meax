---
name: connect-meaxone
description: Vincula un sistema .NET (Blazor Web App o MVC) a MEAX One como satélite. Solo pregunta el código del sistema y la carpeta de publicación en meaxs066. Instala el SSO del hub con cookie propia, el login de desarrollo con doble candado y el latido con ?system=. Deja todas las URLs relativas al PathBase (nada estático) y prepara el appsettings.json del servidor sin mostrar secretos. Arma el script de publicación con respaldo y app_offline, publica, verifica y entrega el SQL de registro en meax_db. Úsala cuando el usuario diga "connect-meaxone", "conecta el sistema a MEAX One", "vincúlalo al hub", "agrégalo a meax.one", "integra el login de MEAX One" o "publícalo como satélite".
---

# connect-meaxone

Deja un sistema de MEAX listo para vivir como **satélite de MEAX One**
(`http://meax.one/<ALIAS>/`). La persona entra por la tarjeta del hub sin volver a escribir su
contraseña, el sistema sabe quién es por su nómina y el hub cuenta su uso. Se basa en la
receta oficial del hub, `Login/Docs/Satelites.md` (si el repo `Login` está clonado junto a
este, léelo; manda sobre este documento si algo difiere).

`SKILL_DIR` es la carpeta de este `SKILL.md`. Con el plugin instalado está dentro de
`~/.claude/plugins/`; búscala con Glob `**/connect-meaxone/SKILL.md` si no la conoces.

**El diseño no es el objetivo.** Solo se agregan al layout el menú de usuario y el latido. Si
el sistema ya usa la plantilla de MEAX One (`#mo-user-dropdown`, `mo-dd-item`), se usan esas
clases; si no, se agrega el mínimo HTML sin rediseñar nada.

## Lo único que se pregunta

1. **Código del sistema** (`Hub:SystemCode`): mayúsculas, letras, números o `_`, hasta 20
   caracteres. Es el `code` de `meax_all_system` y el `system=` del SSO y del latido.
2. **Carpeta de publicación**: `\\meaxs066\inetpub\Services\<Carpeta>`.

Si vienen en el mensaje o en los argumentos, no los vuelvas a preguntar. Todo lo demás se
deduce (paso 1). Solo pregunta otra cosa si bloquea el trabajo (p. ej., reemplazar una app
que ya opera, o de qué archivo tomar el secreto si no hay ninguno conocido).

## Cómo funciona lo que se instala (patrón B de Satelites.md)

1. Clic en la tarjeta → el hub (`/go/{id}`) valida la fila en `meax_system_access`.
2. El satélite no tiene sesión → redirige a
   `http://meax.one/auth/sso?system=<CODE>&returnUrl=<URL absoluta con PathBase>`.
3. El hub regresa con `?meax_token=` (JWT HS256, 5 min, trae `SystemRole`).
4. `HubSso` valida la firma con `Jwt:Secret`, abre la cookie propia `.<CODE>.AUTH` (ruta =
   PathBase, 10 h, sin sliding) y redirige a la misma URL sin el token.
5. `meax-heartbeat.js`: `POST <hub>/auth/heartbeat?system=<CODE>` cada 20 min.
6. `/salir` cierra la cookie propia y manda a `<hub>/Account/Logout`.

Por qué no se lee la cookie `.MEAX.JWT` directo: la trae **cualquiera** con sesión en
meax.one, tenga o no acceso al sistema, y no trae `SystemRole`. Solo el token del SSO pasó
por la revisión de permisos.

En la máquina de desarrollo (`Development` **y** `Auth:DevLogin = true`, solo en
`appsettings.Development.json`, que no se publica) entra un usuario falso con los mismos
claims del hub. En IIS el entorno es `Production` y nunca se enciende.

## Flujo

Avisa en una línea al empezar cada paso. No te saltes las pruebas.

### 0. Respaldo

- Repo git: trabaja en la rama actual; avisa si hay cambios sin commit.
- Sin git: copia la solución a `<carpeta>.respaldo-<yyyyMMdd-HHmm>` sin `bin`, `obj`, `.vs`
  (robocopy desde **PowerShell**; códigos < 8 son éxito) y di dónde quedó.

### 1. Reconocimiento (sin editar)

**El proyecto**

- `.csproj` web (`Sdk="Microsoft.NET.Sdk.Web"`), `TargetFramework` (net8.0 o mayor),
  `RootNamespace`.
- Tipo: Blazor Web App (`Components/App.razor`) o MVC/Razor Pages (`Views/Shared/_Layout.cshtml`).
  Otra cosa (WebForms, SPA): detente y avisa.
- Modos de render (`@rendermode` global o por página) y dónde está el layout con el menú.
- **Autenticación existente**:
  - Ninguna: se instala todo.
  - Ya usa el hub con `JwtBearer` (patrón A, p. ej. SolicitudDeMovimiento) o `.MEAX.JWT`
    directo: avísale al usuario que eso deja entrar a cualquiera con sesión en meax.one y
    propón cambiar a este patrón. Si dice que no, solo revisa URLs, latido y config.
  - Windows auth, Identity propio u otro: pregunta antes de reemplazarlo.
- Clase de claims existente (`MeaxClaims`, `AppUser`): reúsala en vez de duplicar.

**URLs estáticas** (lo que más se rompe en IIS: con `/` al principio apunta a la raíz de
meax.one, es decir, **al hub**). Busca con Grep y anota archivo y línea:

| Buscar | Por qué |
|---|---|
| `<base href="/"` | Blazor necesita el PathBase |
| `href="/`, `src="/`, `action="/` en `.razor`/`.cshtml`/`.html` | apuntan al hub |
| `NavigateTo("/`, `NavigateTo($"/` | navegación a la raíz |
| `Redirect("/`, `LocalRedirect("/`, `Results.Redirect("/` | igual en el servidor |
| `fetch('/`, `fetch("/`, `url: '/`, `$.ajax` con `/` | llamadas JS a la raíz |
| `$"/` armando rutas en C# para links (`$"/coverage?…"`) | links internos |
| `http://10.228.`, `meaxs066`, `localhost:` en código o vistas | host fijo |
| `Request.Host`, `Request.Scheme` para armar links de correo | en jobs no hay request |
| `UseHttpsRedirection` | meax.one es HTTP; rompe el regreso del SSO |
| `Environment.UserName`, `WindowsIdentity`, `User.Identity.Name` como "quién capturó" | en IIS es la cuenta del App Pool |

`href="/_framework/…"` o `@Assets[...]` no aplican si el `<base href>` es correcto, pero
revísalos igual.

**La carpeta de publicación** (solo lectura, desde PowerShell):

- ¿Existe y hay acceso? ¿Qué app tiene? (`web.config` → `arguments=".\X.dll"`; fecha del `.dll`).
- **Si tiene otra app distinta** de este proyecto: se va a **reemplazar**. Dilo claro y pide
  confirmación antes del paso 9. El script de publicación respalda todo antes de copiar.
- Del `appsettings.json` del servidor, mira **solo los nombres de las secciones** (nunca
  imprimas valores). Si es del mismo sistema, será la base de la configuración del servidor
  (conserva sus cadenas de conexión).

**El alias (la URL)**

- Por omisión, `ALIAS = CODE` (`http://meax.one/CODE/`). La carpeta puede llamarse distinto
  (`/RWM` → `Planeacion_RWM`, `/Tickets` → `TicketsIT`).
- Si la carpeta ya está publicada, confirma con qué alias responde: prueba
  `http://meax.one/<CODE>/` y `http://meax.one/<Carpeta>/` (sin seguir redirecciones) y
  compara con la app de la carpeta (nombre de su CSS `<Proyecto>.styles.css`, `<title>`,
  `<base href>`). Usa el alias que ya existe; si no es el código, díselo al usuario.
- Si no está publicada, el alias lo crea el admin de meaxs066 (paso 10). No puede chocar con
  rutas del hub: `Account`, `Admin`, `ApiAuth`, `ApiDispositivos`, `ApiFirma`,
  `ApiNotificaciones`, `Avisos`, `AvisosAdmin`, `Chat`, `ContrasenasAdmin`, `ExternosAdmin`,
  `Favorites`, `Home`, `Notificaciones`, `PasswordReset`, `Profile`, `Systems`, `api`, `auth`,
  `go`, `notificaciones`, `cuenta`, `css`, `images`, `js`, `lib`, `uploads`. Tampoco puede ser
  el principio de otro alias (`PR` y `PRC`).
- **Siempre las mismas mayúsculas** en la URL registrada, los links y los correos.

### 2. Instalar la autenticación

1. Copia `SKILL_DIR/templates/Auth/*.cs` a `<Proyecto>/Auth/` y cambia `__NAMESPACE__` por
   `<RootNamespace>.Auth`:
   - `MeaxClaims.cs`: nombres de los claims del hub.
   - `HubSso.cs`: SSO, cookie propia y `/salir`.
   - `DevAuth.cs`: login de desarrollo con doble candado.
   - `UsuarioActual.cs`: la persona de la sesión (Blazor). En MVC basta con
     `UsuarioActual.Login(User)` / `Nombre(User)` (métodos estáticos).

   Si el proyecto ya tiene `MeaxClaims`, no lo dupliques: agrega lo que falte.
2. `.csproj`:
   ```xml
   <PropertyGroup>
     <!-- La directiva de grupo bloquea el .exe del SDK; IIS no lo necesita. -->
     <UseAppHost>false</UseAppHost>
   </PropertyGroup>
   <ItemGroup>
     <PackageReference Include="System.IdentityModel.Tokens.Jwt" Version="8.*" />
   </ItemGroup>
   <ItemGroup>
     <Content Update="appsettings.Development.json" CopyToPublishDirectory="Never" />
     <Content Update="appsettings.Local.json" CopyToPublishDirectory="Never" />
   </ItemGroup>
   ```
   Para la versión del paquete usa una 8.x que ya esté en `~/.nuget/packages/system.identitymodel.tokens.jwt`
   (los equipos pueden no tener salida a nuget.org). Si existe `App_Data` con datos locales,
   tampoco se publica (`<Content Update="App_Data\**" CopyToPublishDirectory="Never" />`).
3. `Program.cs`:
   ```csharp
   using <RootNamespace>.Auth;

   // Servicios
   var modoDev = DevAuth.Habilitado(builder.Environment, builder.Configuration);
   if (modoDev) builder.Services.AddDevAuth();
   else builder.Services.AddHubSso(builder.Configuration);
   builder.Services.AddAuthorization();
   builder.Services.AddCascadingAuthenticationState();   // solo Blazor
   builder.Services.AddScoped<UsuarioActual>();           // solo Blazor

   // Llaves de DataProtection: solo si la carpeta existe en el servidor con permiso de
   // escritura para el App Pool; si no, déjalo vacío (las sesiones se renuevan solas por SSO).
   if (builder.Configuration["DataProtection:KeysPath"] is { Length: > 0 } keys)
       builder.Services.AddDataProtection()
           .PersistKeysToFileSystem(new DirectoryInfo(keys))
           .SetApplicationName(HubSso.SystemCode(builder.Configuration));

   // Pipeline, en este orden
   if (app.Configuration["App:PathBase"] is { Length: > 0 } pathBase) app.UsePathBase(pathBase);
   // (quitar app.UseHttpsRedirection())
   app.UseStaticFiles();            // si el proyecto lo usa; MapStaticAssets no cambia
   app.UseRouting();
   app.UseAuthentication();
   if (!modoDev) app.UseHubSso(app.Configuration);
   app.UseAuthorization();
   app.UseAntiforgery();            // Blazor
   // Map*
   if (!modoDev) app.MapHubSsoLogout(app.Configuration);
   app.MapRazorComponents<App>().AddInteractiveServerRenderMode().RequireAuthorization();  // Blazor
   app.MapControllerRoute(...).RequireAuthorization();                                    // MVC
   ```
   - Si `UseStatusCodePagesWithReExecute` o `UseExceptionHandler` ya existen, déjalos donde
     están.
   - Las **APIs para programas** (handies, servicios) no pueden seguir el SSO: con
     `[AllowAnonymous]` y su propia autenticación (Bearer del hub o clave de API).
   - `wwwroot` es público: nada privado ahí.
4. `appsettings.json` (se versiona, **sin secretos**):
   ```json
   "Hub": { "BaseUrl": "http://meax.one", "SystemCode": "<CODE>" },
   "Jwt": { "Secret": "", "Issuer": "meaxHub", "Audience": "meax-services" },
   "DataProtection": { "KeysPath": "" }
   ```
   `Hub:BaseUrl` siempre `http://meax.one`, nunca la IP: son hosts distintos para el
   navegador y la cookie del hub no viajaría.
5. `appsettings.Development.json`:
   ```json
   "Auth": {
     "DevLogin": true,
     "DevUser": {
       "PcLoginId": "<cuenta>", "DisplayName": "Usuario Dev", "Department": "IT",
       "Position": "Dev", "EmployeeId": "<nómina>", "LoginTipo": "IDL",
       "SystemRole": "Admin", "Idioma": "es"
     }
   }
   ```
   Para `PcLoginId` usa la cuenta de Windows de quien desarrolla (así "Registrado por" sale
   igual que antes en local). Si el sistema ya tenía un usuario de prueba, reúsalo.
6. `dotnet build` hasta que compile.

### 3. URLs dinámicas

Corrige todo lo que anotaste en el paso 1. Reglas:

| Mal | Bien |
|---|---|
| Blazor `<base href="/" />` | `<base href="@BaseHref" />` calculado del `PathBase` (ver abajo) |
| `<a href="/">`, `<NavLink href="/">` (inicio) | `href=""` (relativo a `<base>`) |
| `href="/solicitudes"`, `NavigateTo("/solicitudes")` | `href="solicitudes"`, `NavigateTo("solicitudes")` |
| C# `$"/coverage?material={x}"` para un link | `$"coverage?material={x}"` |
| MVC `href="/css/site.css"`, `Redirect("/x")` | `~/css/site.css`, `asp-*`, `RedirectToAction`, `LocalRedirect(Url.Content("~/x"))` |
| JS `fetch('/api/datos')` | `fetch('api/datos')` (relativo a `<base>`) o URL armada por Razor en un `data-` |
| Link al hub escrito a mano | `Config["Hub:BaseUrl"]` |
| Links de correos o jobs con `Request.Host` | `App:BaseUrl = http://meax.one/<ALIAS>` en configuración |

Base href en `App.razor` (Blazor):

```razor
<base href="@BaseHref" />
@code {
    [CascadingParameter] private HttpContext? Ctx { get; set; }
    private string BaseHref => Ctx?.Request.PathBase.Value is { Length: > 0 } pb ? $"{pb.TrimEnd('/')}/" : "/";
}
```

Con el `<base>` correcto, `@Assets["…"]` y `_framework/blazor.web.js` (sin `/`) ya
funcionan. **La cookie propia no lleva `Path = "/"`**: no lo cambies.

### 4. Layout: menú de usuario y latido

1. Copia `SKILL_DIR/templates/wwwroot/js/meax-heartbeat.js` a `<Proyecto>/wwwroot/js/`.
2. En `App.razor` (Blazor) o `_Layout.cshtml` (MVC), antes de los scripts:
   ```razor
   @inject IConfiguration Config
   @if (HeartbeatUrl is not null) { <script>window.meaxHeartbeatUrl = '@HeartbeatUrl';</script> }
   <script src="@Assets["js/meax-heartbeat.js"]"></script>   @* MVC: ~/js/meax-heartbeat.js *@
   @code {
       // Solo con sesión del SSO; el login de desarrollo no tiene hub.
       private string? HeartbeatUrl =>
           Ctx?.User.Identity is { IsAuthenticated: true, AuthenticationType: var t } && t != DevAuth.SchemeName
               ? $"{HubSso.HubBaseUrl(Config)}/auth/heartbeat?system={Uri.EscapeDataString(HubSso.SystemCode(Config))}"
               : null;
   }
   ```
   En MVC usa `User` en vez de `Ctx?.User`.
3. Menú de usuario (dentro de `<AuthorizeView><Authorized>` en Blazor, o
   `@if (User.Identity?.IsAuthenticated == true)` en MVC):
   - iniciales y nombre: `UsuarioActual.Iniciales(...)`, `UsuarioActual.Nombre(context.User)`;
   - "Nómina N · Departamento" (claims `EmployeeId`, `Department`);
   - **"Volver a MEAX One"** → `@($"{HubSso.HubBaseUrl(Config)}/")`;
   - **"Cerrar sesión"** → `href="salir"` con `data-enhance-nav="false"`, oculto en modo
     desarrollo (`DevAuth.Habilitado(Env, Config)`).

   Si ya existía un menú, cambia solo su contenido. No rediseñes.

### 5. Quién es la persona

- Reemplaza `Environment.UserName` / `WindowsIdentity` / similares usados como "quién
  capturó" por la cuenta de la sesión:
  - Blazor: inyecta `UsuarioActual` y en `OnInitializedAsync` asigna `campo = await Usuario.LoginAsync();`.
    Deja el campo con `string.Empty` como valor inicial.
  - MVC: `UsuarioActual.Login(User)`.
  - El campo sigue siendo editable si antes lo era. No cambies la lógica.
- Para tablas nuevas, la llave de la persona es **la nómina** (`EmployeeId` → `int`), no
  `PcLoginId`.
- `SystemRole` es el rol en este sistema (de `meax_system_access`). Si el sistema necesita
  permisos, `[Authorize(Roles = "Admin")]` funciona (sensible a mayúsculas). Si no los
  necesita, no agregues reglas: el SSO ya filtró quién entra.

### 6. Probar en local

1. `dotnet build` y `dotnet test` si hay pruebas.
2. **Modo desarrollo**: arranca normal (`dotnet run` o `preview_start`). Debe entrar el
   usuario de prueba, verse en el menú, prellenar "Registrado por" y **no** publicar
   `meaxHeartbeatUrl`.
3. **Modo hub, simulando IIS**: arranca el `.dll` ya compilado con
   ```bash
   MSYS_NO_PATHCONV=1 ASPNETCORE_ENVIRONMENT=Development Auth__DevLogin=false App__PathBase=/<ALIAS> \
   Jwt__Secret=prueba-local-solo-para-test-0123456789abcdef ASPNETCORE_URLS=http://localhost:5199 \
   dotnet bin/Debug/<tfm>/<Proyecto>.dll
   ```
   desde la carpeta del proyecto, en segundo plano. `MSYS_NO_PATHCONV=1` es obligatorio en
   Git Bash: sin él convierte `/<ALIAS>` en `C:/Program Files/Git/<ALIAS>`. **El secreto es
   de prueba**: nunca uses el de producción en local.
4. Corre las pruebas del SSO (desde PowerShell):
   ```powershell
   & "SKILL_DIR\scripts\Probar-Sso.ps1" -Url http://localhost:5199/<ALIAS> -Code <CODE> `
       -Secreto prueba-local-solo-para-test-0123456789abcdef [-Ruta <una página>] [-Estatico <archivo de wwwroot>]
   ```
   Debe terminar en **RESULTADO: OK**. La prueba de "rutas absolutas a la raíz" revisa la
   página que pidas; pásale con `-Ruta` las pantallas principales.
5. Detén el proceso de prueba (`Get-NetTCPConnection -LocalPort 5199` → `Stop-Process`).

### 7. Archivos de publicación en el proyecto

En `<Solución>/deploy/`:

- `Publicar.ps1`, desde `SKILL_DIR/templates/deploy/Publicar.ps1`, con los reemplazos:
  - `__PROYECTO__`;
  - `__ALIAS__` y `__CODE__`;
  - `__CODE_LOWER__`;
  - `__DESTINO__` (la carpeta UNC);
  - `__CSPROJ_RELATIVO__` (ruta del `.csproj` desde la raíz de la solución).

  Si el proyecto necesita flags de restauración (p. ej. `-p:RestoreIgnoreFailedSources=true`),
  agrégalos al `dotnet publish`.
- `registrar-sistema.sql`, desde `templates/deploy/registrar-sistema.sql`, con `__CODE__`,
  `__ALIAS__` y los nombres.
- `Properties/PublishProfiles/FolderProfile.pubxml` con `PublishUrl` y `PublishDir` a la
  carpeta, `DeleteExistingFiles=false` y un comentario que diga que el deploy completo es
  `deploy\Publicar.ps1`.

Revisa la sintaxis de los `.ps1` con
`[Management.Automation.Language.Parser]::ParseFile(<ruta>, [ref]$t, [ref]$e)`.

Haz un `dotnet publish -c Release -o C:\temp\publish-verificacion` y confirma que **no** trae
`appsettings.Development.json`, `appsettings.Local.json` ni `App_Data`. Revisa también que
`web.config` diga `inheritInChildApplications="false"` y `arguments=".\<Proyecto>.dll"`.

### 8. Configuración del servidor (secretos)

El `appsettings.json` del servidor vive allá y el deploy normal no lo toca. Necesita `Hub`,
`Jwt:Secret` (idéntico al del hub) y las cadenas de conexión de producción.

- **Base**: el `appsettings.json` del servidor si es del **mismo** sistema; si la carpeta está
  vacía o es otra app, el `appsettings.json` del proyecto más las secciones de producción que
  falten.
- **Secreto**: cópialo de un archivo que ya lo tenga, sin imprimirlo. Por ejemplo:
  - el `appsettings.Production.json` de otro satélite clonado (SolicitudDeMovimiento, LegalSystem);
  - el del hub (`\\meaxs066\inetpub\Services\Login\appsettings.json`).
  Si no sabes cuál, pregunta. Según Satelites.md §10, el secreto del repo del hub es el
  mismo que el del servidor.
- Arma el archivo con:
  ```powershell
  & "SKILL_DIR\scripts\Escribir-ConfigServidor.ps1" -Base <appsettings base> `
      -FuenteSecreto <archivo con Jwt:Secret> -SystemCode <CODE> -Salida C:\temp\<code>-servidor
  ```
  Lista solo los nombres de las llaves que siguen vacías (cadenas de conexión, rutas de
  producción). Pide esos datos al usuario o toma los de su fuente conocida, **siempre por
  script y sin mostrarlos**.
- Archivos extra que el sistema necesite en el servidor (credenciales de lectura, etc.): ponlos
  en la misma carpeta de salida. `Publicar.ps1 -ConfigServidor` los copia a `App_Data`, que
  IIS no sirve y el deploy no borra. Las rutas relativas en configuración (`App_Data\x`)
  resuélvelas en código contra `AppContext.BaseDirectory`, no contra el directorio actual.

**Nunca** escribas un secreto en el chat, en un commit ni en un archivo del repo. Si la
plataforma bloquea el manejo de secretos, no busques cómo saltarte el bloqueo. Deja los
scripts listos y dale al usuario los comandos exactos para correrlos él.

### 9. Publicar (con confirmación)

1. Confirma con el usuario antes de publicar si se **reemplaza** una app que ya opera o si es
   la primera vez en esa carpeta.
2. Primera vez (o al cambiar secretos), desde PowerShell:
   ```powershell
   .\deploy\Publicar.ps1 -ConfigServidor C:\temp\<code>-servidor
   ```
   Después **borra** `C:\temp\<code>-servidor`.
3. Siguientes veces: `.\deploy\Publicar.ps1`.
4. Si `robocopy` falla a media copia, el `finally` quita `app_offline.htm`. Revisa el código
   de salida (< 8 = éxito). El respaldo queda en `C:\temp\respaldo-<Carpeta>-<fecha>`.

### 10. Verificar y registrar en el hub

1. Contra producción:
   ```powershell
   & "SKILL_DIR\scripts\Probar-Sso.ps1" -Url http://meax.one/<ALIAS> -Code <CODE> -Publicado [-Estatico <archivo>]
   ```
   Debe responder 302 a `/auth/sso?system=<CODE>&returnUrl=http://meax.one/<ALIAS>/`, y el
   estático, 200.
2. Pide sin sesión `http://meax.one/auth/sso?system=<CODE>&returnUrl=http://meax.one/<ALIAS>/`
   (sin seguir redirecciones):
   - 302 a `/Account/Login` es lo normal sin sesión;
   - un mensaje "no registrado o inactivo" o "returnUrl no corresponde" indica que falta el
     registro.
3. **El registro en `meax_db` (producción, 10.228.25.66) lo hace el usuario o IT** desde
   Gestión de Sistemas. No lo modifiques tú. Entrégale `deploy/registrar-sistema.sql` y la
   lista:
   - `code = <CODE>`, `is_active = 1`, `url = http://meax.one/<ALIAS>/` **con `/` final**;
   - `is_public_visible = 0` mientras se prueba;
   - accesos en `meax_system_access` con rol no nulo, empezando por quien prueba.
4. Si el alias no existe en IIS (404 en `http://meax.one/<ALIAS>/`), pide al admin de
   meaxs066:
   - una aplicación bajo el sitio de meax.one con alias `<ALIAS>` → la carpeta;
   - un **App Pool propio** "Sin código administrado" (dos apps ASP.NET Core en un pool = HTTP 500.35);
   - si se usan, `C:\DataProtectionKeys\<CODE>` y las carpetas de datos con escritura para el pool.
5. Prueba final con sesión real (la hace el usuario; tú no inicias sesión ni capturas
   contraseñas):

   | Prueba | Debe pasar |
   |---|---|
   | Tarjeta del sistema con acceso | Entra sin contraseña; URL sin `meax_token` |
   | Link directo a una página interna con sesión en el hub | Parpadeo por el SSO y cae en esa página |
   | Persona **sin** fila en `meax_system_access` | "Sin acceso" del hub, nunca el sistema |
   | Dashboard del hub tras unos minutos | Entradas **y** tiempo de permanencia |

### 11. Reporte final

Breve, en español:
- Alias y URL final.
- Archivos agregados o cambiados.
- URLs estáticas corregidas.
- Dónde quedaron los respaldos (código y servidor).
- Resultado de `Probar-Sso.ps1` en local y en producción.
- Qué falta del lado del hub/IIS, con el SQL y los pasos.
- Cómo publicar la próxima vez.

## Problemas conocidos

| Síntoma | Causa |
|---|---|
| `Sistema 'X' no registrado o inactivo` | `Hub:SystemCode` ≠ `code`, `is_active = 0`, o se registró en la base de pruebas (.13) |
| `returnUrl no corresponde a la URL registrada` | Falta PathBase en la URL de regreso, se entró por IP, `http` vs `https`, o la `url` registrada no es el alias exacto con `/` final |
| Ciclo infinito satélite ↔ hub | La cookie propia no se guarda (Path cambiado), reloj del servidor desfasado > 6 min |
| "Token de meax.one inválido. Revisa Jwt:Secret." | `Jwt:Secret` distinto al del hub (`IDX10503`) |
| HTTP 500.30 al arrancar | Falta `Jwt:Secret` o `Hub:SystemCode` en el appsettings.json del servidor |
| HTTP 500.35 | Dos apps ASP.NET Core en el mismo App Pool |
| HTTP 500.19 | Se quitó `inheritInChildApplications="false"` del `web.config` |
| Pasa por el SSO en cada página | Mayúsculas distintas en el alias o `Cookie.Path` cambiado |
| Links que llevan a la raíz de meax.one | Rutas con `/` al principio (paso 3) |
| Entradas en el Dashboard pero 0 minutos | El latido no manda `?system=` |
| Se cae la conexión a la base tras un deploy | Se sobrescribió el `appsettings.json` del servidor |
| "The antiforgery token could not be decrypted" tras reciclar | Llaves de DataProtection en memoria (aceptable; o configura `KeysPath`) |
| `App__PathBase=/X` falla con "must start with '/'" en Git Bash | Falta `MSYS_NO_PATHCONV=1` |
| El circuito de Blazor se desconecta al vencer la cookie | Normal: `/_blazor` recibe 401 y al recargar pasa por el SSO solo |
