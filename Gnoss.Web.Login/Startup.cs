using Es.Riam.AbstractsOpen;
using Es.Riam.Gnoss.AD.EntityModel;
using Es.Riam.Gnoss.AD.EntityModelBASE;
using Es.Riam.Gnoss.AD.Usuarios;
using Es.Riam.Gnoss.AD.Virtuoso;
using Es.Riam.Gnoss.CL;
using Es.Riam.Gnoss.CL.RelatedVirtuoso;
using Es.Riam.Gnoss.Util.Configuracion;
using Es.Riam.Gnoss.Util.General;
using Es.Riam.Gnoss.Util.Seguridad;
using Es.Riam.Gnoss.UtilServiciosWeb;
using Es.Riam.Interfaces.InterfacesOpen;
using Es.Riam.Open;
using Es.Riam.OpenReplication;
using Es.Riam.Util;
using Gnoss.Web.Login;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Gnoss.Web.Login
{
    public class Startup
    {
        public Startup(IConfiguration configuration, Microsoft.AspNetCore.Hosting.IHostingEnvironment environment)
        {
            Configuration = configuration;
            mEnvironment = environment;
        }

        public IConfiguration Configuration { get; }
        public Microsoft.AspNetCore.Hosting.IHostingEnvironment mEnvironment { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
			ILoggerFactory loggerFactory =
			LoggerFactory.Create(builder =>
			{
				builder.AddConfiguration(Configuration.GetSection("Logging"));
				builder.AddSimpleConsole(options =>
				{
					options.IncludeScopes = true;
					options.SingleLine = true;
					options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
					options.UseUtcTimestamp = true;
				});
			});
			services.AddSingleton(loggerFactory);
			services.AddMvc();
            services.AddControllers();
            services.AddHttpContextAccessor();
            services.AddScoped(typeof(UtilTelemetry));
            services.AddScoped(typeof(Usuario));
            services.AddScoped(typeof(UtilPeticion));
            services.AddScoped(typeof(Conexion));
            services.AddScoped(typeof(UtilGeneral));
            services.AddScoped(typeof(LoggingService));
            services.AddScoped(typeof(RedisCacheWrapper));
            services.AddScoped(typeof(Configuracion));
            services.AddScoped(typeof(GnossCache));
            services.AddScoped(typeof(VirtuosoAD));
            services.AddScoped(typeof(UtilServicios));
            services.AddScoped<IServicesUtilVirtuosoAndReplication, ServicesVirtuosoAndBidirectionalReplicationOpen>();
            services.AddScoped(typeof(RelatedVirtuosoCL));
            services.AddScoped<IAvailableServices, AvailableServicesOpen>();
            services.AddCors(options =>
            {
                options.AddPolicy(name: "_myAllowSpecificOrigins",
                builder =>
                {
                    builder.SetIsOriginAllowed(UtilServicios.ComprobarDominioPermitidoCORS);
                    builder.AllowAnyHeader();
                    builder.AllowAnyMethod();
                    builder.AllowCredentials();
                });
            });
			services.AddRazorPages().AddRazorRuntimeCompilation();
			services.AddControllersWithViews().AddRazorRuntimeCompilation();

            var assemblyOpen = Assembly.Load("Gnoss.Web.Login.Open");
            var externalControllerOpen = new AssemblyPart(assemblyOpen);
            // ApplicationPartManager
            services.AddControllers()
                    .ConfigureApplicationPartManager(apm =>
                    {
                        apm.ApplicationParts.Add(externalControllerOpen);
                    });

            string bdType = "";
            IDictionary environmentVariables = Environment.GetEnvironmentVariables();
            if (environmentVariables.Contains("connectionType"))
            {
                bdType = environmentVariables["connectionType"] as string;
            }
            else
            {
                bdType = Configuration.GetConnectionString("connectionType");
            }
            if (bdType.Equals("2") || bdType.Equals("1"))
            {
                services.AddScoped(typeof(DbContextOptions<EntityContext>));
                services.AddScoped(typeof(DbContextOptions<EntityContextBASE>));
            }
            services.AddSingleton(typeof(ConfigService));

            Conexion.ServicioWeb = true;
            string acid = "";
            if (environmentVariables.Contains("acid"))
            {
                acid = environmentVariables["acid"] as string;
            }
            else
            {
                acid = Configuration.GetConnectionString("acid");
            }
            string baseConnection = "";
            if (environmentVariables.Contains("base"))
            {
                baseConnection = environmentVariables["base"] as string;
            }
            else
            {
                baseConnection = Configuration.GetConnectionString("base");
            }
            if (bdType.Equals("0"))
            {
                services.AddDbContext<EntityContext>(options =>
                        options.UseSqlServer(acid, o => o.UseCompatibilityLevel(110))
                        );
                services.AddDbContext<EntityContextBASE>(options =>
                        options.UseSqlServer(baseConnection, o => o.UseCompatibilityLevel(110))

                        );
            }
			else if (bdType.Equals("1"))
			{
				services.AddDbContext<EntityContext, EntityContextOracle>(options =>
						options.UseOracle(acid)
						);
				services.AddDbContext<EntityContextBASE, EntityContextBASEOracle>(options =>
						options.UseOracle(baseConnection)
						);
			}
			else if (bdType.Equals("2"))
            {
                services.AddDbContext<EntityContext, EntityContextPostgres>(opt =>
                {
                    var builder = new NpgsqlDbContextOptionsBuilder(opt);
                    builder.SetPostgresVersion(new Version(9, 6));
                    opt.UseNpgsql(acid);

                });
                services.AddDbContext<EntityContextBASE, EntityContextBASEPostgres>(opt =>
                {
                    var builder = new NpgsqlDbContextOptionsBuilder(opt);
                    builder.SetPostgresVersion(new Version(9, 6));
                    opt.UseNpgsql(baseConnection);

                });
            }

            var sp = services.BuildServiceProvider();

            // Resolve the services from the service provider
            var configService = sp.GetService<ConfigService>();
            var entityContext = sp.GetService<EntityContext>();
            var servicesUtilVirtuosoAndReplication = sp.GetService<IServicesUtilVirtuosoAndReplication>();
			var loggingService = sp.GetService<LoggingService>();
			var redisCacheWrapper = sp.GetService<RedisCacheWrapper>();
            Dictionary<string, string> loginFacebook = ObtenerParametrosLoginExterno(TipoRedSocialLogin.Facebook, entityContext);
            if (loginFacebook.Count > 0)
            {
                services.AddAuthentication(options =>
                {
                    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                })
                    .AddCookie(options =>
                    {
                        options.LoginPath = "/login/loginfacebook"; // Must be lowercase
                    }).AddFacebook(options =>
                    {
                        options.CallbackPath = "/login/signin-facebook";
                        options.AppId = loginFacebook["id"];
                        options.AppSecret = loginFacebook["clientsecret"];
                        options.CorrelationCookie.Path = "/login/login/signin-facebook";

                    });

            }
            services.AddSession(options => {
				options.IdleTimeout = TimeSpan.FromMinutes(60); // Tiempo de expiración   
																//options.Cookie.Name = "AppTest";
																//options.Cookie.HttpOnly = true; // correct initialization

			});
			string configLogStash = configService.ObtenerLogStashConnection();
            if (!string.IsNullOrEmpty(configLogStash))
            {
                LoggingService.InicializarLogstash(configLogStash);
            }
            var entity = sp.GetService<EntityContext>();
            LoggingService.RUTA_DIRECTORIO_ERROR = Path.Combine(mEnvironment.ContentRootPath, "logs");

            EstablecerDominioCache(entity);

            UtilServicios.CargarIdiomasPlataforma(entityContext, loggingService, configService, servicesUtilVirtuosoAndReplication, redisCacheWrapper, loggerFactory);
            UtilServicios.CargarDominiosPermitidosCORS(entity);
			ConfigurarApplicationInsights(configService);

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Gnoss.Web.Login", Version = "v1" });
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("v1/swagger.json", "Gnoss.Web.Login v1"));
            }

            app.UseHttpsRedirection();

            app.UseRouting();
			app.UseCors("_myAllowSpecificOrigins");
			app.UseAuthentication();
			app.UseAuthorization();
			app.UseSession();
			app.UseGnossMiddleware();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }

        private Dictionary<string, string> ObtenerParametrosLoginExterno(TipoRedSocialLogin pTipoRedSocial, EntityContext pEntityContext)
        {
            string key = "login" + pTipoRedSocial.ToString();
            string parametros = "";
            if (pEntityContext.ParametroAplicacion.Any(parametro => parametro.Parametro.Equals(key)))
            {
                parametros = pEntityContext.ParametroAplicacion.Where(parametro => parametro.Parametro.Equals(key)).First().Valor;
            }


            string[] listaParametros = parametros.Split(new string[] { "@@@" }, StringSplitOptions.RemoveEmptyEntries);

            Dictionary<string, string> parametrosLogin = new Dictionary<string, string>();
            foreach (string param in listaParametros)
            {
                string clave = param.Split(new string[] { "|||" }, StringSplitOptions.RemoveEmptyEntries)[0];
                string valor = param.Split(new string[] { "|||" }, StringSplitOptions.RemoveEmptyEntries)[1];
                parametrosLogin.Add(clave, valor);
            }

            return parametrosLogin;
        }

        /// <summary>
        /// Establece el dominio de la cache.
        /// </summary>
        private void EstablecerDominioCache(EntityContext entity)
        {
            string dominio = entity.ParametroAplicacion.Where(parametroApp => parametroApp.Parametro.Equals("UrlIntragnoss")).FirstOrDefault().Valor;

            dominio = dominio.Replace("http://", "").Replace("https://", "").Replace("www.", "");

            if (dominio[dominio.Length - 1] == '/')
            {
                dominio = dominio.Substring(0, dominio.Length - 1);
            }

            BaseCL.DominioEstatico = dominio;
        }

        private void ConfigurarApplicationInsights(ConfigService configService)
        {
            string valor = configService.ObtenerImplementationKeyLogin();

            if (!string.IsNullOrEmpty(valor))
            {
                Microsoft.ApplicationInsights.Extensibility.TelemetryConfiguration.Active.InstrumentationKey = valor.ToLower();
            }

            if (UtilTelemetry.EstaConfiguradaTelemetria)
            {
                //Configuración de los logs

                string ubicacionLogs = configService.ObtenerUbicacionLogsLogin();

                int valorInt = 0;
                if (int.TryParse(ubicacionLogs, out valorInt))
                {
                    if (Enum.IsDefined(typeof(UtilTelemetry.UbicacionLogsYTrazas), valorInt))
                    {
                        LoggingService.UBICACIONLOGS = (UtilTelemetry.UbicacionLogsYTrazas)valorInt;
                    }
                }


                //Configuración de las trazas

                string ubicacionTrazas = configService.ObtenerUbicacionTrazasLogin();

                int valorInt2 = 0;
                if (int.TryParse(ubicacionTrazas, out valorInt2))
                {
                    if (Enum.IsDefined(typeof(UtilTelemetry.UbicacionLogsYTrazas), valorInt2))
                    {
                        LoggingService.UBICACIONTRAZA = (UtilTelemetry.UbicacionLogsYTrazas)valorInt2;
                    }
                }

            }

        }
    }
}
