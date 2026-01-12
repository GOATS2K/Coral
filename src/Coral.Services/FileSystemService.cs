namespace Coral.Services
{

    public interface IFileSystemService
    {
        public List<string> GetDirectoriesInPath(string path);
        public List<string> GetRootDirectories();
    }

    public class FileSystemService : IFileSystemService
    {
        public List<string> GetDirectoriesInPath(string path)
        {
            try
            {
                return new DirectoryInfo(path).EnumerateDirectories().Select(s => s.FullName).ToList();
            } catch (Exception)
            {
                return new List<string>();
            }
        }

        public List<string> GetRootDirectories()
        {
            if (OperatingSystem.IsWindows())
            {
                // Return all logical drives on Windows (C:\, D:\, etc.)
                // Trim trailing backslash for cleaner display
                return DriveInfo.GetDrives()
                    .Where(d => d.IsReady)
                    .Select(d => d.RootDirectory.FullName.TrimEnd(Path.DirectorySeparatorChar))
                    .ToList();
            }

            // Unix-like systems: return root directory
            return ["/"];
        }
    }
}
