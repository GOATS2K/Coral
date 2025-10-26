using Coral.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace Coral.TestProviders;

/// <summary>
/// Base class for tests that use transaction-based isolation.
/// Each test runs in its own transaction that gets rolled back after the test completes.
/// This is much faster than spinning up a new database instance per test.
/// </summary>
public abstract class TransactionTestBase : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    protected DatabaseFixture Fixture { get; }
    protected TestDatabase TestDatabase => Fixture.TestDb;

    private IDbContextTransaction? _transaction;

    protected TransactionTestBase(DatabaseFixture fixture)
    {
        Fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        // Start a new transaction before each test
        _transaction = await Fixture.TestDb.Context.Database.BeginTransactionAsync();
    }

    public async Task DisposeAsync()
    {
        // Roll back the transaction after each test to ensure isolation
        if (_transaction != null)
        {
            await _transaction.RollbackAsync();
            await _transaction.DisposeAsync();
        }

        // Clear the change tracker to avoid issues with tracked entities
        Fixture.TestDb.Context.ChangeTracker.Clear();
    }
}
