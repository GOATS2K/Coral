using Microsoft.AspNetCore.SignalR;

namespace Coral.Api;

public interface ILibraryHubClient
{
    Task LibraryScanProgress(ScanProgressDto progress);
}

public class LibraryHub : Hub<ILibraryHubClient>
{
}
