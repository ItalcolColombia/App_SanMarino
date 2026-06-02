# Plan — Pestaña "Gráficas" en Seguimiento Diario Pollo Engorde (Ecuador vs Panamá)

> Fecha: 2026-06-01 · Módulo: `frontend/src/app/features/aves-engorde`
> Alcance: **100% frontend**. No requiere cambios de backend, entidades ni BD.
> Los datos ya están cargados en el componente (`tablaFilas` + `seguimientos`).

## 1. Objetivo

En el módulo de **seguimiento diario pollo engorde**, reorganizar la pestaña de gráficas:

1. Las **4 gráficas actuales** (`app-graficas-indicadores-diarios-engorde`, que comparan contra la **guía genética Ecuador**) pasan a ser **visibles solo para Ecuador**.
2. Se agregan **gráficas nuevas de PRODUCTIVIDAD** (Diaria y Semanal), calculadas **solo con los datos del seguimiento diario** (sin overlay de estándar/guía), **visibles solo para Panamá**.
3. **Sin filtros** (cliente/granja/galpón/lote): ya estamos dentro del seguimiento de un lote concreto; se grafica el lote seleccionado.

### Decisiones de negocio confirmadas con el usuario

| Tema | Decisión |
|------|----------|
| Visibilidad gráficas actuales | Solo **Ecuador** |
| Visibilidad gráficas nuevas | Solo **Panamá** |
| Estándar/guía en las nuevas | **No** — solo datos reales del seguimiento |
| Definición de **QQ** (quintal) | **Peso vivo en pie**: `QQ = saldoAvesVivas × pesoPromAve_lb / 100` (1 quintal = 100 lb; `pesoPromAve_lb = pesoPromAve_kg × 2.20462`) |

## 2. Gráficas nuevas (referencia: imágenes del usuario)

### A) Productividad DIARIA — 2 gráficas lado a lado
1. **Desempeño diario** (eje X = Edad Día):
   - Barras: **Gramos (g)** = peso promedio del ave del día (mixto) — eje Y izquierdo.
   - Línea: **Total QQ** = `saldoAves × pesoLb / 100` — eje Y derecho.
2. **Mortalidad diaria** (eje X = Edad Día):
   - Barras: **% Mortalidad**, **% Selección**, **% Mortalidad + Selección** (sobre saldo al inicio del día) — eje Y izquierdo (%).
   - Líneas: **Mortalidad Total** (acumulado de muertes), **Selección Total** (acumulado de selección) — eje Y derecho (cantidades).

### B) Productividad SEMANAL — 2 gráficas lado a lado
3. **Desempeño productivo** (eje X = Semana):
   - Barras: **GRS** (peso prom. ave g, representativo de la semana = último día con peso), **QQ** (peso vivo en pie al final de la semana).
   - Línea: **CA** (conversión alimenticia acumulada = `consumoAcumKg / pesoVivoTotalKg` al final de la semana) — eje Y derecho.
4. **Mortalidad y selección acumulada** (eje X = Semana):
   - Barras: **% Mortalidad**, **% Selección**, **% Mortalidad + Selección** de la semana.
   - Líneas: **Mortalidad Total**, **Selección Total** (acumulados hasta el fin de la semana).

> Las fórmulas de % usan el saldo de aves al **inicio** del periodo como denominador. CA usa peso vivo total (kg) y consumo acumulado (kg). Documentadas como **parametrizables**: si negocio pide otra base, se ajusta en el compute service sin tocar la UI.

## 3. Fuentes de datos (ya disponibles, sin nuevas llamadas HTTP)

`SeguimientoDiarioTablaFilaDto[]` (`tablaFilas`, precalculado por `fn_seguimiento_diario_engorde`, ya pasado a `tabs-principal-engorde`):
- `edadDia`, `semana`
- `mortalidadHembras`, `mortalidadMachos`, `selH`, `selM`, `errorSexajeHembras`, `errorSexajeMachos`, `totalMortSelDia`
- `consumoDiaKg`, `acumConsumoKg`, `saldoAves`
- `despachoHembras`, `despachoMachos`, `despachoMixtas`
- `pesoPromHembras`, `pesoPromMachos` (kg)

`selectedLote` (`LoteDto`): `avesEncasetadas` (aves iniciales, fallback de denominador).

## 4. Detección de país

Reutilizar `CountryFilterService` (`core/services/country/country-filter.service.ts`):
- `isEcuador()` (ID 2 / nombre "Ecuador")
- `isPanama()` (ID 3 / nombre "Panamá")

## 5. Archivos a crear / modificar

### Crear
1. `frontend/src/app/features/aves-engorde/services/productividad-engorde.models.ts`
   - Interfaces `ProductividadDiariaFila`, `ProductividadSemanalFila`, `ProductividadEngordeResult`.
2. `frontend/src/app/features/aves-engorde/services/productividad-engorde-compute.service.ts`
   - Servicio puro (sin HTTP). Recibe `tablaFilas` + `selectedLote`, devuelve filas diarias y semanales con los cálculos anteriores. Constante `LB_POR_KG = 2.20462`.
3. `frontend/src/app/features/aves-engorde/pages/graficas-productividad-engorde/`
   - `graficas-productividad-engorde.component.ts` (standalone, `NgChartsModule`).
   - `graficas-productividad-engorde.component.html` (toggle Diaria/Semanal + 2 gráficas por modo, grid responsivo).
   - `graficas-productividad-engorde.component.scss`.
   - Inputs: `tablaFilas`, `selectedLote`, `loading`. Toggle interno `modo: 'diaria' | 'semanal'`.

### Modificar
4. `pages/tabs-principal-engorde/tabs-principal-engorde.component.ts`
   - Inyectar `CountryFilterService`; setear `isEcuador` / `isPanama` en `ngOnInit`.
   - Importar `GraficasProductividadEngordeComponent`.
5. `pages/tabs-principal-engorde/tabs-principal-engorde.component.html`
   - Renombrar tab "📊 Gráfica" → "📊 Gráficas".
   - En el panel `grafica`:
     - `app-graficas-indicadores-diarios-engorde` con `*ngIf="isEcuador"`.
     - `app-graficas-productividad-engorde` con `*ngIf="isPanama"` (pasar `tablaFilas`, `selectedLote`, `loading`).
     - Mensaje fallback si no es ni Ecuador ni Panamá.

## 6. Validación

- `yarn build` (o `ng build`) sin errores de tipos/plantilla.
- Verificación manual con sesión Ecuador (ve actuales) y Panamá (ve nuevas) — `make down` al terminar.
- Revisar consistencia: QQ y % con un lote con datos reales (lote 78 de las imágenes si está disponible localmente).

## 7. Fuera de alcance

- No se toca backend, migraciones ni BD.
- No se agrega overlay de estándar/guía a las gráficas nuevas (decisión: solo datos reales).
- No se agregan filtros cliente/granja/galpón/lote (ya estamos en el lote).
