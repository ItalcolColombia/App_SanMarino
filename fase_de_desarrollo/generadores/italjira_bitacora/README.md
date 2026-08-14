# Generador de la bitácora ItalJira (julio–agosto 2026)

Reconstruye el SQL de la migración `20260814010000_SeedBitacoraSesionesJulAgo2026`. Vive en el
repo y no en un scratchpad a propósito: el seed anterior quedó marcado como «regenerable con el
script del scratchpad» y ese directorio ya no existe.

## Orden de ejecución (desde este directorio)

```bash
python extraer_sesiones.py   # transcripciones de sesión → sesiones.json (fechas, pedido, archivos)
python cruzar.py             # + git log y el seed anterior → cruce.json (commits por sesión)
python armar_items.py        # → items.json + tabla_revision.txt (la tabla que se lee para estimar)
python generar_seed.py       # → ...SeedBitacoraSesionesJulAgo2026.Seed.cs
```

Los intermedios (`sesiones.json`, `cruce.json`, `items.json`, `tabla_revision.txt`) son
temporales y no se versionan.

## Qué es dato y qué es criterio

- **Dato:** el pedido textual, las fechas, la duración de sesión, los commits y los bugs
  (`fix(...)`). Sale de las transcripciones de `~/.claude/projects/…` y de `git log`.
- **Criterio:** `horas_estimadas` y, en las tareas nuevas, el título, el tipo y la historia.
  Todo eso está a mano en [`../../italjira_bitacora_sesiones_jul_ago_2026_horas.json`](../../italjira_bitacora_sesiones_jul_ago_2026_horas.json)
  y es lo único que hay que editar para corregir una estimación.

## Dos trampas que ya se pagaron

1. **Atribuir commits por la ventana de la sesión no funciona.** Hay sesiones que quedan abiertas
   días (la del 01-jul se retomó el 25-jul): una sola se llevaba 113 commits. Se atribuye por
   **segmentos de actividad** (cortes con hueco > 30 min) y se desempata por solape de archivos,
   porque el repo se trabaja con varias sesiones en paralelo.
2. **No se le inventa dueño a un commit.** Si no cae en ningún segmento y no comparte archivos
   con nadie, queda sin atribuir (96 de 447, casi todos `docs(tracker)` y merges). Un hueco es
   preferible a una evidencia falsa.
