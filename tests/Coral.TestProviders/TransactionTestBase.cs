using Coral.Database;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Coral.TestProviders;

/// <summary>
/// Base class for tests with isolated in-memory SQLite databases.
/// Each test gets its own fresh in-memory database that's disposed after the test.
/// </summary>
[Collection(nameof(DatabaseCollection))]
public abstract class TransactionTestBase : IAsyncLifetime
{
    protected DatabaseFixture Fixture { get; }
    protected TestDatabase TestDatabase { get; private set; } = null!;
    private Microsoft.Data.Sqlite.SqliteConnection? _connection;

    protected TransactionTestBase(DatabaseFixture fixture)
    {
        Fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        // Create a brand new in-memory database for this test
        _connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        TestDatabase = new TestDatabase(opt =>
        {
            opt.UseSqlite(_connection);
        });
    }

    public async Task DisposeAsync()
    {
        TestDatabase.Context.ChangeTracker.Clear();
        TestDatabase.Dispose();
        _connection?.Close();
        _connection?.Dispose();
        await Task.CompletedTask;
    }
}
