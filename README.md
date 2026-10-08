# Skills-For-Meax

Skills de [Claude Code](https://code.claude.com) para los sistemas internos de MEAX, publicados
como marketplace de plugins.

| Plugin | Qué hace |
|---|---|
| [`translate-system`](plugins/translate-system/skills/translate-system/SKILL.md) | Traduce un sistema Blazor Web App (.NET 8+) a Español, English y 日本語. Instala `Resources/{es,en,ja}.json`, toma el idioma del claim `Idioma` del JWT de MEAX One, agrega el selector con banderas en el menú de usuario, reemplaza los textos de todas las pantallas y valida que no falte nada. |
| [`connect-meaxone`](plugins/connect-meaxone/skills/connect-meaxone/SKILL.md) | Vincula un sistema .NET (Blazor o MVC) a MEAX One como satélite. Solo pregunta el código del sistema y la carpeta de publicación. Instala el SSO del hub con cookie propia, el login de desarrollo y el latido; deja las URLs relativas al PathBase; arma la configuración del servidor sin mostrar secretos; publica con respaldo y `app_offline`; verifica el SSO y entrega el SQL de registro en `meax_db`. |

## Instalación

**Si el administrador lo habilitó en claude.ai:** no hay que hacer nada. Se sincroniza solo con
Claude Code al abrir una sesión.

**Manual, desde Claude Code:**

```
/plugin marketplace add MEAX-Development/Skills-For-Meax
/plugin install translate-system@skills-for-meax
/plugin install connect-meaxone@skills-for-meax
```

Para traer cambios: `/plugin marketplace update skills-for-meax`.

## Uso

Dentro del repo del sistema, en Claude Code:

```
/translate-system
```

o pídelo con tus palabras ("traduce este sistema a español, inglés y japonés"). Sirve también
para completar traducciones de un sistema que ya la usa.

```
/connect-meaxone
```

o "conecta este sistema a MEAX One". Pregunta solo el código del sistema (el `code` de
`meax_all_system`) y la carpeta de publicación (`\\meaxs066\inetpub\Services\<Carpeta>`).
El registro en `meax_db` y el alias de IIS los hace IT; la skill entrega el SQL y la lista.

**Requisitos en el equipo:** Python 3 (translate-system), PowerShell y el .NET SDK del sistema.
Para publicar, acceso a la carpeta del servidor.

## Mantenimiento

- **Glosario** (`plugins/translate-system/skills/translate-system/glossary/`): son las
  traducciones aprobadas que se reusan entre sistemas, para que todos usen los mismos términos.
  Solo lo actualiza el mantenedor. Para sumar las llaves de un sistema recién traducido, sin
  cambiar las que ya están:

  ```
  python plugins/translate-system/skills/translate-system/scripts/i18n_check.py <proyecto> --to-glossary plugins/translate-system/skills/translate-system/glossary
  ```

- **Terminología** (`glossary/terms.tsv`): inglés → español → japonés, generada del
  `glossary.tsv` del Traductor (v3: Diccionario Técnico MEAX). No se edita a mano: se corrige
  en el Traductor y se regenera:

  ```
  python plugins/translate-system/skills/translate-system/scripts/terms_from_traductor.py <Traductor>/Traductor/glossary.tsv plugins/translate-system/skills/translate-system/glossary/terms.tsv
  ```

  Las llaves completas de `es.json`/`ja.json` le ganan: son las traducciones de pantalla
  aprobadas ("Resume" en una pantalla es *Reanudar*, no *currículum*).

- **Versiones:** `plugin.json` no fija `version` a propósito, así que cada push a `main` es una
  versión nueva. Con la sincronización automática de la organización, el cambio les llega a todos
  en su siguiente sesión de Claude Code.

Mantenedor: Fabian Gonzalez.
