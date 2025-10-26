using Coral.TestProviders;
using Xunit;

namespace Coral.Dto.Tests;

[CollectionDefinition(nameof(DatabaseCollection))]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
}
