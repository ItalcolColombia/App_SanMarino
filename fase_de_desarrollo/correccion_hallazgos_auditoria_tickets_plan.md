# Corrección de los 12 hallazgos de la auditoría de tickets cerrados (31-ago-2026)

Continúa [`validar_seguimiento_doble_descuento_plan.md`](validar_seguimiento_doble_descuento_plan.md),
que cerró el primero (el doble descuento, commit `9a7b3d8`).

## 0. De dónde salen

Auditoría adversarial de los 13 casos que se habían dado por resueltos: un agente por caso con la
consigna de **probar que el síntoma vuelve a ocurrir**, y refutación independiente de cada hallazgo
severo. **12 de 13 casos siguen fallando por algún lado.** El patrón que se repite en 6 de los 12:
**el fix se aplicó en un camino y su gemelo quedó atrás** — engorde arreglado / levante no, aves
arreglado / lote no, fn arreglada / vista no.

⚠️ Ninguno se corrige con `if (empresa == X)`. Los dos que dependen de la empresa ya tienen su flag
tipado (`requiere_validacion_seguimiento_diario`, `maneja_alimento_por_galpon`); los otros diez son
correctitud o presentación, iguales para las 6 empresas.

## 1. Los 12, por severidad real para la operación

| # | Sev | Qué | Datos |
|---|---|---|---|
| 1 | 🔴 crítico | **TK-164** · Borrar un seguimiento de reproductora YA CONFIRMADO no devuelve el alimento; la UI empuja a hacer justo eso | **SÍ** 952,560 kg |
| 2 | 🔴 crítico | **TK-166** · Con el flag ON el backend **no valida stock** en ningún seguimiento: el único freno es el front | no |
| 3 | 🟠 alto | **TK-163** · `RegistrarIngresoAsync` no valida duplicados; 3 pares vivos | **SÍ** |
| 4 | 🟠 alto | **TK-012/A** · El traslado por cierre de levante se sella con `new Date()` del navegador | no |
| 5 | 🟠 alto | **TK-014** · El modal de levante lee la fecha con un regex **anclado** y resta un día | no |
| 6 | 🟠 alto | **TK-012/C** · El modal de movimientos-aves manda **medianoche UTC** | opcional |
| 7 | 🟠 alto | **TK-020/A** · La carga masiva descarta el **día completo** ante una Advertencia y reporta «Procesado» | no |
| 8 | 🟠 alto | **TK-020/B** · S369 sigue sin poder cerrarse: el remedio indicado está bloqueado | operativo |
| 9 | 🟡 medio-alto | **TK-176** · La tarjeta de Lote Reproductora Engorde muestra el SALDO bajo el rótulo «(inicial)» | no |
| 10 | 🟡 medio | **TK-177** · El gate del ajuste mide el TOTAL y el clamp es por SEXO ⇒ borra hembras vivas en silencio | no |
| 11 | 🟡 medio | **TK-012/B** · Trasladar/mover un LOTE no tiene campo de fecha en ningún lado | no (tabla vacía) |
| 12 | 🟢 medio-bajo | **TK-015** · La vista Power BI `vw_seguimiento_pollo_engorde` nunca recibió el corte v14 | no |

## 2. Orden de ejecución

Por **valor entregado sobre esfuerzo**, no por número:

- **Tanda A — dos cambios chicos de alto impacto**: #4 (una línea) y #7 (dos líneas). Los dos son
  pérdida silenciosa de datos del usuario y se arreglan sin tocar arquitectura.
- **Tanda B — el crítico con datos perdidos**: #1, guarda + vía de corrección + remediación.
- **Tanda C — el crítico latente**: #2.
- **Tanda D — fechas y presentación**: #5, #6, #9, #10.
- **Tanda E — lo más grande**: #3, #11, #12.
- **#8** no lleva código: es reabrir el caso con la instrucción correcta.

Cada tanda se valida y commitea por separado, para que un problema en la última no bloquee lo demás.

## 3. Reglas que aplican a todas

- **Refactor ≠ cambio de comportamiento**: con el flag OFF, las empresas que hoy no lo tienen deben
  quedar **byte a byte** iguales. Donde el cambio es por empresa, el gate es el flag que ya existe.
- **Cálculo puro con tests xUnit** para toda decisión nueva (`Application/Calculos/`), y el service
  solo resuelve el flag y delega.
- **Datos**: nada se toca sin simular en transacción y revertir. La remediación va por migración
  idempotente, y **no** se anulan filas «para cuadrar» sin entender qué representan.
- El front usa las primitivas obligatorias (`ConfirmDialogService`, `ToastService`) y todo componente
  nuevo lleva `changeDetection` explícito.
