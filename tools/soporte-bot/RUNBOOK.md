# SoporteBot — Runbook del loop (procedimiento por iteración)

Este es el "cerebro" que ejecuta el loop en cada vuelta (lo corre Claude, local con `/loop` o el cron `/schedule`). El [CLI](soporte-bot.sh) es solo transporte; la lógica/decisiones viven acá. Guardrails de deploy: ver §2 del plan `fase_de_desarrollo/soporte_bot_loop_tickets_plan.md`.

## Config previa
```bash
export SOPORTE_BOT_BASE_URL="http://localhost:5002"   # prod: URL pública
export SOPORTE_BOT_PAT="$(cat .../soporte_bot_pat.txt)"
```

## Iteración (una vuelta)

1. **Listar lo actionable.** `soporte-bot.sh list` → tickets asignados a mí. Actionable = estado `ABIERTO` o `EN_ANALISIS` o `EN_IMPLEMENTACION`. Ignorar `SOLUCIONADO`/`CERRADO`/`SUSPENDIDO`/`TRANSFERIDO`. Priorizar: ABIERTO > EN_ANALISIS, más antiguo primero.
2. **Armar el packet.** `loop.sh prepare <id> <workdir>` → `tomar` (ABIERTO→EN_ANALISIS) + baja `ticket.json`, imágenes y documentos (ARCHIVO) a `<workdir>`, y `links.txt` con los LINK.
3. **Clasificar** (por `tipo` + contenido + `paisId`):
   - **DUDAS / SOPORTE de uso-configuración** (no requiere cambio de código): respondé en una nota pública con la solución concreta y cerrá con `estado SOLUCIONADO --solucion "…"`.
   - **SOPORTE con bug de código / DESARROLLO / REQUERIMIENTO**: investigá en el repo (grep/read), reproducí en local si aplica, y seguí a §4 (fix + PR). Dejá `EN_IMPLEMENTACION`.
   - **Insuficiente info**: nota pública pidiendo el dato puntual; dejar en `EN_ANALISIS`.
4. **Fix + PR** (solo bug/desarrollo): rama → fix → `dotnet build` + `dotnet test` + `yarn build` → PR con link al ticket → nota con el PR → `estado EN_IMPLEMENTACION`. **NO** auto-deploy (eso es Fase 3, con guardrails).

## Contexto multipaís (clave para el diagnóstico)
`paisId` del ticket enmarca dónde mirar:
- **Colombia (1):** inventario **modelo B a nivel granja** (sin galpón); postura vs `guia_genetica_sanmarino_colombia`.
- **Ecuador (2):** inventario modelo B con núcleo/galpón; vistas Power BI sobre `seguimiento_diario_aves_engorde`; liquidador engorde (descuadres conocidos: merma NULL, peso_neto vs báscula).
- **Panamá:** informe semanal engorde.
Usar la memoria del proyecto y el código actual como fuente de verdad, no planes viejos.

## Guardrails (NO negociables)
- **Migración EF en el fix → compuerta humana siempre** (nunca auto-deploy; causa raíz de crashes de prod).
- **Nunca `dotnet ef database update` contra RDS desde el bot.** Deploy solo por el pipeline.
- **Cambios en cálculos de liquidación / inventario / indicadores → compuerta humana.**
- **Deploy que no estabiliza → auto-rollback + ticket NO SOLUCIONADO + alerta.**
- **Refactor ≠ cambio de comportamiento.** Preservar UI, contratos y aritmética.
- Registrar SIEMPRE una nota de bitácora con lo actuado (trazabilidad).

## Notas
- El correo de cierre lo dispara el módulo automáticamente al `SOLUCIONADO`/cierre. En LOCAL debe estar `Email:Queue:Enabled=false` para no mandar correos reales.
- El bot nunca gestiona un ticket donde es el creador (`soyCreador=true`).
