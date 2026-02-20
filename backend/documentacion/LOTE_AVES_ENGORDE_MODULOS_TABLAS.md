# Lot Management (Engorde) – Modules and Tables

## Overview

Three modules are connected to **Lote Aves de Engorde** (fat/chicken fattening lots). They use the **lote_ave_engorde** table and related tables, not the old “crop/levante” lot tables.

---

## Tables

| Table | Purpose |
|-------|--------|
| **lote_ave_engorde** | Main “fat lot” management. One record per lot of birds in fattening (created in “Lote Aves de Engorde” / Lot Management Engorde). |
| **lote_reproductora_ave_engorde** | Reproductive lots created from a fat lot. Several reproductora lots can be created per lote_ave_engorde (using encased birds from that lot). |
| **seguimiento_diario_aves_engorde** | Daily follow-up records for a fat lot. One record per lote_ave_engorde per date (mortality, selection, consumption, weights, etc.). |

---

## Modules and their tables

| Module | Table(s) used | Filter “Lote” dropdown |
|--------|----------------|-------------------------|
| **1. Lote Aves de Engorde** (Lot Management – Engorde) | `lote_ave_engorde` | N/A (this module creates/edits the fat lots). |
| **2. Lote Reproductora Aves de Engorde** | `lote_reproductora_ave_engorde` | Lots from `lote_ave_engorde`. |
| **3. Seguimiento Diario Aves de Engorde** (Daily follow-up chicken fattening) | `seguimiento_diario_aves_engorde` | Lots from `lote_ave_engorde`. |

---

## Change from previous behavior

- **Before:** “Seguimiento Diario Aves de Engorde” used filter-data from **Lote Levante** (lots from table `lotes` in Levante phase) and saved in the unified table `seguimiento_diario` with `tipo_seguimiento = 'engorde'`.
- **After:** “Seguimiento Diario Aves de Engorde” uses filter-data from **Lote Aves de Engorde** (lots from `lote_ave_engorde`) and saves in the dedicated table **seguimiento_diario_aves_engorde** (one record per `lote_ave_engorde_id` per date).

---

## Summary

- **Lote Aves de Engorde:** creates and manages fat lots → `lote_ave_engorde`.
- **Lote Reproductora Aves de Engorde:** creates reproductora lots from a chosen fat lot → `lote_reproductora_ave_engorde`.
- **Seguimiento Diario Aves de Engorde:** daily follow-up for a chosen fat lot → `seguimiento_diario_aves_engorde`.

All three are tied to the same concept: **Lote Aves de Engorde** (fat lot), with no dependency on the old lot (levante/producción) tables for this flow.
