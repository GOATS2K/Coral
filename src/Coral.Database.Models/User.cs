namespace Coral.Database.Models;

public enum UserRole
{
    User = 0,
    Admin = 1
}

public class User : BaseTable
{
    public string Username { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public UserRole Role { get; set; } = UserRole.User;

    public List<Device> Devices { get; set; } = new();
}
