using Coral.TestProviders;
using Xunit;

namespace Coral.Services.Tests;

[CollectionDefinition(nameof(DatabaseCollection))]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
}
