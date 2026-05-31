# Validación Entidades Backend ↔ Tablas de Base de Datos

> **Fuente de verdad:** esquema real de la BD local `sanmarinoapplocal` (PostgreSQL), cruzado con las entidades (`ZooSanMarino.Domain/Entities`) y sus configuraciones EF (`ZooSanMarino.Infrastructure/Persistence/Configurations`).
> **Generado:** 2026-05-31 · **Tablas analizadas:** 80 · **FKs:** 141

Convenciones de la columna **Relaciones**:
- `FK → tabla.columna` : clave foránea saliente (esta tabla apunta a otra).
- `← tabla` : tablas que apuntan a esta (relación entrante / hijos).

---

## `catalogo_items`

- **Entidad:** `CatalogItem`
- **PK:** `id`
- **FK salientes:** —
- **Referenciada por:** `farm_inventory_movements`, `farm_product_inventory`

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `id` | int | NOT NULL | PK |
| 2 | `codigo` | varchar | NOT NULL |  |
| 3 | `nombre` | varchar | NOT NULL |  |
| 4 | `metadata` | jsonb | NOT NULL |  |
| 5 | `activo` | bool | NOT NULL |  |
| 6 | `created_at` | timestamptz | NOT NULL |  |
| 7 | `updated_at` | timestamptz | NOT NULL |  |
| 8 | `company_id` | int | NULL |  |
| 9 | `pais_id` | int | NULL |  |
| 10 | `item_type` | varchar | NOT NULL |  |

---

## `clientes`

- **Entidad:** `Cliente`
- **PK:** `id`
- **FK salientes:** —
- **Referenciada por:** —

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `id` | int | NOT NULL | PK |
| 2 | `tipo_documento` | varchar | NOT NULL |  |
| 3 | `numero_identificacion` | varchar | NOT NULL |  |
| 4 | `nombre` | varchar | NOT NULL |  |
| 5 | `correo` | varchar | NULL |  |
| 6 | `telefono` | varchar | NULL |  |
| 7 | `tipo_cliente` | varchar | NULL |  |
| 8 | `pais` | varchar | NULL |  |
| 9 | `provincia` | varchar | NULL |  |
| 10 | `distrito` | varchar | NULL |  |
| 11 | `planta` | varchar | NULL |  |
| 12 | `zona` | varchar | NULL |  |
| 13 | `status` | varchar | NOT NULL |  |
| 14 | `company_id` | int | NOT NULL |  |
| 15 | `created_by_user_id` | int | NOT NULL |  |
| 16 | `created_at` | timestamptz | NOT NULL |  |
| 17 | `updated_by_user_id` | int | NULL |  |
| 18 | `updated_at` | timestamptz | NULL |  |
| 19 | `deleted_at` | timestamptz | NULL |  |

---

## `companies`

- **Entidad:** `Company`
- **PK:** `id`
- **FK salientes:** —
- **Referenciada por:** `company_menus`, `company_pais`, `espejo_huevo_produccion`, `farm_inventory_movements`, `farm_product_inventory`, `farms`, `galpones`, `guia_genetica_ecuador_header`, `historial_lote_pollo_engorde`, `historico_lote_postura`, `inventario_gasto`, `inventario_gestion_movimiento`, `inventario_gestion_stock`, `item_inventario_ecuador`, `lote_ave_engorde`, `lote_postura_levante`, `lote_postura_produccion`, `mapa`, `mapa_ejecucion`, `movimiento_pollo_engorde`, `regionales`, `role_companies`, `user_companies`, `user_roles`, `zonas`

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `id` | int | NOT NULL | PK |
| 2 | `name` | varchar | NOT NULL |  |
| 3 | `identifier` | varchar | NOT NULL |  |
| 4 | `document_type` | varchar | NOT NULL |  |
| 5 | `address` | varchar | NULL |  |
| 6 | `phone` | varchar | NULL |  |
| 7 | `email` | varchar | NULL |  |
| 8 | `country` | varchar | NULL |  |
| 9 | `state` | varchar | NULL |  |
| 10 | `city` | varchar | NULL |  |
| 11 | `visual_permissions` | ARRAY | NOT NULL |  |
| 12 | `mobile_access` | bool | NOT NULL |  |
| 13 | `logo_bytes` | bytea | NULL |  |
| 14 | `logo_content_type` | varchar | NULL |  |

---

## `company_menus`

- **Entidad:** `CompanyMenu`
- **PK:** `company_id`, `menu_id`
- **FK salientes:** `company_id` → `companies.id`, `menu_id` → `menus.id`
- **Referenciada por:** —

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `company_id` | int | NOT NULL | PK FK |
| 2 | `menu_id` | int | NOT NULL | PK FK |
| 3 | `is_enabled` | bool | NOT NULL |  |
| 4 | `sort_order` | int | NOT NULL |  |
| 5 | `parent_menu_id` | int | NULL |  |

---

## `company_pais`

- **Entidad:** `CompanyPais`
- **PK:** `company_id`, `pais_id`
- **FK salientes:** `company_id` → `companies.id`, `pais_id` → `paises.pais_id`
- **Referenciada por:** —

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `company_id` | int | NOT NULL | PK FK |
| 2 | `pais_id` | int | NOT NULL | PK FK |
| 3 | `created_at` | timestamptz | NOT NULL |  |
| 4 | `updated_at` | timestamptz | NULL |  |

---

## `departamentos`

- **Entidad:** `Departamento`
- **PK:** `departamento_id`
- **FK salientes:** `pais_id` → `paises.pais_id`
- **Referenciada por:** `farms`, `municipios`

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `departamento_id` | int | NOT NULL | PK |
| 2 | `departamento_nombre` | varchar | NOT NULL |  |
| 3 | `pais_id` | int | NOT NULL | FK |
| 4 | `active` | bool | NOT NULL |  |

---

## `email_queue`

- **Entidad:** `EmailQueue`
- **PK:** `id`
- **FK salientes:** —
- **Referenciada por:** —

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `id` | int | NOT NULL | PK |
| 2 | `to_email` | varchar | NOT NULL |  |
| 3 | `subject` | varchar | NOT NULL |  |
| 4 | `body` | text | NOT NULL |  |
| 5 | `email_type` | varchar | NOT NULL |  |
| 6 | `status` | varchar | NOT NULL |  |
| 7 | `error_message` | text | NULL |  |
| 8 | `error_type` | varchar | NULL |  |
| 9 | `retry_count` | int | NOT NULL |  |
| 10 | `max_retries` | int | NOT NULL |  |
| 11 | `created_at` | timestamp | NOT NULL |  |
| 12 | `processed_at` | timestamp | NULL |  |
| 13 | `sent_at` | timestamp | NULL |  |
| 14 | `failed_at` | timestamp | NULL |  |
| 15 | `metadata` | jsonb | NULL |  |

---

## `espejo_huevo_produccion`

- **Entidad:** `EspejoHuevoProduccion`
- **PK:** `lote_postura_produccion_id`
- **FK salientes:** `company_id` → `companies.id`, `lote_postura_produccion_id` → `lote_postura_produccion.lote_postura_produccion_id`
- **Referenciada por:** —

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `lote_postura_produccion_id` | int | NOT NULL | PK FK |
| 2 | `company_id` | int | NOT NULL | FK |
| 3 | `huevo_tot_historico` | int | NOT NULL |  |
| 4 | `huevo_tot_dinamico` | int | NOT NULL |  |
| 5 | `huevo_inc_historico` | int | NOT NULL |  |
| 6 | `huevo_inc_dinamico` | int | NOT NULL |  |
| 7 | `huevo_limpio_historico` | int | NOT NULL |  |
| 8 | `huevo_limpio_dinamico` | int | NOT NULL |  |
| 9 | `huevo_tratado_historico` | int | NOT NULL |  |
| 10 | `huevo_tratado_dinamico` | int | NOT NULL |  |
| 11 | `huevo_sucio_historico` | int | NOT NULL |  |
| 12 | `huevo_sucio_dinamico` | int | NOT NULL |  |
| 13 | `huevo_deforme_historico` | int | NOT NULL |  |
| 14 | `huevo_deforme_dinamico` | int | NOT NULL |  |
| 15 | `huevo_blanco_historico` | int | NOT NULL |  |
| 16 | `huevo_blanco_dinamico` | int | NOT NULL |  |
| 17 | `huevo_doble_yema_historico` | int | NOT NULL |  |
| 18 | `huevo_doble_yema_dinamico` | int | NOT NULL |  |
| 19 | `huevo_piso_historico` | int | NOT NULL |  |
| 20 | `huevo_piso_dinamico` | int | NOT NULL |  |
| 21 | `huevo_pequeno_historico` | int | NOT NULL |  |
| 22 | `huevo_pequeno_dinamico` | int | NOT NULL |  |
| 23 | `huevo_roto_historico` | int | NOT NULL |  |
| 24 | `huevo_roto_dinamico` | int | NOT NULL |  |
| 25 | `huevo_desecho_historico` | int | NOT NULL |  |
| 26 | `huevo_desecho_dinamico` | int | NOT NULL |  |
| 27 | `huevo_otro_historico` | int | NOT NULL |  |
| 28 | `huevo_otro_dinamico` | int | NOT NULL |  |
| 29 | `historico_semanal` | jsonb | NULL |  |
| 30 | `created_at` | timestamptz | NOT NULL |  |
| 31 | `updated_at` | timestamptz | NULL |  |

---

## `farm_inventory_movements`

- **Entidad:** `FarmInventoryMovement`
- **PK:** `id`
- **FK salientes:** `catalog_item_id` → `catalogo_items.id`, `company_id` → `companies.id`, `farm_id` → `farms.id`, `pais_id` → `paises.pais_id`
- **Referenciada por:** —

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `id` | int | NOT NULL | PK |
| 2 | `farm_id` | int | NOT NULL | FK |
| 3 | `catalog_item_id` | int | NOT NULL | FK |
| 4 | `quantity` | numeric | NOT NULL |  |
| 5 | `movement_type` | varchar | NOT NULL |  |
| 6 | `unit` | varchar | NOT NULL |  |
| 7 | `reference` | varchar | NULL |  |
| 8 | `reason` | varchar | NULL |  |
| 9 | `transfer_group_id` | uuid | NULL |  |
| 10 | `metadata` | jsonb | NOT NULL |  |
| 11 | `responsible_user_id` | varchar | NULL |  |
| 12 | `created_at` | timestamptz | NOT NULL |  |
| 13 | `origin` | varchar | NULL |  |
| 14 | `destination` | varchar | NULL |  |
| 15 | `documento_origen` | varchar | NULL |  |
| 16 | `tipo_entrada` | varchar | NULL |  |
| 17 | `galpon_destino_id` | varchar | NULL |  |
| 18 | `fecha_movimiento` | timestamptz | NULL |  |
| 19 | `item_type` | varchar | NULL |  |
| 20 | `company_id` | int | NOT NULL | FK |
| 21 | `pais_id` | int | NOT NULL | FK |

---

## `farm_product_inventory`

- **Entidad:** `FarmProductInventory`
- **PK:** `id`
- **FK salientes:** `catalog_item_id` → `catalogo_items.id`, `company_id` → `companies.id`, `farm_id` → `farms.id`, `pais_id` → `paises.pais_id`
- **Referenciada por:** —

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `id` | int | NOT NULL | PK |
| 2 | `farm_id` | int | NOT NULL | FK |
| 3 | `catalog_item_id` | int | NOT NULL | FK |
| 4 | `quantity` | numeric | NOT NULL |  |
| 5 | `unit` | varchar | NOT NULL |  |
| 6 | `location` | varchar | NULL |  |
| 7 | `lot_number` | varchar | NULL |  |
| 8 | `expiration_date` | timestamptz | NULL |  |
| 9 | `unit_cost` | numeric | NULL |  |
| 10 | `metadata` | jsonb | NOT NULL |  |
| 11 | `active` | bool | NOT NULL |  |
| 12 | `responsible_user_id` | varchar | NULL |  |
| 13 | `created_at` | timestamptz | NOT NULL |  |
| 14 | `updated_at` | timestamptz | NOT NULL |  |
| 15 | `company_id` | int | NOT NULL | FK |
| 16 | `pais_id` | int | NOT NULL | FK |

---

## `farms`

- **Entidad:** `Farm`
- **PK:** `id`
- **FK salientes:** `company_id` → `companies.id`, `departamento_id` → `departamentos.departamento_id`, `municipio_id` → `municipios.municipio_id`
- **Referenciada por:** `farm_inventory_movements`, `farm_product_inventory`, `galpones`, `historial_traslado_lote`, `inventario_gasto`, `inventario_gestion_movimiento`, `inventario_gestion_stock`, `lote_ave_engorde`, `lote_postura_levante`, `lote_postura_produccion`, `lotes`, `movimiento_pollo_engorde`, `nucleos`, `user_farms`

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `id` | int | NOT NULL | PK |
| 2 | `name` | varchar | NOT NULL |  |
| 3 | `regional_id` | int | NOT NULL |  |
| 4 | `status` | varchar | NOT NULL |  |
| 5 | `municipio_id` | int | NOT NULL | FK |
| 6 | `company_id` | int | NOT NULL | FK |
| 7 | `created_by_user_id` | int | NOT NULL |  |
| 8 | `created_at` | timestamptz | NOT NULL |  |
| 9 | `updated_by_user_id` | int | NULL |  |
| 10 | `updated_at` | timestamptz | NULL |  |
| 11 | `deleted_at` | timestamptz | NULL |  |
| 12 | `departamento_id` | int | NOT NULL | FK |
| 13 | `cliente_id` | int | NULL |  |
| 14 | `zona` | varchar | NULL |  |
| 15 | `certificado_gab` | bool | NOT NULL |  |
| 16 | `latitud` | numeric | NULL |  |
| 17 | `longitud` | numeric | NULL |  |

---

## `galpones`

- **Entidad:** `Galpon`
- **PK:** `galpon_id`
- **FK salientes:** `company_id` → `companies.id`, `granja_id` → `nucleos.nucleo_id`, `granja_id` → `farms.id`, `granja_id` → `nucleos.granja_id`, `nucleo_id` → `nucleos.granja_id`, `nucleo_id` → `nucleos.nucleo_id`
- **Referenciada por:** `lote_ave_engorde`, `lote_galpones`, `lotes`, `plan_gramaje_galpon`, `produccion_lotes`

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `galpon_id` | varchar | NOT NULL | PK |
| 2 | `nucleo_id` | varchar | NOT NULL | FK |
| 3 | `granja_id` | int | NOT NULL | FK |
| 4 | `galpon_nombre` | varchar | NOT NULL |  |
| 5 | `ancho` | varchar | NULL |  |
| 6 | `largo` | varchar | NULL |  |
| 7 | `tipo_galpon` | varchar | NULL |  |
| 8 | `company_id` | int | NOT NULL | FK |
| 9 | `created_by_user_id` | int | NOT NULL |  |
| 10 | `created_at` | timestamptz | NOT NULL |  |
| 11 | `updated_by_user_id` | int | NULL |  |
| 12 | `updated_at` | timestamptz | NULL |  |
| 13 | `deleted_at` | timestamptz | NULL |  |

---

## `guia_genetica_ecuador_detalle`

- **Entidad:** `GuiaGeneticaEcuadorDetalle`
- **PK:** `id`
- **FK salientes:** `guia_genetica_ecuador_header_id` → `guia_genetica_ecuador_header.id`
- **Referenciada por:** —

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `id` | int | NOT NULL | PK |
| 2 | `guia_genetica_ecuador_header_id` | int | NOT NULL | FK |
| 3 | `sexo` | varchar | NOT NULL |  |
| 4 | `dia` | int | NOT NULL |  |
| 5 | `peso_corporal_g` | numeric | NOT NULL |  |
| 6 | `ganancia_diaria_g` | numeric | NOT NULL |  |
| 7 | `promedio_ganancia_diaria_g` | numeric | NOT NULL |  |
| 8 | `cantidad_alimento_diario_g` | numeric | NOT NULL |  |
| 9 | `alimento_acumulado_g` | numeric | NOT NULL |  |
| 10 | `ca` | numeric | NOT NULL |  |
| 11 | `mortalidad_seleccion_diaria` | numeric | NOT NULL |  |
| 12 | `company_id` | int | NOT NULL |  |
| 13 | `created_by_user_id` | int | NOT NULL |  |
| 14 | `created_at` | timestamptz | NOT NULL |  |
| 15 | `updated_by_user_id` | int | NULL |  |
| 16 | `updated_at` | timestamptz | NULL |  |
| 17 | `deleted_at` | timestamptz | NULL |  |

---

## `guia_genetica_ecuador_header`

- **Entidad:** `GuiaGeneticaEcuadorHeader`
- **PK:** `id`
- **FK salientes:** `company_id` → `companies.id`
- **Referenciada por:** `guia_genetica_ecuador_detalle`

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `id` | int | NOT NULL | PK |
| 2 | `raza` | varchar | NOT NULL |  |
| 3 | `anio_guia` | int | NOT NULL |  |
| 4 | `estado` | varchar | NOT NULL |  |
| 5 | `company_id` | int | NOT NULL | FK |
| 6 | `created_by_user_id` | int | NOT NULL |  |
| 7 | `created_at` | timestamptz | NOT NULL |  |
| 8 | `updated_by_user_id` | int | NULL |  |
| 9 | `updated_at` | timestamptz | NULL |  |
| 10 | `deleted_at` | timestamptz | NULL |  |

---

## `guia_genetica_sanmarino_colombia`

- **Entidad:** `ProduccionAvicolaRaw`
- **PK:** `id`
- **FK salientes:** —
- **Referenciada por:** —

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `id` | int | NOT NULL | PK |
| 2 | `company_id` | int | NOT NULL |  |
| 3 | `created_by_user_id` | int | NOT NULL |  |
| 4 | `created_at` | timestamptz | NOT NULL |  |
| 5 | `updated_by_user_id` | int | NULL |  |
| 6 | `updated_at` | timestamptz | NULL |  |
| 7 | `deleted_at` | timestamptz | NULL |  |
| 8 | `anio_guia` | text | NULL |  |
| 9 | `raza` | text | NULL |  |
| 10 | `edad` | text | NULL |  |
| 11 | `mort_sem_h` | text | NULL |  |
| 12 | `retiro_ac_h` | text | NULL |  |
| 13 | `mort_sem_m` | text | NULL |  |
| 14 | `retiro_ac_m` | text | NULL |  |
| 15 | `cons_ac_h` | text | NULL |  |
| 16 | `cons_ac_m` | text | NULL |  |
| 17 | `gr_ave_dia_h` | text | NULL |  |
| 18 | `gr_ave_dia_m` | text | NULL |  |
| 19 | `peso_h` | text | NULL |  |
| 20 | `peso_m` | text | NULL |  |
| 21 | `uniformidad` | text | NULL |  |
| 22 | `h_total_aa` | text | NULL |  |
| 23 | `prod_porcentaje` | text | NULL |  |
| 24 | `h_inc_aa` | text | NULL |  |
| 25 | `aprov_sem` | text | NULL |  |
| 26 | `peso_huevo` | text | NULL |  |
| 27 | `masa_huevo` | text | NULL |  |
| 28 | `grasa_porcentaje` | text | NULL |  |
| 29 | `nacim_porcentaje` | text | NULL |  |
| 30 | `pollito_aa` | text | NULL |  |
| 31 | `kcal_ave_dia_h` | text | NULL |  |
| 32 | `kcal_ave_dia_m` | text | NULL |  |
| 33 | `aprov_ac` | text | NULL |  |
| 34 | `gr_huevo_t` | text | NULL |  |
| 35 | `gr_huevo_inc` | text | NULL |  |
| 36 | `gr_pollito` | text | NULL |  |
| 37 | `valor_1000` | text | NULL |  |
| 38 | `valor_150` | text | NULL |  |
| 39 | `apareo` | text | NULL |  |
| 40 | `peso_mh` | text | NULL |  |
| 41 | `codigo_guia_genetica` | varchar | NULL |  |
| 42 | `hembras` | varchar | NULL |  |
| 43 | `machos` | varchar | NULL |  |
| 44 | `kcal_h` | varchar | NULL |  |
| 45 | `prot_h` | varchar | NULL |  |
| 46 | `kcal_m` | varchar | NULL |  |
| 47 | `prot_m` | varchar | NULL |  |
| 48 | `kcal_sem_h` | varchar | NULL |  |
| 49 | `prot_h_sem` | varchar | NULL |  |
| 50 | `kcal_sem_m` | varchar | NULL |  |
| 51 | `prot_sem_m` | varchar | NULL |  |
| 52 | `alim_h` | text | NULL |  |
| 53 | `alim_m` | text | NULL |  |

---

## `guia_semana`

- **Entidad:** `⚠️ sin entidad/config explícita`
- **PK:** `id`
- **FK salientes:** —
- **Referenciada por:** —

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `id` | bigint | NOT NULL | PK |
| 2 | `codigo_guia_genetica` | text | NULL |  |
| 3 | `raza` | text | NULL |  |
| 4 | `ano_tabla_genetica` | int | NULL |  |
| 5 | `sexo` | character | NOT NULL |  |
| 6 | `semana` | int | NOT NULL |  |
| 7 | `peso_obj` | float8 | NULL |  |
| 8 | `unif_obj` | float8 | NULL |  |
| 9 | `mort_pct_obj` | float8 | NULL |  |
| 10 | `cons_ac_gr_obj` | float8 | NULL |  |
| 11 | `gr_ave_dia_obj` | float8 | NULL |  |
| 12 | `incr_cons_obj` | float8 | NULL |  |
| 13 | `kcal_sem_obj` | float8 | NULL |  |
| 14 | `kcal_sem_ac_obj` | float8 | NULL |  |
| 15 | `prot_sem_obj` | float8 | NULL |  |
| 16 | `prot_sem_ac_obj` | float8 | NULL |  |
| 17 | `alimento_nom` | text | NULL |  |

---

## `historial_inventario`

- **Entidad:** `HistorialInventario`
- **PK:** `id`
- **FK salientes:** —
- **Referenciada por:** —

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `id` | int | NOT NULL | PK |
| 2 | `inventario_id` | int | NOT NULL |  |
| 3 | `lote_id` | varchar | NOT NULL |  |
| 4 | `fecha_cambio` | timestamptz | NOT NULL |  |
| 5 | `tipo_cambio` | varchar | NOT NULL |  |
| 6 | `movimiento_id` | int | NULL |  |
| 7 | `cantidad_hembras_anterior` | int | NOT NULL |  |
| 8 | `cantidad_machos_anterior` | int | NOT NULL |  |
| 9 | `cantidad_mixtas_anterior` | int | NOT NULL |  |
| 10 | `cantidad_hembras_nueva` | int | NOT NULL |  |
| 11 | `cantidad_machos_nueva` | int | NOT NULL |  |
| 12 | `cantidad_mixtas_nueva` | int | NOT NULL |  |
| 13 | `granja_id` | int | NOT NULL |  |
| 14 | `nucleo_id` | varchar | NULL |  |
| 15 | `galpon_id` | varchar | NULL |  |
| 16 | `usuario_cambio_id` | int | NOT NULL |  |
| 17 | `usuario_nombre` | varchar | NULL |  |
| 18 | `motivo` | varchar | NULL |  |
| 19 | `observaciones` | varchar | NULL |  |
| 20 | `company_id` | int | NOT NULL |  |
| 21 | `created_by_user_id` | int | NOT NULL |  |
| 22 | `created_at` | timestamptz | NOT NULL |  |
| 23 | `updated_by_user_id` | int | NULL |  |
| 24 | `updated_at` | timestamptz | NULL |  |
| 25 | `deleted_at` | timestamptz | NULL |  |

---

## `historial_lote_pollo_engorde`

- **Entidad:** `HistorialLotePolloEngorde`
- **PK:** `id`
- **FK salientes:** `company_id` → `companies.id`, `lote_ave_engorde_id` → `lote_ave_engorde.lote_ave_engorde_id`, `lote_reproductora_ave_engorde_id` → `lote_reproductora_ave_engorde.id`, `movimiento_id` → `movimiento_pollo_engorde.id`
- **Referenciada por:** —

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `id` | int | NOT NULL | PK |
| 2 | `company_id` | int | NOT NULL | FK |
| 3 | `tipo_lote` | varchar | NOT NULL |  |
| 4 | `lote_ave_engorde_id` | int | NULL | FK |
| 5 | `lote_reproductora_ave_engorde_id` | int | NULL | FK |
| 6 | `tipo_registro` | varchar | NOT NULL |  |
| 7 | `aves_hembras` | int | NOT NULL |  |
| 8 | `aves_machos` | int | NOT NULL |  |
| 9 | `aves_mixtas` | int | NOT NULL |  |
| 10 | `fecha_registro` | timestamptz | NOT NULL |  |
| 11 | `movimiento_id` | int | NULL | FK |
| 12 | `created_at` | timestamptz | NOT NULL |  |

---

## `historial_traslado_lote`

- **Entidad:** `HistorialTrasladoLote`
- **PK:** `id`
- **FK salientes:** `granja_destino_id` → `farms.id`, `granja_origen_id` → `farms.id`
- **Referenciada por:** —

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `id` | int | NOT NULL | PK |
| 2 | `lote_original_id` | int | NOT NULL |  |
| 3 | `lote_nuevo_id` | int | NOT NULL |  |
| 4 | `granja_origen_id` | int | NOT NULL | FK |
| 5 | `granja_destino_id` | int | NOT NULL | FK |
| 6 | `nucleo_destino_id` | varchar | NULL |  |
| 7 | `galpon_destino_id` | varchar | NULL |  |
| 8 | `observaciones` | varchar | NULL |  |
| 9 | `company_id` | int | NOT NULL |  |
| 10 | `created_by_user_id` | int | NOT NULL |  |
| 11 | `created_at` | timestamp | NOT NULL |  |

---

## `historico_lote_postura`

- **Entidad:** `HistoricoLotePostura`
- **PK:** `id`
- **FK salientes:** `company_id` → `companies.id`, `lote_postura_levante_id` → `lote_postura_levante.lote_postura_levante_id`, `lote_postura_produccion_id` → `lote_postura_produccion.lote_postura_produccion_id`
- **Referenciada por:** —

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `id` | int | NOT NULL | PK |
| 2 | `company_id` | int | NOT NULL | FK |
| 3 | `tipo_lote` | varchar | NOT NULL |  |
| 4 | `lote_postura_levante_id` | int | NULL | FK |
| 5 | `lote_postura_produccion_id` | int | NULL | FK |
| 6 | `tipo_registro` | varchar | NOT NULL |  |
| 7 | `fecha_registro` | timestamptz | NOT NULL |  |
| 8 | `usuario_id` | int | NULL |  |
| 9 | `snapshot` | jsonb | NULL |  |
| 10 | `created_at` | timestamptz | NOT NULL |  |

---

## `inventario_aves`

- **Entidad:** `InventarioAves`
- **PK:** `id`
- **FK salientes:** —
- **Referenciada por:** —

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `id` | int | NOT NULL | PK |
| 2 | `lote_id` | varchar | NOT NULL |  |
| 3 | `granja_id` | int | NOT NULL |  |
| 4 | `nucleo_id` | varchar | NULL |  |
| 5 | `galpon_id` | varchar | NULL |  |
| 6 | `cantidad_hembras` | int | NOT NULL |  |
| 7 | `cantidad_machos` | int | NOT NULL |  |
| 8 | `cantidad_mixtas` | int | NOT NULL |  |
| 9 | `fecha_actualizacion` | timestamptz | NOT NULL |  |
| 10 | `estado` | varchar | NOT NULL |  |
| 11 | `observaciones` | varchar | NULL |  |
| 12 | `company_id` | int | NOT NULL |  |
| 13 | `created_by_user_id` | int | NOT NULL |  |
| 14 | `created_at` | timestamptz | NOT NULL |  |
| 15 | `updated_by_user_id` | int | NULL |  |
| 16 | `updated_at` | timestamptz | NULL |  |
| 17 | `deleted_at` | timestamptz | NULL |  |

---

## `inventario_gasto`

- **Entidad:** `InventarioGasto`
- **PK:** `id`
- **FK salientes:** `company_id` → `companies.id`, `farm_id` → `farms.id`, `lote_ave_engorde_id` → `lote_ave_engorde.lote_ave_engorde_id`, `pais_id` → `paises.pais_id`
- **Referenciada por:** `inventario_gasto_auditoria`, `inventario_gasto_detalle`

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `id` | int | NOT NULL | PK |
| 2 | `company_id` | int | NOT NULL | FK |
| 3 | `pais_id` | int | NOT NULL | FK |
| 4 | `farm_id` | int | NOT NULL | FK |
| 5 | `nucleo_id` | varchar | NULL |  |
| 6 | `galpon_id` | varchar | NULL |  |
| 7 | `lote_ave_engorde_id` | int | NULL | FK |
| 8 | `fecha` | date | NOT NULL |  |
| 9 | `observaciones` | varchar | NULL |  |
| 10 | `estado` | varchar | NOT NULL |  |
| 11 | `created_at` | timestamptz | NOT NULL |  |
| 12 | `created_by_user_id` | varchar | NULL |  |
| 13 | `deleted_at` | timestamptz | NULL |  |
| 14 | `deleted_by_user_id` | varchar | NULL |  |

---

## `inventario_gasto_auditoria`

- **Entidad:** `InventarioGastoAuditoria`
- **PK:** `id`
- **FK salientes:** `inventario_gasto_id` → `inventario_gasto.id`
- **Referenciada por:** —

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `id` | int | NOT NULL | PK |
| 2 | `inventario_gasto_id` | int | NOT NULL | FK |
| 3 | `accion` | varchar | NOT NULL |  |
| 4 | `fecha` | timestamptz | NOT NULL |  |
| 5 | `user_id` | varchar | NOT NULL |  |
| 6 | `detalle` | text | NULL |  |

---

## `inventario_gasto_detalle`

- **Entidad:** `InventarioGastoDetalle`
- **PK:** `id`
- **FK salientes:** `inventario_gasto_id` → `inventario_gasto.id`, `item_inventario_ecuador_id` → `item_inventario_ecuador.id`
- **Referenciada por:** —

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `id` | int | NOT NULL | PK |
| 2 | `inventario_gasto_id` | int | NOT NULL | FK |
| 3 | `item_inventario_ecuador_id` | int | NOT NULL | FK |
| 4 | `concepto` | varchar | NULL |  |
| 5 | `cantidad` | numeric | NOT NULL |  |
| 6 | `unidad` | varchar | NOT NULL |  |
| 7 | `stock_antes` | numeric | NULL |  |
| 8 | `stock_despues` | numeric | NULL |  |

---

## `inventario_gestion_movimiento`

- **Entidad:** `InventarioGestionMovimiento`
- **PK:** `id`
- **FK salientes:** `company_id` → `companies.id`, `farm_id` → `farms.id`, `item_inventario_ecuador_id` → `item_inventario_ecuador.id`, `pais_id` → `paises.pais_id`
- **Referenciada por:** —

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `id` | int | NOT NULL | PK |
| 2 | `company_id` | int | NOT NULL | FK |
| 3 | `pais_id` | int | NOT NULL | FK |
| 4 | `farm_id` | int | NOT NULL | FK |
| 5 | `nucleo_id` | varchar | NULL |  |
| 6 | `galpon_id` | varchar | NULL |  |
| 7 | `item_inventario_ecuador_id` | int | NOT NULL | FK |
| 8 | `quantity` | numeric | NOT NULL |  |
| 9 | `unit` | varchar | NOT NULL |  |
| 10 | `movement_type` | varchar | NOT NULL |  |
| 11 | `from_farm_id` | int | NULL |  |
| 12 | `from_nucleo_id` | varchar | NULL |  |
| 13 | `from_galpon_id` | varchar | NULL |  |
| 14 | `reference` | varchar | NULL |  |
| 15 | `reason` | varchar | NULL |  |
| 16 | `transfer_group_id` | uuid | NULL |  |
| 17 | `created_at` | timestamptz | NOT NULL |  |
| 18 | `created_by_user_id` | varchar | NULL |  |
| 19 | `estado` | varchar | NULL |  |

---

## `inventario_gestion_stock`

- **Entidad:** `InventarioGestionStock`
- **PK:** `id`
- **FK salientes:** `company_id` → `companies.id`, `farm_id` → `farms.id`, `item_inventario_ecuador_id` → `item_inventario_ecuador.id`, `pais_id` → `paises.pais_id`
- **Referenciada por:** —

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `id` | int | NOT NULL | PK |
| 2 | `company_id` | int | NOT NULL | FK |
| 3 | `pais_id` | int | NOT NULL | FK |
| 4 | `farm_id` | int | NOT NULL | FK |
| 5 | `nucleo_id` | varchar | NULL |  |
| 6 | `galpon_id` | varchar | NULL |  |
| 7 | `item_inventario_ecuador_id` | int | NOT NULL | FK |
| 8 | `quantity` | numeric | NOT NULL |  |
| 9 | `unit` | varchar | NOT NULL |  |
| 10 | `created_at` | timestamptz | NOT NULL |  |
| 11 | `updated_at` | timestamptz | NOT NULL |  |

---

## `item_inventario_ecuador`

- **Entidad:** `ItemInventarioEcuador`
- **PK:** `id`
- **FK salientes:** `company_id` → `companies.id`, `pais_id` → `paises.pais_id`
- **Referenciada por:** `inventario_gasto_detalle`, `inventario_gestion_movimiento`, `inventario_gestion_stock`

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `id` | int | NOT NULL | PK |
| 2 | `codigo` | varchar | NOT NULL |  |
| 3 | `nombre` | varchar | NOT NULL |  |
| 4 | `tipo_item` | varchar | NOT NULL |  |
| 5 | `unidad` | varchar | NOT NULL |  |
| 6 | `descripcion` | varchar | NULL |  |
| 7 | `activo` | bool | NOT NULL |  |
| 8 | `company_id` | int | NOT NULL | FK |
| 9 | `pais_id` | int | NOT NULL | FK |
| 10 | `created_at` | timestamptz | NOT NULL |  |
| 11 | `updated_at` | timestamptz | NOT NULL |  |
| 12 | `grupo` | varchar | NULL |  |
| 13 | `tipo_inventario_codigo` | varchar | NULL |  |
| 14 | `descripcion_tipo_inventario` | varchar | NULL |  |
| 15 | `referencia` | varchar | NULL |  |
| 16 | `descripcion_item` | varchar | NULL |  |
| 17 | `concepto` | varchar | NULL |  |

---

## `lesiones`

- **Entidad:** `Lesion`
- **PK:** `id`
- **FK salientes:** —
- **Referenciada por:** —

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `id` | bigint | NOT NULL | PK |
| 2 | `cliente_id` | int | NULL |  |
| 3 | `farm_id` | int | NOT NULL |  |
| 4 | `galpon_id` | varchar | NULL |  |
| 5 | `lote_id` | int | NULL |  |
| 6 | `lote_reproductora_id` | varchar | NULL |  |
| 7 | `edad_dias` | int | NULL |  |
| 8 | `aves_macho` | int | NULL |  |
| 9 | `aves_hembra` | int | NULL |  |
| 10 | `aves_mixtas` | int | NULL |  |
| 11 | `tipo_lesion` | varchar | NOT NULL |  |
| 12 | `observaciones` | text | NULL |  |
| 13 | `fecha_registro` | timestamptz | NOT NULL |  |
| 14 | `modulo_origen` | varchar | NOT NULL |  |
| 15 | `status` | varchar | NOT NULL |  |
| 16 | `company_id` | int | NOT NULL |  |
| 17 | `created_by_user_id` | int | NOT NULL |  |
| 18 | `created_at` | timestamptz | NOT NULL |  |
| 19 | `updated_by_user_id` | int | NULL |  |
| 20 | `updated_at` | timestamptz | NULL |  |
| 21 | `deleted_at` | timestamptz | NULL |  |

---

## `liquidacion_cierre_lote_levante`

- **Entidad:** `LiquidacionCierreLoteLevante`
- **PK:** `id`
- **FK salientes:** `lote_postura_levante_id` → `lote_postura_levante.lote_postura_levante_id`
- **Referenciada por:** —

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `id` | int | NOT NULL | PK |
| 2 | `lote_postura_levante_id` | int | NOT NULL | FK |
| 3 | `fecha_cierre` | timestamptz | NOT NULL |  |
| 4 | `hembras_encasetadas` | int | NULL |  |
| 5 | `machos_encasetados` | int | NULL |  |
| 6 | `porcentaje_mortalidad_hembras` | numeric | NOT NULL |  |
| 7 | `porcentaje_seleccion_hembras` | numeric | NOT NULL |  |
| 8 | `porcentaje_error_sexaje_hembras` | numeric | NOT NULL |  |
| 9 | `porcentaje_retiro_acumulado` | numeric | NOT NULL |  |
| 10 | `consumo_alimento_real_gramos` | numeric | NOT NULL |  |
| 11 | `consumo_alimento_guia_gramos` | numeric | NULL |  |
| 12 | `porcentaje_diferencia_consumo` | numeric | NULL |  |
| 13 | `peso_semana25real` | numeric | NULL |  |
| 14 | `peso_semana25guia` | numeric | NULL |  |
| 15 | `porcentaje_diferencia_peso` | numeric | NULL |  |
| 16 | `uniformidad_real` | numeric | NULL |  |
| 17 | `uniformidad_guia` | numeric | NULL |  |
| 18 | `porcentaje_diferencia_uniformidad` | numeric | NULL |  |
| 19 | `porcentaje_retiro_guia` | numeric | NULL |  |
| 20 | `raza_guia` | text | NULL |  |
| 21 | `ano_guia` | int | NULL |  |
| 22 | `metadata` | jsonb | NULL |  |
| 23 | `company_id` | int | NOT NULL |  |
| 24 | `created_by_user_id` | int | NOT NULL |  |
| 25 | `created_at` | timestamptz | NOT NULL |  |
| 26 | `updated_at` | timestamptz | NULL |  |
| 27 | `closed_by_user_id` | int | NULL |  |
| 28 | `consumo_gr_ave_dia_semana25guia` | numeric | NULL |  |

---

## `logins`

- **Entidad:** `Login`
- **PK:** `id`
- **FK salientes:** —
- **Referenciada por:** `user_logins`

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `id` | uuid | NOT NULL | PK |
| 2 | `email` | varchar | NOT NULL |  |
| 3 | `password_hash` | text | NOT NULL |  |
| 4 | `is_email_login` | bool | NOT NULL |  |
| 5 | `is_deleted` | bool | NOT NULL |  |

---

## `lote_ave_engorde`

- **Entidad:** `LoteAveEngorde`
- **PK:** `lote_ave_engorde_id`
- **FK salientes:** `company_id` → `companies.id`, `galpon_id` → `galpones.galpon_id`, `granja_id` → `farms.id`, `granja_id` → `nucleos.granja_id`, `granja_id` → `nucleos.nucleo_id`, `nucleo_id` → `nucleos.granja_id`, `nucleo_id` → `nucleos.nucleo_id`
- **Referenciada por:** `historial_lote_pollo_engorde`, `inventario_gasto`, `lote_reproductora_ave_engorde`, `movimiento_pollo_engorde`, `seguimiento_diario_aves_engorde`

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `lote_ave_engorde_id` | int | NOT NULL | PK |
| 2 | `lote_nombre` | varchar | NOT NULL |  |
| 3 | `granja_id` | int | NOT NULL | FK |
| 4 | `nucleo_id` | varchar | NULL | FK |
| 5 | `galpon_id` | varchar | NULL | FK |
| 6 | `regional` | varchar | NULL |  |
| 7 | `fecha_encaset` | timestamptz | NULL |  |
| 8 | `hembras_l` | int | NULL |  |
| 9 | `machos_l` | int | NULL |  |
| 10 | `peso_inicial_h` | float8 | NULL |  |
| 11 | `peso_inicial_m` | float8 | NULL |  |
| 12 | `unif_h` | float8 | NULL |  |
| 13 | `unif_m` | float8 | NULL |  |
| 14 | `mort_caja_h` | int | NULL |  |
| 15 | `mort_caja_m` | int | NULL |  |
| 16 | `raza` | varchar | NULL |  |
| 17 | `ano_tabla_genetica` | int | NULL |  |
| 18 | `linea` | varchar | NULL |  |
| 19 | `tipo_linea` | varchar | NULL |  |
| 20 | `codigo_guia_genetica` | varchar | NULL |  |
| 21 | `linea_genetica_id` | int | NULL |  |
| 22 | `tecnico` | varchar | NULL |  |
| 23 | `mixtas` | int | NULL |  |
| 24 | `peso_mixto` | float8 | NULL |  |
| 25 | `aves_encasetadas` | int | NULL |  |
| 26 | `edad_inicial` | int | NULL |  |
| 27 | `lote_erp` | varchar | NULL |  |
| 28 | `estado_traslado` | varchar | NULL |  |
| 29 | `pais_id` | int | NULL |  |
| 30 | `pais_nombre` | varchar | NULL |  |
| 31 | `empresa_nombre` | varchar | NULL |  |
| 32 | `company_id` | int | NOT NULL | FK |
| 33 | `created_by_user_id` | int | NOT NULL |  |
| 34 | `created_at` | timestamptz | NOT NULL |  |
| 35 | `updated_by_user_id` | int | NULL |  |
| 36 | `updated_at` | timestamptz | NULL |  |
| 37 | `deleted_at` | timestamptz | NULL |  |
| 38 | `estado_operativo_lote` | varchar | NOT NULL |  |
| 39 | `liquidado_at` | timestamptz | NULL |  |
| 40 | `liquidado_por_user_id` | varchar | NULL |  |
| 41 | `reabierto_at` | timestamptz | NULL |  |
| 42 | `reabierto_por_user_id` | varchar | NULL |  |
| 43 | `motivo_reapertura` | varchar | NULL |  |
| 44 | `fecha_alistamiento` | timestamptz | NULL |  |
| 45 | `merma_unidades` | int | NULL |  |
| 46 | `merma_kilos` | numeric | NULL |  |
| 47 | `merma_registrada_at` | timestamptz | NULL |  |
| 48 | `merma_registrada_por_user_id` | varchar | NULL |  |
| 49 | `aves_sobrante` | int | NOT NULL |  |

---

## `lote_etapa_levante`

- **Entidad:** `LoteEtapaLevante`
- **PK:** `id`
- **FK salientes:** `lote_id` → `lotes.lote_id`
- **Referenciada por:** —

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `id` | int | NOT NULL | PK |
| 2 | `lote_id` | int | NOT NULL | FK |
| 3 | `aves_inicio_hembras` | int | NOT NULL |  |
| 4 | `aves_inicio_machos` | int | NOT NULL |  |
| 5 | `fecha_inicio` | timestamptz | NOT NULL |  |
| 6 | `fecha_fin` | timestamptz | NULL |  |
| 7 | `aves_fin_hembras` | int | NULL |  |
| 8 | `aves_fin_machos` | int | NULL |  |
| 9 | `created_at` | timestamptz | NOT NULL |  |
| 10 | `updated_at` | timestamptz | NULL |  |

---

## `lote_galpones`

- **Entidad:** `LoteGalpon`
- **PK:** `lote_id`, `reproductora_id`, `galpon_id`
- **FK salientes:** `galpon_id` → `galpones.galpon_id`, `lote_id` → `lote_reproductoras.lote_id`, `lote_id` → `lote_reproductoras.reproductora_id`, `reproductora_id` → `lote_reproductoras.lote_id`, `reproductora_id` → `lote_reproductoras.reproductora_id`
- **Referenciada por:** —

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `lote_id` | varchar | NOT NULL | PK FK |
| 2 | `reproductora_id` | varchar | NOT NULL | PK FK |
| 3 | `galpon_id` | varchar | NOT NULL | PK FK |
| 4 | `m` | int | NULL |  |
| 5 | `h` | int | NULL |  |

---

## `lote_postura_base`

- **Entidad:** `LotePosturaBase`
- **PK:** `lote_postura_base_id`
- **FK salientes:** —
- **Referenciada por:** `lotes`

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `lote_postura_base_id` | int | NOT NULL | PK |
| 2 | `lote_nombre` | varchar | NOT NULL |  |
| 3 | `codigo_erp` | varchar | NULL |  |
| 4 | `cantidad_hembras` | int | NOT NULL |  |
| 5 | `cantidad_machos` | int | NOT NULL |  |
| 6 | `cantidad_mixtas` | int | NOT NULL |  |
| 7 | `company_id` | int | NOT NULL |  |
| 8 | `created_by_user_id` | int | NOT NULL |  |
| 9 | `created_at` | timestamp | NOT NULL |  |
| 10 | `updated_by_user_id` | int | NULL |  |
| 11 | `updated_at` | timestamp | NULL |  |
| 12 | `deleted_at` | timestamp | NULL |  |
| 13 | `pais_id` | int | NULL |  |
| 14 | `farm_id` | int | NULL |  |
| 15 | `erp_create` | date | NULL |  |

---

## `lote_postura_levante`

- **Entidad:** `LotePosturaLevante`
- **PK:** `lote_postura_levante_id`
- **FK salientes:** `company_id` → `companies.id`, `granja_id` → `farms.id`, `lote_id` → `lotes.lote_id`, `lote_postura_levante_padre_id` → `lote_postura_levante.lote_postura_levante_id`
- **Referenciada por:** `historico_lote_postura`, `liquidacion_cierre_lote_levante`, `lote_postura_levante`, `lote_postura_produccion`

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `lote_postura_levante_id` | int | NOT NULL | PK |
| 2 | `lote_nombre` | varchar | NOT NULL |  |
| 3 | `granja_id` | int | NOT NULL | FK |
| 4 | `nucleo_id` | varchar | NULL |  |
| 5 | `galpon_id` | varchar | NULL |  |
| 6 | `regional` | varchar | NULL |  |
| 7 | `fecha_encaset` | timestamptz | NULL |  |
| 8 | `hembras_l` | int | NULL |  |
| 9 | `machos_l` | int | NULL |  |
| 10 | `peso_inicial_h` | float8 | NULL |  |
| 11 | `peso_inicial_m` | float8 | NULL |  |
| 12 | `unif_h` | float8 | NULL |  |
| 13 | `unif_m` | float8 | NULL |  |
| 14 | `mort_caja_h` | int | NULL |  |
| 15 | `mort_caja_m` | int | NULL |  |
| 16 | `raza` | varchar | NULL |  |
| 17 | `ano_tabla_genetica` | int | NULL |  |
| 18 | `linea` | varchar | NULL |  |
| 19 | `tipo_linea` | varchar | NULL |  |
| 20 | `codigo_guia_genetica` | varchar | NULL |  |
| 21 | `linea_genetica_id` | int | NULL |  |
| 22 | `tecnico` | varchar | NULL |  |
| 23 | `mixtas` | int | NULL |  |
| 24 | `peso_mixto` | float8 | NULL |  |
| 25 | `aves_encasetadas` | int | NULL |  |
| 26 | `edad_inicial` | int | NULL |  |
| 27 | `lote_erp` | varchar | NULL |  |
| 28 | `estado_traslado` | varchar | NULL |  |
| 29 | `pais_id` | int | NULL |  |
| 30 | `pais_nombre` | varchar | NULL |  |
| 31 | `empresa_nombre` | varchar | NULL |  |
| 32 | `lote_id` | int | NULL | FK |
| 33 | `lote_padre_id` | int | NULL |  |
| 34 | `lote_postura_levante_padre_id` | int | NULL | FK |
| 35 | `aves_h_inicial` | int | NULL |  |
| 36 | `aves_m_inicial` | int | NULL |  |
| 37 | `aves_h_actual` | int | NULL |  |
| 38 | `aves_m_actual` | int | NULL |  |
| 39 | `empresa_id` | int | NULL |  |
| 40 | `usuario_id` | int | NULL |  |
| 41 | `estado` | varchar | NULL |  |
| 42 | `etapa` | varchar | NULL |  |
| 43 | `edad` | int | NULL |  |
| 44 | `company_id` | int | NOT NULL | FK |
| 45 | `created_by_user_id` | int | NOT NULL |  |
| 46 | `created_at` | timestamptz | NOT NULL |  |
| 47 | `updated_by_user_id` | int | NULL |  |
| 48 | `updated_at` | timestamptz | NULL |  |
| 49 | `deleted_at` | timestamptz | NULL |  |
| 50 | `estado_cierre` | varchar | NULL |  |
| 51 | `levante_traslado_ingreso_hembras` | int | NOT NULL |  |
| 52 | `levante_traslado_ingreso_machos` | int | NOT NULL |  |
| 53 | `levante_traslado_salida_hembras` | int | NOT NULL |  |
| 54 | `levante_traslado_salida_machos` | int | NOT NULL |  |

---

## `lote_postura_produccion`

- **Entidad:** `LotePosturaProduccion`
- **PK:** `lote_postura_produccion_id`
- **FK salientes:** `company_id` → `companies.id`, `granja_id` → `farms.id`, `lote_id` → `lotes.lote_id`, `lote_postura_levante_id` → `lote_postura_levante.lote_postura_levante_id`
- **Referenciada por:** `espejo_huevo_produccion`, `historico_lote_postura`, `traslado_huevos`

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `lote_postura_produccion_id` | int | NOT NULL | PK |
| 2 | `lote_nombre` | varchar | NOT NULL |  |
| 3 | `granja_id` | int | NOT NULL | FK |
| 4 | `nucleo_id` | varchar | NULL |  |
| 5 | `galpon_id` | varchar | NULL |  |
| 6 | `regional` | varchar | NULL |  |
| 7 | `fecha_encaset` | timestamptz | NULL |  |
| 8 | `hembras_l` | int | NULL |  |
| 9 | `machos_l` | int | NULL |  |
| 10 | `peso_inicial_h` | float8 | NULL |  |
| 11 | `peso_inicial_m` | float8 | NULL |  |
| 12 | `unif_h` | float8 | NULL |  |
| 13 | `unif_m` | float8 | NULL |  |
| 14 | `mort_caja_h` | int | NULL |  |
| 15 | `mort_caja_m` | int | NULL |  |
| 16 | `raza` | varchar | NULL |  |
| 17 | `ano_tabla_genetica` | int | NULL |  |
| 18 | `linea` | varchar | NULL |  |
| 19 | `tipo_linea` | varchar | NULL |  |
| 20 | `codigo_guia_genetica` | varchar | NULL |  |
| 21 | `linea_genetica_id` | int | NULL |  |
| 22 | `tecnico` | varchar | NULL |  |
| 23 | `mixtas` | int | NULL |  |
| 24 | `peso_mixto` | float8 | NULL |  |
| 25 | `aves_encasetadas` | int | NULL |  |
| 26 | `edad_inicial` | int | NULL |  |
| 27 | `lote_erp` | varchar | NULL |  |
| 28 | `estado_traslado` | varchar | NULL |  |
| 29 | `pais_id` | int | NULL |  |
| 30 | `pais_nombre` | varchar | NULL |  |
| 31 | `empresa_nombre` | varchar | NULL |  |
| 32 | `fecha_inicio_produccion` | timestamptz | NULL |  |
| 33 | `hembras_iniciales_prod` | int | NULL |  |
| 34 | `machos_iniciales_prod` | int | NULL |  |
| 35 | `huevos_iniciales` | int | NULL |  |
| 36 | `tipo_nido` | varchar | NULL |  |
| 37 | `nucleo_p` | varchar | NULL |  |
| 38 | `ciclo_produccion` | varchar | NULL |  |
| 39 | `fecha_fin_produccion` | timestamptz | NULL |  |
| 40 | `aves_fin_hembras_prod` | int | NULL |  |
| 41 | `aves_fin_machos_prod` | int | NULL |  |
| 42 | `huevo_tot` | int | NULL |  |
| 43 | `huevo_inc` | int | NULL |  |
| 44 | `huevo_limpio` | int | NULL |  |
| 45 | `huevo_tratado` | int | NULL |  |
| 46 | `huevo_sucio` | int | NULL |  |
| 47 | `huevo_deforme` | int | NULL |  |
| 48 | `huevo_blanco` | int | NULL |  |
| 49 | `huevo_doble_yema` | int | NULL |  |
| 50 | `huevo_piso` | int | NULL |  |
| 51 | `huevo_pequeno` | int | NULL |  |
| 52 | `huevo_roto` | int | NULL |  |
| 53 | `huevo_desecho` | int | NULL |  |
| 54 | `huevo_otro` | int | NULL |  |
| 55 | `peso_huevo` | numeric | NULL |  |
| 56 | `lote_id` | int | NULL | FK |
| 57 | `lote_padre_id` | int | NULL |  |
| 58 | `lote_postura_levante_id` | int | NULL | FK |
| 59 | `aves_h_inicial` | int | NULL |  |
| 60 | `aves_m_inicial` | int | NULL |  |
| 61 | `aves_h_actual` | int | NULL |  |
| 62 | `aves_m_actual` | int | NULL |  |
| 63 | `empresa_id` | int | NULL |  |
| 64 | `usuario_id` | int | NULL |  |
| 65 | `estado` | varchar | NULL |  |
| 66 | `etapa` | varchar | NULL |  |
| 67 | `edad` | int | NULL |  |
| 68 | `company_id` | int | NOT NULL | FK |
| 69 | `created_by_user_id` | int | NOT NULL |  |
| 70 | `created_at` | timestamptz | NOT NULL |  |
| 71 | `updated_by_user_id` | int | NULL |  |
| 72 | `updated_at` | timestamptz | NULL |  |
| 73 | `deleted_at` | timestamptz | NULL |  |
| 74 | `estado_cierre` | varchar | NULL |  |
| 75 | `produccion_traslado_ingreso_hembras` | int | NOT NULL |  |
| 76 | `produccion_traslado_ingreso_machos` | int | NOT NULL |  |
| 77 | `produccion_traslado_salida_hembras` | int | NOT NULL |  |
| 78 | `produccion_traslado_salida_machos` | int | NOT NULL |  |

---

## `lote_registro_historico_unificado`

- **Entidad:** `LoteRegistroHistoricoUnificado`
- **PK:** `id`
- **FK salientes:** —
- **Referenciada por:** —

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `id` | bigint | NOT NULL | PK |
| 2 | `company_id` | int | NOT NULL |  |
| 3 | `lote_ave_engorde_id` | int | NULL |  |
| 4 | `farm_id` | int | NOT NULL |  |
| 5 | `nucleo_id` | varchar | NULL |  |
| 6 | `galpon_id` | varchar | NULL |  |
| 7 | `fecha_operacion` | date | NOT NULL |  |
| 8 | `tipo_evento` | varchar | NOT NULL |  |
| 9 | `origen_tabla` | varchar | NOT NULL |  |
| 10 | `origen_id` | int | NOT NULL |  |
| 11 | `movement_type_original` | varchar | NULL |  |
| 12 | `item_inventario_ecuador_id` | int | NULL |  |
| 13 | `item_resumen` | varchar | NULL |  |
| 14 | `cantidad_kg` | numeric | NULL |  |
| 15 | `unidad` | varchar | NULL |  |
| 16 | `cantidad_hembras` | int | NULL |  |
| 17 | `cantidad_machos` | int | NULL |  |
| 18 | `cantidad_mixtas` | int | NULL |  |
| 19 | `referencia` | varchar | NULL |  |
| 20 | `numero_documento` | varchar | NULL |  |
| 21 | `acumulado_entradas_alimento_kg` | numeric | NULL |  |
| 22 | `anulado` | bool | NOT NULL |  |
| 23 | `created_at` | timestamptz | NOT NULL |  |
| 24 | `peso_neto` | numeric | NULL |  |
| 25 | `peso_tara_real` | numeric | NULL |  |
| 26 | `promedio_peso_ave` | numeric | NULL |  |

---

## `lote_reproductora_ave_engorde`

- **Entidad:** `LoteReproductoraAveEngorde`
- **PK:** `id`
- **FK salientes:** `lote_ave_engorde_id` → `lote_ave_engorde.lote_ave_engorde_id`
- **Referenciada por:** `historial_lote_pollo_engorde`, `movimiento_pollo_engorde`, `seguimiento_diario_lote_reproductora_aves_engorde`

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `id` | int | NOT NULL | PK |
| 2 | `lote_ave_engorde_id` | int | NOT NULL | FK |
| 3 | `reproductora_id` | varchar | NOT NULL |  |
| 4 | `nombre_lote` | varchar | NOT NULL |  |
| 5 | `fecha_encasetamiento` | timestamptz | NULL |  |
| 6 | `m` | int | NULL |  |
| 7 | `h` | int | NULL |  |
| 8 | `aves_inicio_hembras` | int | NULL |  |
| 9 | `aves_inicio_machos` | int | NULL |  |
| 10 | `mixtas` | int | NULL |  |
| 11 | `mort_caja_h` | int | NULL |  |
| 12 | `mort_caja_m` | int | NULL |  |
| 13 | `unif_h` | int | NULL |  |
| 14 | `unif_m` | int | NULL |  |
| 15 | `peso_inicial_m` | numeric | NULL |  |
| 16 | `peso_inicial_h` | numeric | NULL |  |
| 17 | `peso_mixto` | numeric | NULL |  |
| 18 | `created_at` | timestamptz | NOT NULL |  |
| 19 | `updated_at` | timestamptz | NOT NULL |  |

---

## `lote_reproductoras`

- **Entidad:** `LoteReproductora`
- **PK:** `lote_id`, `reproductora_id`
- **FK salientes:** —
- **Referenciada por:** `lote_galpones`, `lote_seguimientos`

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `lote_id` | varchar | NOT NULL | PK |
| 2 | `reproductora_id` | varchar | NOT NULL | PK |
| 3 | `nombre_lote` | varchar | NOT NULL |  |
| 4 | `fecha_encasetamiento` | timestamptz | NULL |  |
| 5 | `m` | int | NULL |  |
| 6 | `h` | int | NULL |  |
| 7 | `mixtas` | int | NULL |  |
| 8 | `mort_caja_h` | int | NULL |  |
| 9 | `mort_caja_m` | int | NULL |  |
| 10 | `unif_h` | int | NULL |  |
| 11 | `unif_m` | int | NULL |  |
| 12 | `peso_inicial_m` | numeric | NULL |  |
| 13 | `peso_inicial_h` | numeric | NULL |  |
| 14 | `peso_mixto` | numeric | NULL |  |
| 15 | `aves_inicio_hembras` | int | NULL |  |
| 16 | `aves_inicio_machos` | int | NULL |  |

---

## `lote_seguimientos`

- **Entidad:** `LoteSeguimiento`
- **PK:** `id`
- **FK salientes:** `lote_id` → `lote_reproductoras.reproductora_id`, `lote_id` → `lote_reproductoras.lote_id`, `reproductora_id` → `lote_reproductoras.reproductora_id`, `reproductora_id` → `lote_reproductoras.lote_id`
- **Referenciada por:** —

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `id` | int | NOT NULL | PK |
| 2 | `fecha` | timestamptz | NOT NULL |  |
| 3 | `lote_id` | varchar | NOT NULL | FK |
| 4 | `reproductora_id` | varchar | NOT NULL | FK |
| 5 | `peso_inicial` | numeric | NULL |  |
| 6 | `peso_final` | numeric | NULL |  |
| 7 | `mortalidad_m` | int | NULL |  |
| 8 | `mortalidad_h` | int | NULL |  |
| 9 | `sel_m` | int | NULL |  |
| 10 | `sel_h` | int | NULL |  |
| 11 | `error_m` | int | NULL |  |
| 12 | `error_h` | int | NULL |  |
| 13 | `tipo_alimento` | varchar | NULL |  |
| 14 | `consumo_alimento` | numeric | NULL |  |
| 15 | `observaciones` | varchar | NULL |  |
| 16 | `company_id` | int | NOT NULL |  |
| 17 | `created_by_user_id` | int | NOT NULL |  |
| 18 | `created_at` | timestamptz | NOT NULL |  |
| 19 | `updated_by_user_id` | int | NULL |  |
| 20 | `updated_at` | timestamptz | NULL |  |
| 21 | `deleted_at` | timestamptz | NULL |  |
| 22 | `consumo_agua_diario` | float8 | NULL |  |
| 23 | `consumo_agua_ph` | float8 | NULL |  |
| 24 | `consumo_agua_orp` | float8 | NULL |  |
| 25 | `consumo_agua_temperatura` | float8 | NULL |  |
| 26 | `peso_prom_h` | float8 | NULL |  |
| 27 | `peso_prom_m` | float8 | NULL |  |
| 28 | `uniformidad_h` | float8 | NULL |  |
| 29 | `uniformidad_m` | float8 | NULL |  |
| 30 | `cv_h` | float8 | NULL |  |
| 31 | `cv_m` | float8 | NULL |  |
| 32 | `consumo_kg_machos` | float8 | NULL |  |
| 33 | `metadata` | jsonb | NULL |  |
| 34 | `items_adicionales` | jsonb | NULL |  |
| 35 | `ciclo` | varchar | NULL |  |

---

## `lotes`

- **Entidad:** `Lote`
- **PK:** `lote_id`
- **FK salientes:** `galpon_id` → `galpones.galpon_id`, `granja_id` → `farms.id`, `granja_id` → `nucleos.granja_id`, `granja_id` → `nucleos.nucleo_id`, `lote_padre_id` → `lotes.lote_id`, `lote_postura_base_id` → `lote_postura_base.lote_postura_base_id`, `nucleo_id` → `nucleos.granja_id`, `nucleo_id` → `nucleos.nucleo_id`
- **Referenciada por:** `lote_etapa_levante`, `lote_postura_levante`, `lote_postura_produccion`, `lotes`, `produccion_seguimiento`, `reporte_tecnico_guia`, `seguimiento_diario_levante_reproductoras`

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `lote_nombre` | varchar | NOT NULL |  |
| 2 | `granja_id` | int | NOT NULL | FK |
| 3 | `nucleo_id` | varchar | NULL | FK |
| 4 | `galpon_id` | varchar | NULL | FK |
| 5 | `regional` | varchar | NULL |  |
| 6 | `fecha_encaset` | timestamptz | NULL |  |
| 7 | `hembras_l` | int | NULL |  |
| 8 | `machos_l` | int | NULL |  |
| 9 | `peso_inicial_h` | float8 | NULL |  |
| 10 | `peso_inicial_m` | float8 | NULL |  |
| 11 | `unif_h` | float8 | NULL |  |
| 12 | `unif_m` | float8 | NULL |  |
| 13 | `mort_caja_h` | int | NULL |  |
| 14 | `mort_caja_m` | int | NULL |  |
| 15 | `raza` | varchar | NULL |  |
| 16 | `ano_tabla_genetica` | int | NULL |  |
| 17 | `linea` | varchar | NULL |  |
| 18 | `tipo_linea` | varchar | NULL |  |
| 19 | `codigo_guia_genetica` | varchar | NULL |  |
| 20 | `tecnico` | varchar | NULL |  |
| 21 | `mixtas` | int | NULL |  |
| 22 | `peso_mixto` | float8 | NULL |  |
| 23 | `aves_encasetadas` | int | NULL |  |
| 24 | `edad_inicial` | int | NULL |  |
| 25 | `company_id` | int | NOT NULL |  |
| 26 | `created_by_user_id` | int | NOT NULL |  |
| 27 | `created_at` | timestamptz | NOT NULL |  |
| 28 | `updated_by_user_id` | int | NULL |  |
| 29 | `updated_at` | timestamptz | NULL |  |
| 30 | `deleted_at` | timestamptz | NULL |  |
| 31 | `linea_genetica_id` | int | NULL |  |
| 32 | `lote_id` | int | NOT NULL | PK |
| 33 | `lote_erp` | varchar | NULL |  |
| 34 | `estado_traslado` | varchar | NULL |  |
| 35 | `lote_padre_id` | int | NULL | FK |
| 36 | `fecha_recepcion` | timestamp | NULL |  |
| 37 | `incubadora_origen` | text | NULL |  |
| 38 | `fase` | varchar | NOT NULL |  |
| 39 | `fecha_inicio_produccion` | timestamptz | NULL |  |
| 40 | `hembras_iniciales_prod` | int | NULL |  |
| 41 | `machos_iniciales_prod` | int | NULL |  |
| 42 | `huevos_iniciales` | int | NULL |  |
| 43 | `tipo_nido` | varchar | NULL |  |
| 44 | `nucleo_p` | varchar | NULL |  |
| 45 | `ciclo_produccion` | varchar | NULL |  |
| 46 | `fecha_fin_produccion` | timestamptz | NULL |  |
| 47 | `aves_fin_hembras_prod` | int | NULL |  |
| 48 | `aves_fin_machos_prod` | int | NULL |  |
| 49 | `pais_id` | int | NULL |  |
| 50 | `pais_nombre` | varchar | NULL |  |
| 51 | `empresa_nombre` | varchar | NULL |  |
| 52 | `lote_postura_base_id` | int | NULL | FK |

---

## `mapa`

- **Entidad:** `Mapa`
- **PK:** `id`
- **FK salientes:** `company_id` → `companies.id`, `created_by_user_id` → `users.id`, `pais_id` → `paises.pais_id`
- **Referenciada por:** `mapa_ejecucion`, `mapa_paso`

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `id` | int | NOT NULL | PK |
| 2 | `nombre` | varchar | NOT NULL |  |
| 3 | `descripcion` | text | NULL |  |
| 4 | `company_id` | int | NOT NULL | FK |
| 5 | `pais_id` | int | NULL | FK |
| 6 | `is_active` | bool | NOT NULL |  |
| 7 | `created_by_user_id` | uuid | NOT NULL | FK |
| 8 | `created_at` | timestamptz | NOT NULL |  |
| 9 | `updated_by_user_id` | uuid | NULL |  |
| 10 | `updated_at` | timestamptz | NULL |  |
| 11 | `deleted_at` | timestamptz | NULL |  |
| 12 | `codigo_plantilla` | varchar | NULL |  |

---

## `mapa_ejecucion`

- **Entidad:** `MapaEjecucion`
- **PK:** `id`
- **FK salientes:** `company_id` → `companies.id`, `mapa_id` → `mapa.id`, `usuario_id` → `users.id`
- **Referenciada por:** —

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `id` | int | NOT NULL | PK |
| 2 | `mapa_id` | int | NOT NULL | FK |
| 3 | `usuario_id` | uuid | NOT NULL | FK |
| 4 | `company_id` | int | NOT NULL | FK |
| 5 | `parametros` | jsonb | NOT NULL |  |
| 6 | `tipo_archivo` | varchar | NULL |  |
| 7 | `resultado_json` | jsonb | NULL |  |
| 8 | `estado` | varchar | NOT NULL |  |
| 9 | `mensaje_error` | text | NULL |  |
| 10 | `mensaje_estado` | varchar | NULL |  |
| 11 | `paso_actual` | int | NULL |  |
| 12 | `total_pasos` | int | NULL |  |
| 13 | `fecha_ejecucion` | timestamptz | NOT NULL |  |

---

## `mapa_paso`

- **Entidad:** `MapaPaso`
- **PK:** `id`
- **FK salientes:** `mapa_id` → `mapa.id`
- **Referenciada por:** —

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `id` | int | NOT NULL | PK |
| 2 | `mapa_id` | int | NOT NULL | FK |
| 3 | `orden` | int | NOT NULL |  |
| 4 | `tipo` | varchar | NOT NULL |  |
| 5 | `nombre_etiqueta` | varchar | NULL |  |
| 6 | `script_sql` | text | NULL |  |
| 7 | `opciones` | jsonb | NULL |  |
| 8 | `created_at` | timestamptz | NOT NULL |  |
| 9 | `updated_at` | timestamptz | NULL |  |

---

## `master_list_options`

- **Entidad:** `MasterListOption`
- **PK:** `id`
- **FK salientes:** `master_list_id` → `master_lists.id`
- **Referenciada por:** —

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `id` | int | NOT NULL | PK |
| 2 | `master_list_id` | int | NOT NULL | FK |
| 3 | `value` | varchar | NOT NULL |  |
| 4 | `order` | int | NOT NULL |  |

---

## `master_lists`

- **Entidad:** `MasterList`
- **PK:** `id`
- **FK salientes:** —
- **Referenciada por:** `master_list_options`

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `id` | int | NOT NULL | PK |
| 2 | `key` | varchar | NOT NULL |  |
| 3 | `name` | varchar | NOT NULL |  |
| 4 | `company_id` | int | NULL |  |
| 5 | `company_name` | varchar | NULL |  |
| 6 | `country_id` | int | NULL |  |
| 7 | `country_name` | varchar | NULL |  |

---

## `menu_permissions`

- **Entidad:** `MenuPermission`
- **PK:** `menu_id`, `permission_id`
- **FK salientes:** `menu_id` → `menus.id`, `permission_id` → `permissions.id`
- **Referenciada por:** —

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `menu_id` | int | NOT NULL | PK FK |
| 2 | `permission_id` | int | NOT NULL | PK FK |

---

## `menus`

- **Entidad:** `Menu`
- **PK:** `id`
- **FK salientes:** `parent_id` → `menus.id`
- **Referenciada por:** `company_menus`, `menu_permissions`, `menus`, `role_menus`

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `id` | int | NOT NULL | PK |
| 2 | `label` | varchar | NOT NULL |  |
| 3 | `icon` | varchar | NULL |  |
| 4 | `route` | varchar | NULL |  |
| 5 | `order` | int | NOT NULL |  |
| 6 | `is_active` | bool | NOT NULL |  |
| 7 | `parent_id` | int | NULL | FK |
| 8 | `key` | text | NOT NULL |  |
| 9 | `sort_order` | int | NOT NULL |  |
| 10 | `is_group` | bool | NOT NULL |  |
| 11 | `created_at` | timestamptz | NULL |  |
| 12 | `updated_at` | timestamptz | NULL |  |

---

## `movimiento_aves`

- **Entidad:** `MovimientoAves`
- **PK:** `id`
- **FK salientes:** —
- **Referenciada por:** —

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `id` | int | NOT NULL | PK |
| 2 | `numero_movimiento` | varchar | NOT NULL |  |
| 3 | `fecha_movimiento` | timestamptz | NOT NULL |  |
| 4 | `tipo_movimiento` | varchar | NOT NULL |  |
| 5 | `inventario_origen_id` | int | NULL |  |
| 6 | `lote_origen_id` | int | NULL |  |
| 7 | `granja_origen_id` | int | NULL |  |
| 8 | `nucleo_origen_id` | varchar | NULL |  |
| 9 | `galpon_origen_id` | varchar | NULL |  |
| 10 | `inventario_destino_id` | int | NULL |  |
| 11 | `lote_destino_id` | int | NULL |  |
| 12 | `granja_destino_id` | int | NULL |  |
| 13 | `nucleo_destino_id` | varchar | NULL |  |
| 14 | `galpon_destino_id` | varchar | NULL |  |
| 15 | `cantidad_hembras` | int | NOT NULL |  |
| 16 | `cantidad_machos` | int | NOT NULL |  |
| 17 | `cantidad_mixtas` | int | NOT NULL |  |
| 18 | `motivo_movimiento` | varchar | NULL |  |
| 19 | `observaciones` | varchar | NULL |  |
| 20 | `estado` | varchar | NOT NULL |  |
| 21 | `usuario_movimiento_id` | int | NOT NULL |  |
| 22 | `usuario_nombre` | varchar | NULL |  |
| 23 | `fecha_procesamiento` | timestamptz | NULL |  |
| 24 | `fecha_cancelacion` | timestamptz | NULL |  |
| 25 | `company_id` | int | NOT NULL |  |
| 26 | `created_by_user_id` | int | NOT NULL |  |
| 27 | `created_at` | timestamptz | NOT NULL |  |
| 28 | `updated_by_user_id` | int | NULL |  |
| 29 | `updated_at` | timestamptz | NULL |  |
| 30 | `deleted_at` | timestamptz | NULL |  |
| 31 | `nucleo_destino_granja_id` | int | NULL |  |
| 32 | `nucleo_destino_nucleo_id` | varchar | NULL |  |
| 33 | `nucleo_origen_granja_id` | int | NULL |  |
| 34 | `nucleo_origen_nucleo_id` | varchar | NULL |  |
| 35 | `planta_destino` | varchar | NULL |  |
| 36 | `descripcion` | varchar | NULL |  |
| 37 | `edad_aves` | int | NULL |  |
| 38 | `raza` | varchar | NULL |  |
| 39 | `placa` | varchar | NULL |  |
| 40 | `hora_salida` | time without time zone | NULL |  |
| 41 | `guia_agrocalidad` | varchar | NULL |  |
| 42 | `sellos` | varchar | NULL |  |
| 43 | `ayuno` | varchar | NULL |  |
| 44 | `conductor` | varchar | NULL |  |
| 45 | `total_pollos_galpon` | int | NULL |  |
| 46 | `peso_bruto` | float8 | NULL |  |
| 47 | `peso_tara` | float8 | NULL |  |

---

## `movimiento_pollo_engorde`

- **Entidad:** `MovimientoPolloEngorde`
- **PK:** `id`
- **FK salientes:** `company_id` → `companies.id`, `granja_destino_id` → `farms.id`, `granja_origen_id` → `farms.id`, `lote_ave_engorde_destino_id` → `lote_ave_engorde.lote_ave_engorde_id`, `lote_ave_engorde_origen_id` → `lote_ave_engorde.lote_ave_engorde_id`, `lote_reproductora_ave_engorde_destino_id` → `lote_reproductora_ave_engorde.id`, `lote_reproductora_ave_engorde_origen_id` → `lote_reproductora_ave_engorde.id`
- **Referenciada por:** `historial_lote_pollo_engorde`

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `id` | int | NOT NULL | PK |
| 2 | `numero_movimiento` | varchar | NOT NULL |  |
| 3 | `fecha_movimiento` | timestamptz | NOT NULL |  |
| 4 | `tipo_movimiento` | varchar | NOT NULL |  |
| 5 | `lote_ave_engorde_origen_id` | int | NULL | FK |
| 6 | `lote_reproductora_ave_engorde_origen_id` | int | NULL | FK |
| 7 | `granja_origen_id` | int | NULL | FK |
| 8 | `nucleo_origen_id` | varchar | NULL |  |
| 9 | `galpon_origen_id` | varchar | NULL |  |
| 10 | `lote_ave_engorde_destino_id` | int | NULL | FK |
| 11 | `lote_reproductora_ave_engorde_destino_id` | int | NULL | FK |
| 12 | `granja_destino_id` | int | NULL | FK |
| 13 | `nucleo_destino_id` | varchar | NULL |  |
| 14 | `galpon_destino_id` | varchar | NULL |  |
| 15 | `planta_destino` | varchar | NULL |  |
| 16 | `cantidad_hembras` | int | NOT NULL |  |
| 17 | `cantidad_machos` | int | NOT NULL |  |
| 18 | `cantidad_mixtas` | int | NOT NULL |  |
| 19 | `motivo_movimiento` | varchar | NULL |  |
| 20 | `descripcion` | varchar | NULL |  |
| 21 | `observaciones` | varchar | NULL |  |
| 22 | `estado` | varchar | NOT NULL |  |
| 23 | `usuario_movimiento_id` | int | NOT NULL |  |
| 24 | `usuario_nombre` | varchar | NULL |  |
| 25 | `fecha_procesamiento` | timestamptz | NULL |  |
| 26 | `fecha_cancelacion` | timestamptz | NULL |  |
| 27 | `company_id` | int | NOT NULL | FK |
| 28 | `created_by_user_id` | int | NOT NULL |  |
| 29 | `created_at` | timestamptz | NOT NULL |  |
| 30 | `updated_by_user_id` | int | NULL |  |
| 31 | `updated_at` | timestamptz | NULL |  |
| 32 | `deleted_at` | timestamptz | NULL |  |
| 33 | `numero_despacho` | varchar | NULL |  |
| 34 | `edad_aves` | int | NULL |  |
| 35 | `total_pollos_galpon` | int | NULL |  |
| 36 | `raza` | varchar | NULL |  |
| 37 | `placa` | varchar | NULL |  |
| 38 | `hora_salida` | time without time zone | NULL |  |
| 39 | `guia_agrocalidad` | varchar | NULL |  |
| 40 | `sellos` | varchar | NULL |  |
| 41 | `ayuno` | varchar | NULL |  |
| 42 | `conductor` | varchar | NULL |  |
| 43 | `peso_bruto` | float8 | NULL |  |
| 44 | `peso_tara` | float8 | NULL |  |
| 45 | `peso_bruto_global` | float8 | NULL |  |
| 46 | `peso_neto` | float8 | NULL |  |
| 47 | `peso_neto_global` | float8 | NULL |  |
| 48 | `peso_tara_global` | float8 | NULL |  |
| 49 | `promedio_peso_ave` | float8 | NULL |  |
| 50 | `peso_bruto_real` | float8 | NULL |  |
| 51 | `peso_tara_real` | float8 | NULL |  |
| 52 | `factura_id` | uuid | NULL |  |
| 53 | `aves_sobrante` | int | NOT NULL |  |

---

## `municipios`

- **Entidad:** `Municipio`
- **PK:** `municipio_id`
- **FK salientes:** `departamento_id` → `departamentos.departamento_id`, `departamento_id1` → `departamentos.departamento_id`
- **Referenciada por:** `farms`

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `municipio_id` | int | NOT NULL | PK |
| 2 | `nombre` | varchar | NOT NULL |  |
| 3 | `departamento_id` | int | NOT NULL | FK |
| 4 | `departamento_id1` | int | NULL | FK |

---

## `nucleos`

- **Entidad:** `Nucleo`
- **PK:** `nucleo_id`, `granja_id`
- **FK salientes:** `granja_id` → `farms.id`
- **Referenciada por:** `galpones`, `lote_ave_engorde`, `lotes`, `produccion_lotes`

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `nucleo_id` | varchar | NOT NULL | PK |
| 2 | `granja_id` | int | NOT NULL | PK FK |
| 3 | `nucleo_nombre` | varchar | NOT NULL |  |
| 4 | `company_id` | int | NOT NULL |  |
| 5 | `created_by_user_id` | int | NOT NULL |  |
| 6 | `created_at` | timestamptz | NOT NULL |  |
| 7 | `updated_by_user_id` | int | NULL |  |
| 8 | `updated_at` | timestamptz | NULL |  |
| 9 | `deleted_at` | timestamptz | NULL |  |

---

## `paises`

- **Entidad:** `Pais`
- **PK:** `pais_id`
- **FK salientes:** —
- **Referenciada por:** `company_pais`, `departamentos`, `farm_inventory_movements`, `farm_product_inventory`, `inventario_gasto`, `inventario_gestion_movimiento`, `inventario_gestion_stock`, `item_inventario_ecuador`, `mapa`, `user_companies`, `user_paises`

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `pais_id` | int | NOT NULL | PK |
| 2 | `pais_nombre` | varchar | NOT NULL |  |

---

## `permissions`

- **Entidad:** `Permission`
- **PK:** `id`
- **FK salientes:** —
- **Referenciada por:** `menu_permissions`, `role_permissions`

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `id` | int | NOT NULL | PK |
| 2 | `key` | varchar | NOT NULL |  |
| 3 | `description` | varchar | NOT NULL |  |

---

## `plan_gramaje_galpon`

- **Entidad:** `PlanGramajeGalpon`
- **PK:** `id`
- **FK salientes:** `galpon_id` → `galpones.galpon_id`
- **Referenciada por:** —

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `id` | int | NOT NULL | PK |
| 2 | `galpon_id` | varchar | NOT NULL | FK |
| 3 | `semana_desde` | int | NOT NULL |  |
| 4 | `semana_hasta` | int | NOT NULL |  |
| 5 | `tipo_alimento` | varchar | NULL |  |
| 6 | `gramaje_gr_por_ave` | float8 | NOT NULL |  |
| 7 | `vigente` | bool | NOT NULL |  |
| 8 | `observaciones` | varchar | NULL |  |

---

## `produccion_lotes`

- **Entidad:** `ProduccionLote`
- **PK:** `id`
- **FK salientes:** `galpon_id` → `galpones.galpon_id`, `granja_id` → `nucleos.nucleo_id`, `granja_id` → `nucleos.granja_id`, `nucleo_id` → `nucleos.granja_id`, `nucleo_id` → `nucleos.nucleo_id`
- **Referenciada por:** `seguimiento_diario_produccion_reproductoras`

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `id` | int | NOT NULL | PK |
| 2 | `lote_id` | varchar | NOT NULL |  |
| 3 | `fecha_inicio_produccion` | timestamptz | NOT NULL |  |
| 4 | `hembras_iniciales` | int | NOT NULL |  |
| 5 | `machos_iniciales` | int | NOT NULL |  |
| 6 | `huevos_iniciales` | int | NOT NULL |  |
| 7 | `tipo_nido` | varchar | NOT NULL |  |
| 8 | `granja_id` | int | NOT NULL | FK |
| 9 | `nucleo_id` | varchar | NOT NULL | FK |
| 10 | `galpon_id` | varchar | NULL | FK |
| 11 | `ciclo` | varchar | NOT NULL |  |
| 12 | `company_id` | int | NOT NULL |  |
| 13 | `created_by_user_id` | int | NOT NULL |  |
| 14 | `created_at` | timestamptz | NOT NULL |  |
| 15 | `updated_by_user_id` | int | NULL |  |
| 16 | `updated_at` | timestamptz | NULL |  |
| 17 | `deleted_at` | timestamptz | NULL |  |
| 18 | `nucleo_p` | varchar | NULL |  |
| 19 | `observaciones` | varchar | NULL |  |
| 20 | `fecha_fin` | timestamptz | NULL |  |
| 21 | `aves_fin_hembras` | int | NULL |  |
| 22 | `aves_fin_machos` | int | NULL |  |

---

## `produccion_resultado_levante`

- **Entidad:** `ProduccionResultadoLevante`
- **PK:** `id`
- **FK salientes:** —
- **Referenciada por:** —

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `id` | bigint | NOT NULL | PK |
| 2 | `lote_id` | text | NOT NULL |  |
| 3 | `fecha` | date | NOT NULL |  |
| 4 | `edad_semana` | int | NULL |  |
| 5 | `hembra_viva` | int | NULL |  |
| 6 | `mort_h` | int | NULL |  |
| 7 | `sel_h_out` | int | NULL |  |
| 8 | `err_h` | int | NULL |  |
| 9 | `cons_kg_h` | float8 | NULL |  |
| 10 | `peso_h` | float8 | NULL |  |
| 11 | `unif_h` | float8 | NULL |  |
| 12 | `cv_h` | float8 | NULL |  |
| 13 | `mort_h_pct` | float8 | NULL |  |
| 14 | `sel_h_pct` | float8 | NULL |  |
| 15 | `err_h_pct` | float8 | NULL |  |
| 16 | `ms_eh_h` | int | NULL |  |
| 17 | `ac_mort_h` | int | NULL |  |
| 18 | `ac_sel_h` | int | NULL |  |
| 19 | `ac_err_h` | int | NULL |  |
| 20 | `ac_cons_kg_h` | float8 | NULL |  |
| 21 | `cons_ac_gr_h` | float8 | NULL |  |
| 22 | `gr_ave_dia_h` | float8 | NULL |  |
| 23 | `dif_cons_h_pct` | float8 | NULL |  |
| 24 | `dif_peso_h_pct` | float8 | NULL |  |
| 25 | `retiro_h_pct` | float8 | NULL |  |
| 26 | `retiro_h_ac_pct` | float8 | NULL |  |
| 27 | `macho_vivo` | int | NULL |  |
| 28 | `mort_m` | int | NULL |  |
| 29 | `sel_m_out` | int | NULL |  |
| 30 | `err_m` | int | NULL |  |
| 31 | `cons_kg_m` | float8 | NULL |  |
| 32 | `peso_m` | float8 | NULL |  |
| 33 | `unif_m` | float8 | NULL |  |
| 34 | `cv_m` | float8 | NULL |  |
| 35 | `mort_m_pct` | float8 | NULL |  |
| 36 | `sel_m_pct` | float8 | NULL |  |
| 37 | `err_m_pct` | float8 | NULL |  |
| 38 | `ms_em_m` | int | NULL |  |
| 39 | `ac_mort_m` | int | NULL |  |
| 40 | `ac_sel_m` | int | NULL |  |
| 41 | `ac_err_m` | int | NULL |  |
| 42 | `ac_cons_kg_m` | float8 | NULL |  |
| 43 | `cons_ac_gr_m` | float8 | NULL |  |
| 44 | `gr_ave_dia_m` | float8 | NULL |  |
| 45 | `dif_cons_m_pct` | float8 | NULL |  |
| 46 | `dif_peso_m_pct` | float8 | NULL |  |
| 47 | `retiro_m_pct` | float8 | NULL |  |
| 48 | `retiro_m_ac_pct` | float8 | NULL |  |
| 49 | `rel_m_h_pct` | float8 | NULL |  |
| 50 | `peso_h_guia` | float8 | NULL |  |
| 51 | `unif_h_guia` | float8 | NULL |  |
| 52 | `cons_ac_gr_h_guia` | float8 | NULL |  |
| 53 | `gr_ave_dia_h_guia` | float8 | NULL |  |
| 54 | `mort_h_pct_guia` | float8 | NULL |  |
| 55 | `peso_m_guia` | float8 | NULL |  |
| 56 | `unif_m_guia` | float8 | NULL |  |
| 57 | `cons_ac_gr_m_guia` | float8 | NULL |  |
| 58 | `gr_ave_dia_m_guia` | float8 | NULL |  |
| 59 | `mort_m_pct_guia` | float8 | NULL |  |
| 60 | `alimento_h_guia` | text | NULL |  |
| 61 | `alimento_m_guia` | text | NULL |  |

---

## `produccion_seguimiento`

- **Entidad:** `ProduccionSeguimiento`
- **PK:** `id`
- **FK salientes:** `lote_id` → `lotes.lote_id`
- **Referenciada por:** —

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `id` | int | NOT NULL | PK |
| 2 | `lote_id` | int | NOT NULL | FK |
| 3 | `fecha_registro` | date | NOT NULL |  |
| 4 | `mortalidad_h` | int | NOT NULL |  |
| 5 | `mortalidad_m` | int | NOT NULL |  |
| 6 | `consumo_kg` | numeric | NOT NULL |  |
| 7 | `huevos_totales` | int | NOT NULL |  |
| 8 | `huevos_incubables` | int | NOT NULL |  |
| 9 | `peso_huevo` | numeric | NOT NULL |  |
| 10 | `observaciones` | varchar | NULL |  |
| 11 | `traslado_hembras` | int | NULL |  |
| 12 | `traslado_machos` | int | NULL |  |
| 13 | `lote_destino_id` | int | NULL |  |
| 14 | `granja_destino_id` | int | NULL |  |
| 15 | `fecha_traslado` | date | NULL |  |
| 16 | `traslado_observaciones` | varchar | NULL |  |
| 17 | `company_id` | int | NULL |  |
| 18 | `created_by_user_id` | int | NULL |  |
| 19 | `created_at` | timestamptz | NOT NULL |  |
| 20 | `updated_at` | timestamptz | NULL |  |
| 21 | `deleted_at` | timestamptz | NULL |  |
| 22 | `traslado_ingreso_hembras` | int | NOT NULL |  |
| 23 | `traslado_ingreso_machos` | int | NOT NULL |  |
| 24 | `traslado_salida_hembras` | int | NOT NULL |  |
| 25 | `traslado_salida_machos` | int | NOT NULL |  |
| 26 | `es_traslado` | bool | NOT NULL |  |
| 27 | `traslado_lote_contraparte_id` | int | NULL |  |
| 28 | `traslado_granja_contraparte_id` | int | NULL |  |
| 29 | `traslado_direccion` | varchar | NULL |  |
| 30 | `sel_h` | int | NOT NULL |  |
| 31 | `sel_m` | int | NOT NULL |  |
| 32 | `error_sexaje_hembras` | int | NOT NULL |  |
| 33 | `error_sexaje_machos` | int | NOT NULL |  |
| 34 | `updated_by_user_id` | int | NULL |  |

---

## `regionales`

- **Entidad:** `Regional`
- **PK:** `regional_cia`, `regional_id`
- **FK salientes:** `regional_cia` → `companies.id`
- **Referenciada por:** —

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `regional_cia` | int | NOT NULL | PK FK |
| 2 | `regional_id` | int | NOT NULL | PK |
| 3 | `regional_nombre` | varchar | NOT NULL |  |
| 4 | `regional_estado` | varchar | NOT NULL |  |
| 5 | `regional_codigo` | varchar | NOT NULL |  |

---

## `reporte_tecnico_guia`

- **Entidad:** `ReporteTecnicoGuia`
- **PK:** `id`
- **FK salientes:** `lote_id` → `lotes.lote_id`
- **Referenciada por:** —

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `id` | int | NOT NULL | PK |
| 2 | `company_id` | int | NOT NULL |  |
| 3 | `created_by_user_id` | int | NOT NULL |  |
| 4 | `created_at` | timestamptz | NOT NULL |  |
| 5 | `updated_by_user_id` | int | NULL |  |
| 6 | `updated_at` | timestamptz | NULL |  |
| 7 | `deleted_at` | timestamptz | NULL |  |
| 8 | `lote_id` | int | NOT NULL | FK |
| 9 | `semana` | int | NOT NULL |  |
| 10 | `porc_mort_h_guia` | numeric | NULL |  |
| 11 | `retiro_h_guia` | numeric | NULL |  |
| 12 | `cons_ac_gr_h_guia` | numeric | NULL |  |
| 13 | `gr_ave_dia_guia_h` | numeric | NULL |  |
| 14 | `incr_cons_h_guia` | numeric | NULL |  |
| 15 | `peso_h_guia` | numeric | NULL |  |
| 16 | `unif_h_guia` | numeric | NULL |  |
| 17 | `porc_mort_m_guia` | numeric | NULL |  |
| 18 | `retiro_m_guia` | numeric | NULL |  |
| 19 | `cons_ac_gr_m_guia` | numeric | NULL |  |
| 20 | `gr_ave_dia_guia_m` | numeric | NULL |  |
| 21 | `incr_cons_m_guia` | numeric | NULL |  |
| 22 | `peso_m_guia` | numeric | NULL |  |
| 23 | `unif_m_guia` | numeric | NULL |  |
| 24 | `alim_h_guia` | varchar | NULL |  |
| 25 | `kcal_sem_h_guia` | numeric | NULL |  |
| 26 | `prot_sem_h_guia` | numeric | NULL |  |
| 27 | `alim_m_guia` | varchar | NULL |  |
| 28 | `kcal_sem_m_guia` | numeric | NULL |  |
| 29 | `prot_sem_m_guia` | numeric | NULL |  |
| 30 | `err_sex_ac_h` | int | NULL |  |
| 31 | `err_sex_ac_m` | int | NULL |  |
| 32 | `cod_guia` | varchar | NULL |  |
| 33 | `id_lote_rap` | varchar | NULL |  |
| 34 | `traslado` | int | NULL |  |
| 35 | `nucleo_l` | varchar | NULL |  |
| 36 | `anon` | int | NULL |  |

---

## `role_companies`

- **Entidad:** `RoleCompany`
- **PK:** `role_id`, `company_id`
- **FK salientes:** `company_id` → `companies.id`, `role_id` → `roles.id`
- **Referenciada por:** —

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `role_id` | int | NOT NULL | PK FK |
| 2 | `company_id` | int | NOT NULL | PK FK |

---

## `role_menus`

- **Entidad:** `RoleMenu`
- **PK:** `role_id`, `menu_id`
- **FK salientes:** `menu_id` → `menus.id`, `role_id` → `roles.id`
- **Referenciada por:** —

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `role_id` | int | NOT NULL | PK FK |
| 2 | `menu_id` | int | NOT NULL | PK FK |

---

## `role_permissions`

- **Entidad:** `RolePermission`
- **PK:** `role_id`, `permission_id`
- **FK salientes:** `permission_id` → `permissions.id`, `role_id` → `roles.id`
- **Referenciada por:** —

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `role_id` | int | NOT NULL | PK FK |
| 2 | `permission_id` | int | NOT NULL | PK FK |

---

## `roles`

- **Entidad:** `Role`
- **PK:** `id`
- **FK salientes:** —
- **Referenciada por:** `role_companies`, `role_menus`, `role_permissions`, `user_roles`

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `id` | int | NOT NULL | PK |
| 2 | `name` | varchar | NOT NULL |  |
| 3 | `description` | varchar | NULL |  |
| 4 | `allow_multiple_countries` | bool | NOT NULL |  |
| 5 | `allow_multiple_companies` | bool | NOT NULL |  |

---

## `seguimiento_diario_aves_engorde`

- **Entidad:** `SeguimientoDiarioAvesEngorde`
- **PK:** `id`
- **FK salientes:** `lote_ave_engorde_id` → `lote_ave_engorde.lote_ave_engorde_id`
- **Referenciada por:** —

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `id` | bigint | NOT NULL | PK |
| 2 | `lote_ave_engorde_id` | int | NOT NULL | FK |
| 3 | `fecha` | timestamptz | NOT NULL |  |
| 4 | `mortalidad_hembras` | int | NULL |  |
| 5 | `mortalidad_machos` | int | NULL |  |
| 6 | `sel_h` | int | NULL |  |
| 7 | `sel_m` | int | NULL |  |
| 8 | `error_sexaje_hembras` | int | NULL |  |
| 9 | `error_sexaje_machos` | int | NULL |  |
| 10 | `consumo_kg_hembras` | numeric | NULL |  |
| 11 | `consumo_kg_machos` | numeric | NULL |  |
| 12 | `tipo_alimento` | varchar | NULL |  |
| 13 | `observaciones` | text | NULL |  |
| 14 | `ciclo` | varchar | NULL |  |
| 15 | `peso_prom_hembras` | float8 | NULL |  |
| 16 | `peso_prom_machos` | float8 | NULL |  |
| 17 | `uniformidad_hembras` | float8 | NULL |  |
| 18 | `uniformidad_machos` | float8 | NULL |  |
| 19 | `cv_hembras` | float8 | NULL |  |
| 20 | `cv_machos` | float8 | NULL |  |
| 21 | `consumo_agua_diario` | float8 | NULL |  |
| 22 | `consumo_agua_ph` | float8 | NULL |  |
| 23 | `consumo_agua_orp` | float8 | NULL |  |
| 24 | `consumo_agua_temperatura` | float8 | NULL |  |
| 25 | `metadata` | jsonb | NULL |  |
| 26 | `items_adicionales` | jsonb | NULL |  |
| 27 | `kcal_al_h` | float8 | NULL |  |
| 28 | `prot_al_h` | float8 | NULL |  |
| 29 | `kcal_ave_h` | float8 | NULL |  |
| 30 | `prot_ave_h` | float8 | NULL |  |
| 31 | `created_by_user_id` | varchar | NULL |  |
| 32 | `created_at` | timestamptz | NOT NULL |  |
| 33 | `updated_at` | timestamptz | NULL |  |
| 34 | `saldo_alimento_kg` | numeric | NULL |  |
| 35 | `historico_consumo_alimento` | jsonb | NULL |  |
| 36 | `qq_mixtas` | numeric | NULL |  |
| 37 | `qq_hembras` | numeric | NULL |  |
| 38 | `qq_machos` | numeric | NULL |  |

---

## `seguimiento_diario_levante_reproductoras`

- **Entidad:** `SeguimientoDiario`
- **PK:** `id`
- **FK salientes:** `lote_id_int` → `lotes.lote_id`
- **Referenciada por:** —

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `id` | bigint | NOT NULL | PK |
| 2 | `tipo_seguimiento` | varchar | NOT NULL |  |
| 3 | `lote_id` | varchar | NOT NULL |  |
| 4 | `reproductora_id` | varchar | NULL |  |
| 5 | `fecha` | timestamptz | NOT NULL |  |
| 6 | `mortalidad_hembras` | int | NULL |  |
| 7 | `mortalidad_machos` | int | NULL |  |
| 8 | `sel_h` | int | NULL |  |
| 9 | `sel_m` | int | NULL |  |
| 10 | `error_sexaje_hembras` | int | NULL |  |
| 11 | `error_sexaje_machos` | int | NULL |  |
| 12 | `consumo_kg_hembras` | numeric | NULL |  |
| 13 | `consumo_kg_machos` | numeric | NULL |  |
| 14 | `tipo_alimento` | varchar | NULL |  |
| 15 | `observaciones` | text | NULL |  |
| 16 | `ciclo` | varchar | NULL |  |
| 17 | `peso_prom_hembras` | float8 | NULL |  |
| 18 | `peso_prom_machos` | float8 | NULL |  |
| 19 | `uniformidad_hembras` | float8 | NULL |  |
| 20 | `uniformidad_machos` | float8 | NULL |  |
| 21 | `cv_hembras` | float8 | NULL |  |
| 22 | `cv_machos` | float8 | NULL |  |
| 23 | `consumo_agua_diario` | float8 | NULL |  |
| 24 | `consumo_agua_ph` | float8 | NULL |  |
| 25 | `consumo_agua_orp` | float8 | NULL |  |
| 26 | `consumo_agua_temperatura` | float8 | NULL |  |
| 27 | `metadata` | jsonb | NULL |  |
| 28 | `items_adicionales` | jsonb | NULL |  |
| 29 | `peso_inicial` | numeric | NULL |  |
| 30 | `peso_final` | numeric | NULL |  |
| 31 | `kcal_al_h` | float8 | NULL |  |
| 32 | `prot_al_h` | float8 | NULL |  |
| 33 | `kcal_ave_h` | float8 | NULL |  |
| 34 | `prot_ave_h` | float8 | NULL |  |
| 35 | `huevo_tot` | int | NULL |  |
| 36 | `huevo_inc` | int | NULL |  |
| 37 | `huevo_limpio` | int | NULL |  |
| 38 | `huevo_tratado` | int | NULL |  |
| 39 | `huevo_sucio` | int | NULL |  |
| 40 | `huevo_deforme` | int | NULL |  |
| 41 | `huevo_blanco` | int | NULL |  |
| 42 | `huevo_doble_yema` | int | NULL |  |
| 43 | `huevo_piso` | int | NULL |  |
| 44 | `huevo_pequeno` | int | NULL |  |
| 45 | `huevo_roto` | int | NULL |  |
| 46 | `huevo_desecho` | int | NULL |  |
| 47 | `huevo_otro` | int | NULL |  |
| 48 | `peso_huevo` | float8 | NULL |  |
| 49 | `etapa` | int | NULL |  |
| 50 | `peso_h` | numeric | NULL |  |
| 51 | `peso_m` | numeric | NULL |  |
| 52 | `uniformidad` | numeric | NULL |  |
| 53 | `coeficiente_variacion` | numeric | NULL |  |
| 54 | `observaciones_pesaje` | text | NULL |  |
| 55 | `created_by_user_id` | varchar | NULL |  |
| 56 | `created_at` | timestamptz | NOT NULL |  |
| 57 | `updated_at` | timestamptz | NULL |  |
| 58 | `lote_id_int` | int | NULL | FK |
| 59 | `lote_postura_levante_id` | int | NULL |  |
| 60 | `lote_postura_produccion_id` | int | NULL |  |
| 61 | `traslado_aves_entrante` | int | NULL |  |
| 62 | `traslado_aves_salida` | int | NULL |  |
| 63 | `venta_aves_cantidad` | int | NULL |  |
| 64 | `venta_aves_motivo` | text | NULL |  |
| 65 | `es_traslado` | bool | NOT NULL |  |
| 66 | `traslado_lote_contraparte_id` | int | NULL |  |
| 67 | `traslado_granja_contraparte_id` | int | NULL |  |
| 68 | `traslado_direccion` | varchar | NULL |  |
| 69 | `traslado_ingreso_hembras` | int | NOT NULL |  |
| 70 | `traslado_ingreso_machos` | int | NOT NULL |  |
| 71 | `traslado_salida_hembras` | int | NOT NULL |  |
| 72 | `traslado_salida_machos` | int | NOT NULL |  |
| 73 | `updated_by_user_id` | varchar | NULL |  |

---

## `seguimiento_diario_lote_reproductora_aves_engorde`

- **Entidad:** `SeguimientoDiarioLoteReproductoraAvesEngorde`
- **PK:** `id`
- **FK salientes:** `lote_reproductora_ave_engorde_id` → `lote_reproductora_ave_engorde.id`
- **Referenciada por:** —

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `id` | bigint | NOT NULL | PK |
| 2 | `lote_reproductora_ave_engorde_id` | int | NOT NULL | FK |
| 3 | `fecha` | timestamptz | NOT NULL |  |
| 4 | `mortalidad_hembras` | int | NULL |  |
| 5 | `mortalidad_machos` | int | NULL |  |
| 6 | `sel_h` | int | NULL |  |
| 7 | `sel_m` | int | NULL |  |
| 8 | `error_sexaje_hembras` | int | NULL |  |
| 9 | `error_sexaje_machos` | int | NULL |  |
| 10 | `consumo_kg_hembras` | numeric | NULL |  |
| 11 | `consumo_kg_machos` | numeric | NULL |  |
| 12 | `tipo_alimento` | varchar | NULL |  |
| 13 | `observaciones` | text | NULL |  |
| 14 | `ciclo` | varchar | NULL |  |
| 15 | `peso_prom_hembras` | float8 | NULL |  |
| 16 | `peso_prom_machos` | float8 | NULL |  |
| 17 | `uniformidad_hembras` | float8 | NULL |  |
| 18 | `uniformidad_machos` | float8 | NULL |  |
| 19 | `cv_hembras` | float8 | NULL |  |
| 20 | `cv_machos` | float8 | NULL |  |
| 21 | `consumo_agua_diario` | float8 | NULL |  |
| 22 | `consumo_agua_ph` | float8 | NULL |  |
| 23 | `consumo_agua_orp` | float8 | NULL |  |
| 24 | `consumo_agua_temperatura` | float8 | NULL |  |
| 25 | `metadata` | jsonb | NULL |  |
| 26 | `items_adicionales` | jsonb | NULL |  |
| 27 | `kcal_al_h` | float8 | NULL |  |
| 28 | `prot_al_h` | float8 | NULL |  |
| 29 | `kcal_ave_h` | float8 | NULL |  |
| 30 | `prot_ave_h` | float8 | NULL |  |
| 31 | `created_by_user_id` | varchar | NULL |  |
| 32 | `created_at` | timestamptz | NOT NULL |  |
| 33 | `updated_at` | timestamptz | NULL |  |
| 34 | `qq_mixtas` | numeric | NULL |  |
| 35 | `qq_hembras` | numeric | NULL |  |
| 36 | `qq_machos` | numeric | NULL |  |

---

## `seguimiento_diario_produccion_reproductoras`

- **Entidad:** `SeguimientoProduccion`
- **PK:** `id`
- **FK salientes:** `lote_produccion_id` → `produccion_lotes.id`
- **Referenciada por:** —

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `id` | int | NOT NULL | PK |
| 2 | `lote_id` | int | NOT NULL |  |
| 3 | `fecha_registro` | timestamptz | NOT NULL |  |
| 4 | `mortalidad_hembras` | int | NOT NULL |  |
| 5 | `mortalidad_machos` | int | NOT NULL |  |
| 6 | `sel_h` | int | NOT NULL |  |
| 7 | `cons_kg_h` | float8 | NOT NULL |  |
| 8 | `cons_kg_m` | float8 | NOT NULL |  |
| 9 | `huevo_tot` | int | NOT NULL |  |
| 10 | `huevo_inc` | int | NOT NULL |  |
| 11 | `tipo_alimento` | text | NOT NULL |  |
| 12 | `observaciones` | text | NULL |  |
| 13 | `peso_huevo` | float8 | NULL |  |
| 14 | `etapa` | int | NOT NULL |  |
| 15 | `lote_produccion_id` | int | NULL | FK |
| 16 | `huevo_limpio` | int | NOT NULL |  |
| 17 | `huevo_tratado` | int | NOT NULL |  |
| 18 | `huevo_sucio` | int | NOT NULL |  |
| 19 | `huevo_deforme` | int | NOT NULL |  |
| 20 | `huevo_blanco` | int | NOT NULL |  |
| 21 | `huevo_doble_yema` | int | NOT NULL |  |
| 22 | `huevo_piso` | int | NOT NULL |  |
| 23 | `huevo_pequeno` | int | NOT NULL |  |
| 24 | `huevo_roto` | int | NOT NULL |  |
| 25 | `huevo_desecho` | int | NOT NULL |  |
| 26 | `huevo_otro` | int | NOT NULL |  |
| 27 | `peso_h` | numeric | NULL |  |
| 28 | `peso_m` | numeric | NULL |  |
| 29 | `uniformidad` | numeric | NULL |  |
| 30 | `coeficiente_variacion` | numeric | NULL |  |
| 31 | `observaciones_pesaje` | text | NULL |  |
| 32 | `metadata` | jsonb | NULL |  |
| 33 | `sel_m` | int | NOT NULL |  |
| 34 | `consumo_agua_diario` | float8 | NULL |  |
| 35 | `consumo_agua_ph` | float8 | NULL |  |
| 36 | `consumo_agua_orp` | float8 | NULL |  |
| 37 | `consumo_agua_temperatura` | float8 | NULL |  |
| 38 | `lote_postura_produccion_id` | int | NULL |  |

---

## `seguimiento_lote_levante`

- **Entidad:** `SeguimientoLoteLevante`
- **PK:** `id`
- **FK salientes:** —
- **Referenciada por:** —

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `id` | int | NOT NULL | PK |
| 2 | `fecha_registro` | timestamptz | NOT NULL |  |
| 3 | `mortalidad_hembras` | int | NOT NULL |  |
| 4 | `mortalidad_machos` | int | NOT NULL |  |
| 5 | `sel_h` | int | NOT NULL |  |
| 6 | `sel_m` | int | NOT NULL |  |
| 7 | `error_sexaje_hembras` | int | NOT NULL |  |
| 8 | `error_sexaje_machos` | int | NOT NULL |  |
| 9 | `consumo_kg_hembras` | float8 | NOT NULL |  |
| 10 | `tipo_alimento` | varchar | NOT NULL |  |
| 11 | `observaciones` | varchar | NULL |  |
| 12 | `kcal_al_h` | float8 | NULL |  |
| 13 | `prot_al_h` | float8 | NULL |  |
| 14 | `kcal_ave_h` | float8 | NULL |  |
| 15 | `prot_ave_h` | float8 | NULL |  |
| 16 | `ciclo` | varchar | NOT NULL |  |
| 17 | `consumo_kg_machos` | float8 | NULL |  |
| 18 | `cv_h` | float8 | NULL |  |
| 19 | `cv_m` | float8 | NULL |  |
| 20 | `peso_prom_h` | float8 | NULL |  |
| 21 | `peso_prom_m` | float8 | NULL |  |
| 22 | `uniformidad_h` | float8 | NULL |  |
| 23 | `uniformidad_m` | float8 | NULL |  |
| 24 | `lote_id` | int | NULL |  |
| 25 | `metadata` | jsonb | NULL |  |
| 26 | `consumo_agua_ph` | float8 | NULL |  |
| 27 | `consumo_agua_orp` | float8 | NULL |  |
| 28 | `consumo_agua_temperatura` | float8 | NULL |  |
| 29 | `medicamento_nombre` | varchar | NULL |  |
| 30 | `medicamento_dosis` | varchar | NULL |  |
| 31 | `medicamento_fecha` | timestamp | NULL |  |
| 32 | `consumo_agua_diario` | float8 | NULL |  |
| 33 | `items_adicionales` | jsonb | NULL |  |
| 34 | `traslado_hembras` | int | NULL |  |
| 35 | `traslado_machos` | int | NULL |  |
| 36 | `lote_destino_id` | int | NULL |  |
| 37 | `granja_destino_id` | int | NULL |  |
| 38 | `fecha_traslado` | date | NULL |  |
| 39 | `traslado_observaciones` | varchar | NULL |  |
| 40 | `qq_mixtas` | numeric | NULL |  |
| 41 | `qq_hembras` | numeric | NULL |  |
| 42 | `qq_machos` | numeric | NULL |  |

---

## `traslado_huevos`

- **Entidad:** `TrasladoHuevos`
- **PK:** `id`
- **FK salientes:** `lote_postura_produccion_id` → `lote_postura_produccion.lote_postura_produccion_id`
- **Referenciada por:** —

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `id` | int | NOT NULL | PK |
| 2 | `numero_traslado` | varchar | NOT NULL |  |
| 3 | `fecha_traslado` | timestamp | NOT NULL |  |
| 4 | `tipo_operacion` | varchar | NOT NULL |  |
| 5 | `lote_id` | varchar | NOT NULL |  |
| 6 | `granja_origen_id` | int | NOT NULL |  |
| 7 | `granja_destino_id` | int | NULL |  |
| 8 | `lote_destino_id` | varchar | NULL |  |
| 9 | `tipo_destino` | varchar | NULL |  |
| 10 | `motivo` | varchar | NULL |  |
| 11 | `descripcion` | text | NULL |  |
| 12 | `cantidad_limpio` | int | NOT NULL |  |
| 13 | `cantidad_tratado` | int | NOT NULL |  |
| 14 | `cantidad_sucio` | int | NOT NULL |  |
| 15 | `cantidad_deforme` | int | NOT NULL |  |
| 16 | `cantidad_blanco` | int | NOT NULL |  |
| 17 | `cantidad_doble_yema` | int | NOT NULL |  |
| 18 | `cantidad_piso` | int | NOT NULL |  |
| 19 | `cantidad_pequeno` | int | NOT NULL |  |
| 20 | `cantidad_roto` | int | NOT NULL |  |
| 21 | `cantidad_desecho` | int | NOT NULL |  |
| 22 | `cantidad_otro` | int | NOT NULL |  |
| 23 | `estado` | varchar | NOT NULL |  |
| 24 | `usuario_traslado_id` | int | NOT NULL |  |
| 25 | `usuario_nombre` | varchar | NULL |  |
| 26 | `fecha_procesamiento` | timestamp | NULL |  |
| 27 | `fecha_cancelacion` | timestamp | NULL |  |
| 28 | `observaciones` | text | NULL |  |
| 29 | `created_at` | timestamptz | NOT NULL |  |
| 30 | `updated_at` | timestamptz | NULL |  |
| 31 | `deleted_at` | timestamptz | NULL |  |
| 32 | `company_id` | int | NOT NULL |  |
| 33 | `created_by_user_id` | int | NOT NULL |  |
| 34 | `updated_by_user_id` | int | NULL |  |
| 35 | `lote_postura_produccion_id` | int | NULL | FK |

---

## `user_companies`

- **Entidad:** `UserCompany`
- **PK:** `user_id`, `company_id`, `pais_id`
- **FK salientes:** `company_id` → `companies.id`, `pais_id` → `paises.pais_id`, `user_id` → `users.id`
- **Referenciada por:** —

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `user_id` | uuid | NOT NULL | PK FK |
| 2 | `company_id` | int | NOT NULL | PK FK |
| 3 | `is_default` | bool | NOT NULL |  |
| 4 | `pais_id` | int | NOT NULL | PK FK |

---

## `user_farms`

- **Entidad:** `UserFarm`
- **PK:** `user_id`, `farm_id`
- **FK salientes:** `farm_id` → `farms.id`, `user_id` → `users.id`
- **Referenciada por:** —

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `user_id` | uuid | NOT NULL | PK FK |
| 2 | `farm_id` | int | NOT NULL | PK FK |
| 3 | `is_admin` | bool | NOT NULL |  |
| 4 | `is_default` | bool | NOT NULL |  |
| 5 | `created_at` | timestamptz | NOT NULL |  |
| 6 | `created_by_user_id` | uuid | NOT NULL |  |

---

## `user_logins`

- **Entidad:** `UserLogin`
- **PK:** `user_id`, `login_id`
- **FK salientes:** `login_id` → `logins.id`, `user_id` → `users.id`
- **Referenciada por:** —

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `user_id` | uuid | NOT NULL | PK FK |
| 2 | `login_id` | uuid | NOT NULL | PK FK |
| 3 | `is_locked_by_admin` | bool | NOT NULL |  |
| 4 | `lock_reason` | varchar | NULL |  |

---

## `user_paises`

- **Entidad:** `⚠️ sin entidad/config explícita`
- **PK:** `user_id`, `pais_id`
- **FK salientes:** `pais_id` → `paises.pais_id`, `user_id` → `users.id`
- **Referenciada por:** —

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `user_id` | uuid | NOT NULL | PK FK |
| 2 | `pais_id` | int | NOT NULL | PK FK |

---

## `user_roles`

- **Entidad:** `UserRole`
- **PK:** `user_id`, `role_id`
- **FK salientes:** `company_id` → `companies.id`, `role_id` → `roles.id`, `user_id` → `users.id`
- **Referenciada por:** —

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `user_id` | uuid | NOT NULL | PK FK |
| 2 | `role_id` | int | NOT NULL | PK FK |
| 3 | `company_id` | int | NOT NULL | FK |

---

## `users`

- **Entidad:** `User`
- **PK:** `id`
- **FK salientes:** —
- **Referenciada por:** `mapa`, `mapa_ejecucion`, `user_companies`, `user_farms`, `user_logins`, `user_paises`, `user_roles`

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `id` | uuid | NOT NULL | PK |
| 2 | `sur_name` | varchar | NOT NULL |  |
| 3 | `first_name` | varchar | NOT NULL |  |
| 4 | `cedula` | varchar | NOT NULL |  |
| 5 | `telefono` | varchar | NOT NULL |  |
| 6 | `ubicacion` | varchar | NOT NULL |  |
| 7 | `is_active` | bool | NOT NULL |  |
| 8 | `is_locked` | bool | NOT NULL |  |
| 9 | `locked_at` | timestamptz | NULL |  |
| 10 | `failed_attempts` | int | NOT NULL |  |
| 11 | `created_at` | timestamptz | NOT NULL |  |
| 12 | `last_login_at` | timestamptz | NULL |  |
| 13 | `updated_at` | timestamptz | NOT NULL |  |
| 14 | `zona` | varchar | NULL |  |

---

## `vw_indicadores_diarios_engorde`

- **Entidad:** `🔎 VISTA (sin entidad — ver Parte B §4)`
- **PK:** —
- **FK salientes:** —
- **Referenciada por:** —

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `company_id` | int | NULL |  |
| 2 | `empresa_nombre` | varchar | NULL |  |
| 3 | `lote_ave_engorde_id` | int | NULL |  |
| 4 | `lote_nombre` | varchar | NULL |  |
| 5 | `granja_id` | int | NULL |  |
| 6 | `granja_nombre` | varchar | NULL |  |
| 7 | `galpon_id` | varchar | NULL |  |
| 8 | `galpon_nombre` | varchar | NULL |  |
| 9 | `nucleo_id` | varchar | NULL |  |
| 10 | `nucleo_nombre` | varchar | NULL |  |
| 11 | `raza` | text | NULL |  |
| 12 | `ano_tabla_genetica` | int | NULL |  |
| 13 | `guia_genetica_ecuador_header_id` | int | NULL |  |
| 14 | `fecha_ymd` | text | NULL |  |
| 15 | `fecha_registro` | date | NULL |  |
| 16 | `dia_vida` | int | NULL |  |
| 17 | `aves_iniciales` | bigint | NULL |  |
| 18 | `aves_inicio_dia` | bigint | NULL |  |
| 19 | `aves_fin_dia` | bigint | NULL |  |
| 20 | `peso_inicial_mixto_g` | numeric | NULL |  |
| 21 | `peso_real_g` | numeric | NULL |  |
| 22 | `peso_tabla_g` | numeric | NULL |  |
| 23 | `ganancia_diaria_real_g` | numeric | NULL |  |
| 24 | `ganancia_diaria_tabla_g` | numeric | NULL |  |
| 25 | `consumo_diario_real_g` | numeric | NULL |  |
| 26 | `consumo_diario_tabla_g` | numeric | NULL |  |
| 27 | `alimento_acum_real_g` | numeric | NULL |  |
| 28 | `alimento_acum_tabla_g` | numeric | NULL |  |
| 29 | `ca_real` | numeric | NULL |  |
| 30 | `ca_tabla` | numeric | NULL |  |
| 31 | `mort_sel_real_pct` | numeric | NULL |  |
| 32 | `mort_sel_tabla_pct` | numeric | NULL |  |
| 33 | `dif_peso_vs_tabla_pct` | numeric | NULL |  |
| 34 | `mort_acum_pct` | numeric | NULL |  |

---

## `vw_liquidacion_ecuador_pollo_engorde`

- **Entidad:** `🔎 VISTA (sin entidad — ver Parte B §4)`
- **PK:** —
- **FK salientes:** —
- **Referenciada por:** —

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `company_id` | int | NULL |  |
| 2 | `empresa_nombre` | varchar | NULL |  |
| 3 | `granja_id` | int | NULL |  |
| 4 | `granja_nombre` | varchar | NULL |  |
| 5 | `nucleo_id` | varchar | NULL |  |
| 6 | `nucleo_nombre` | varchar | NULL |  |
| 7 | `galpon_id` | varchar | NULL |  |
| 8 | `galpon_nombre` | varchar | NULL |  |
| 9 | `lote_ave_engorde_id` | int | NULL |  |
| 10 | `lote_nombre` | varchar | NULL |  |
| 11 | `fecha_encaset` | date | NULL |  |
| 12 | `estado_operativo_lote` | varchar | NULL |  |
| 13 | `liquidado_at` | timestamptz | NULL |  |
| 14 | `cantidad_lotes_reproductores` | int | NULL |  |
| 15 | `aves_encasetadas` | bigint | NULL |  |
| 16 | `aves_sacrificadas` | bigint | NULL |  |
| 17 | `mortalidad` | bigint | NULL |  |
| 18 | `mortalidad_porcentaje` | numeric | NULL |  |
| 19 | `supervivencia_porcentaje` | numeric | NULL |  |
| 20 | `consumo_total_alimento_kg` | numeric | NULL |  |
| 21 | `consumo_ave_gramos` | numeric | NULL |  |
| 22 | `kg_carne_pollos` | numeric | NULL |  |
| 23 | `peso_promedio_kilos` | numeric | NULL |  |
| 24 | `conversion` | numeric | NULL |  |
| 25 | `conversion_ajustada2700` | numeric | NULL |  |
| 26 | `peso_ajuste_variable` | numeric | NULL |  |
| 27 | `divisor_ajuste_variable` | numeric | NULL |  |
| 28 | `edad_promedio` | numeric | NULL |  |
| 29 | `metros_cuadrados` | numeric | NULL |  |
| 30 | `aves_por_metro_cuadrado` | numeric | NULL |  |
| 31 | `kg_por_metro_cuadrado` | numeric | NULL |  |
| 32 | `eficiencia_americana` | numeric | NULL |  |
| 33 | `eficiencia_europea` | numeric | NULL |  |
| 34 | `indice_productividad` | numeric | NULL |  |
| 35 | `ganancia_dia` | numeric | NULL |  |
| 36 | `aves_trasladadas_rep` | bigint | NULL |  |
| 37 | `aves_actuales` | bigint | NULL |  |
| 38 | `tiene_aves` | bool | NULL |  |
| 39 | `lote_cerrado_logico` | bool | NULL |  |
| 40 | `cerrado_por_aves_cero` | bool | NULL |  |
| 41 | `cerrado_por_reproductores_vendidos` | bool | NULL |  |
| 42 | `fecha_cierre_ultimo_despacho` | timestamptz | NULL |  |
| 43 | `fecha_cierre_efectiva` | timestamptz | NULL |  |

---

## `vw_seguimiento_pollo_engorde`

- **Entidad:** `🔎 VISTA (sin entidad — ver Parte B §4)`
- **PK:** —
- **FK salientes:** —
- **Referenciada por:** —

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `seguimiento_id` | bigint | NULL |  |
| 2 | `lote_ave_engorde_id` | int | NULL |  |
| 3 | `lote_nombre` | varchar | NULL |  |
| 4 | `company_id` | int | NULL |  |
| 5 | `company_nombre` | varchar | NULL |  |
| 6 | `granja_id` | int | NULL |  |
| 7 | `granja_nombre` | varchar | NULL |  |
| 8 | `galpon_id` | varchar | NULL |  |
| 9 | `galpon_nombre` | varchar | NULL |  |
| 10 | `nucleo_id` | varchar | NULL |  |
| 11 | `nucleo_nombre` | varchar | NULL |  |
| 12 | `fecha_dmy` | text | NULL |  |
| 13 | `fecha_registro` | date | NULL |  |
| 14 | `semana` | int | NULL |  |
| 15 | `edad_dias_vida` | int | NULL |  |
| 16 | `dia_calendario_corto` | text | NULL |  |
| 17 | `mortalidad_hembras` | int | NULL |  |
| 18 | `mortalidad_machos` | int | NULL |  |
| 19 | `seleccion_hembras` | int | NULL |  |
| 20 | `seleccion_machos` | int | NULL |  |
| 21 | `total_mort_mas_sel_dia` | int | NULL |  |
| 22 | `error_sexaje_hembras` | int | NULL |  |
| 23 | `error_sexaje_machos` | int | NULL |  |
| 24 | `despacho_hembras_hist` | bigint | NULL |  |
| 25 | `despacho_machos_hist` | bigint | NULL |  |
| 26 | `despacho_mixtas_hist` | bigint | NULL |  |
| 27 | `saldo_alimento_kg_bd` | numeric | NULL |  |
| 28 | `saldo_alimento_kg_calculado` | numeric | NULL |  |
| 29 | `saldo_aves_vivas` | bigint | NULL |  |
| 30 | `saldo_aves_vivas_hembras` | bigint | NULL |  |
| 31 | `saldo_aves_vivas_machos` | bigint | NULL |  |
| 32 | `tipo_alimento` | varchar | NULL |  |
| 33 | `tipo_alimento_corto` | text | NULL |  |
| 34 | `ingreso_alimento_texto_hist` | text | NULL |  |
| 35 | `traslado_texto_hist` | text | NULL |  |
| 36 | `documento_hist` | text | NULL |  |
| 37 | `metadata_ingreso_alimento` | text | NULL |  |
| 38 | `metadata_traslado` | text | NULL |  |
| 39 | `metadata_documento` | text | NULL |  |
| 40 | `consumo_kg_hembras` | numeric | NULL |  |
| 41 | `consumo_kg_machos` | numeric | NULL |  |
| 42 | `consumo_real_dia_kg` | numeric | NULL |  |
| 43 | `consumo_acumulado_kg` | numeric | NULL |  |
| 44 | `consumo_bodega_kg` | numeric | NULL |  |
| 45 | `consumo_agua_diario` | numeric | NULL |  |
| 46 | `pct_perdidas_dia` | numeric | NULL |  |
| 47 | `peso_prom_hembras` | numeric | NULL |  |
| 48 | `peso_prom_machos` | numeric | NULL |  |
| 49 | `observaciones` | text | NULL |  |
| 50 | `metadata` | jsonb | NULL |  |
| 51 | `items_adicionales` | jsonb | NULL |  |

---

## `zonas`

- **Entidad:** `Zona`
- **PK:** `zona_id`
- **FK salientes:** `company_id` → `companies.id`
- **Referenciada por:** —

| # | Columna | Tipo | Null | Clave |
|---|---------|------|------|-------|
| 1 | `zona_id` | int | NOT NULL | PK |
| 2 | `zona_cia` | int | NOT NULL |  |
| 3 | `zona_nombre` | text | NOT NULL |  |
| 4 | `zona_estado` | text | NOT NULL |  |
| 5 | `company_id` | int | NOT NULL | FK |

---
