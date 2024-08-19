using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RabbitMQAdapter.Models;

namespace RabbitMQAdapterConsole.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<Consumer> Consumers { get; set; }

        private readonly string _connectionString;
        private readonly string _connectionString2;

        public AppDbContext(DbContextOptions<AppDbContext> options, IConfiguration configuration)
            : base(options)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            _connectionString2 = "Data Source = WIN-Q595KQ6H928\\SQLEXPRESS; Initial Catalog = RabbitMQAdapterDB; Trusted_Connection=True; TrustServerCertificate=True";
        }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(_connectionString2);
        }
    }
}
