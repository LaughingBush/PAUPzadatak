using System.Data.Entity;

namespace PAUPzadatak.Models
{
    public class BankingContext : DbContext
    {
        public BankingContext() : base("name=BankingConnection")
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<TransactionViewModel> Transactions { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            // Configure User entity
            modelBuilder.Entity<User>()
                .Property(u => u.Balance)
                .HasPrecision(18, 2);

            // Configure Transaction entity
            modelBuilder.Entity<TransactionViewModel>()
                .Property(t => t.Amount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<TransactionViewModel>()
                .Property(t => t.BalanceAfterTransaction)
                .HasPrecision(18, 2);

            base.OnModelCreating(modelBuilder);
        }
    }
} 