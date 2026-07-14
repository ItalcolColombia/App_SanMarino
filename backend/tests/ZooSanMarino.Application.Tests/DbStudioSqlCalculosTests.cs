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

}
