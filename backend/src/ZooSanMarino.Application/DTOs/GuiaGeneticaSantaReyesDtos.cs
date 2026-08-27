// src/ZooSanMarino.Application/DTOs/GuiaGeneticaSantaReyesDtos.cs
// Contrato del módulo de guía genética REDUCIDA (tabla plana de 3 métricas por raza/año/semana).
namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// Una línea de la guía genética reducida tal como la ve el front.
/// </summary>
/// <param name="Edad">Semana de vida. La guía sembrada cubre 18–140 (arranca en producción).</param>
/// <param name="ProdPorcentaje">
/// % de producción de la semana. <c>null</c> significa «la línea no tiene dato para esa semana»,
/// que <b>no</b> es lo mismo que 0: la raza Criolla tiene 40 semanas legítimamente nulas.
/// </param>
/// <param name="RetiroAcH">% de mortalidad ACUMULADA de hembras a esa semana (no semanal).</param>
/// <param name="GrAveDiaH">Consumo en gramos/ave/día de hembras a esa semana.</param>
/// <param name="CodigoGuiaGenetica">
/// Clave natural derivada <c>Raza+AnioGuia+Edad</c>. Se recalcula sola al cambiar cualquiera de los
/// tres; el front la muestra, no la edita.
/// </param>
public record GuiaGeneticaSantaReyesDto(
    int Id,
    int CompanyId,
    string Raza,
    string AnioGuia,
    int Edad,
    decimal? ProdPorcentaje,
    decimal? RetiroAcH,
    decimal? GrAveDiaH,
    string? CodigoGuiaGenetica,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

/// <summary>
/// Alta de una línea. <b>La raza es texto libre</b>, no un <c>select</c> alimentado por lo que ya
/// existe: ese es el <i>deadlock de arranque</i> que hoy vuelve inservible la pantalla de Ecuador
/// (sin guía cargada no hay raza que elegir ⇒ no se puede crear la primera).
/// </summary>
public record CreateGuiaGeneticaSantaReyesDto(
    string Raza,
    string AnioGuia,
    int Edad,
    decimal? ProdPorcentaje,
    decimal? RetiroAcH,
    decimal? GrAveDiaH
);

/// <summary>
/// Edición de una línea. Cambiar <c>Raza</c>, <c>AnioGuia</c> o <c>Edad</c> <b>recalcula</b> el
/// código: la clave natural nunca queda apuntando al valor viejo.
/// </summary>
public record UpdateGuiaGeneticaSantaReyesDto(
    int Id,
    string Raza,
    string AnioGuia,
    int Edad,
    decimal? ProdPorcentaje,
    decimal? RetiroAcH,
    decimal? GrAveDiaH
);

/// <summary>
/// Filtros y paginación del listado.
/// </summary>
/// <param name="Raza">Coincidencia parcial, case-insensitive.</param>
/// <param name="AnioGuia">Coincidencia parcial.</param>
/// <param name="EdadDesde">Semana mínima, inclusive.</param>
/// <param name="EdadHasta">Semana máxima, inclusive.</param>
/// <param name="PageSize">
/// Se normaliza con <c>PaginacionCalculos</c>: sin especificar ⇒ 20; pedir de más ⇒ <b>el tope</b>
/// (2.000, tope de tabla MAESTRA), nunca el default. El clamp casero
/// <c>pageSize &gt; 200 ⇒ 20</c> que anda dando vueltas por el repo hacía justo lo contrario y ya
/// costó dos incidentes.
/// </param>
/// <param name="SortBy">
/// <c>raza</c> | <c>anioGuia</c> | <c>edad</c> | <c>prodPorcentaje</c> | <c>retiroAcH</c> |
/// <c>grAveDiaH</c>. Sin especificar, la guía sale en su orden natural: raza, año, semana.
/// </param>
public record GuiaGeneticaSantaReyesSearchRequest(
    string? Raza = null,
    string? AnioGuia = null,
    int? EdadDesde = null,
    int? EdadHasta = null,
    int Page = 1,
    int PageSize = 20,
    string? SortBy = null,
    bool SortDesc = false
);

/// <summary>Una fila del Excel que no se pudo importar, con el número de fila TAL COMO SE VE en Excel.</summary>
/// <param name="Fila">Número de fila del archivo (1 = encabezados), para que el usuario la ubique.</param>
/// <param name="Motivo">Qué pasa con esa fila, en el idioma del usuario.</param>
public record GuiaGeneticaSantaReyesImportErrorDto(
    int Fila,
    string Motivo
);

/// <summary>
/// Resultado del import.
///
/// <para>
/// 🔴 <b>El import es idempotente</b> por <c>codigo_guia_genetica = Raza+AnioGuia+Edad</c> contra el
/// UNIQUE parcial <c>ux_guia_genetica_santa_reyes_codigo</c>: reimportar el mismo archivo
/// <b>actualiza, no duplica</b>. La segunda pasada del mismo archivo da
/// <c>Insertados = 0</c> y todo el resto en <see cref="Omitidos"/>.
/// </para>
/// </summary>
/// <param name="Success">
/// El archivo entró <b>completo</b>: ninguna fila quedó rechazada. Un import parcial devuelve
/// <c>false</c> con las filas buenas ya aplicadas y <see cref="Errores"/> diciendo cuáles no —
/// el usuario tiene que ver las que faltan, no un «listo» que esconde 3 filas perdidas.
/// </param>
/// <param name="TotalFilas">Filas de datos leídas del archivo (sin contar el encabezado).</param>
/// <param name="Insertados">Líneas nuevas.</param>
/// <param name="Actualizados">Líneas que ya existían y cambiaron al menos una métrica.</param>
/// <param name="Omitidos">
/// Líneas idénticas a lo que ya estaba (no se tocan: reescribirlas ensuciaría <c>updated_at</c> de
/// toda la guía en cada reimport) más las filas en blanco que Excel arrastra al final de la hoja.
/// </param>
/// <param name="Errores">Filas rechazadas, con su número y su motivo.</param>
public record GuiaGeneticaSantaReyesImportResultDto(
    bool Success,
    int TotalFilas,
    int Insertados,
    int Actualizados,
    int Omitidos,
    IReadOnlyList<GuiaGeneticaSantaReyesImportErrorDto> Errores
);
