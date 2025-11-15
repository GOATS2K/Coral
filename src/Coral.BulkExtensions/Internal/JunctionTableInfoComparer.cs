namespace Coral.BulkExtensions.Internal;

internal class JunctionTableInfoComparer : IEqualityComparer<JunctionTableInfo>
{
    public bool Equals(JunctionTableInfo? x, JunctionTableInfo? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (x is null || y is null) return false;

        return x.TableName == y.TableName &&
               x.Schema == y.Schema &&
               x.LeftColumnName == y.LeftColumnName &&
               x.RightColumnName == y.RightColumnName;
    }

    public int GetHashCode(JunctionTableInfo obj)
    {
        return HashCode.Combine(
            obj.TableName,
            obj.Schema,
            obj.LeftColumnName,
            obj.RightColumnName);
    }
}
