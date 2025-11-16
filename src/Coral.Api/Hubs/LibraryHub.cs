using Microsoft.AspNetCore.SignalR;

namespace Coral.Api;

public interface ILibraryHubClient
{
    Task LibraryScanProgress(ScanProgressDto progress);
}

public class LibraryHub : Hub<ILibraryHubClient>
{
    private readonly IScanReporter _scanReporter;

    public LibraryHub(IScanReporter scanReporter)
    {
        _scanReporter = scanReporter;
    }

    /// <summary>
    /// Get all active scans. Useful for clients reconnecting to recover scan state.
    /// </summary>
    public List<ScanJobProgress> GetActiveScans()
    {
        return _scanReporter.GetActiveScans();
    }
}
