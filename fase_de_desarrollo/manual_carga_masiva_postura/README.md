# Material de carga masiva de postura — versión vigente

Manual y plantillas que se entregan al equipo de operación para cargar por Excel la historia de un
lote de postura (levante y producción). **Esta carpeta es la versión vigente**; la copia suelta
`fase_de_desarrollo/Manual_Carga_Masiva_Postura.docx` es un documento anterior y distinto
(«ITALGRANJA · de punta a punta», 17 pág.), que quedó superado por esta entrega.

| Archivo | Qué es |
|---|---|
| `Manual_Carga_Masiva_Postura.docx` / `.pdf` | Manual, **v1.2** (25 pág.) |
| `Plantilla_Carga_Masiva_LEVANTE.xlsx` | Plantilla de trabajo para preparar el levante antes de entrar al sistema |
| `Plantilla_Carga_Masiva_PRODUCCION.xlsx` | Ídem, producción |
| `Plantilla_SANTA_REYES_LEVANTE.xlsx` | Archivo **tal como lo entrega el sistema** para una empresa con configuración propia |
| `Plantilla_SANTA_REYES_PRODUCCION.xlsx` | Ídem, producción |

## Qué trae la v1.1 (4-sep-2026)

La plantilla dejó de ser una sola para todas las empresas: ahora **el sistema la arma según la
configuración de la empresa del lote** (commits `628a48a` y `683660d`). El manual se actualizó para
que no describa columnas que a un lector concreto no le van a aparecer.

- **§1.3** — la plantilla se adapta a la empresa; abre en «Instrucciones» y trae una hoja
  **«Ejemplo»** con tres días resueltos que no se importa.
- **§4.2 / §7.3** (hoja `Datos`) — fila `Silo Alimento 1/2 H-M`.
- **§4.3** (hoja `Alimento`) — filas `Silo` y `Silo Origen`.
- **§7.4** — con clasificación por ítems la hoja `Datos` no trae `Huevo Total`, `Huevo Incubable`,
  `Peso Huevo (g)` ni las 11 categorías; el desplegable de `Ítem` solo ofrece los tipos declarados
  por el lote; y `Movimientos Huevos` no se emitía para esas empresas *(superado en v1.2)*.
- **§9** — seis mensajes nuevos de silo, con su causa y su solución.
- **Anexo A** — qué hoja emite cada configuración.
- **Anexo C (nuevo)** — «Empresas con configuración propia»: una fila por opción de configuración,
  con qué cambia en la plantilla y qué cambia al cargar.

## Qué trae la v1.2 (5-sep-2026)

`Movimientos Huevos` dejó de ser una hoja que esas empresas no podían usar: ahora tiene **dos
formas** (commit `5dc6b40`).

- **§7.4** — la hoja se explica en sus dos formas, con una tabla de columnas para cada una
  («Columnas cuando el huevo se clasifica por ítems» / «…por las 11 categorías»), cómo se agrupan
  las filas en movimientos, y que ahí se acepta cualquier tipo del catálogo —no solo los declarados
  por el lote— porque un traslado mueve lo que ya se produjo.
- **§9** — dos mensajes nuevos (disponibilidad por tipo de huevo, y tipo inexistente en el catálogo).
- **Anexo A** — `Movimientos Huevos` ya no dice «salvo empresas con clasificación por ítems».
- **Anexo C** — la fila de clasificación por ítems describe la nueva forma de la hoja.

Las plantillas de Santa Reyes se volvieron a descargar del backend **con los tipos de huevo del lote
declarados**, así que muestran la forma completa: 8 hojas en producción, con `Movimientos Huevos` de
9 columnas por ítem y `Huevos` de 3.

## Cómo regenerar el PDF

El `.docx` lleva un índice como campo TOC, así que hay que actualizarlo antes de exportar:

```bash
powershell -NoProfile -Command "$w=New-Object -ComObject Word.Application; $w.Visible=$false; $d=$w.Documents.Open('<ruta>\Manual_Carga_Masiva_Postura.docx',$false,$false); foreach($t in $d.TablesOfContents){$t.Update()}; $d.Fields.Update()|Out-Null; $d.Repaginate(); $d.SaveAs([ref]'<ruta>\Manual_Carga_Masiva_Postura.pdf',[ref]17); $d.Close([ref]$false); $w.Quit()"
```

⚠️ Word COM cuelga si el archivo está en `%TEMP%`: trabajá sobre una carpeta normal.

## Las plantillas de Santa Reyes son una MEDICIÓN, no un diseño

Se descargaron del backend real (lote 152, empresa 6) con los flags encendidos, así que muestran
exactamente lo que ve esa empresa: 22 columnas en vez de 43, sin machos, sin huevo por columnas
fijas, con `Silo Alimento 1/2 H`, y la hoja `Referencias` con el silo asignado al lote. Si se vuelve
a tocar el generador, la forma de actualizar este material es **volver a descargarlas**, no editarlas
a mano.
