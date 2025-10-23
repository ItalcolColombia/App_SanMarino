namespace ZooSanMarino.Application.DTOs.DbStudio;

public record SchemaDto(string Name, long Tables);
public record TableDto(string Schema, string Name, string Kind, long Rows);
public record ColumnDto(string Name, string DataType, bool IsNullable, string? Default, bool IsPrimaryKey);

public record CreateTableDto(
  string Schema,
  string Table,
  List<NewColumnDto> Columns,
  List<string>? PrimaryKey,
  List<List<string>>? Uniques
);
public record NewColumnDto(string Name, string Type, bool Nullable, string? Default, string? Identity /* "always"|"by_default"|null */);

public record AddColumnDto(string Name, string Type, bool Nullable, string? Default);
public record AlterColumnDto(string? NewType, bool? SetNotNull, bool? DropNotNull, string? SetDefault, bool? DropDefault);

public record SelectQueryDto(string Sql, Dictionary<string, object?> Params, int Limit = 100, int Offset = 0);
public record QueryPageDto(List<Dictionary<string, object?>> Rows, int Count, int Limit, int Offset);
