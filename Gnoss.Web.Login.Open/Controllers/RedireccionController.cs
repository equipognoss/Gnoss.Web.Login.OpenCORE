using Es.Riam.AbstractsOpen;
using Es.Riam.Gnoss.AD.EntityModel;
using Es.Riam.Gnoss.AD.EntityModelBASE;
using Es.Riam.Gnoss.AD.Virtuoso;
using Es.Riam.Gnoss.CL;
using Es.Riam.Gnoss.Util.Configuracion;
using Es.Riam.Gnoss.Util.General;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Gnoss.Web.Login
{
    [Controller]
    [Route("[controller]")]
    [EnableCors("_myAllowSpecificOrigins")]
    public class RedireccionController : ControllerBaseLogin
    {
        private ILogger mlogger;
        private ILoggerFactory mLoggerFactory;
        public RedireccionController(LoggingService loggingService, IHttpContextAccessor httpContextAccessor, EntityContext entityContext, ConfigService configService, RedisCacheWrapper redisCacheWrapper, GnossCache gnossCache, IWebHostEnvironment env, EntityContextBASE entityContextBASE, IServicesUtilVirtuosoAndReplication servicesUtilVirtuosoAndReplication, ILogger<RedireccionController> logger, ILoggerFactory loggerFactory) : base(loggingService, httpContextAccessor, entityContext, configService, redisCacheWrapper, gnossCache, env, entityContextBASE, servicesUtilVirtuosoAndReplication, logger, loggerFactory)
        {
            mlogger = logger;
            mLoggerFactory = loggerFactory;
        }

        [HttpGet()]
        public ActionResult Index()
        {
            string urlInicio = mHttpContextAccessor.HttpContext.Request.Cookies["urlInicio"];

            return Redirect(urlInicio);
        }
    }
}
