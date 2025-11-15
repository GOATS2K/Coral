namespace Coral.BulkExtensions.Internal;

internal record RelationshipRegistration(
    JunctionTableInfo JunctionInfo,
    Guid LeftKey,
    Guid RightKey);
