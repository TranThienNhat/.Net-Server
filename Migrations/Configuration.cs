namespace API.Migrations
{
    using System;
    using System.Data.Entity;
    using System.Data.Entity.Migrations;
    using System.Linq;
    using Microsoft.AspNet.Identity;
    using SHOPAPI.Models.Enum;
    using SHOPAPI.Models;

    internal sealed class Configuration : DbMigrationsConfiguration<SHOPAPI.Data.AppDbContext>
    {
        public Configuration()
        {
            AutomaticMigrationsEnabled = false;
        }

        protected override void Seed(SHOPAPI.Data.AppDbContext context)
        {
            //  This method will be called after migrating to the latest version.

            //  You can use the DbSet<T>.AddOrUpdate() helper extension method
            //  to avoid creating duplicate seed data.
            if (!context.admins.Any())
            {
                var hasher = new PasswordHasher();
                var hashedPassword = hasher.HashPassword("123456");

                var admin = new Admin
                {
                    Username = "admin",
                    PasswordHash = hashedPassword,
                    Role = Role.ADMIN
                };

                context.admins.Add(admin);
                context.SaveChanges();
            }
        }
    }
}
