using Microsoft.EntityFrameworkCore;
using PowerSentinel.Models;

namespace PowerSentinel.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Event> Events => Set<Event>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<Subscriber> Subscribers => Set<Subscriber>();
}
