using System;
using System.IO;
using System.Reflection;
using DbUp;

namespace PowerSentinel.Services;

public static class DbUpMigration
{
    public static void ApplyMigrations(string connectionString)
    {
        var upgradeEngine = DeployChanges.To
            .SqliteDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
            .LogToConsole()
            .Build();

        var result = upgradeEngine.PerformUpgrade();

        if (!result.Successful)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(result.Error);
            Console.ResetColor();
            throw new Exception("Database upgrade failed. See logs for details.", result.Error);
        }
    }
}
