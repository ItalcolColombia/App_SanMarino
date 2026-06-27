# Plan — Verificador / Auditoría de Liquidación Pollo Engorde (Ecuador)

Botón "Verificar liquidación" → modal: el usuario sube el Excel correcto (formato vertical etiqueta|valor del TOTAL de la corrida); el sistema corre el mismo análisis que hicimos a mano y muestra: **reconciliación** (sistema vs Excel por indicador), **hallazgos** (registros con falla) y **simulación de corrección**. v1: **diagnostica y simula, NO aplica** cambios.

## Arquitectura (grueso en BD)
```
Front (modal) --upload .xlsx--> Back endpoint
   Back: parsea .xlsx (ClosedXML) -> JSONB {clave: valor}  (NO calcula nada)
         llama fn_auditoria_liquidacion_engorde(company, granja, nucleo, codigo, excel_jsonb)
   BD (plpgsql): resuelve lotes -> agrega fn_indicadores_pollo_engorde -> reconcilia vs Excel
                 -> corre detectores -> simula corrección -> devuelve UN jsonb armado
   Back: retorna el jsonb tal cual al front
Front: solo organiza y pinta el jsonb
```

## Componente nuevo de BD: `fn_auditoria_liquidacion_engorde`
- Firma: `(p_company_id int, p_granja_id int, p_nucleo_id text, p_lote_codigo text, p_excel jsonb) RETURNS jsonb`.
- Resuelve lotes: `lote_ave_engorde` por company+granja+nucleo+`lote_nombre LIKE codigo%`, `deleted_at IS NULL`.
- Totales del sistema: agrega `fn_indicadores_pollo_engorde(lote,2.7,4.5)` por lote (definiciones autoritativas: peso=kg/sac, conv=cons/kg, consumo_ave=cons/sac, etc.).
- **Reconciliación**: por indicador → `{clave,label,unidad,sistema,excel,diferencia,difPct,cuadra,clase}`.
  - `clase='dato'` (rojo si difiere) vs `clase='definicion'` (amarillo: merma %, días engorde, edad ponderada).
- **Hallazgos** (detectores genéricos, devuelven registros exactos):
  - `MOV_SIN_PESO` (crítico/dato): despachos Venta/Despacho/Retiro activos con aves>0 y `peso_neto IS NULL` y sin bruto-tara ⇒ 0 kg. Lista registros + estimación de kg faltantes.
  - `ANULADO_ACTIVO` (alerta): `estado='Anulado'` con `deleted_at IS NULL` (se contarían).
  - `MERMA_NO_REGISTRADA` (info): el Excel trae merma>0 pero ningún lote la tiene registrada.
  - `DESPACHO_MULTILOTE` (info): camión (placa+fecha) que toca >1 lote (báscula duplica si se suma por línea).
  - `AJUSTE_ALTO` (alerta): lote con |enc-sac-mort|/enc > 1% (posibles aves no registradas).
- **Simulación**: `gap = excel.produccion - sistema.produccion`; si los registros `MOV_SIN_PESO` explican el gap, corrige producción=excel y recomputa derivados → muestra que cuadra; nota con el peso/ave implícito (confirmar tiquete físico).

## Back (.NET) — delgado
- `IndicadorEcuador/auditoria-liquidacion` (POST multipart): form { granjaId, nucleoId, loteCodigo, excel:file }.
- Servicio: `AuditoriaExcelParser` (ClosedXML) mapea etiquetas→claves canónicas (normaliza acentos/espacios), arma JSONB; llama la fn; retorna el jsonb (Content-Type json).
- Sin cálculos de negocio en el back (solo parseo + passthrough).

## Front (Angular) — solo pinta
- Botón "Verificar liquidación" en `indicador-ecuador-list` (cuando hay liquidación cargada).
- Modal `auditoria-liquidacion-modal`: input file, llama el servicio, pinta 3 secciones (reconciliación, hallazgos, simulación). Funciones puras en `funciones/`, tipos en `models/`.

## Casos de prueba
1. Corrida 2601/granja 38 con el Excel del usuario → detecta `MOV_SIN_PESO` (id 102, 3192 aves), reconcilia producción 245.129 vs 251.052 (gap 5.923), simula corregido = Excel.
2. Excel sin merma + lote sin merma → `MERMA_NO_REGISTRADA` informativo.
3. Corrida sin fallas → reconciliación todo verde, hallazgos vacíos.
4. `yarn build` y `dotnet build` sin errores.

## v2 — Aplicar corrección (gateado por permiso)
Pedido del usuario: poder aplicar la corrección sugerida (cargar el peso faltante de los despachos sin peso) desde el modal, solo para usuarios con permiso.
- **Permiso nuevo:** `liquidacion.aplicar_correccion` (seed en migración, patrón `permissions(key,description)`).
- **Función BD (escribe, auditada):** `fn_aplicar_correccion_despachos_sin_peso(company,granja,nucleo,codigo,kg_total,user_id)`:
  distribuye `kg_total` entre los despachos sin peso (proporcional a aves), escribe `peso_neto`/`peso_neto_global`,
  `updated_at`/`updated_by_user_id`. Guarda: kg>0, hay despachos sin peso, alcance válido. Devuelve resumen jsonb.
- **Back:** endpoint `POST IndicadorEcuador/auditoria-liquidacion/aplicar` (JSON: granjaId,nucleoId,loteCodigo,kgTotal).
  Chequea permiso (`_current.Permissions.Contains(...)` → Forbid). Llama la fn con company/userId del contexto.
- **Front:** botón "Aplicar corrección" en el modal, `*appHasPermission="'liquidacion.aplicar_correccion'"`,
  visible solo si hay hallazgo MOV_SIN_PESO con gap>0. Campo kg editable (default = gap sugerido). Confirmación.
  Tras aplicar, re-ejecuta la verificación (reusa el archivo cargado) para mostrar el cuadre.
- **Seguridad:** escribe en `movimiento_pollo_engorde` (dato operativo). Gateado + confirmación + auditoría.
  En local escribe a la copia; en prod solo un usuario con el permiso lo dispara.

## Fuera de alcance
- Quirks de display del front total (peso prom ponderado por kg, consumo ave /enc) → se documentan aparte.
- Otros reportes (esto es Ecuador pollo engorde).
