namespace Coral.BulkExtensions.Internal;

internal record JunctionTableInfo(
    string TableName,
    string? Schema,
    string LeftColumnName,
    string RightColumnName,
    Type LeftType,
    Type RightType);
