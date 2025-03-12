using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using AgData.Data;
using AgData.Data.Repositories;
using AgData.DTOs;
using AgData.Services;
using AgData.Validators;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register database services
builder.Services.AddSingleton<IDbConnectionFactory>(provider =>
    new SqliteConnectionFactory(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=database.db"));

// Register repositories
builder.Services.AddScoped<IUserRepository, UserRepository>();

// Register business services
builder.Services.AddScoped<IUserService, UserService>();

// Register validators
builder.Services.AddScoped<IValidator<CreateUserDto>, CreateUserDtoValidator>();
builder.Services.AddScoped<IValidator<UpdateUserDto>, UpdateUserDtoValidator>();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularApp", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

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
            Email TEXT NOT NULL
        )
    ");
}).GetAwaiter().GetResult();

// Configure middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowAngularApp");
app.UseAuthorization();
app.MapControllers();

app.Run();