using System;
using System.Collections.Generic;

namespace EFCoreHelper.Core.Models
{
    public class DatabaseProvider
    {
        public string Name { get; set; } = string.Empty;
        public string PackageName { get; set; } = string.Empty;
        public string DefaultConnectionStringTemplate { get; set; } = string.Empty;

        public static IReadOnlyList<DatabaseProvider> WellKnownProviders { get; } = new List<DatabaseProvider>
        {
            new DatabaseProvider
            {
                Name = "SQL Server",
                PackageName = "Microsoft.EntityFrameworkCore.SqlServer",
                DefaultConnectionStringTemplate = "Server=(localdb)\\mssqllocaldb;Database=MyDatabase;Trusted_Connection=True;MultipleActiveResultSets=true"
            },
            new DatabaseProvider
            {
                Name = "PostgreSQL (Npgsql)",
                PackageName = "Npgsql.EntityFrameworkCore.PostgreSQL",
                DefaultConnectionStringTemplate = "Host=localhost;Database=mydb;Username=postgres;Password=password"
            },
            new DatabaseProvider
            {
                Name = "SQLite",
                PackageName = "Microsoft.EntityFrameworkCore.Sqlite",
                DefaultConnectionStringTemplate = "Data Source=app.db"
            },
            new DatabaseProvider
            {
                Name = "MySQL / MariaDB (Pomelo)",
                PackageName = "Pomelo.EntityFrameworkCore.MySql",
                DefaultConnectionStringTemplate = "server=localhost;database=mydb;user=root;password=password"
            },
            new DatabaseProvider
            {
                Name = "Oracle",
                PackageName = "Oracle.EntityFrameworkCore",
                DefaultConnectionStringTemplate = "User Id=myuser;Password=mypassword;Data Source=localhost:1521/XEPDB1"
            },
            new DatabaseProvider
            {
                Name = "Azure Cosmos DB",
                PackageName = "Microsoft.EntityFrameworkCore.Cosmos",
                DefaultConnectionStringTemplate = "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==;Database=mydb"
            }
        };
    }
}
