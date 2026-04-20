using Es.Riam.Gnoss.Util.General;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Gnoss.Web.Login
{
    public class Program
    {
        private static Serilog.ILogger _startupLogger;
        public static void Main(string[] args)
        {
            _startupLogger = LoggingService.ConfigurarBasicStartupSerilog().CreateBootstrapLogger().ForContext<Program>();
            try
            {
                CreateHostBuilder(args).Build().Run();
            }
            catch (Exception ex)
            {
                _startupLogger.Fatal(ex, "Error fatal durante el arranque");
            }
            finally
            {
                (_startupLogger as IDisposable)?.Dispose();
                Log.CloseAndFlush(); // asegura que se escriben todos los logs pendientes
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((hostContext, config) =>
                {
                    LoggingService.ConfigurarSeguimientoFicheros(hostContext, config, _startupLogger);
                })
                .UseSerilog((context, services, configuration) => LoggingService.ConfigurarSerilog(context.Configuration, services, configuration))
                .ConfigureServices((context, services) =>
                {
                    LoggingService.SuscribirCambios(context, _startupLogger);
                    _startupLogger.Information("Suscripción a cambios de configuración registrada");
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                    AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
                });
    }
}
