using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using AgData.Data;
using AgData.Data.Repositories;
using AgData.DTOs;
using AgData.Services;
using AgData.Validators;
using Dapper;
using System.Threading.Tasks;
using System.Security.Cryptography; // Add this for manual hash calculation
using System;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register database services
builder.Services.AddSingleton<IDbConnectionFactory>(provider =>
    new SqliteConnectionFactory(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=database.db"));

// Register repositories - properly with logger
builder.Services.AddScoped<IUserRepository>(provider =>
    new UserRepository(
        provider.GetRequiredService<IDbConnectionFactory>(),
        provider.GetRequiredService<ILogger<UserRepository>>()
    ));

// Make sure IPostRepository is registered properly
builder.Services.AddScoped<IPostRepository, PostRepository>();

// Register business services
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IEmailService, MailtrapEmailService>();
builder.Services.AddScoped<IPostService, PostService>();

// Register validators
builder.Services.AddScoped<IValidator<CreateUserDto>, CreateUserDtoValidator>();
builder.Services.AddScoped<IValidator<UpdateUserDto>, UpdateUserDtoValidator>();
builder.Services.AddScoped<IValidator<SetPasswordDto>, SetPasswordDtoValidator>();
builder.Services.AddScoped<IValidator<CreatePostDto>, CreatePostDtoValidator>();
builder.Services.AddScoped<IValidator<UpdatePostDto>, UpdatePostDtoValidator>();

// Configure JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? "bN9vE3xP7kR2mZ5sA4dT8qW6yF1uG0jL3cX9pM7nB2vK5hJ8tR4wA6dS9fG0lQ3x";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "agdata";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "agdataapi";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    };
});

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularApp", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Helper function to calculate hash the same way UserService does
string HashPassword(string password)
{
    using var sha256 = SHA256.Create();
    var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
    return Convert.ToBase64String(hashedBytes);
}

// Now that app is declared, we can initialize the database
// We need to make this section async since we're using await
Task.Run(async () =>
{
    using var scope = app.Services.CreateScope();
    var dbFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
    using var connection = await dbFactory.CreateConnectionAsync();

    // Create the Users table if it doesn't exist
    await connection.ExecuteAsync(@"
        CREATE TABLE IF NOT EXISTS Users (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Username TEXT NOT NULL,
            Email TEXT NOT NULL,
            PasswordHash TEXT,
            PasswordResetToken TEXT,
            PasswordResetTokenExpiry DATETIME
        )
    ");

    // Create the NotificationPreferences table if it doesn't exist
    await connection.ExecuteAsync(@"
        CREATE TABLE IF NOT EXISTS NotificationPreferences (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            UserId INTEGER NOT NULL,
            EmailEnabled BOOLEAN NOT NULL DEFAULT 1,
            SlackEnabled BOOLEAN NOT NULL DEFAULT 0,
            SlackWebhookUrl TEXT,
            FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
        )
    ");

    // Create the Posts table if it doesn't exist
    await connection.ExecuteAsync(@"
        CREATE TABLE IF NOT EXISTS Posts (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            UserId INTEGER NOT NULL,
            Title TEXT NOT NULL,
            Content TEXT NOT NULL,
            CreatedAt DATETIME NOT NULL,
            UpdatedAt DATETIME,
            FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
        )
    ");

    // Generate password hashes directly
    string adminPass = "Admin123!";
    string userPass = "User123!";

    string adminHash = HashPassword(adminPass);
    string userHash = HashPassword(userPass);

    Console.WriteLine($"Admin password: {adminPass}");
    Console.WriteLine($"Admin password hash: {adminHash}");
    Console.WriteLine($"User password: {userPass}");
    Console.WriteLine($"User password hash: {userHash}");

    // First clear existing users for clean slate
    await connection.ExecuteAsync("DELETE FROM NotificationPreferences");
    await connection.ExecuteAsync("DELETE FROM Users");

    // Create admin user with explicit password hash
    int adminId = await connection.ExecuteScalarAsync<int>(@"
        INSERT INTO Users (Username, Email, PasswordHash)
        VALUES (@Username, @Email, @PasswordHash);
        SELECT last_insert_rowid()",
        new {
            Username = "admin",
            Email = "admin@admin.com",
            PasswordHash = adminHash
        });

    await connection.ExecuteAsync(@"
        INSERT INTO NotificationPreferences (UserId, EmailEnabled, SlackEnabled)
        VALUES (@UserId, 1, 0)",
        new { UserId = adminId });

    Console.WriteLine($"Admin user created with ID: {adminId}");

    // Create regular user with explicit password hash
    int userId = await connection.ExecuteScalarAsync<int>(@"
        INSERT INTO Users (Username, Email, PasswordHash)
        VALUES (@Username, @Email, @PasswordHash);
        SELECT last_insert_rowid()",
        new {
            Username = "user",
            Email = "user@example.com",
            PasswordHash = userHash
        });

    await connection.ExecuteAsync(@"
        INSERT INTO NotificationPreferences (UserId, EmailEnabled, SlackEnabled)
        VALUES (@UserId, 1, 0)",
        new { UserId = userId });

    Console.WriteLine($"Regular user created with ID: {userId}");

    // Verify users and their password hashes
    var users = await connection.QueryAsync<dynamic>(@"
        SELECT Id, Username, Email, PasswordHash
        FROM Users");

    Console.WriteLine("\nUsers in database after seeding:");
    foreach (var user in users)
    {
        Console.WriteLine($"ID: {user.Id}, Username: {user.Username}, Email: {user.Email}");
        Console.WriteLine($"PasswordHash: {user.PasswordHash}");
        Console.WriteLine($"Hash is null or empty: {string.IsNullOrEmpty(user.PasswordHash)}");
        Console.WriteLine();
    }
}).GetAwaiter().GetResult();

// Configure middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowAngularApp");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();