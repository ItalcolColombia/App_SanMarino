# 15 — Tickets: Rediseño UX (pro/responsive) + Adjuntos + Código de Gestión

**Fecha:** 2026-06-04
**Base:** continúa [14_modulo_tickets_soporte_plan.md](14_modulo_tickets_soporte_plan.md) (módulo ya funcional y probado e2e).
**Orden pedido por el usuario:** 1º **rediseño UX completo y responsive**, después las features.

---

## A. Rediseño UX (PRIORIDAD)

Dirección estética: **refinado-profesional empresarial**, cohesivo con ItalGranja (no maximalista). Mobile-first/responsive. Paleta Italfoods (`ital-orange #e85c25`, `ital-green #2d7a3e`, `ital-cream #faf8f5`, `ital-muted`), fuente del sistema, profundidad con sombras/anillos, micro-interacciones.

Lenguaje visual nuevo:
- **Header tipo hero** por página: tile con ícono en color, título + subtítulo, acción primaria (botón con gradiente/sombra).
- **Fondo** suave (cream/gradiente sutil) en vez de blanco plano.
- **Cards** `rounded-2xl`, `ring-1`, `shadow-sm`, hover lift + acento lateral por tipo/estado.
- **Fila de stats/resumen** (información que pidió el usuario).
- **Empty states** con ícono en círculo suave + CTA.
- **Tipografía**: títulos bold tracking-tight; código en mono; jerarquía clara.

Alcance (todas las páginas/comp.):
- [ ] `ticket-list` (cards compartidas → impacta mis-tickets/gestión/admin)
- [ ] `mis-tickets` (hero + filtros refinados + stats)
- [ ] `ticket-create` (form 2 columnas + tips, dropzone pulido)
- [ ] `ticket-detalle` (layout 2 columnas: contenido + panel lateral meta/gestión, stepper pulido, timeline con conectores)
- [ ] `gestion-tickets` / `admin-tickets` (headers + filtros refinados)
- [ ] `ticket-stepper` / `ticket-estado-badge` (pulido)

## B. Features (después del UX)

### B1. Código de Gestión (campo deshabilitado, único, incremental)
- Formato **4 letras + 5 dígitos**: `AAAA00000`, siguiente `AAAA00001`, … `AAAA99999` → `AAAB00000` … (números incrementan; al desbordar, incrementan las letras, base-26).
- **Único, nunca se repite, no editable** (campo disabled en la UI).
- Implementación robusta: **derivar del `Id` identity global** del ticket (garantiza unicidad + incremental sin secuencia aparte). `n = Id - 1`; letras = base26(n / 100000, 4), dígitos = (n % 100000) `D5`.
- Reemplaza el `codigo` actual (`TK-2026-000001`) por este formato. Se muestra en detalle y en el form (disabled, "se asigna al crear").

### B2. Adjuntar archivos PDF / Excel (además de imágenes)
- Permitir subir **PDF / Excel** (`.pdf`, `.xlsx`, `.xls`) en el ticket, no solo imágenes.
- Reusar la tabla `ticket_imagenes` → renombrar concepto a **adjuntos** (`ticket_adjuntos`) o agregar `tipo`/`content_type` y guardar Base64/`s3_key`. Validar tamaño/tipo. Listar con ícono por tipo; PDF/Excel se descargan, imágenes se ven en lightbox.

### B3. Links de archivos (alternativa a subir)
- Campo para **pegar un link** a un archivo externo (Drive, etc.) en vez de/además de subir. Tabla `ticket_enlaces` (o columna en adjuntos con `url`). Mostrar como chips clickeables (abren en pestaña nueva, con verificación de URL).

---

## Notas técnicas
- Backend del código de gestión: generar en `TicketService.CreateAsync` tras `SaveChanges` (ya hay 2-fases por el Id). Migración no necesaria para el formato (columna `codigo` ya existe, varchar(20) alcanza para 9 chars).
- Adjuntos PDF/Excel + links: requieren migración (nueva tabla/columnas) + endpoints. Hacer idempotente.
- Validar en navegador con HMR del dev server.
