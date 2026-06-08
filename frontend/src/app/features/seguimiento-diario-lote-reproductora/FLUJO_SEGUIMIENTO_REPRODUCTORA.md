# Seguimiento Diario Lote Reproductora – Dónde se guarda y flujo de campos

## Dónde se guarda la información

> ⚠️ **Actualizado 2026-06-05:** la tabla real es `seguimiento_diario_lote_reproductora_aves_engorde` (no `lote_seguimientos`). Esta tabla es la **fuente del cruce automático** hacia `seguimiento_diario_aves_engorde` (trigger `trg_cruce_reproductora_engorde`).

| Capa | Ubicación |
|------|-----------|
| **Base de datos** | Tabla `public.seguimiento_diario_lote_reproductora_aves_engorde` (PostgreSQL). FK a `lote_reproductora_ave_engorde.id`. |
| **Entidad** | `ZooSanMarino.Domain.Entities.SeguimientoDiarioLoteReproductoraAvesEngorde`. |
| **API** | `POST /api/SeguimientoDiarioLoteReproductora`, `PUT /api/SeguimientoDiarioLoteReproductora/{id}`, `GET .../por-lote-reproductora/{id}`, `DELETE .../{id}`. |
| **Controlador** | `backend/src/ZooSanMarino.API/Controllers/SeguimientoDiarioLoteReproductoraController.cs`. |
| **Servicio** | `backend/src/ZooSanMarino.Infrastructure/Services/SeguimientoDiarioLoteReproductoraService.cs`. `DeleteAsync` exige reapertura con novedad si el lote está cerrado. |
| **Frontend servicio** | `features/seguimiento-diario-lote-reproductora/services/seguimiento-diario-lote-reproductora.service.ts`. |
| **Modal** | `pages/modal-seguimiento-reproductora/` (reusa el modal de levante) construye el DTO y emite `save`. |

**Reapertura (2026-06-05):** un lote cerrado puede reabrirse con una *novedad* (`POST /api/LoteReproductoraAveEngorde/{id}/reabrir`) para habilitar la eliminación de registros. Al eliminar, el trigger borra el registro espejo en `seguimiento_diario_aves_engorde` y el lote "recierra solo".

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
