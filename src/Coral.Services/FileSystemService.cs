namespace Coral.Services
{

    public interface IFileSystemService
    {
        public List<string> GetDirectoriesInPath(string path);
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
    }
}
