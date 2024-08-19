using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQAdapterConsole.Data;
using RabbitMQAdapterConsole.Services;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;


public class Program
{
    public static void Main(string[] args)
    {
        // Настройка Serilog
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug() // Уровень логирования
            .WriteTo.File("logs/log.txt", rollingInterval: RollingInterval.Day) // Запись в файл
            .CreateLogger();

        try
        {
            Log.Information("Starting up the application...");
            CreateHostBuilder(args).Build().Run();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application start-up failed.");
        }
        finally
        {
            Log.CloseAndFlush(); // Закрытие логгера
        }
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseSerilog() // Заменяем стандартный логгер на Serilog
            .ConfigureServices((hostContext, services) =>
            {
                // Регистрация AppDbContext с использованием SQL Server
                services.AddDbContext<AppDbContext>(options =>
                    options.UseSqlServer("Data Source = WIN-Q595KQ6H928\\SQLEXPRESS; Initial Catalog = RabbitMQAdapterDB; Trusted_Connection=True; TrustServerCertificate=True")); // Замените на вашу строку подключения
                services.AddHostedService<RabbitMqListener>(); // Регистрация вашего сервиса
            });
}
