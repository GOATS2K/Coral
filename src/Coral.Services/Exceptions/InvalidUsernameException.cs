namespace Coral.Services.Exceptions;

public class InvalidUsernameException : Exception
{
    public InvalidUsernameException() : base("Invalid username") { }
}
