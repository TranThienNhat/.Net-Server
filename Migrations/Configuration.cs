namespace API.Migrations
{
    using API.Models;
    using Microsoft.AspNet.Identity;
    using SHOPAPI.Models;
    using SHOPAPI.Models.Enum;
    using System;
    using System.Data.Entity;
    using System.Data.Entity.Migrations;
    using System.Linq;

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
            if (!context.Categories.Any())
            {
                context.Categories.AddOrUpdate(
                    c => c.Name,
                    new Category { Name = "Bàn" },
                    new Category { Name = "Ghế" },
                    new Category { Name = "Tủ" },
                    new Category { Name = "Trang trí" }
                );
                
            }

            if (!context.Users.Any(u => u.Username == "admin"))
            {
                var hasher = new PasswordHasher();
                var admin = new Users
                {
                    Username = "admin",
                    PasswordHash = hasher.HashPassword("admin"),
                    Role = Role.ADMIN,
                    Name = "Quản trị viên",
                };

                context.Users.Add(admin);
            }
            context.SaveChanges();
        }
    }
}
