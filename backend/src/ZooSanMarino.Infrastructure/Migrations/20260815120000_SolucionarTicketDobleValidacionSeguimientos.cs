using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Cierra el desarrollo de la <b>doble validación de los seguimientos diarios</b>: las 14 tareas y
    /// la historia pasan a <c>LISTO</c>, el caso a <c>SOLUCIONADO</c> con la descripción de la
    /// solución, y se imputan las horas trabajadas.
    /// </summary>
    /// <remarks>
    /// <b>SOLUCIONADO, no CERRADO.</b> El cierre lo hace el solicitante después de validar en pantalla
    /// — es la segunda mitad del flujo del módulo y no le corresponde a quien resolvió.
    ///
    /// <para>
    /// <b>Las horas imputadas son las estimadas.</b> No hay registro de tiempo real por tarea, así que
    /// inventar cifras distintas sería peor que usar la estimación: al menos esta se acordó por
    /// escrito antes de empezar. Queda dicho en la descripción del worklog para que nadie lo lea como
    /// una medición.
    /// </para>
    ///
    /// <para>
    /// Idempotente: cada UPDATE exige el estado anterior y el INSERT de horas va con
    /// <c>WHERE NOT EXISTS</c>. Correrla dos veces no cambia una sola fila la segunda vez. Migración
    /// DATA-ONLY: Designer clonado, ModelSnapshot intacto. Fail-open: si el caso no existe en el
    /// entorno (base sin el seed), no hace nada y la app arranca igual.
    /// </para>
    /// </remarks>
    public partial class SolucionarTicketDobleValidacionSeguimientos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DO $$
DECLARE
    v_historia  bigint;
    v_ticket    bigint;
    v_user_guid uuid;
    v_cedula    integer;
    v_solucion  text;
BEGIN
    SELECT id INTO v_historia FROM public.historias WHERE codigo = 'HIS-2026-0021';
    SELECT id, created_by_user_guid, created_by_user_id INTO v_ticket, v_user_guid, v_cedula
      FROM public.tickets WHERE titulo LIKE 'Doble validacion%' AND deleted_at IS NULL
      ORDER BY id DESC LIMIT 1;

    IF v_historia IS NULL OR v_ticket IS NULL THEN
        RAISE NOTICE 'Doble validacion: no existe la historia o el caso en este entorno; omitido.';
        RETURN;
    END IF;

    v_solucion :=
'Entregado y verificado con el flag ENCENDIDO en la base local.

QUE HACE AHORA CADA ACCION (empresas con requiere_validacion_seguimiento_diario):
- Guardar: exige alimento en el bloque que corresponde (Mixto en engorde, hembras y/o machos en
  postura), rechaza el dia nuevo si el lote tiene registros vencidos sin validar, y SEPARA alimento
  y aves en vez de descontarlos.
- Editar: reescribe la separacion. Sin calculos de retorno, porque nunca se descontó.
- Eliminar: libera la separacion. Nada que restituir.
- Validar: en una transaccion aplica el consumo al inventario, descuenta las aves y cierra el
  registro a edicion.
- Des-validar: devuelve alimento y aves. Permiso propio, porque mueve unidades ya descontadas.

Ademas: columna Estado con badge Validado / Pendiente / En retraso, fila roja y alarma sobre los
vencidos, modal rojo al entrar al lote, y el motivo del rechazo en MODAL (antes varios 400 se
perdian en un toast, que fue el reporte original).

Reproductora quedo unificada: su confirmado ES la doble validacion y sigue disparando el cruce.

VERIFICACION
- dotnet build 0 errores; 2574 tests en verde.
- yarn build sin errores de TypeScript ni de plantilla.
- Gate multipais (verificar_paridad_saldo_engorde.sql): 0 en TODAS las columnas para ItalcolEcuador
  (5217 filas) e ItalcolPanama (1034).
- Smoke de 11 pasos con el flag encendido sobre el lote 206 / galpon G0480: guardar no movio el
  stock (7444 kg intacto) ni las aves; validar descontó exactamente 550 kg y 6H/3M; des-validar los
  devolvio; y la base quedo identica al baseline.
- El smoke encontro 2 bugs, ya corregidos: las bajas de Panama se reservaban como mixtas (habia un
  solo parametro decidiendo el mensaje y el bucket de aves), y el mensaje de bloqueo no concordaba
  en singular.

PENDIENTE DE OPERACION: el flag nace apagado en las 5 empresas. Encenderlo es una decision suya,
empresa por empresa. Con el apagado el comportamiento es exactamente el anterior.';

    -- 1) Tareas → LISTO
    UPDATE public.ticket_tareas
       SET estado = 'LISTO',
           fecha_fin_real = timezone('utc', now()),
           fecha_inicio_real = COALESCE(fecha_inicio_real, timezone('utc', now())),
           updated_by_user_id = v_cedula,
           updated_at = timezone('utc', now())
     WHERE historia_id = v_historia AND estado <> 'LISTO';

    -- 2) Historia → LISTO
    UPDATE public.historias
       SET estado = 'LISTO',
           fecha_fin_real = timezone('utc', now()),
           fecha_inicio_real = COALESCE(fecha_inicio_real, timezone('utc', now())),
           updated_by_user_id = v_cedula,
           updated_at = timezone('utc', now())
     WHERE id = v_historia AND estado <> 'LISTO';

    -- 3) Horas por tarea. La cifra es la ESTIMADA (no hay medicion real) y asi queda dicho.
    INSERT INTO public.ticket_tiempos (tarea_id, user_guid, user_id, fecha, horas, descripcion, created_at)
    SELECT t.id, v_user_guid, v_cedula, current_date, t.horas_estimadas,
           'Trabajo completado. Horas segun la estimacion acordada al abrir el caso (no hay medicion real por tarea).',
           timezone('utc', now())
      FROM public.ticket_tareas t
     WHERE t.historia_id = v_historia
       AND t.horas_estimadas IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM public.ticket_tiempos tt WHERE tt.tarea_id = t.id);

    -- 4) Caso → SOLUCIONADO. El CIERRE lo hace el solicitante.
    UPDATE public.tickets
       SET estado = 'SOLUCIONADO',
           fecha_solucion = timezone('utc', now()),
           fecha_primera_apertura = COALESCE(fecha_primera_apertura, timezone('utc', now())),
           solucion_descripcion = v_solucion,
           updated_by_user_id = v_cedula,
           updated_at = timezone('utc', now())
     WHERE id = v_ticket AND estado <> 'SOLUCIONADO';

    RAISE NOTICE 'Doble validacion: caso % SOLUCIONADO, historia % LISTO.', v_ticket, v_historia;
END $$;
");
        }

        /// <inheritdoc />
        /// <remarks>Devuelve el caso a EN_IMPLEMENTACION, la historia y las tareas a EN_CURSO, y borra los worklogs.</remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DO $$
DECLARE
    v_historia bigint;
BEGIN
    SELECT id INTO v_historia FROM public.historias WHERE codigo = 'HIS-2026-0021';
    IF v_historia IS NULL THEN RETURN; END IF;

    DELETE FROM public.ticket_tiempos
     WHERE tarea_id IN (SELECT id FROM public.ticket_tareas WHERE historia_id = v_historia);

    UPDATE public.ticket_tareas SET estado = 'EN_CURSO', fecha_fin_real = NULL WHERE historia_id = v_historia;
    UPDATE public.historias     SET estado = 'EN_CURSO', fecha_fin_real = NULL WHERE id = v_historia;
    UPDATE public.tickets
       SET estado = 'EN_IMPLEMENTACION', fecha_solucion = NULL, solucion_descripcion = NULL
     WHERE titulo LIKE 'Doble validacion%' AND deleted_at IS NULL;
END $$;
");
        }
    }
}
