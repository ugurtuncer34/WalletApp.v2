using Microsoft.EntityFrameworkCore;
using WalletApp.Entities;

namespace WalletApp.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Transaction> Transactions { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<Currency> Currencies { get; set; }
    public DbSet<Tag> Tags { get; set; }
    public DbSet<Country> Countries { get; set; }
    public DbSet<Merchant> Merchants { get; set; }
    public DbSet<TransactionTag> TransactionTags { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder) // special options
    {
        base.OnModelCreating(modelBuilder); // first, initialize the base builder settings, then add my own below

        // Primary Key of TransactionTag is the unification of TrxId and TagId
        // Same tag cannot be added to a trx twice
        modelBuilder.Entity<TransactionTag>()
            .HasKey(tt => new { tt.TransactionId, tt.TagId });
        
        // Declaring relations just in case
        modelBuilder.Entity<TransactionTag>()
            .HasOne(tt => tt.Transaction)
            .WithMany(t => t.TransactionTags)
            .HasForeignKey(tt => tt.TransactionId);

        modelBuilder.Entity<TransactionTag>()
            .HasOne(tt => tt.Tag)
            .WithMany(t => t.TransactionTags)
            .HasForeignKey(tt => tt.TagId);
        
        // Codes should be unique
        modelBuilder.Entity<Currency>()
            .HasIndex(c => c.Code)
            .IsUnique();
    }
}