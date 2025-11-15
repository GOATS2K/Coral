namespace Coral.BulkExtensions.Internal;

/// <summary>
/// Non-generic interface for entity cache to avoid reflection.
/// </summary>
internal interface IEntityCache
{
    System.Collections.IList GetNewEntitiesUntyped();
    void MarkAllAsExisting();
}
