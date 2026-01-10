using Coral.Database.Models;
using Coral.Services.Exceptions;
using Coral.TestProviders;
using Microsoft.AspNetCore.Identity;
using Xunit;

namespace Coral.Services.Tests;

public class UserServiceTests(DatabaseFixture fixture) : TransactionTestBase(fixture)
{
    private readonly IPasswordHasher<User> _passwordHasher = new PasswordHasher<User>();

    private IUserService UserService => new UserService(
        TestDatabase.Context,
        _passwordHasher);

    [Fact]
    public async Task CreateUserAsync_FirstUser_HasAdminRole()
    {
        // arrange
        var username = "admin";
        var password = "password123";

        // act
        var user = await UserService.CreateUserAsync(username, password);

        // assert
        Assert.NotNull(user);
        Assert.Equal(username, user.Username);
        Assert.Equal(UserRole.Admin, user.Role);
        Assert.NotEmpty(user.PasswordHash);
    }

    [Fact]
    public async Task CreateUserAsync_SecondUser_HasUserRole()
    {
        // arrange
        await UserService.CreateUserAsync("firstuser", "password123");

        // act
        var secondUser = await UserService.CreateUserAsync("seconduser", "password456");

        // assert
        Assert.NotNull(secondUser);
        Assert.Equal(UserRole.User, secondUser.Role);
    }

    [Fact]
    public async Task GetUserByIdAsync_ExistingUser_ReturnsUser()
    {
        // arrange
        var createdUser = await UserService.CreateUserAsync("testuser", "password123");

        // act
        var retrievedUser = await UserService.GetUserByIdAsync(createdUser.Id);

        // assert
        Assert.NotNull(retrievedUser);
        Assert.Equal(createdUser.Id, retrievedUser.Id);
        Assert.Equal(createdUser.Username, retrievedUser.Username);
    }

    [Fact]
    public async Task GetUserByIdAsync_NonExistentUser_ReturnsNull()
    {
        // arrange
        var nonExistentId = Guid.NewGuid();

        // act
        var user = await UserService.GetUserByIdAsync(nonExistentId);

        // assert
        Assert.Null(user);
    }

    [Fact]
    public async Task GetUserByUsernameAsync_ExistingUser_ReturnsUser()
    {
        // arrange
        var username = "findme";
        await UserService.CreateUserAsync(username, "password123");

        // act
        var user = await UserService.GetUserByUsernameAsync(username);

        // assert
        Assert.NotNull(user);
        Assert.Equal(username, user.Username);
    }

    [Fact]
    public async Task GetUserByUsernameAsync_NonExistentUser_ReturnsNull()
    {
        // arrange
        // act
        var user = await UserService.GetUserByUsernameAsync("nonexistent");

        // assert
        Assert.Null(user);
    }

    [Fact]
    public async Task IsFirstUserAsync_NoUsers_ReturnsTrue()
    {
        // arrange
        // act
        var isFirst = await UserService.IsFirstUserAsync();

        // assert
        Assert.True(isFirst);
    }

    [Fact]
    public async Task IsFirstUserAsync_HasUsers_ReturnsFalse()
    {
        // arrange
        await UserService.CreateUserAsync("existinguser", "password123");

        // act
        var isFirst = await UserService.IsFirstUserAsync();

        // assert
        Assert.False(isFirst);
    }

    [Fact]
    public async Task ValidatePassword_CorrectPassword_ReturnsSuccess()
    {
        // arrange
        var password = "correctpassword";
        var user = await UserService.CreateUserAsync("testuser", password);

        // act
        var result = UserService.ValidatePassword(user, password);

        // assert
        Assert.Equal(PasswordVerificationResult.Success, result);
    }

    [Fact]
    public async Task ValidatePassword_WrongPassword_ReturnsFailed()
    {
        // arrange
        var user = await UserService.CreateUserAsync("testuser", "correctpassword");

        // act
        var result = UserService.ValidatePassword(user, "wrongpassword");

        // assert
        Assert.Equal(PasswordVerificationResult.Failed, result);
    }

    [Theory]
    [InlineData("valid_user")]
    [InlineData("valid-user")]
    [InlineData("ValidUser123")]
    [InlineData("user")]
    [InlineData("a")]
    public async Task CreateUserAsync_ValidUsername_Succeeds(string username)
    {
        // arrange
        // act
        var user = await UserService.CreateUserAsync(username, "password123");

        // assert
        Assert.NotNull(user);
        Assert.Equal(username, user.Username);
    }

    [Theory]
    [InlineData("invalid user")]
    [InlineData("invalid@user")]
    [InlineData("invalid.user")]
    [InlineData("invalid!user")]
    [InlineData("")]
    public async Task CreateUserAsync_InvalidUsername_ThrowsInvalidUsernameException(string username)
    {
        // arrange
        // act & assert
        await Assert.ThrowsAsync<InvalidUsernameException>(
            () => UserService.CreateUserAsync(username, "password123"));
    }
}
