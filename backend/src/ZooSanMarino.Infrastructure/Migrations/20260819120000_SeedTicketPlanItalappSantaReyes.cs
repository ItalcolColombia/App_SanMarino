using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Registra en ItalJira el <b>Plan de Trabajo de Italapp para Santa Reyes</b>: la historia que
    /// agrupa el proyecto, el caso que lo pide, sus <b>13 tareas</b> (una por paquete de trabajo)
    /// y sus <b>29 subtareas</b> (una por actividad), con horas y fechas planificadas.
    /// </summary>
    /// <remarks>
    /// <b>Quién pide y quién registra.</b> El caso lo <b>crea el administrador</b>
    /// (<c>moiesbbuga@gmail.com</c>) <b>a nombre de Lenin</b>, usuario de la empresa Santa Reyes:
    /// es el «solicitante delegado» del módulo, privilegio exclusivo de <c>tickets.admin</c>. Por
    /// eso el caso queda en la empresa de Lenin y no en la del administrador — <c>SearchMisTickets</c>
    /// filtra por la empresa efectiva de quien mira, así que un caso alojado en la empresa del
    /// administrador NUNCA aparecería en «Mis solicitudes» del solicitante. Se siembra además la
    /// nota <c>SISTEMA_SOLICITANTE</c> que escribe el servicio al delegar, para que Lenin no vea un
    /// caso suyo que nunca creó.
    ///
    /// <b>Fuente de los datos (no inventados).</b> <c>~/Desktop/Plan_de_Trabajo_Santa_Reyes.xlsx</c>
    /// v2.0 (18ago26), hojas <i>Plan diario</i>, <i>Cronograma</i>, <i>Carga por jornada</i>,
    /// <i>Alcance</i> y <i>Guías genéticas</i>: 100 h en 10 jornadas de 10 h, mié 19-ago-2026 →
    /// mar 1-sep-2026, 4 hitos, 29 actividades. Es el mismo entregable que declara
    /// <c>tracker_estado.md</c> V30.2. Las horas de cada tarea son la suma exacta de sus subtareas y
    /// el total da 100,00 h.
    ///
    /// <b>Los paquetes son 13, no 12.</b> La portada del Excel (y el bloque V30) dicen «12 paquetes»;
    /// la hoja <i>Cronograma</i> trae <b>13 códigos distintos</b> (F0…F12) — F0, que es transversal,
    /// quedó fuera de ese conteo. El seed usa los 13 que trae el dato. Las cifras que sí coinciden
    /// (29 actividades, 100 h, 10 jornadas de 10 h) se conservan intactas, y el texto del caso no
    /// menciona el número de paquetes para no contradecir el documento que ya tiene el cliente.
    ///
    /// <b>Registro del texto sembrado.</b> El solicitante ve el caso <i>y sus tareas</i>
    /// (<c>TicketTareaService.PuedeVerAsync</c> acepta al solicitante). Por eso las descripciones
    /// son las del <b>entregable comercial</b>, que presenta todo el alcance como trabajo por
    /// ejecutar. La §2 de <c>fase_de_desarrollo/santa_reyes_requerimientos_italapp_plan.md</c> —la
    /// auditoría de qué base ya existe en el repo— <b>no se copia a la base de datos</b>: es interna
    /// y V30.4 la declara no-exponible.
    ///
    /// <b>Estados.</b> El caso nace <c>ABIERTO</c> y las 42 tareas en <c>BACKLOG</c>, porque nada
    /// arrancó: V30.5 (aprobación del cliente) sigue sin darse y V30 declara la ejecución «sin
    /// arrancar». <c>fecha_primera_apertura</c> queda NULL — ningún resolutor lo tomó todavía.
    ///
    /// <b>Identidad y fail-open.</b> Nada se referencia por guid ni por id literal (difieren
    /// local↔prod): el administrador se resuelve <b>por email</b>; Lenin, por nombre o correo dentro
    /// de la empresa Santa Reyes y, si no está ahí, en cualquier empresa. Sin el administrador no se
    /// siembra nada (<c>RAISE NOTICE</c> + <c>RETURN</c>). <b>Sin Lenin el caso se siembra igual</b>,
    /// sin delegación y con aviso: perder el plan entero porque el nombre esté escrito distinto sería
    /// peor que dejar el solicitante para asignar en un clic desde la pantalla. Un seed no puede
    /// tumbar el arranque de la app (<c>Database__RunMigrations=true</c>).
    ///
    /// <b>Idempotencia.</b> Historia y caso se buscan por <c>titulo</c>; las tareas y subtareas por
    /// <c>codigo</c> (<c>HIS-2026-NNNN-Tn</c> y <c>HIS-2026-NNNN-Tn-Sm</c>, derivados del id real de
    /// la historia); la nota, por su texto. El código de la historia se completa <i>antes</i> de
    /// insertar las tareas: si quedara NULL, el <c>WHERE NOT EXISTS</c> compararía contra NULL y
    /// reinsertaría las 42 filas en cada corrida. Correrla dos veces no cambia una sola fila.
    ///
    /// <b>El <c>orden</c> va 0..41 dentro del caso</b>, contiguo y sin huecos: el tablero de tareas
    /// está scopeado por <c>ticket_id</c> y un orden con huecos o repetido deja las tarjetas
    /// barajadas en la próxima carga.
    ///
    /// <b>No manda ningún correo:</b> es SQL, no pasa por el servicio de notificación.
    /// <c>notificado_correo</c> queda en <c>false</c>.
    ///
    /// Plan: <c>fase_de_desarrollo/ticket_italjira_plan_italapp_santa_reyes_plan.md</c>.
    /// Migración DATA-ONLY: Designer clonado, ModelSnapshot intacto. El SQL vive en el partial
    /// <c>.Seed.cs</c> por tamaño.
    /// </remarks>
    public partial class SeedTicketPlanItalappSantaReyes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SEED_SQL);
        }

        /// <summary>
        /// Borra exactamente lo que sembró, en orden inverso a las FK: subtareas → tareas → nota →
        /// caso → historia, todo localizado por el mismo <c>codigo</c>/<c>titulo</c> que usó el
        /// <c>Up</c>. No toca ninguna otra historia, caso ni tarea.
        /// </summary>
        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DOWN_SQL);
        }
    }
}
