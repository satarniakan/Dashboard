// Dashboard.Infrastructure/Data/AppDbContext.cs
using Microsoft.EntityFrameworkCore;
using Dashboard.Domain.Entities;

namespace Dashboard.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<Product> Products => Set<Product>();
}