using Library.Models;
using Microsoft.EntityFrameworkCore;

namespace Library.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Book> Books { get; set; }
        public DbSet<BorrowRecord> BorrowRecords { get; set; }
        public DbSet<LateReturn> LateReturns { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<ApiKey> ApiKeys { get; set; }
        public DbSet<PendingBorrow> PendingBorrows { get; set; }
        public DbSet<EmailOtp> EmailOtps { get; set; }
        public DbSet<PendingReturn> PendingReturns { get; set; }
        public DbSet<UserLoginHistory> UserLoginHistories { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Book>()
                .Property(b => b.SerialNumber)
                .HasColumnType("NVARCHAR(100)")
                .UseCollation("SQL_Latin1_General_CP1_CS_AS");

            // ✅ Ensure UserType is consistent
            modelBuilder.Entity<User>()
                .Property(u => u.UserType)
                .HasColumnType("NVARCHAR(10)"); // "Student", "Lecturer", "Admin"

            modelBuilder.Entity<User>()
                .Property(u => u.Rating)
                .HasDefaultValue(5.0); // Only for students

            modelBuilder.Entity<User>()
                .Property(u => u.IsAdmin)
                .HasDefaultValue(false);

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // ✅ Ensure BorrowerId is consistent
            modelBuilder.Entity<BorrowRecord>()
                .Property(b => b.UserId)
                .HasColumnType("NVARCHAR(20)");

            // ✅ Define relationship between BorrowRecord and User
            modelBuilder.Entity<User>()
                .HasMany(u => u.BorrowRecords)
                .WithOne()
                .HasForeignKey(b => b.UserId)
                .HasPrincipalKey(u => u.UserId);

            

            base.OnModelCreating(modelBuilder);
        }
    }
}
