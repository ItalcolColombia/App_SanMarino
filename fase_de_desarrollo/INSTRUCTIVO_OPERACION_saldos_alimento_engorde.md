# Saldos de alimento en pollo engorde — qué se corrigió y qué queda por revisar

**Para:** equipos de Costos y Producción de **ItalcolEcuador** e **ItalcolPanama**
**Fecha:** 30 de julio de 2026

> ⚠️ **Leer primero:** las cifras de este documento son las que van a quedar **después del despliegue**.
> Mientras no se despliegue, la pantalla sigue mostrando los números viejos (en Ecuador, muchos más
> saldos en rojo de los que figuran acá). Cuando el despliegue esté hecho, esta lista es la que aplica.

> 📍 **Cómo ubicar cada galpón:** en las tablas va **Granja · Núcleo · Galpón · ID**.
> **En Panamá el núcleo es imprescindible**, porque los galpones se llaman «1», «2», «3»… y esos
> nombres **se repiten en cada núcleo**: DOÑA MARIA tiene un galpón «3» en el núcleo A, otro en el B y
> otro en el C, y son tres galpones distintos. El **ID** (`G0474`, `G0478`, `G0469`) es el único
> identificador que nunca se repite: si hay dudas, guiarse por el ID.

---

## 1. Qué pasaba

En la tabla de **Registros Diarios** de pollo engorde, la columna **«Saldo alimento (kg)»** venía
mostrando un número más bajo del que correspondía —en algunos lotes, en rojo desde el primer día—
aunque el **stock de Gestión de Inventario estuviera correcto**.

**La causa era del sistema, no de la carga de ustedes.** El cálculo del saldo arrancaba mirando
**demasiado atrás en el tiempo** y se llevaba por delante los movimientos del **ciclo anterior** del
mismo galpón: los traslados con los que se vacía la bodega cuando termina una corrida. Ese alimento ya
no era del lote nuevo, pero se lo descontaba igual.

Como en Ecuador cada galpón encadena 3 y 4 corridas seguidas, el problema **solo aparece desde la
tercera corrida en adelante**. Por eso Costos lo vio en las corridas 2603 y 2604, y no en la 2601 ni la
2602. En Panamá todavía no se había manifestado.

**Ya está corregido.** El cálculo ahora arranca donde termina el ciclo anterior, y además el saldo
guardado se actualiza solo cada vez que se registra un movimiento de inventario.

---

## 2. Cómo se lee un saldo en negativo

Esto es importante y conviene que quede claro en los dos países:

> **Un saldo negativo NO significa que el sistema esté fallando.**
> Significa: *«este lote consumió alimento cuya llegada no está registrada».*

Se decidió mostrarlo tal cual, en vez de recortarlo a cero, porque recortarlo regalaba kilos que no
existen y dejaba el acumulado por encima del inventario real. El negativo es **información**: dice
exactamente cuántos kilos faltan por registrar y desde qué día.

**Qué hacer cuando aparece uno:** buscar el ingreso de alimento de ese período y verificar que esté
cargado, con la fecha correcta. En la enorme mayoría de los casos el alimento llegó y se consumió, pero
el ingreso se cargó días después.

---

## 3. ECUADOR — los 5 casos que quedan

De **330 filas en rojo repartidas en 27 lotes**, después del despliegue quedan **25 filas en 5 lotes**,
y **ninguna en las corridas activas 2603 y 2604**. Los 5 que quedan son de corridas ya cerradas.

### 3.1 Kilometro 86 · Núcleo N1 · Galpón «Galpon-2» (id `G0040`) · corrida 2601 — **faltan 8.020 kg**

| | |
|---|---|
| Granja | Kilometro 86 |
| Núcleo | N1 |
| Galpón | **Galpon-2** — id `G0040` |
| Corrida | 2601 |
| Días en rojo | 27/03 al 22/04 (21 días seguidos) |
| Peor saldo | **−9.020 kg** |
| Período del lote | 17/02 al 21/04 |

**Qué pasa:** durante ese ciclo el lote consumió **135.960 kg**, pero en el galpón solo quedaron
registrados **127.940 kg** de entrada. Hay **8.020 kg consumidos que nunca se cargaron como ingreso**
dentro de ese período.

**Qué hay que revisar:**
1. Buscar las remisiones o facturas de alimento del galpón **Galpon-2 (`G0040`)** de Kilometro 86 entre
   el **17/02 y el 21/04**.
2. Cruzar contra los ingresos cargados en el sistema en ese rango.
3. Si aparece una entrega sin cargar → **registrarla con la fecha real de llegada**.
4. Si aparece cargada pero con **fecha posterior al 21/04** → hay que **corregirle la fecha**.

> ⚠️ Ojo: los ingresos que figuran del 24/04 en adelante **ya son de la corrida siguiente (2602)**. No
> son estos. El faltante está dentro del período del ciclo, no después.

### 3.2 Sacachun 2 · Núcleo N1 · tres galpones · corrida 2602 — traslado de cierre

| Granja | Núcleo | Galpón | ID | Día en rojo | Último día con seguimiento | Saldo |
|---|---|---|---|---|---|---:|
| Sacachun 2 | N1 | **Galpon-5** | `G0055` | 16/05 | 13/05 | −3.920 kg |
| Sacachun 2 | N1 | **Galpon-1** | `G0051` | 15/05 | 14/05 | −3.220 kg |
| Sacachun 2 | N1 | **Galpon-2** | `G0052` | 16/05 | 13/05 | −600 kg |

**Qué pasa:** en los tres, el rojo aparece en **un solo día, y es posterior al último seguimiento
cargado**. Es el traslado con el que se vació la bodega al cerrar la corrida: **sacó más kilos de los
que el sistema tenía contados como disponibles**.

**Qué hay que revisar:** el documento de traslado de esa fecha. Dos posibilidades:
- **La cantidad trasladada está mal cargada** (se cargó de más) → corregir la cantidad.
- **La cantidad es correcta** → entonces faltó registrar un ingreso previo, igual que en el caso 3.1.

### 3.3 Kilometro 86 · Núcleo N1 · Galpón «Galpon-4» (id `G0042`) — **no hay nada que hacer**

−1 kg el 27/04, corrida 2601. Es redondeo acumulado. Se ignora.

---

## 4. PANAMÁ — 19 lotes, 43 días en rojo

**Importante:** en Panamá el cálculo **no cambió** —se verificó fila por fila y dio cero diferencias—.
Estos saldos en rojo **ya estaban**, y son del mismo tipo que los que quedan en Ecuador: alimento
consumido cuyo ingreso no está registrado en la fecha que corresponde.

Los **25 galpones de Panamá cuadran contra el inventario**. Estos rojos no son un descuadre contra el
stock: son días puntuales dentro del ciclo.

> 📍 Recordar: acá el **núcleo** distingue galpones con el mismo nombre. Guiarse por el **ID** ante
> cualquier duda.

### 4.1 Los dos casos grandes — revisar primero

| Granja | Núcleo | Galpón | ID | Corrida | Días en rojo | Período | Peor saldo |
|---|---|---|---|---|---:|---|---:|
| **DAYLAND** | A | **6** | `G0471` | 13 - 1 | 17 días | 07/06 al 17/07 | **−10.634 kg** |
| **DOÑA MARIA** | A | **1** | `G0472` | 94 - 3 | 7 días | 03/07 al 26/07 | **−10.129 kg** |

Son **déficits sostenidos**: el rojo se mantiene muchos días seguidos. Eso indica que falta registrar
una entrega completa, no un desfase de un día.

**Qué hay que revisar:** las remisiones de alimento de esos galpones en el período indicado, contra lo
cargado en el sistema. Buscar una entrega faltante del orden de los 10.000 kg.

### 4.2 Caso mediano

| Granja | Núcleo | Galpón | ID | Corrida | Días en rojo | Período | Peor saldo |
|---|---|---|---|---|---:|---|---:|
| MENDOZA | A | **3** | `G0486` | 17 - 1 | 3 días | 18/06 al 20/07 | −2.426 kg |

### 4.3 Los 16 casos de un solo día — desfase de fecha

Todos tienen **un único día en rojo** y montos chicos. El patrón típico es: **el alimento llegó un día
y se cargó al día siguiente**, así que por 24 horas el consumo va por delante de la entrada.

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
> - **DOÑA MARIA, núcleo C, galpón «1»** tiene el ID literal **`GALPON`**, no un código `G00xx` como
>   los demás. Es un dato mal cargado en el maestro de galpones y conviene normalizarlo, porque
>   dificulta buscarlo y puede confundirse con otro.
> - **DOÑA MARIA** tiene galpones llamados «3» y «4» en **más de un núcleo** (A, B y C). Antes de
>   corregir, confirmar el núcleo o buscar directamente por el ID.

**Qué hay que revisar:** en cada uno, el ingreso de alimento **más cercano a esa fecha**. Si llegó el
día anterior y se cargó después, corregirle la fecha al día real de llegada. Con eso el rojo desaparece.

Son montos chicos y no afectan el cuadre contra el inventario, así que **tienen prioridad baja**. Lo
importante es tomar la costumbre para que no se sigan generando.

---

## 5. Cómo evitar que se repita — lo que depende de la operación

**1. Registrar el ingreso el día que llega el alimento.** Es la causa de fondo de casi todos los rojos
que quedan. Si el alimento llega el viernes y se carga el lunes con fecha lunes, el lote muestra tres
días en rojo aunque todo esté bien.

**2. Si hay que cargar con fecha atrasada, ponerle la fecha real de llegada**, no la de hoy. El sistema
lo permite y ahora **avisa** cuando la fecha cae fuera del ciclo vigente del galpón, indicando a qué
corrida pertenece esa fecha. **Ese aviso no bloquea nada** — es para que se revise antes de guardar.

**3. No usar el «Ajuste manual de stock» para cuadrar alimento.** Esto es importante:

> El ajuste manual **cambia el stock pero no entra en el cálculo del saldo**. Si se usa para emparejar
> números, el stock y la tabla diaria quedan diciendo cosas distintas, y el problema se vuelve invisible.

Para corregir alimento hay que usar **ingreso, traslado o consumo**, según lo que realmente pasó.

**4. Cuando aparezca un rojo nuevo, revisarlo en el momento.** Un rojo de un día se resuelve mirando el
ingreso más cercano. Un rojo que se sostiene una semana significa que falta una entrega completa, y
cuanto más tarde se detecte, más difícil es encontrar el papel.

---

## 6. Cómo verificar que todo está en orden

Se agregó una verificación automática que compara, galpón por galpón, el saldo de la tabla diaria
contra el stock físico de inventario.

**Estado al día de hoy:**

| Empresa | Galpones que cuadran |
|---|---|
| ItalcolEcuador | **35 de 35** |
| ItalcolPanama | **25 de 25** |

Diferencia total: **0,0 kg** en ambos países.

Si en algún momento un galpón deja de cuadrar, esa verificación lo marca. **Ese es el número a mirar:
si deja de estar en cero, algo se desalineó y conviene avisar a sistemas antes de que crezca.**

---

## 7. Resumen de acciones

| Prioridad | Quién | Granja · Núcleo · Galpón (ID) | Qué |
|---|---|---|---|
| **Alta** | Costos Ecuador | Kilometro 86 · N1 · **Galpon-2** (`G0040`) | Buscar los **8.020 kg** faltantes entre el 17/02 y el 21/04 |
| **Alta** | Costos Panamá | DAYLAND · A · **6** (`G0471`) | Buscar la entrega faltante (~10.600 kg), 07/06 al 17/07 |
| **Alta** | Costos Panamá | DOÑA MARIA · A · **1** (`G0472`) | Buscar la entrega faltante (~10.100 kg), 03/07 al 26/07 |
| Media | Costos Ecuador | Sacachun 2 · N1 · **Galpon-5** (`G0055`), **Galpon-1** (`G0051`), **Galpon-2** (`G0052`) | Revisar el documento del traslado de cierre |
| Media | Costos Panamá | MENDOZA · A · **3** (`G0486`) | Revisar el período 18/06 al 20/07 |
| Baja | Producción Panamá | Los 16 de la tabla 4.3 | Corregir fechas de ingreso |
| Baja | Sistemas / maestros | DOÑA MARIA · C · **1** (`GALPON`) | Normalizar el ID del galpón, que está mal cargado |
| — | Ambos | — | Adoptar los 4 puntos de la sección 5 |
