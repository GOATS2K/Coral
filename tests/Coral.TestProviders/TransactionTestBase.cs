using Coral.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace Coral.TestProviders;

/// <summary>
/// Base class for tests that use transaction-based isolation.
/// Each test gets its own DbContext instance and runs in its own transaction
/// that gets rolled back after the test completes.
/// All tests share the same database container (via collection fixture).
/// </summary>
[Collection(nameof(DatabaseCollection))]
public abstract class TransactionTestBase : IAsyncLifetime
{
    protected DatabaseFixture Fixture { get; }
    protected TestDatabase TestDatabase { get; private set; } = null!;

    private IDbContextTransaction? _transaction;

    protected TransactionTestBase(DatabaseFixture fixture)
    {
        Fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        var connectionString = Fixture.TestDb.Context.Database.GetConnectionString();
        TestDatabase = new TestDatabase(opt =>
        {
            opt.UseNpgsql(connectionString, p => p.UseVector());
        });
        _transaction = await TestDatabase.Context.Database.BeginTransactionAsync();
    }

    public async Task DisposeAsync()
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync();
            await _transaction.DisposeAsync();
        }
        TestDatabase.Context.ChangeTracker.Clear();
        TestDatabase.Dispose();
    }
}
