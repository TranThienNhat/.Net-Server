using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;
using SHOPAPI.Models;

namespace SHOPAPI.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext() : base("name=DefaultConnection") 
        {
            this.Configuration.LazyLoadingEnabled = false;
            this.Configuration.ProxyCreationEnabled = false;
        }

        public DbSet<Product> Products { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<OrderItem> Items { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<Admin> admins { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            // Many-to-Many: Product <-> Category
            modelBuilder.Entity<Product>()
                .HasMany(p => p.Categories)
                .WithMany(c => c.Products)
                .Map(m =>
                {
                    m.ToTable("ProductCategories");
                    m.MapLeftKey("ProductId");
                    m.MapRightKey("CategoriId");
                });

            // One-to-Many: Order -> OrderItems
            modelBuilder.Entity<OrderItem>()
                .HasRequired(oi => oi.Order)
                .WithMany(o => o.OrderItems)
                .HasForeignKey(oi => oi.OrderId);

            // One-to-Many: Product -> OrderItems
            modelBuilder.Entity<OrderItem>()
                .HasRequired(oi => oi.Product)
                .WithMany(p => p.Items)
                .HasForeignKey(oi => oi.ProductId);

            base.OnModelCreating(modelBuilder);
        }
    }
}