# Tracker — Fix consumo inventario Colombia multi-empresa (400 "no tiene equivalente")

**Plan:** [fase_de_desarrollo/fix_consumo_inventario_colombia_multiempresa_plan.md](fase_de_desarrollo/fix_consumo_inventario_colombia_multiempresa_plan.md)

## Diagnóstico
- [x] Reproducir causa raíz: ítem 208 = company 4 (Demo) vs hardcode `CompanyColombia = 1` en `ColombiaInventarioConsumoService` (camino 2 rechaza)
- [x] Confirmar riesgo de colisión de ids catálogo(61–366) ↔ inventario(1–355) por aplanado de ids en el parser

## Application (cálculo puro + contratos)
- [x] Crear `Calculos/ItemConsumoKey.cs` (record struct Id + EsItemInventario)
- [x] `MetadataEngordeCalculos.ParseMetadataItemsToKgPorOrigen` (parser tipado; el plano queda intacto)
- [x] Reescribir `ColombiaInventarioIdResolutionCalculos.Resolver` con clave tipada (sin heurístico de colisión)
- [x] `IColombiaInventarioConsumoService`: firmas con `IReadOnlyDictionary<ItemConsumoKey, decimal>` + docs

## Infrastructure (servicios)
- [x] `ColombiaInventarioConsumoService`: empresa efectiva desde `farms.company_id` (fuera hardcode company 1) + resolución por camino explícito + mensajes por camino
- [x] `SeguimientoLoteLevanteService.Crud.cs`: ramas Colombia create/update/delete con parser tipado
- [x] `ProduccionService.cs`: `AcumularItemsRequestPorOrigen` (twin tipado) + ramas Colombia create/update/delete
- [x] `SeguimientoAvesEngordeService.Crud.cs`: ramas Colombia tipadas (snapshot old tipado antes de pisar metadata)
- [x] `SeguimientoAvesEngordeEcuadorService.Crud.cs`: ramas Colombia tipadas (ídem)

## Tests
- [x] `ColombiaInventarioIdResolutionCalculosTests`: contrato tipado (incl. caso Demo 208 y colisión resuelta por tipo)
- [x] `MetadataEngordeCalculosTests`: 5 tests nuevos del parser tipado
- [x] `SeguimientoEngordeColombiaInventarioTests`: réplicas de ramas Colombia con clave tipada + caso colisión espejo↔nuevo

## Validación
- [x] `dotnet build`: 0 errores, 0 advertencias
- [x] `dotnet test`: 494 + 1 verdes (0 fallos)
- [x] Smoke local end-to-end (BD sanmarinoapplocal): POST levante demo (lote 118, ítem 208, 400 kg) → **201 Created**, stock 4000→3600 + movimiento `Consumo`; DELETE → **204**, devolución 400 kg, stock de vuelta a 4000. Backend detenido al terminar (sin huérfanos).

## Pendiente (usuario)
- [ ] Commit + deploy a prod (pipeline con gate de tests) y re-probar el consumo con el usuario demo en zootecnico.sanmarino.com.co
