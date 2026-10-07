#!/usr/bin/env python3
"""Genera glossary/terms.tsv a partir del glossary.tsv del Traductor (v3+).

Uso (mantenedor):
    python terms_from_traductor.py <Traductor>/Traductor/glossary.tsv ../glossary/terms.tsv

Toma las lineas "tri" (espanol / english / japones) y deja una tabla con la
llave en ingles, que es como la skill escribe las llaves de Resources/*.json.
Reglas:
  - Fuera las filas cuya celda en ingles lleva "~": en ingles ese termino es
    ambiguo (key, power, fire...) y en una pantalla casi siempre significa
    otra cosa.
  - En espanol y japones se quita la marca "~": ahi solo indicaba que el
    Traductor no lo busca, la traduccion sigue siendo la aprobada.
  - A igual llave en ingles gana la primera del archivo, igual que en el
    Traductor (lo curado va antes que lo masivo).
  - Al principio van los terminos de MX-6016 (el unico procedimiento trilingue
    de MEAX), que en el Traductor no tienen columna en ingles.
"""
import sys
from pathlib import Path

# MX-6016 "Control de salidas no conformes", trilingue (es / en / ja).
MX6016 = [
    ("suspect product", "producto sospechoso", "保留品"),
    ("nonconforming product", "producto no conforme", "不適合品"),
    ("NG product", "producto NG", "NG品"),
    ("containment", "contención", "封じ込め"),
    ("containment action", "acción de contención", "封じ込め処置"),
    ("sorting", "sorteo", "選別"),
    ("rework", "retrabajo", "手直し"),
    ("reprocess", "reproceso", "再加工"),
    ("quarantine", "cuarentena", "隔離"),
    ("scrap", "scrap", "スクラップ"),
    ("concession", "concesión", "特採"),
    ("escalation", "escalamiento", "エスカレーション"),
    ("minor stoppage", "paro menor", "チョコ停"),
    ("requesting area", "área solicitante", "要求部門"),
    # Nombres de departamento. "Quality" o "Production" sueltos en una
    # pantalla casi nunca son el departamento: van aparte (品質, 生産).
    ("Production Control", "Control de Producción", "生産計画部門"),
    ("Production department", "Producción", "製造部門"),
    ("Quality department", "Calidad", "品質部門"),
    ("Engineering department", "Ingeniería", "技術部門"),
    ("Sales department", "Ventas", "営業"),
    ("operator", "operador", "作業者"),
    ("abnormality", "anormalidad", "異常"),
    ("nonconformity", "no conformidad", "不適合"),
]

# En una pantalla estas palabras casi siempre son otra cosa ("Resume" es
# reanudar, no curriculum).
UI_MEANS_OTHER = {"resume"}

HEADER = """# terms.tsv — terminologia MEAX en tres idiomas, para traducir pantallas.
# Columnas: english <TAB> espanol <TAB> 日本語
#
# GENERADO con scripts/terms_from_traductor.py desde el glossary.tsv del
# Traductor (v3: Diccionario Tecnico MEAX + procedimientos MX-6xxx/7xxx). No
# se edita a mano: se corrige en el Traductor y se vuelve a generar.
#
# Como se usa: cuando una llave nueva lleva un termino tecnico (produccion,
# calidad, alternadores, marchas, mantenimiento, seguridad, RH, finanzas,
# legal), busca aqui el termino en ingles y usa ESA traduccion en es.json y
# ja.json. Si glossary/es.json o glossary/ja.json ya traen la llave completa,
# esas mandan: son las traducciones de pantalla aprobadas.
#
# Las primeras filas son de MX-6016, el unico procedimiento trilingue de
# MEAX: ganan sobre cualquier otra grafia (保留品, 封じ込め, 選別...).
"""


def main(src: Path, out: Path) -> int:
    rows, seen = [], set()

    def add(en: str, es: str, ja: str) -> None:
        key = en.strip().lower()
        if not key or key in seen or not es or not ja:
            return
        seen.add(key)
        rows.append((en.strip(), es.strip(), ja.strip()))

    for en, es, ja in MX6016:
        add(en, es, ja)
    for line in src.read_text(encoding="utf-8").splitlines():
        parts = line.split("\t")
        if len(parts) != 4 or parts[0] != "tri":
            continue
        es, en, ja = (p.strip() for p in parts[1:])
        if en.startswith("~") or en.lower() in UI_MEANS_OTHER or en in ("", "-") or es in ("", "-") or ja in ("", "-"):
            continue
        add(en, es.lstrip("~").strip(), ja.lstrip("~").strip())

    body = "\n".join("\t".join(r) for r in rows[:len(MX6016)])
    rest = sorted(rows[len(MX6016):], key=lambda r: r[0].lower())
    text = HEADER + "\n" + body + "\n\n" + "\n".join("\t".join(r) for r in rest) + "\n"
    out.write_text(text, encoding="utf-8", newline="\n")
    print(f"{out}: {len(rows)} terminos")
    return 0


if __name__ == "__main__":
    if len(sys.argv) != 3:
        print(__doc__)
        sys.exit(2)
    sys.exit(main(Path(sys.argv[1]), Path(sys.argv[2])))
