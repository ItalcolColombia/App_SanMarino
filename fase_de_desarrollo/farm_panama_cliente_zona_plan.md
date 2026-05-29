# Plan: Campos Cliente/Zona Panamá en Modal de Crear/Editar Granja

**Fecha:** 2026-05-27  
**Branch:** req-panama-inicial

---

## Contexto

El modal de Granjas (tab "Granjas" en Gestión de Granja) vive en `farm-list.component.ts/.html`.  
El `FarmDto`, `CreateFarmDto` y `UpdateFarmDto` **ya tienen** los campos Panama (`clienteId`, `zona`, `certificadoGab`, `latitud`, `longitud`), pero el modal NO los expone en el formulario.

La directiva `*appShowIfCountry` y el `CountryFilterService.isPanama()` ya existen.  
El `ClienteService.getAll()` ya devuelve `ClienteDto[]` con campo `zona`.

---

## Archivos a modificar

| Archivo | Cambio |
|---|---|
| `farm-list.component.ts` | Imports, propiedad `clientes`, getter `isPanama`, `buildForm`, `loadModalData`, `openModal`, `save`, nuevos métodos |
| `farm-list.component.html` | Sección Panamá en modal crear/editar + detalle |

---

## Cambios en farm-list.component.ts

1. **Imports nuevos:**
   - `ClienteService` desde `'../../../clientes/services/cliente.service'`
   - `ClienteDto` desde `'../../../clientes/models/cliente.models'`
   - `CountryFilterService` desde `'../../../../core/services/country/country-filter.service'`

2. **Propiedad:** `clientes: ClienteDto[] = []`

3. **Constructor:** inyectar `clienteSvc: ClienteService` y `countryFilter: CountryFilterService`

4. **Getter:** `get isPanama(): boolean { return this.countryFilter.isPanama(); }`

5. **`buildForm()`** — agregar campos Panamá:
   - `clienteId: [null]`
   - `zona: [{ value: '', disabled: true }]`  ← disabled porque es sólo lectura (auto-poblada)
   - `certificadoGab: [false]`
   - `latitud: [null]`
   - `longitud: [null]`

6. **`loadModalData()`** — cargar clientes si es Panamá y aún no se han cargado

7. **`loadClientes()`** — método privado que llama `clienteSvc.getAll()`

8. **`onClienteChange(clienteIdRaw)`** — busca el cliente y auto-puebla `zona`

9. **`capturarUbicacion()`** — Geolocation API, parcha `latitud`/`longitud`

10. **`getClienteNombre(id)`** — helper para el modal detalle

11. **`openModal(farm?)`** — patch Panama fields al resetear el form (edición y nuevo)

12. **`save()`** — incluir Panama fields en `dtoBase`

---

## Cambios en farm-list.component.html

### Modal crear/editar
Insertar bloque `@if (isPanama)` después del campo Ciudad y antes de la meta de auditoría:
- Divisor de sección "Información Panamá"
- Select de Cliente (trigger `onClienteChange`)
- Input de Zona (readonly/disabled)
- Select de Certificado GAB (Sí/No)
- Botón "Capturar ubicación actual"
- Input Latitud
- Input Longitud

### Modal detalle
Insertar bloque `@if (isPanama)` después de la sección Ubicación:
- Fila Cliente (nombre)
- Fila Zona
- Fila Certificado GAB
- Fila Coordenadas (si existen)

---

## Regla de visibilidad

La sección Panama **solo aparece** cuando `isPanama === true`, que lee `session.activePaisNombre.toLowerCase() === 'panama'` (lo que viene del header `x-active-pais-nombre: Panama`). Para otros países queda completamente oculta.

---

## No requiere cambios en backend

Los campos ya existen en la entidad `Farm`, el DTO `CreateFarmDto`/`UpdateFarmDto`, y el endpoint acepta los campos. Solo se trata de conectar el frontend del modal.
