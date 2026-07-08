using Microsoft.EntityFrameworkCore;

namespace Watodoo.Shared.Data;

public sealed class WatodooDbContext(DbContextOptions<WatodooDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }
}
