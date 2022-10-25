using Xunit;

namespace Coral.Services.Tests;

public class IndexerServiceTests : IClassFixture<TestDatabase>
{
    private TestDatabase _testDatabase;
    private IIndexerService _indexerService;

    public IndexerServiceTests(TestDatabase testDatabase)
    {
        _testDatabase = testDatabase;
        _indexerService = new IndexerService(testDatabase.Context);
    }

    [Fact]
    public void EnsureTestFilesExist()
    {
        var testFilesDirectory = Path.Join(AppDomain.CurrentDomain.BaseDirectory, "Content");
        var testFiles = new DirectoryInfo(testFilesDirectory);
        
        Assert.NotEmpty(testFiles.GetFiles());
    }
}