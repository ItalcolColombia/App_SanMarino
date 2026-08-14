using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using static ZooSanMarino.Application.Calculos.DbStudioSqlCalculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Tests de la lógica PURA de DB Studio: clasificación de SQL, quoting de identificadores y armado de
/// DDL/literales para el backup completo descargable (ver plan
/// fase_de_desarrollo/db_studio_backup_descargable_plan.md).
/// </summary>
public class DbStudioSqlCalculosTests
{
    // ===================== Clasificación =====================

    [Theory]
    [InlineData("select * from foo", true)]
    [InlineData("  SELECT 1", true)]
    [InlineData("insert into foo values (1)", false)]
    [InlineData("select * from foo; drop table foo;", false)]
    public void IsPureSelect_DetectaSoloSelectSinEscritura(string sql, bool esperado)
        => Assert.Equal(esperado, IsPureSelect(sql));

    [Fact]
    public void Classify_SelectSimple_EsReadOnlySinConfirmacion()
    {
        var c = Classify("select * from foo");
        Assert.Equal(SqlKind.Select, c.Kind);
        Assert.True(c.IsReadOnly);
        Assert.False(c.RequiresConfirmation);
    }

    [Fact]
    public void Classify_DropTable_RequiereConfirmacion()
    {
        var c = Classify("drop table foo");
        Assert.Equal(SqlKind.Ddl, c.Kind);
        Assert.True(c.RequiresConfirmation);
    }

    [Fact]
    public void Classify_PgTerminateBackend_QuedaBloqueado()
    {
        var c = Classify("select pg_terminate_backend(123)");
        Assert.Equal(SqlKind.Dangerous, c.Kind);
        Assert.False(c.IsReadOnly);
    }

    // ===================== Identificadores =====================

    [Theory]
    [InlineData("public", true)]
    [InlineData("seguimiento_diario", true)]
    [InlineData("1invalido", false)]
    [InlineData("con-guion", false)]
    [InlineData("", false)]
    public void IsValidIdentifier_ValidaFormato(string ident, bool esperado)
        => Assert.Equal(esperado, IsValidIdentifier(ident));

    [Fact]
    public void QuoteIdent_EscapaComillasDobles()
        => Assert.Equal("\"foo\"\"bar\"", QuoteIdent("foo\"bar"));

    // ===================== SqlLiteral (usado por export de tabla y backup completo) =====================

    [Fact]
    public void SqlLiteral_Null_DevuelveNull()
        => Assert.Equal("NULL", SqlLiteral(null));

    [Theory]
    [InlineData(true, "true")]
    [InlineData(false, "false")]
    public void SqlLiteral_Bool(bool valor, string esperado)
        => Assert.Equal(esperado, SqlLiteral(valor));

    [Fact]
    public void SqlLiteral_String_EscapaComillaSimple()
        => Assert.Equal("'O''Higgins'", SqlLiteral("O'Higgins"));

    [Fact]
    public void SqlLiteral_Numero_SinComillas()
        => Assert.Equal("42", SqlLiteral(42));

    [Fact]
    public void SqlLiteral_ByteArrayVacio_DevuelveLiteralHexVacio()
        => Assert.Equal("'\\x'", SqlLiteral(Array.Empty<byte>()));

    [Fact]
    public void SqlLiteral_ByteArray_DevuelveLiteralHex()
        => Assert.Equal("'\\xDEADBEEF'", SqlLiteral(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF }));

    [Fact]
    public void SqlLiteral_ArrayDeTexto_DevuelveArrayLiteral()
        => Assert.Equal("ARRAY['a', 'b''c']", SqlLiteral(new[] { "a", "b'c" }));

    [Fact]
    public void SqlLiteral_ArrayDeEnteros_DevuelveArrayLiteral()
        => Assert.Equal("ARRAY[1, 2, 3]", SqlLiteral(new[] { 1, 2, 3 }));

    [Fact]
    public void SqlLiteral_ArrayVacioDeTexto_AgregaCastExplicito()
        => Assert.Equal("ARRAY[]::text[]", SqlLiteral(Array.Empty<string>()));

    [Fact]
    public void SqlLiteral_ArrayVacioDeEnteros_AgregaCastAIntegerArray()
        => Assert.Equal("ARRAY[]::integer[]", SqlLiteral(Array.Empty<int>()));

    // ===================== IsAutoIncrementColumn =====================

    [Fact]
    public void IsAutoIncrementColumn_Identity_EsTrue()
        => Assert.True(IsAutoIncrementColumn(new ColumnDto { Name = "id", IsIdentity = true }));

    [Fact]
    public void IsAutoIncrementColumn_DefaultNextval_EsTrue()
        => Assert.True(IsAutoIncrementColumn(new ColumnDto { Name = "id", Default = "nextval('foo_id_seq'::regclass)" }));

    [Fact]
    public void IsAutoIncrementColumn_ColumnaComun_EsFalse()
        => Assert.False(IsAutoIncrementColumn(new ColumnDto { Name = "nombre", Default = null }));

    // ===================== BuildCreateTableSql =====================

    [Fact]
    public void BuildCreateTableSql_ExportSchema_NoIncludeIdentity_PreservaComportamientoPreexistente()
    {
        var cols = new List<ColumnDto>
        {
            new() { Name = "id", DataType = "integer", IsNullable = false, IsPrimaryKey = true, IsIdentity = true },
            new() { Name = "nombre", DataType = "text", IsNullable = true }
        };

        var sql = BuildCreateTableSql("public", "foo", cols, ifNotExists: false, includeIdentity: false);

        Assert.Contains("CREATE TABLE \"public\".\"foo\" (", sql);
        Assert.DoesNotContain("GENERATED", sql); // includeIdentity=false: no debe agregar la cláusula
        Assert.Contains("PRIMARY KEY (\"id\")", sql);
    }

    [Fact]
    public void BuildCreateTableSql_Backup_IncludeIdentity_AgregaGeneratedByDefault()
    {
        var cols = new List<ColumnDto>
        {
            new() { Name = "id", DataType = "integer", IsNullable = false, IsPrimaryKey = true, IsIdentity = true }
        };

        var sql = BuildCreateTableSql("public", "foo", cols, ifNotExists: true, includeIdentity: true);

        Assert.Contains("CREATE TABLE IF NOT EXISTS", sql);
        Assert.Contains("GENERATED BY DEFAULT AS IDENTITY", sql);
    }

    // ===================== MakeCreateIndexIdempotent / BuildSetvalSql =====================

    [Fact]
    public void MakeCreateIndexIdempotent_Simple_AgregaIfNotExists()
    {
        var sql = MakeCreateIndexIdempotent("CREATE INDEX ix_foo_codigo ON public.foo USING btree (codigo)");
        Assert.Equal("CREATE INDEX IF NOT EXISTS ix_foo_codigo ON public.foo USING btree (codigo)", sql);
    }

    [Fact]
    public void MakeCreateIndexIdempotent_Unique_PreservaUniqueYAgregaIfNotExists()
    {
        var sql = MakeCreateIndexIdempotent("CREATE UNIQUE INDEX ux_foo_codigo ON public.foo USING btree (codigo)");
        Assert.Equal("CREATE UNIQUE INDEX IF NOT EXISTS ux_foo_codigo ON public.foo USING btree (codigo)", sql);
    }

    [Fact]
    public void MakeCreateIndexIdempotent_PreservaIndicesParciales()
    {
        // pg_get_indexdef ya trae el WHERE de un índice parcial: reconstruirlo a mano lo perdería.
        const string original = "CREATE UNIQUE INDEX ix_master_lists_key_null ON public.master_lists USING btree (key) WHERE (deleted_at IS NULL)";
        Assert.Contains("WHERE (deleted_at IS NULL)", MakeCreateIndexIdempotent(original));
    }

    [Fact]
    public void BuildSetvalSql_UsaPgGetSerialSequence_ConGuardaNullSafe()
    {
        var sql = BuildSetvalSql("public", "foo", "id");
        Assert.Contains("pg_get_serial_sequence('public.foo', 'id')", sql);
        Assert.Contains("IS NOT NULL;", sql);
    }

    // ===================== Orden de rutinas del backup =====================
    //
    // El backup emitía las funciones por OID (orden de creación) y el restore fallaba con 42883:
    // fn_seguimiento_diario_engorde se recreó (DROP+CREATE, obligatorio al cambiarle el RETURNS TABLE),
    // le tocó un OID nuevo y quedó DESPUÉS de sus 4 llamadores LANGUAGE sql, que se validan al crearse.
    // Ver plan: fase_de_desarrollo/db_studio_backup_orden_funciones_plan.md.

    private static RoutineDef Fn(long oid, string name, string cuerpo = "") =>
        new(oid, name, $"CREATE OR REPLACE FUNCTION public.{name}(p_lote_id integer)\nAS $function$ {cuerpo} $function$");

    private static string[] Nombres(IReadOnlyList<RoutineDef> rs) => rs.Select(r => r.Name).ToArray();

    [Fact]
    public void OrdenarRutinas_CalleeRecreadoConOidMayor_SaleAntesQueSusLlamadores()
    {
        // El caso real: el callee tiene el OID MÁS ALTO y aun así tiene que ir primero.
        var rutinas = new List<RoutineDef>
        {
            Fn(100, "fn_reporte_indicadores_panama",   "FROM fn_seguimiento_diario_engorde(p_lote_id) f"),
            Fn(200, "fn_cuadre_alimento_engorde",      "FROM fn_seguimiento_diario_engorde(a.lote_id) f"),
            Fn(900, "fn_seguimiento_diario_engorde",   "SELECT 1")
        };

        var orden = Nombres(OrdenarRutinasPorDependencia(rutinas));

        Assert.Equal("fn_seguimiento_diario_engorde", orden[0]);
        Assert.Equal(3, orden.Length);
    }

    [Fact]
    public void OrdenarRutinas_SinDependencias_ConservaElOrdenDeOid()
    {
        // No reordenar porque sí: sin aristas, el resultado es el de creación, como siempre.
        var rutinas = new List<RoutineDef> { Fn(30, "fn_c"), Fn(10, "fn_a"), Fn(20, "fn_b") };
        Assert.Equal(new[] { "fn_a", "fn_b", "fn_c" }, Nombres(OrdenarRutinasPorDependencia(rutinas)));
    }

    [Fact]
    public void OrdenarRutinas_CadenaDeclaradaAlReves_QuedaEnOrdenDeDependencia()
    {
        var rutinas = new List<RoutineDef>
        {
            Fn(10, "fn_a", "select fn_b(1)"),
            Fn(20, "fn_b", "select fn_c(1)"),
            Fn(30, "fn_c", "select 1")
        };
        Assert.Equal(new[] { "fn_c", "fn_b", "fn_a" }, Nombres(OrdenarRutinasPorDependencia(rutinas)));
    }

    [Fact]
    public void OrdenarRutinas_Recursiva_NoSeCuelgaNiSeDuplica()
    {
        // Una recursiva se esperaría a sí misma y nunca entraría a la cola.
        var rutinas = new List<RoutineDef> { Fn(10, "fn_rec", "select fn_rec(p_lote_id - 1)"), Fn(20, "fn_otra") };
        Assert.Equal(new[] { "fn_rec", "fn_otra" }, Nombres(OrdenarRutinasPorDependencia(rutinas)));
    }

    [Fact]
    public void OrdenarRutinas_CicloPorAristaFalsa_NoPierdeRutinas_YCaenAlFinalPorOid()
    {
        var rutinas = new List<RoutineDef>
        {
            Fn(10, "fn_ok"),
            Fn(30, "fn_y", "select fn_x(1)"),
            Fn(20, "fn_x", "select fn_y(1)")
        };

        var orden = Nombres(OrdenarRutinasPorDependencia(rutinas));

        Assert.Equal("fn_ok", orden[0]);
        Assert.Equal(new[] { "fn_x", "fn_y" }, orden.Skip(1).ToArray()); // degradación: por OID
    }

    [Fact]
    public void OrdenarRutinas_SiempreDevuelveUnaPermutacionExactaDeLaEntrada()
    {
        // Invariante fuerte: ninguna rutina se pierde ni se duplica, pase lo que pase con el grafo.
        var rutinas = new List<RoutineDef>
        {
            Fn(10, "fn_a", "select fn_b(1), fn_c(1)"),
            Fn(20, "fn_b", "select fn_c(1)"),
            Fn(30, "fn_c", "select fn_a(1)"),          // cierra un ciclo
            Fn(40, "fn_d", "select fn_inexistente(1)"), // llama a algo que no está en el lote
            Fn(50, "fn_e")
        };

        var orden = OrdenarRutinasPorDependencia(rutinas);

        Assert.Equal(rutinas.Count, orden.Count);
        Assert.Equal(rutinas.OrderBy(r => r.Oid).ToList(), orden.OrderBy(r => r.Oid).ToList());
    }

    [Fact]
    public void OrdenarRutinas_Overloads_MismoNombreDistintaFirma_SalenAmbosAntesDelLlamador()
    {
        var rutinas = new List<RoutineDef>
        {
            Fn(10, "fn_llama", "select fn_over(1)"),
            Fn(80, "fn_over", "select 1"),
            Fn(90, "fn_over", "select 2")
        };

        var orden = Nombres(OrdenarRutinasPorDependencia(rutinas));

        Assert.Equal("fn_llama", orden[2]);
        Assert.Equal(new[] { "fn_over", "fn_over" }, orden.Take(2).ToArray());
    }

    [Theory]
    // El paréntesis de apertura es lo que descarta los prefijos.
    [InlineData("select fn_cuadre_alimento_engorde(1)", "fn_cuadre", false)]
    [InlineData("select fn_cuadre(1)", "fn_cuadre", true)]
    // Calificado con esquema: el punto no rompe la frontera de palabra.
    [InlineData("CROSS JOIN LATERAL public.fn_x(lo.lote_id) d", "fn_x", true)]
    // Sufijo pegado a la izquierda: no es la misma función.
    [InlineData("select mi_fn_x(1)", "fn_x", false)]
    // Espacios entre el nombre y el paréntesis.
    [InlineData("select fn_x  (1)", "fn_x", true)]
    // Mencionada sin invocar (no hay paréntesis): no genera arista.
    [InlineData("-- se arma con la función fn_x, misma fuente", "fn_x", false)]
    public void RutinaInvocaA_RespetaFronterasDePalabra(string cuerpo, string nombre, bool esperado)
        => Assert.Equal(esperado, RutinaInvocaA(cuerpo, nombre));

    [Fact]
    public void OrdenarRutinas_MencionEnComentarioAdemasDeLaLlamada_NoRompeElOrden()
    {
        // Caso textual de fn_reporte_indicadores_panama: nombra la función en un comentario Y la invoca.
        var rutinas = new List<RoutineDef>
        {
            Fn(10, "fn_llama", "-- se arma con fn_dep (misma fuente)\nFROM fn_dep(p_lote_id) f"),
            Fn(20, "fn_dep", "select 1")
        };
        Assert.Equal(new[] { "fn_dep", "fn_llama" }, Nombres(OrdenarRutinasPorDependencia(rutinas)));
    }
}
