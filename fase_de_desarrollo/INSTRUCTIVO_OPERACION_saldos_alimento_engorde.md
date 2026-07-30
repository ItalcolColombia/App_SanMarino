# Saldos de alimento en pollo engorde — estado real y qué queda por revisar

**Para:** equipos de Costos y Producción de **ItalcolEcuador** e **ItalcolPanama**
**Fecha:** 30 de julio de 2026

---

## 1. El estado, en una tabla

### ECUADOR — 103 lotes

| Corrida | Lotes | **Hoy en pantalla** (lotes en rojo) | **Con las correcciones** (lotes en rojo) | Lotes OK |
|---|---:|---:|---:|---:|
| 2601 | 33 | 2 | 2 | **31 de 33** |
| 2602 | 35 | **15** | **3** | **32 de 35** |
| **2603** | 29 | **9** | **0** ✅ | **29 de 29** |
| **2604** | 6 | **1** | **0** ✅ | **6 de 6** |
| **Total** | **103** | **27** | **5** | **98 de 103** |

> ✅ **Las dos últimas corridas —2603 y 2604— quedan al 100 %.** Ningún lote en rojo.
> Los 5 que quedan son de corridas **ya cerradas** (2601 y 2602) y son datos por corregir, no cálculo.

En filas: de **330 días en rojo** se baja a **25**.

### PANAMÁ — 31 lotes

| | |
|---|---:|
| Lotes sin ningún día en rojo | **12 de 31** |
| Lotes con al menos un día en rojo | **19 de 31** |
| Días en rojo en total | 43 |

> ⚠️ **En Panamá el cálculo NO cambió** — se comparó fila por fila y dio cero diferencias. Estos rojos
> **ya estaban** y no los produjo la corrección. Son alimento consumido cuyo ingreso no está registrado
> en la fecha que corresponde. Sus **25 galpones cuadran contra el inventario**.

---

## 2. Qué pasó — y por qué una corrida cerrada cambió sin que nadie la tocara

Esta es la pregunta que más se va a repetir, así que va primero.

**La tabla de Registros Diarios no es una foto congelada: se recalcula cada vez que se abre.** Nadie
tocó los datos de la corrida 2602. Lo que cambió fue **la fórmula**, el 28 de julio, y al recalcularse,
una corrida cerrada hace meses empezó a mostrar otra cosa.

Ese día se hicieron **dos cambios distintos**, y conviene no confundirlos:

### (a) Se quitó el «piso en cero» — fue a propósito

Hasta el 28 de julio la fórmula **tapaba** cualquier faltante y mostraba cero. Literalmente no era
posible ver un saldo negativo, aunque lo hubiera.

Se quitó porque ese piso **regalaba kilos que no existían** y dejaba el acumulado por encima del
inventario real: la pantalla decía que había alimento que en la bodega no estaba.

> 🔴 **Importante:** los faltantes de la corrida 2602 **ya existían**. No aparecieron: **se
> destaparon**. Cuando decimos que «estaba cuadrada», lo que estaba era *tapada*.

### (b) Se agregó una ventana de 10 días antes del encaset — esto sí fue un error

El cálculo empezó a mirar 10 días para atrás del encaset y se llevaba por delante los movimientos del
**ciclo anterior** del mismo galpón: los traslados con los que se vacía la bodega al cerrar una corrida.
Ese alimento ya no era del lote nuevo, pero se lo descontaba igual.

**Esto creó rojos falsos, nuevos.** Es lo que se corrigió.

Como necesita que exista un ciclo anterior en el galpón, **solo puede aparecer desde la tercera corrida
en adelante**. Por eso pegó fuerte en 2602, 2603 y 2604, y casi nada en 2601.

### En números, para la corrida 2602

De los **15 lotes** que hoy aparecen en rojo, **12 eran falsos** (el error de la ventana) y **3 son
reales** — faltantes que ya estaban desde antes, pero el piso los escondía.

---

## 3. Cómo se lee un saldo en negativo

> **Un saldo negativo NO significa que el sistema esté fallando.**
> Significa: *«este lote consumió alimento cuya llegada no está registrada».*

Es **información**: dice exactamente cuántos kilos faltan por cargar y desde qué día. De ahora en
adelante ya no se van a tapar — la decisión es preferir ver el hueco a que el sistema lo disimule.

**Qué hacer cuando aparece uno:** buscar el ingreso de alimento de ese período y verificar que esté
cargado con la fecha correcta. En la enorme mayoría de los casos el alimento llegó y se consumió, pero
el ingreso se cargó días después.

---

## 4. Cómo ubicar cada galpón en las tablas que siguen

Va **Granja · Núcleo · Galpón · ID**.

> 📍 **En Panamá el núcleo es imprescindible**: los galpones se llaman «1», «2», «3»… y esos nombres
> **se repiten en cada núcleo**. DOÑA MARIA tiene un galpón «3» en el núcleo A, otro en el B y otro en
> el C, y son tres galpones distintos. El **ID** (`G0474`, `G0478`, `G0469`) es el único identificador
> que nunca se repite: **ante cualquier duda, guiarse por el ID**.

---

## 5. ECUADOR — los 5 lotes que quedan

Todos de corridas **ya cerradas**. Ninguno de la 2603 ni la 2604.

### 5.1 Kilometro 86 · N1 · Galpón «Galpon-2» (`G0040`) · corrida **2601** — **faltan 8.020 kg**

| | |
|---|---|
| Días en rojo | 27/03 al 22/04 (21 días seguidos) |
| Peor saldo | **−9.020 kg** |
| Período del lote | 17/02 al 21/04 |

**Qué pasa:** el lote consumió **135.960 kg**, pero en el galpón solo quedaron registrados **127.940 kg**
de entrada. Hay **8.020 kg consumidos que nunca se cargaron como ingreso** dentro de ese período.

**Qué revisar:**
1. Remisiones o facturas de alimento de **Galpon-2 (`G0040`)** de Kilometro 86, entre el **17/02 y el 21/04**.
2. Cruzar contra los ingresos cargados en el sistema en ese rango.
3. Si hay una entrega sin cargar → **registrarla con la fecha real de llegada**.
4. Si está cargada con **fecha posterior al 21/04** → **corregirle la fecha**.

> ⚠️ Los ingresos del 24/04 en adelante **ya son de la corrida 2602**. No son estos. El faltante está
> dentro del período del ciclo, no después.

### 5.2 Sacachun 2 · N1 · corrida **2602** — el traslado de cierre

| Granja | Núcleo | Galpón | ID | Día en rojo | Último día cargado | Saldo |
|---|---|---|---|---|---|---:|
| Sacachun 2 | N1 | **Galpon-5** | `G0055` | 16/05 | 13/05 | −3.920 kg |
| Sacachun 2 | N1 | **Galpon-1** | `G0051` | 15/05 | 14/05 | −3.220 kg |
| Sacachun 2 | N1 | **Galpon-2** | `G0052` | 16/05 | 13/05 | −600 kg |

**Qué pasa:** en los tres el rojo es de **un solo día, posterior al último seguimiento cargado**. Es el
traslado con el que se vació la bodega al cerrar: **sacó más kilos de los que el sistema tenía contados**.

**Qué revisar:** el documento de traslado de esa fecha.
- Si la **cantidad está cargada de más** → corregirla.
- Si la cantidad es correcta → faltó registrar un ingreso previo, igual que en 5.1.

### 5.3 Kilometro 86 · N1 · Galpón «Galpon-4» (`G0042`) · corrida 2601 — **nada que hacer**

−1 kg el 27/04. Redondeo acumulado. Se ignora.

---

## 6. PANAMÁ — 19 lotes con días en rojo

Recordar: **el cálculo de Panamá no cambió**. Estos rojos ya estaban y son del mismo tipo que los que
quedan en Ecuador. Los 25 galpones cuadran contra el inventario.

### 6.1 Los dos casos grandes — revisar primero

| Granja | Núcleo | Galpón | ID | Corrida | Días en rojo | Período | Peor saldo |
|---|---|---|---|---|---:|---|---:|
| **DAYLAND** | A | **6** | `G0471` | 13 - 1 | 17 | 07/06 al 17/07 | **−10.634 kg** |
| **DOÑA MARIA** | A | **1** | `G0472` | 94 - 3 | 7 | 03/07 al 26/07 | **−10.129 kg** |

Son **déficits sostenidos**: el rojo se mantiene muchos días seguidos, lo que indica que **falta una
entrega completa**, no un desfase de un día.

**Qué revisar:** las remisiones de esos galpones en el período indicado contra lo cargado. Buscar una
entrega faltante del orden de los **10.000 kg**.

### 6.2 Caso mediano

| Granja | Núcleo | Galpón | ID | Corrida | Días en rojo | Período | Peor saldo |
|---|---|---|---|---|---:|---|---:|
| MENDOZA | A | **3** | `G0486` | 17 - 1 | 3 | 18/06 al 20/07 | −2.426 kg |

### 6.3 Los 16 de un solo día — desfase de fecha

Un único día en rojo y montos chicos. El patrón típico: **el alimento llegó un día y se cargó al
siguiente**, así que por 24 horas el consumo va por delante de la entrada.

| Granja | Núcleo | Galpón | ID | Corrida | Día en rojo | Saldo |
|---|---|---|---|---|---|---:|
| DAYLAND | A | 5 | `G0460` | 13 - 1 | 10/06 | −635 kg |
| DOÑA MARIA | A | 3 | `G0474` | 94 - 1 | 28/06 | −635 kg |
| DOÑA MARIA | B | 3 | `G0478` | 86 - 1 | 23/07 | −635 kg |
| MENDOZA | A | 4 | `G0487` | 17 - 1 | 17/06 | −590 kg |
| TROFARELLO | B | 6 | `G0496` | 06 - 1 | 12/07 | −544 kg |
| MENDOZA | A | 1 | `G0484` | 17 - 2 | 20/06 | −544 kg |
| DOÑA MARIA | A | 2 | `G0473` | 94 - 2 | 25/06 | −544 kg |
| DOÑA MARIA | B | 4 | `G0479` | 86 - 1 | 22/07 | −544 kg |
| DOÑA MARIA | C | 4 | `G0470` | 60 - 2 | 09/07 | −499 kg |
| DOÑA MARIA | A | 4 | `G0475` | 94 - 2 | 01/07 | −454 kg |
| DOÑA MARIA | C | 3 | `G0469` | 60 - 2 | 08/07 | −454 kg |
| DOÑA MARIA | C | 1 | `GALPON` | 60 - 2 | 02/07 | −454 kg |
| DAYLAND | A | 3 | `G0463` | 13 - 1 | 12/06 | −363 kg |
| MENDOZA | A | 2 | `G0485` | 17 - 1 | 18/06 | −363 kg |
| DAYLAND | A | 1 | `G0465` | 13 - 1 | 12/06 | −272 kg |
| DOÑA MARIA | C | 2 | `G0490` | 60 - 3 | 02/07 | −136 kg |

> ⚠️ **Dos avisos sobre esta tabla:**
> - **DOÑA MARIA · C · galpón «1»** tiene como ID el texto **`GALPON`**, no un código `G00xx`. Es un
>   dato mal cargado en el maestro; conviene normalizarlo porque dificulta buscarlo.
> - **DOÑA MARIA** tiene galpones «3» y «4» en **más de un núcleo**. Confirmar el núcleo o buscar por ID.

**Qué revisar:** en cada uno, el ingreso más cercano a esa fecha. Si llegó el día anterior y se cargó
después, **corregirle la fecha al día real de llegada** y el rojo desaparece.

Prioridad **baja**: son montos chicos y no afectan el cuadre contra el inventario. Lo importante es
tomar la costumbre para que no se sigan generando.

---

## 7. Cómo evitar que se repita — lo que depende de la operación

**1. Registrar el ingreso el día que llega el alimento.** Es la causa de fondo de casi todos los rojos
que quedan. Si llega el viernes y se carga el lunes con fecha lunes, el lote muestra tres días en rojo
aunque todo esté bien.

**2. Si hay que cargar con fecha atrasada, poner la fecha real de llegada**, no la de hoy. El sistema lo
permite y ahora **avisa** cuando la fecha cae fuera del ciclo vigente del galpón, indicando a qué
corrida pertenece. **El aviso no bloquea nada** — es para revisar antes de guardar.

**3. No usar el «Ajuste manual de stock» para cuadrar alimento.**

> El ajuste manual **cambia el stock pero no entra en el cálculo del saldo**. Si se usa para emparejar
> números, el stock y la tabla diaria quedan diciendo cosas distintas y el problema se vuelve invisible.

Para corregir alimento hay que usar **ingreso, traslado o consumo**, según lo que realmente pasó.

**4. Revisar el rojo en el momento en que aparece.** Uno de un día se resuelve mirando el ingreso más
cercano. Uno que se sostiene una semana significa que falta una entrega completa, y cuanto más tarde se
detecte, más difícil es encontrar el papel.

---

## 8. Cómo verificar que todo está en orden

Se agregó una verificación que compara, galpón por galpón, el saldo de la tabla diaria contra el stock
físico de inventario.

| Empresa | Galpones que cuadran | Diferencia |
|---|---|---|
| ItalcolEcuador | **35 de 35** | **0,0 kg** |
| ItalcolPanama | **25 de 25** | **0,0 kg** |

**Ese es el número a mirar.** Si algún galpón deja de cuadrar, la verificación lo marca; conviene avisar
a sistemas antes de que crezca.

> Ojo: no confundir **cuadrar** con **no tener rojos**. Un galpón puede cuadrar contra el inventario y
> aun así tener días en rojo dentro del ciclo — es el caso de Panamá. Cuadrar significa que la tabla
> diaria y la bodega dicen lo mismo **hoy**; el rojo señala un día puntual en que faltó registrar algo.

---

## 9. Resumen de acciones

| Prioridad | Quién | Granja · Núcleo · Galpón (ID) | Qué |
|---|---|---|---|
| **Alta** | Costos Ecuador | Kilometro 86 · N1 · **Galpon-2** (`G0040`) | Buscar los **8.020 kg** faltantes entre el 17/02 y el 21/04 |
| **Alta** | Costos Panamá | DAYLAND · A · **6** (`G0471`) | Buscar la entrega faltante (~10.600 kg), 07/06 al 17/07 |
| **Alta** | Costos Panamá | DOÑA MARIA · A · **1** (`G0472`) | Buscar la entrega faltante (~10.100 kg), 03/07 al 26/07 |
| Media | Costos Ecuador | Sacachun 2 · N1 · **Galpon-5** (`G0055`), **Galpon-1** (`G0051`), **Galpon-2** (`G0052`) | Revisar el documento del traslado de cierre |
| Media | Costos Panamá | MENDOZA · A · **3** (`G0486`) | Revisar el período 18/06 al 20/07 |
| Baja | Producción Panamá | Los 16 de la tabla 6.3 | Corregir fechas de ingreso |
| Baja | Sistemas / maestros | DOÑA MARIA · C · **1** (`GALPON`) | Normalizar el ID del galpón, mal cargado |
| — | Ambos | — | Adoptar los 4 puntos de la sección 7 |

---

> ℹ️ **Nota sobre las cifras:** las columnas «con las correcciones» son las que van a verse **una vez
> desplegado el cambio**. Mientras tanto la pantalla sigue mostrando los números de la columna «hoy».
