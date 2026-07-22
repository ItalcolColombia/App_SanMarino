# Tracker — Guía genética Panamá: Ross 308 AP 2022 (mixto)

Plan: [guia_genetica_panama_ross308ap_2022_plan.md](fase_de_desarrollo/guia_genetica_panama_ross308ap_2022_plan.md)

Decisión confirmada: **Opción A** — guía año 2022 + repuntar los 31 lotes de Panamá (2026 → 2022).

## Checklist

### Validación e investigación
- [x] Validar PDF Aviagen YP × Ross 308 AP 2022 (legítimo, columnas calzan con el módulo)
- [x] Extraer tabla Mixto (g) por coordenadas: 57 filas (día 0-56) + validación aritmética CA
- [x] Mapear módulo guía genética Ecuador (entidades, config, servicio, indicadores)
- [x] Diagnosticar BD: países (PA=3), guía actual Panamá (header 2, 2026, pesos en 0), 31 lotes 2026
- [x] Confirmar linkage indicadores (raza + anoTablaGenetica del lote + sexo mixto)
- [x] Verificar sin riesgo prod: created_by_user_id sin FK, company 5 existe, tipos ok

### Workflow
- [x] Plan (`fase_de_desarrollo/…_plan.md`)
- [x] Tracker (este archivo)

### Implementación
- [x] Generar bloque VALUES (57 filas) desde el PDF (script, no manual)
- [x] Crear migración `20260722220000_SeedGuiaGeneticaPanamaRoss308AP2022.cs` (DELETE + INSERT + UPDATE, idempotente)
- [x] Crear `…Designer.cs` (clon del snapshot vigente, solo id/clase; sin tocar ModelSnapshot)
- [x] `dotnet build` Infrastructure con .NET 10 (0 errores, 0 warnings)

### Pruebas locales (sanmarinoapplocal:5433)
- [x] Ejecutar Up() completo contra el schema real en transacción + ROLLBACK (sin persistir; backend del usuario intacto)
- [x] Verificar: 0 headers Panamá 2026; 1 header 2022 con 57 filas mixto; día 0/7/56 correctos
- [x] Verificar: 31 lotes Panamá repuntados a 2022; Ecuador (header 1) intacto (3 sexos × 57)
- [x] Verificar idempotencia (segunda corrida de Up = mismo estado, sin duplicar)
- [ ] Aplicación real local: se auto-aplica en el próximo arranque del backend (`Database:RunMigrations=true`) / deploy

### Cierre
- [x] Actualizar memoria
- [ ] Confirmación del usuario antes de deploy a prod (incluye borrado de la guía vieja + repunte de 31 lotes)
