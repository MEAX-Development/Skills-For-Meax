#!/usr/bin/env python3
"""
i18n_check.py: revisa las traducciones de un sistema que usa translate-system.

    python i18n_check.py <proyecto>                          # reporte; exit 1 si hay errores
    python i18n_check.py <proyecto> --hardcoded --out r.txt  # además lista textos que parecen no estar traducidos
    python i18n_check.py <proyecto> --fix-en                 # agrega a en.json las llaves que falten (llave = valor)
    python i18n_check.py <proyecto> --from-glossary <dir>    # llena es/ja faltantes con las traducciones del glosario
    python i18n_check.py <proyecto> --missing-json f.json    # llaves faltantes por idioma, para traducirlas
    python i18n_check.py <proyecto> --merge lote1.json ...   # integra {"es": {...}, "ja": {...}, "en": {...}}
    python i18n_check.py <proyecto> --prune                  # borra de los JSON las llaves que el código ya no usa
    python i18n_check.py <proyecto> --sort                   # reescribe es/en/ja.json ordenados y con formato uniforme
    python i18n_check.py <proyecto> --to-glossary <dir>      # agrega al glosario las llaves nuevas (no cambia las que ya hay)

<proyecto> es la carpeta del .csproj web (o la de la solución si solo hay uno). Las llaves se
buscan en L["..."] y Loc.T("...") (.razor, .cs, .cshtml) y en moT('...') (.js), sin contar
comentarios. Una línea con el comentario i18n-ignore no se reporta en --hardcoded.
"""
from __future__ import annotations

import argparse
import io
import json
import re
import sys
from pathlib import Path

LANGS = ("en", "es", "ja")
SKIP_DIRS = {"bin", "obj", "node_modules", ".git", ".vs", ".vscode", "lib", "Migrations", "TestResults"}
CODE_EXT = {".razor", ".cs", ".cshtml"}

CS_STR = r'"((?:[^"\\\n]|\\.)*)"'
KEY_PATTERNS = [
    re.compile(r'(?<![\w.])L\[\s*' + CS_STR),
    re.compile(r'\bLoc\.T\(\s*' + CS_STR),
]
JS_PATTERN = re.compile(r"""\bmoT\(\s*(['"])((?:(?!\1)[^\\\n]|\\.)*)\1""")
DYNAMIC_PATTERN = re.compile(r'(?<![\w.])(?:L\[\s*\$@?"|Loc\.T\(\s*\$@?")')
PLACEHOLDER = re.compile(r"\{(\d+)(?:[,:][^}]*)?\}")
LETTER = r"A-Za-zÁÉÍÓÚÑÜáéíóúñü"


def unescape_cs(s: str) -> str:
    def rep(m: re.Match) -> str:
        c = m.group(1)
        if c.startswith("u"):
            return chr(int(c[1:], 16))
        return {"n": "\n", "t": "\t", "r": "\r", "0": "\0", '"': '"', "'": "'", "\\": "\\"}.get(c, c)
    return re.sub(r"\\(u[0-9a-fA-F]{4}|.)", rep, s)


def iter_files(root: Path, exts: set[str]):
    for p in sorted(root.rglob("*")):
        if p.suffix.lower() not in exts or not p.is_file():
            continue
        rel = p.relative_to(root).parts
        if any(part in SKIP_DIRS for part in rel[:-1]):
            continue
        if p.name.endswith((".min.js", ".g.cs", ".Designer.cs")) or p.name == "mo-language.js":
            continue
        yield p


def line_of(text: str, idx: int) -> int:
    return text.count("\n", 0, idx) + 1


def strip_code_comments(text: str, razor: bool) -> str:
    """Quita //, /* */ y @* *@ respetando las cadenas "..." y conservando los saltos de línea."""
    res, i, n, in_str = [], 0, len(text), False
    while i < n:
        c = text[i]
        if in_str:
            res.append(c)
            if c == "\\" and i + 1 < n:
                res.append(text[i + 1]); i += 2; continue
            if c == '"' or c == "\n":
                in_str = False
            i += 1
            continue
        end = None
        if c == '"':
            in_str = True
        elif razor and text.startswith("@*", i):
            end = text.find("*@", i + 2); end = n if end < 0 else end + 2
        elif text.startswith("/*", i):
            end = text.find("*/", i + 2); end = n if end < 0 else end + 2
        elif text.startswith("//", i) and (i == 0 or text[i - 1] != ":"):
            end = text.find("\n", i); end = n if end < 0 else end
        if end is not None:
            res.append("\n" * text.count("\n", i, end)); i = end; continue
        res.append(c); i += 1
    return "".join(res)


def collect_keys(root: Path):
    used: dict[str, list[str]] = {}
    dynamic: list[str] = []
    for f in iter_files(root, CODE_EXT | {".js"}):
        raw = f.read_text(encoding="utf-8-sig", errors="replace")
        text = strip_code_comments(raw, razor=f.suffix in (".razor", ".cshtml"))
        rel = f.relative_to(root).as_posix()
        if f.suffix == ".js":
            for m in JS_PATTERN.finditer(text):
                used.setdefault(unescape_cs(m.group(2)), []).append(f"{rel}:{line_of(text, m.start())}")
            continue
        for pat in KEY_PATTERNS:
            for m in pat.finditer(text):
                used.setdefault(unescape_cs(m.group(1)), []).append(f"{rel}:{line_of(text, m.start())}")
        for m in DYNAMIC_PATTERN.finditer(text):
            dynamic.append(f"{rel}:{line_of(text, m.start())}")
    return used, dynamic


def strip_json_comments(text: str) -> str:
    return re.sub(r",(\s*[}\]])", r"\1", strip_code_comments(text, razor=False))


def load_json(path: Path):
    """Devuelve (dict, llaves duplicadas, error)."""
    if not path.exists():
        return {}, [], None
    raw = path.read_text(encoding="utf-8-sig")
    dups: list[str] = []

    def hook(pairs):
        d = {}
        for k, v in pairs:
            if k in d:
                dups.append(k)
            d[k] = v
        return d
    try:
        return json.loads(strip_json_comments(raw), object_pairs_hook=hook), dups, None
    except json.JSONDecodeError as e:
        return {}, [], str(e)


def write_json(path: Path, data: dict) -> None:
    ordered = dict(sorted(data.items(), key=lambda kv: (kv[0].casefold(), kv[0])))
    path.write_text(json.dumps(ordered, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")


# ── Textos que parecen no estar traducidos ───────────────────────────────────
ATTRS = (r"(?:placeholder|title|aria-label|alt|data-label|data-l|data-title|data-tooltip|Title|Subtitle|Label|Text|"
         r"Placeholder|Message|Description|Header|ConfirmText|EmptyText|Tooltip|Caption)")
ATTR_PATTERN = re.compile(r"(?<![\w-])" + ATTRS + r'\s*=\s*"([^"]*)"')
DIRECTIVE = re.compile(r"^\s*@(?:inject|using|page|rendermode|attribute|implements|inherits|layout|namespace|typeparam)\b.*$", re.M)
CODE_LINE = re.compile(r"^\s*(?:[@{}]|else\b|var\b|if\b|for(?:each)?\b|case\b|default\s*:|break\b|return\b|switch\b|//)|;\s*$|=>")
RAZOR_EXPR = re.compile(r"@(?:\([^)]*\)|[\w.?!]+(?:\[[^\]]*\]|\([^)]*\))*(?:\??\.[\w]+(?:\([^)]*\))?)*)")
ACRONYM = re.compile(r"^[A-Z0-9][A-Z0-9/&.\-]{0,5}$")
IN_EXPR_LITERAL = re.compile(r'(?<!L\[)(?<!Loc\.T\()"([' + LETTER + r'][^"\n{}]*)"')
CS_UI_LITERAL = re.compile(r'(?<![\w\[(.])"([A-ZÁÉÍÓÚÑ¿¡][^"\n{}]*?\s[^"\n{}]*?)"')
CS_RETURNED_WORD = re.compile(r'(?:=>|return)\s*"([A-ZÁÉÍÓÚÑ][' + LETTER + r' ]{1,40})"')
CS_SKIP = re.compile(r"Log\w*\(|logger|_log\b|Exception\(|nameof|\[(?:Route|Http\w+|Display|Table|Column|JsonPropertyName)|GetSection|"
                     r"config\[|Configuration\[|Regex|\bsql\b|SELECT |INSERT |UPDATE |DELETE | FROM | WHERE |L\[|Loc\.T\(|moT\(|i18n-ignore", re.I)


def blank(text: str, pattern: str, flags=re.S | re.I) -> str:
    return re.sub(pattern, lambda m: "\n" * m.group(0).count("\n"), text, flags=flags)


def letters(s: str) -> int:
    return len(re.findall(f"[{LETTER}]", s))


def hardcoded(root: Path):
    found: list[str] = []
    for f in iter_files(root, {".razor", ".cshtml"}):
        rel = f.relative_to(root).as_posix()
        text = f.read_text(encoding="utf-8-sig", errors="replace")
        raw_lines = text.split("\n")
        code_at = re.search(r"^@(?:code|functions)\b", text, re.M)
        markup = text[: code_at.start()] if code_at else text
        markup = blank(markup, r"@\*.*?\*@")
        markup = blank(markup, r"<!--.*?-->")
        markup = DIRECTIVE.sub("", markup)
        for tag in ("script", "style", "svg"):
            markup = blank(markup, rf"<{tag}\b.*?</{tag}>")
        # Lambdas y comparaciones de C# no son etiquetas: x => x.Id, Count > 0, a >= b.
        markup = re.sub(r"=>|>=|<=| > | < |&&|\?\?", "§", markup)

        def ignored(line_no: int) -> bool:
            return "i18n-ignore" in raw_lines[line_no - 1] if 0 < line_no <= len(raw_lines) else False

        for m in re.finditer(r">([^<>]+)<", markup):
            start_line = line_of(markup, m.start(1))
            for k, line in enumerate(m.group(1).split("\n")):
                if CODE_LINE.search(line) or ignored(start_line + k):
                    continue
                rest = RAZOR_EXPR.sub("", line).strip()
                if letters(rest) >= 2 and not rest.startswith(("&", "http")) and not ACRONYM.match(rest):
                    found.append(f"{rel}:{start_line + k}: texto  {line.strip()[:90]}")
        for m in ATTR_PATTERN.finditer(markup):
            ln = line_of(markup, m.start())
            if ignored(ln):
                continue
            value = m.group(1)
            if "L[" in value or "Loc.T(" in value:
                continue
            if letters(RAZOR_EXPR.sub("", value)) >= 2 or IN_EXPR_LITERAL.search(value):
                found.append(f"{rel}:{ln}: atributo  {m.group(0)[:90]}")
        # Literales dentro de expresiones Razor: @(x ? "Yes" : "No"), @($"Hi {x}")
        for m in re.finditer(r"@\(([^()\n]*(?:\([^()\n]*\)[^()\n]*)*)\)", markup):
            ln = line_of(markup, m.start())
            if ignored(ln):
                continue
            for lit in IN_EXPR_LITERAL.finditer(m.group(1)):
                s = lit.group(1).strip()
                # Las clases CSS y los valores técnicos van en minúsculas y sin espacios.
                if letters(s) >= 2 and (" " in s or s[0].isupper()):
                    found.append(f"{rel}:{ln}: expresión  \"{s[:80]}\"")
        if code_at:
            base = line_of(text, code_at.start())
            for k, line in enumerate(text[code_at.start():].split("\n")):
                if line.lstrip().startswith(("//", "///", "*")) or CS_SKIP.search(line):
                    continue
                for pat in (CS_UI_LITERAL, CS_RETURNED_WORD):
                    for m in pat.finditer(line):
                        found.append(f"{rel}:{base + k}: @code  \"{m.group(1)[:80]}\"")
    for f in iter_files(root, {".cs"}):
        rel = f.relative_to(root).as_posix()
        if "/Localization/" in f"/{rel}" or rel.endswith(("Program.cs", "SeedData.cs")) or ".Tests" in rel:
            continue
        for k, line in enumerate(f.read_text(encoding="utf-8-sig", errors="replace").split("\n"), 1):
            if line.lstrip().startswith(("//", "///", "*")) or CS_SKIP.search(line):
                continue
            for pat in (CS_UI_LITERAL, CS_RETURNED_WORD):
                for m in pat.finditer(line):
                    found.append(f"{rel}:{k}: C#  \"{m.group(1)[:80]}\"")
    return sorted(set(found), key=lambda s: (s.split(":")[0], int(s.split(":")[1])))


def resolve_project(path: Path) -> Path | None:
    root = path.resolve()
    if any(root.glob("*.csproj")):
        return root
    hits = [p.parent for p in root.rglob("*.csproj")
            if "test" not in p.name.lower() and not any(s in p.parts for s in SKIP_DIRS)]
    web = [h for h in hits if "Microsoft.NET.Sdk.Web" in next(h.glob("*.csproj")).read_text(encoding="utf-8-sig", errors="replace")]
    return (web or hits)[0] if len(web or hits) == 1 else None


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("project", type=Path)
    ap.add_argument("--hardcoded", action="store_true")
    ap.add_argument("--fix-en", action="store_true")
    ap.add_argument("--from-glossary", type=Path)
    ap.add_argument("--missing-json", type=Path)
    ap.add_argument("--merge", type=Path, nargs="*", default=[], help="lotes de traducciones a integrar")
    ap.add_argument("--prune", action="store_true")
    ap.add_argument("--sort", action="store_true")
    ap.add_argument("--to-glossary", type=Path)
    ap.add_argument("--out", type=Path, help="escribe el reporte completo en este archivo")
    ap.add_argument("--max", type=int, default=40, help="máximo de renglones por sección en pantalla")
    a = ap.parse_args()

    buf = io.StringIO()
    out = lambda msg="": print(msg, file=buf)  # noqa: E731
    limit = 10**9 if a.out else a.max

    root = resolve_project(a.project)
    if root is None:
        print(f"ERROR: no encuentro un único .csproj web en {a.project.resolve()}")
        return 2
    res_dir = root / "Resources"

    used, dynamic = collect_keys(root)
    data, errors = {}, 0
    for lang in LANGS:
        d, dups, err = load_json(res_dir / f"{lang}.json")
        if err:
            out(f"ERROR {lang}.json no es JSON válido: {err}")
            errors += 1
        if dups:
            out(f"ERROR {lang}.json tiene llaves duplicadas: {dups[:limit]}")
            errors += 1
        data[lang] = d
    changed = False

    for lote in a.merge:
        piezas, _, err = load_json(lote)
        if err:
            print(f"ERROR {lote} no es JSON válido: {err}")
            return 2
        for lang in LANGS:
            data[lang].update({k: v for k, v in (piezas.get(lang) or {}).items() if isinstance(v, str) and v.strip()})
        out(f"{lote.name}: " + ", ".join(f"{l} +{len(piezas.get(l) or {})}" for l in LANGS))
        changed = True
    if a.from_glossary:
        for lang in ("es", "ja"):
            glos, _, err = load_json(a.from_glossary / f"{lang}.json")
            if err:
                print(f"ERROR glosario {lang}.json: {err}")
                return 2
            nuevas = {k: glos[k] for k in used if k not in data[lang] and isinstance(glos.get(k), str) and glos[k].strip()}
            data[lang].update(nuevas)
            out(f"glosario → {lang}.json: {len(nuevas)} llaves")
        changed = True
    if a.fix_en:
        added = [k for k in used if k not in data["en"]]
        data["en"].update({k: k for k in added})
        out(f"en.json: {len(added)} llaves agregadas.")
        changed = True
    if a.prune:
        for lang in LANGS:
            sobran = [k for k in data[lang] if k not in used]
            for k in sobran:
                del data[lang][k]
            out(f"{lang}.json: {len(sobran)} llaves sin uso borradas" + (f" ({sobran[:limit]})" if sobran else ""))
        changed = True
    if changed or a.sort:
        res_dir.mkdir(exist_ok=True)
        for lang in LANGS:
            write_json(res_dir / f"{lang}.json", data[lang])
        out("Resources/es.json, en.json y ja.json guardados (orden alfabético).")

    if a.to_glossary:
        for lang in ("es", "ja"):
            glos, _, err = load_json(a.to_glossary / f"{lang}.json")
            if err:
                print(f"ERROR glosario {lang}.json: {err}")
                return 2
            nuevas = {k: v for k, v in data[lang].items() if k not in glos}
            glos.update(nuevas)
            write_json(a.to_glossary / f"{lang}.json", glos)
            out(f"glosario {lang}.json: +{len(nuevas)} llaves (total {len(glos)})")

    out(f"\nProyecto: {root}")
    out(f"Llaves usadas en el código: {len(used)}")
    for lang in LANGS:
        out(f"  {lang}.json: {len(data[lang])} entradas")
    if not used and not any(data.values()):
        errors += 1
        out("\nAVISO: no hay llaves en el código ni JSON en Resources/: el sistema todavía no está traducido.")

    missing = {lang: sorted(k for k in used if k not in data[lang]) for lang in LANGS}
    for lang in LANGS:
        if missing[lang]:
            errors += 1
            out(f"\nFALTAN en {lang}.json ({len(missing[lang])}):")
            for k in missing[lang][:limit]:
                out(f"  - {k!r}   ({used[k][0]})")
            if len(missing[lang]) > limit:
                out(f"  … y {len(missing[lang]) - limit} más")

    bad_ph, empty = [], []
    for lang in LANGS:
        for k, v in data[lang].items():
            if not isinstance(v, str) or not v.strip():
                empty.append(f"{lang}: {k!r}")
            elif sorted(set(PLACEHOLDER.findall(k))) != sorted(set(PLACEHOLDER.findall(v))):
                bad_ph.append(f"{lang}: {k!r} → {v!r}")
    if bad_ph:
        errors += 1
        out(f"\nPLACEHOLDERS distintos entre llave y traducción ({len(bad_ph)}):")
        for s in bad_ph[:limit]:
            out(f"  - {s}")
    if empty:
        errors += 1
        out(f"\nVALORES vacíos ({len(empty)}):")
        for s in empty[:limit]:
            out(f"  - {s}")
    if dynamic:
        errors += 1
        out(f"\nLLAVES dinámicas (L[$\"...\"] no se puede traducir; usa placeholders {{0}}) ({len(dynamic)}):")
        for s in dynamic[:limit]:
            out(f"  - {s}")

    same_ja = [k for k, v in data["ja"].items() if v == k and re.search(r"[a-z]{3,}", k)]
    if same_ja:
        out(f"\nAVISO: {len(same_ja)} entradas de ja.json siguen en inglés (revisa si es a propósito):")
        for k in same_ja[:limit]:
            out(f"  - {k!r}")
    unused = sorted(set().union(*[set(data[l]) for l in LANGS]) - set(used))
    if unused:
        out(f"\nAVISO: {len(unused)} llaves en los JSON que no aparecen en el código (sobrantes, o usadas con una variable): --prune las borra")
        for k in unused[:limit]:
            out(f"  - {k!r}")

    if a.missing_json:
        a.missing_json.write_text(json.dumps(missing, ensure_ascii=False, indent=2), encoding="utf-8")
        out(f"\nFaltantes guardadas en {a.missing_json}")

    if a.hardcoded:
        found = hardcoded(root)
        cap = limit if a.out else max(a.max, 200)
        out(f"\nPOSIBLES textos sin traducir ({len(found)}); revisa cada uno, no todos lo son (marca con // i18n-ignore los intencionales):")
        for s in found[:cap]:
            out(f"  {s}")
        if len(found) > cap:
            out(f"  … y {len(found) - cap} más (usa --out para verlos todos)")

    resultado = "RESULTADO: " + ("OK" if errors == 0 else f"{errors} problema(s)")
    out("\n" + resultado)
    try:
        sys.stdout.reconfigure(encoding="utf-8")  # type: ignore[attr-defined]
    except Exception:
        pass
    if a.out:
        a.out.write_text(buf.getvalue(), encoding="utf-8")
        print(f"Reporte completo en {a.out}")
        print(resultado)
    else:
        print(buf.getvalue(), end="")
    return 0 if errors == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
