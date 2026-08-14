using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Completa ItalJira con la BITÁCORA de julio y agosto 2026: qué se pidió, cuánto costó,
    /// cómo se resolvió y qué bugs aparecieron en el camino. El seed anterior
    /// (<c>20260807160000_SeedHistorialDesarrolloItalJira</c>) dejó los TÍTULOS del trabajo;
    /// esto le pone el contenido que hacía falta para que el tablero sirva de historial.
    /// </summary>
    /// <remarks>
    /// <b>Qué hace (tres cosas, ninguna duplica al seed anterior):</b>
    /// <list type="number">
    /// <item><b>Enriquece 98 tareas ya sembradas</b> — les escribe <c>horas_estimadas</c> y una
    /// descripción con el pedido textual del usuario, la solución (los commits), los bugs y la
    /// evidencia. No inserta filas nuevas para trabajo que ya estaba registrado.</item>
    /// <item><b>Inserta 39 tareas nuevas</b> — sesiones de trabajo que nunca tuvieron plan en
    /// <c>fase_de_desarrollo/</c> o que son posteriores al seed anterior (07ago → 13ago).
    /// Códigos <c>SES-AAAAMMDD-xxxx</c> para no colisionar con los <c>HIS-2026-NNNN-Tn</c>.</item>
    /// <item><b>Inserta 99 subtareas BUG</b> — una por commit <c>fix(...)</c> del período,
    /// colgando (<c>parent_tarea_id</c>) de la tarea de su sesión, con la causa que quedó
    /// escrita en el commit.</item>
    /// </list>
    /// Al final, cada historia recibe en <c>horas_estimadas</c> la suma de las de sus tareas.
    ///
    /// <b>Fuente (real, medida — no inventada):</b> las 134 sesiones de trabajo de julio y agosto
    /// (pedido textual, fechas y duración tomadas de la transcripción) cruzadas con los 447
    /// commits del repositorio. La atribución commit→sesión usa los SEGMENTOS de actividad de la
    /// sesión (no su ventana completa: hay sesiones abiertas durante semanas) y desempata por
    /// solape de archivos, porque el repo se trabaja con varias sesiones en paralelo. 96 commits
    /// (mayormente <c>docs(tracker)</c> y merges) quedaron sin dueño claro y NO se atribuyeron a
    /// nadie: preferimos un hueco a una evidencia falsa.
    ///
    /// <b>Lo único estimado son las horas.</b> No existe registro de estimaciones previas: cada
    /// cifra se asignó por juicio leyendo el trabajo del ítem, con la rúbrica y el valor por ítem
    /// versionados en <c>fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json</c>.
    /// La duración REAL de cada sesión queda en la línea «Evidencia» de la descripción, para que
    /// se vea la diferencia entre lo estimado y lo que efectivamente tomó.
    ///
    /// <b>Idempotencia:</b> el UPDATE exige <c>horas_estimadas IS NULL</c> <i>y</i> que la
    /// descripción siga siendo EXACTAMENTE la que escribió el seed anterior — si alguien la editó
    /// a mano, esta migración no la pisa. Los INSERT van con <c>WHERE NOT EXISTS</c> por
    /// <c>codigo</c>. Correrla dos veces no cambia una sola fila la segunda vez.
    ///
    /// <b>Identidad y fail-open:</b> el autor se resuelve por email
    /// (<c>moiesbbuga@gmail.com</c>), nunca por guid fijo (los ids difieren local↔prod); si el
    /// usuario no existe en el entorno, <c>RAISE NOTICE</c> + <c>RETURN</c> sin sembrar nada. Un
    /// seed no puede tumbar el arranque de la app.
    ///
    /// Migración DATA-ONLY: Designer clonado, ModelSnapshot intacto. El SQL vive en el partial
    /// <c>.Seed.cs</c> por tamaño (es generado; ver el <c>remarks</c> de ese archivo).
    /// </remarks>
    public partial class SeedBitacoraSesionesJulAgo2026 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SEED_SQL);
        }

        /// <summary>
        /// Revierte exactamente lo de esta migración: borra las subtareas <c>BUG-&lt;sha&gt;</c> y
        /// las tareas <c>SES-*</c>, y devuelve las 98 descripciones enriquecidas a su texto
        /// original con <c>horas_estimadas</c> en NULL. Las horas de las historias vuelven a NULL.
        /// </summary>
        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DOWN_SQL);
        }
    }
}
