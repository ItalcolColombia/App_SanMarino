# Seguimiento Diario Lote Reproductora – Dónde se guarda y flujo de campos

## Dónde se guarda la información

| Capa | Ubicación |
|------|-----------|
| **Base de datos** | Tabla `public.lote_seguimientos` (PostgreSQL). |
| **Entidad** | `ZooSanMarino.Domain.Entities.LoteSeguimiento`. |
| **API** | `POST /api/LoteSeguimiento` (crear), `PUT /api/LoteSeguimiento/{id}` (actualizar), `GET /api/LoteSeguimiento` (listar/filtrar). |
| **Controlador** | `backend/src/ZooSanMarino.API/Controllers/LoteSeguimientoController.cs`. |
| **Servicio** | `backend/src/ZooSanMarino.Infrastructure/Services/LoteSeguimientoService.cs` → `CreateAsync` / `UpdateAsync` → `_ctx.LoteSeguimientos.Add(ent)` y `SaveChangesAsync()`. |
| **Frontend servicio** | `features/seguimiento-diario-lote-reproductora/services/lote-seguimiento.service.ts` → `create(dto)` hace `POST` a `LoteSeguimiento`. |
| **Modal** | `features/seguimiento-diario-lote-reproductora/pages/modal-seguimiento-reproductora/` construye el DTO y emite `save`; el listado llama a `segApi.create()`. |

**Restricción:** Cada registro de seguimiento debe tener un **Lote Reproductora** existente: existe una FK `(lote_id, reproductora_id)` → tabla `lote_reproductoras`. Si la reproductora no está creada para ese lote, el backend rechaza la petición.

---

## Campos disponibles en backend (y en BD)

Todos estos campos están en el DTO de creación/actualización y en la entidad, y se persisten en `lote_seguimientos`:

| Campo backend (C#) | Columna BD | Modal (formulario) | Descripción |
|--------------------|------------|--------------------|-------------|
| Fecha | fecha | ✅ fecha | Fecha del registro. |
| LoteId | lote_id | ✅ loteId | ID del lote (FK). |
| ReproductoraId | reproductora_id | ✅ reproductoraId | ID reproductora (FK). |
| PesoInicial | peso_inicial | ✅ pesoInicial | Peso inicial (opcional). |
| PesoFinal | peso_final | ✅ pesoFinal | Peso final (opcional). |
| MortalidadH | mortalidad_h | ✅ mortalidadH | Mortalidad hembras. |
| MortalidadM | mortalidad_m | ✅ mortalidadM | Mortalidad machos. |
| SelH | sel_h | ✅ selH | Selección hembras. |
| SelM | sel_m | ✅ selM | Selección machos. |
| ErrorH | error_h | ✅ errorH | Error sexaje hembras. |
| ErrorM | error_m | ✅ errorM | Error sexaje machos. |
| TipoAlimento | tipo_alimento | ✅ (derivado de ítems) | Ej. "Mixto". |
| ConsumoAlimento | consumo_alimento | ✅ (suma ítems hembras) | Consumo hembras (kg). |
| ConsumoKgMachos | consumo_kg_machos | ✅ (suma ítems machos) | Consumo machos (kg). |
| Observaciones | observaciones | ✅ observaciones | Texto libre. |
| Ciclo | ciclo | ✅ ciclo | Normal / Reforzado. |
| PesoPromH | peso_prom_h | ✅ pesoPromH | Peso promedio hembras. |
| PesoPromM | peso_prom_m | ✅ pesoPromM | Peso promedio machos. |
| UniformidadH | uniformidad_h | ✅ uniformidadH | Uniformidad hembras (%). |
| UniformidadM | uniformidad_m | ✅ uniformidadM | Uniformidad machos (%). |
| CvH | cv_h | ✅ cvH | CV hembras. |
| CvM | cv_m | ✅ cvM | CV machos. |
| ConsumoAguaDiario | consumo_agua_diario | ✅ consumoAguaDiario | Solo Ecuador/Panamá. |
| ConsumoAguaPh | consumo_agua_ph | ✅ consumoAguaPh | PH agua. |
| ConsumoAguaOrp | consumo_agua_orp | ✅ consumoAguaOrp | ORP agua. |
| ConsumoAguaTemperatura | consumo_agua_temperatura | ✅ consumoAguaTemperatura | Temperatura agua. |
| Metadata | metadata (jsonb) | ✅ itemsHembras/Machos (alimentos) | Detalle ítems consumo. |
| ItemsAdicionales | items_adicionales (jsonb) | ✅ ítems no alimento | Vacunas, medicamentos, etc. |

---

## Flujo al crear/editar

1. Usuario abre el modal desde **Seguimiento Diario Lote Reproductora** (listado).
2. El modal muestra pestañas: **General**, **Hembras**, **Machos**, **Pesaje**, **Agua** (si aplica).
3. Al guardar, el modal arma un objeto `CreateLoteSeguimientoDto` / `UpdateLoteSeguimientoDto` con todos los campos del formulario (incluyendo `metadata` e `itemsAdicionales`).
4. El listado recibe el evento `save` y llama a `LoteSeguimientoService.create(dto)` o `update(dto)`.
5. El servicio hace `POST /api/LoteSeguimiento` o `PUT /api/LoteSeguimiento/{id}` con ese DTO.
6. El backend valida lote, reproductora y unicidad (lote + reproductora + fecha), luego inserta/actualiza en `lote_seguimientos`.

Para que el backend tenga **disponibilidad completa** de campos, el modal debe enviar en el payload todos los campos anteriores; en particular `pesoInicial` y `pesoFinal` están en el backend y deben incluirse en el formulario y en el DTO que se envía.
