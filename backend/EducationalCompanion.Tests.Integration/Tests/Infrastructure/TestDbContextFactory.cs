using System;
using EducationalCompanion.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EducationalCompanion.Tests.Integration;

public static class TestDbContextFactory
{
    public static (SqliteConnection Connection, ApplicationDbContext Context) CreateContext()
    {
        // SQLite in-memory requires the connection to stay open for the lifetime of the context.
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new ApplicationDbContext(options);
        context.Database.EnsureCreated();

        return (connection, context);
    }
}

