# Tickets — descarga de adjuntos dañada (30-sep-2026)

## Síntoma
En el detalle de un ticket, al descargar un documento adjunto (PDF/Excel/Word) el archivo
no abre ("dañado") o el navegador no lo deja ver.

## Causa raíz
Dos caminos de subida guardaban `ticket_adjuntos.contenido_base64` en formatos distintos:

| Camino | Qué guardaba |
|---|---|
| Detalle del ticket (`onDocumentoSeleccionado`) | base64 puro (hace `split(',')[1]`) ✅ |
| Crear ticket (`TicketAdjuntosInputComponent.fileToBase64`) | **data URL completa** `data:<tipo>;base64,<...>` ❌ |

La descarga arma `data:<tipo>;base64,${contenido}` ⇒ con los adjuntos subidos al CREAR el
ticket quedaba `data:x;base64,data:x;base64,...` → bytes basura. Además, un `<a href="data:...">`
con archivos de varios MB es frágil en Chrome/Edge (límites de data URL).

## Enfoque
1. **Back — lógica pura** `Application/Calculos/TicketAdjuntoCalculos.NormalizarBase64`:
   quita el prefijo `data:...;base64,` y espacios/saltos. Se aplica:
   - al **escribir** (`AddDocumentoAsync`) → no entran más filas con prefijo;
   - al **leer** (`GetDocumentoAsync`) → las filas viejas ya guardadas con prefijo se sirven bien
     sin backfill de datos (cero DDL/DML en prod).
2. **Front — creación**: `ticket-adjuntos-input` manda base64 puro (igual que el detalle).
3. **Front — descarga**: función pura `funciones/base64-a-blob.funcion.ts` (tolera prefijo,
   decodifica a `Blob`); el componente descarga con `URL.createObjectURL` + revoke, sin data URL.
   Contrato del endpoint `GET /api/Tickets/{id}/adjuntos/{adjuntoId}/descargar` sin cambios.

## Archivos
- `backend/src/ZooSanMarino.Application/Calculos/TicketAdjuntoCalculos.cs` (nuevo)
- `backend/src/ZooSanMarino.Infrastructure/Services/Tickets/Funciones/TicketService.Adjuntos.cs`
- `backend/tests/ZooSanMarino.Application.Tests/TicketAdjuntoCalculosTests.cs` (nuevo)
- `frontend/src/app/features/tickets/funciones/base64-a-blob.funcion.ts` (+ spec)
- `frontend/src/app/features/tickets/pages/ticket-detalle/ticket-detalle.component.ts`
- `frontend/src/app/features/tickets/components/ticket-adjuntos-input/ticket-adjuntos-input.component.ts`

## BD
Ninguna. Las filas legacy se corrigen al leer.

## Casos de prueba
- base64 puro → sin cambios.
- data URL (`data:application/pdf;base64,JVBER...`) → queda `JVBER...`.
- base64 con saltos de línea/espacios → sin espacios.
- null/vacío → vacío.
- Front: `base64ABlob` con y sin prefijo produce los mismos bytes.
