using Microsoft.EntityFrameworkCore;
using RabbitMQAdapter.Models;

public class AppDbContext : DbContext
{
    public virtual DbSet<Profile> Profiles { get; set; }
    public virtual DbSet<Consumer> Consumers { get; set; }
    private readonly string _dbConnectionString;
    public AppDbContext() { }
    public AppDbContext(string connection)
    {
        _dbConnectionString = connection;
    }
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) => optionsBuilder.UseSqlServer("Data Source = WIN-Q595KQ6H928\\SQLEXPRESS; Initial Catalog = RabbitMQAdapterDB; Trusted_Connection=True; TrustServerCertificate=True").EnableDetailedErrors().EnableSensitiveDataLogging();//.LogTo(Console.WriteLine);
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Конфигурация для SetConsumer
        modelBuilder.Entity<Consumer>(entity =>
            {
                entity.HasKey(c => c.Id);

                entity.Property(c => c.ProfileName)
                      .IsRequired() 
                      .HasMaxLength(255); 

                entity.Property(c => c.ProfilePassword)
                      .IsRequired() 
                      .HasMaxLength(255); 

                entity.Property(c => c.CallbackUrl)
                      .IsRequired() 
                      .HasMaxLength(255); 

                entity.Property(c => c.ExchangeName)
                      .IsRequired() 
                      .HasMaxLength(255);

                entity.Property(c => c.ConnectionInfo)
                      .IsRequired() 
                      .HasMaxLength(255); 
            }
        );
            
        // Конфигурация для Profile
        modelBuilder.Entity<Profile>(entity =>
        {
            entity.HasKey(c => c.Id);

            entity.Property(c => c.Name)
                  .IsRequired() 
                  .HasMaxLength(255); 

            entity.Property(c => c.AccessPassword)
                  .IsRequired() 
                  .HasMaxLength(255); 

            entity.Property(c => c.Info)
                  .IsRequired() 
                  .HasMaxLength(255); 
        }
        );
    }
}
