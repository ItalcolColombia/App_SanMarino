# Plan — El «Día 1» lo manda el primer día CON registro (reproductora + pollo engorde)

**Reporte (04-sep-2026):** granja **DOÑA MARIA**, lote reproductora `LR-0023649715` «156»
(encaset **30/08/2026**). Sus tres registros —31/08, 01/09, 02/09— se numeran **Día 2, 3 y 4**.
El usuario esperaba **Día 1, 2 y 3**: *«a la fecha jala segundo día»*. Es la **tercera vez** que
entra el mismo síntoma (27-jul `769a48c`, 31-ago `1191b39`).

---

## 1. Por qué volvió a pasar (diagnóstico, no hipótesis)

El arreglo de agosto numeró bien, pero **le pidió el dato a la fuente equivocada**: la *hora de
encasetamiento*.

```
desplazamientoPrimerDia(hora)  =  hora >= '13:00' ? 1 : 0      // fail-closed: sin hora ⇒ 0
dia mostrado                   =  edad − desplazamiento + 1
```

El corrimiento de un día solo existe **si el lote trae hora ≥ 13:00**. El ticket de Panamá del
31-ago (lote 239, hora **21:33**) traía hora, así que el fix se vio funcionar y se dio por cerrado.

**El lote de Doña María no tiene hora**, y no es una excepción — medido en la copia local de prod:

| | lotes | con hora |
|---|---:|---:|
| Lotes reproductora aves engorde | 142 | **0** |
| Lotes pollo engorde | 248 | 26 |

El campo se captura en el formulario del lote de **pollo engorde** y casi nadie lo llena. Sin hora,
`desplazamiento = 0` ⇒ el día del encaset es el «Día 1» **aunque no tenga registro**, y el primer
registro real cae en el «Día 2». O sea: **la regla del 31-ago era correcta pero solo se activaba en
el 0 % de las reproductoras.** El ticket anterior no reapareció: nunca se había cubierto este caso.

Encima quedó una contradicción interna que ya se veía en pantalla: el reporte **«Primera semana»**
([`construir-bloques-reproductora.funcion.ts`](../frontend/src/app/features/aves-engorde/funciones/construir-bloques-reproductora.funcion.ts))
numera con `edadInicial = porEdad.has(0) ? 0 : 1` —es decir, **ya usa el dato, no la hora**— así que
el mismo lote sale **Día 1** ahí y **Día 2** en la lista de seguimiento.

Y el clamp que agregó `1191b39` (`Math.min(teorico, edadMin)`) solo sabe **bajar** el corrimiento
(para no mover lotes históricos que sí capturaron la edad 0); nunca subirlo. Por eso no alcanzó.

## 2. Regla nueva (decidida con el usuario, 04-sep-2026)

> **El día 1 es el primer día CON registro del lote.** Lo manda el dato (la menor edad registrada),
> no la hora. La hora sigue mandando **solo** cuando el lote todavía no tiene ningún registro
> (fecha sugerida y guarda de captura), que es lo único que se sabe antes del primer día.
>
> **Tope: 1 día.** Un corrimiento mayor sería un día que nadie capturó, y esconderlo mentiría sobre
> el hueco ⇒ un lote que arrancó 2+ días tarde sigue mostrando «Día 3». Idéntico al reporte
> «Primera semana», que ya usa 0 ó 1.

```
desplazamientoNumeracion(edadMinRegistrada, hora)
  = edadMinRegistrada == null ? desplazamientoPrimerDia(hora)         // sin registros
                              : clamp(edadMinRegistrada, 0, 1)        // con registros: manda el dato
```

**Lo que NO se toca (invariantes):**
- La **edad** (`fecha − fecha_encaset`, 0 el día del encaset) sigue siendo la de siempre: guía
  genética, indicadores, informe semanal, liquidación y cruce reproductora→engorde cruzan por EDAD.
- El **guarda de captura** (mínimo = encaset, o encaset+1 si la hora es ≥ 13:00) queda igual: la
  numeración es presentación, no habilita ni bloquea fechas.
- La **regla de pesaje obligatorio** (`PesajeEngordeCalculos` / flag `primer_registro_segun_hora_llegada`)
  queda **igual**: sigue evaluándose sobre el día de negocio *por hora* (Panamá) o la edad cruda
  (resto). Mover un día de pesaje es cambiar una validación que bloquea el guardado; fuera de alcance.
- **Cero DDL, cero SQL, cero migración.** El cambio es 100 % de presentación en el front.

## 3. Archivos

**Función pura (nueva lógica + tests):**
- `frontend/src/app/features/engorde-comun/funciones/dia-negocio-engorde.funcion.ts`
  → `DESPLAZAMIENTO_MAX_NUMERACION`, `menorEdadRegistrada(edades)`, `desplazamientoNumeracion(edadMin, hora)`.
- `frontend/src/app/features/engorde-comun/funciones/dia-negocio-engorde.funcion.spec.ts` (nuevo).

**Consumidores (orquestadores que delegan):**
- `…/seguimiento-diario-lote-reproductora/pages/seguimiento-diario-lote-reproductora-list/…component.ts`
  → el getter `desplazamientoPrimerDia` pasa a `desplazamientoNumeracion`. **Caso reportado.**
- `…/aves-engorde/pages/tabs-principal-engorde/tabs-principal-engorde.component.ts`
  → `diaNegocio()` / `semanaNegocio()` de la tabla de seguimiento de pollo engorde.
- `…/engorde-comun/services/indicadores-diarios-engorde-compute.service.ts` → columna «Día» + CSV.
- `…/aves-engorde/services/productividad-engorde-compute.service.ts` → gráficas diaria y semanal.
- `…/engorde-comun/services/indicadores-diarios-engorde-compute.service.spec.ts` → actualizar los
  casos cuyo fixture arranca en edad ≥ 1 (hoy afirman la numeración vieja).

**Sin cambios:** backend (`EncasetamientoCalculos`, `PesajeEngordeCalculos`, services, fn SQL),
`modal-seguimiento-engorde` (pesaje), `modal-seguimiento-reproductora` (guarda y fecha sugerida).

## 4. Impacto medido (copia local de prod, 04-sep-2026)

| Módulo | Lotes que cambian (tope 1) | Detalle |
|---|---:|---|
| Reproductora aves engorde | **2** | Panamá, sin hora, primer registro en edad 1 (incluye el reportado). |
| Pollo engorde | **112** | **110 de ItalcolEcuador** + 2 de Panamá. |
| Reproductora / engorde con edad_min ≥ 2 | 1 / 3 | **No cambian** (el tope de 1 deja el hueco a la vista). |
| Lotes con registro en la edad 0 | 82 / 55 | **No cambian** (ya arrancaban en Día 1). |

⚠️ En los 110 lotes de Ecuador se corre también el **agrupado por semana de esa misma tabla**
(días 1..7 = semana 1 ⇒ edades 1..7 en vez de 0..6). Es coherente con la columna «Día» que se ve al
lado; los reportes que cruzan por edad (informe semanal, guía genética) no se tocan.

## 5. Casos de prueba

`dia-negocio-engorde.funcion.spec.ts` (nuevo):
1. Sin registros ⇒ cae a la hora (`null`/temprana ⇒ 0; `'13:00'`/`'21:33'` ⇒ 1) — comportamiento previo.
2. **Caso reportado**: sin hora, primer registro en edad 1 ⇒ desplazamiento 1 ⇒ Día 1.
3. Registro en la edad 0 ⇒ 0, **aunque la hora sea tardía** (lotes históricos 131/132: conservan 1..7).
4. Tope: edad mínima 3 ⇒ 1 (el primer registro sigue diciendo «Día 3»).
5. Edades negativas o basura (`NaN`, `null` sueltos) ⇒ 0 / se ignoran.
6. `menorEdadRegistrada`: lista vacía ⇒ `null`; ignora `null`/`NaN`; toma el mínimo aunque venga desordenado.

`indicadores-diarios-engorde-compute.service.spec.ts` (actualizar + agregar):
7. Los 5 casos con fixture desde edad ≥ 1 pasan a la numeración nueva (la ganancia diaria y el cruce
   con la guía **no cambian**: siguen en edades).
8. Nuevo: sin hora y primer registro en edad 1 ⇒ `dia` arranca en 1.
9. Se conservan: lote con registro en edad 0 ⇒ 1,2,3; lote tardío con hora ⇒ 1,2,3; guía por EDAD.

**Validación:** `yarn build` (0 errores) + `yarn test` de los specs tocados. Smoke visual del lote
`LR-0023649715` en el front local contra la copia de prod: la fila del 31/08 debe decir **día 1**, y
un lote con registro en el día del encaset (p. ej. cualquiera de los 82) debe seguir diciendo día 1
en su fila del encaset.
