using Coral.TestProviders;
using Xunit;

namespace Coral.BulkExtensions.Tests;

[CollectionDefinition(nameof(DatabaseCollection))]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
}
