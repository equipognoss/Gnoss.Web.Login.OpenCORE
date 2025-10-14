using Es.Riam.AbstractsOpen;
using Es.Riam.Gnoss.AD.EntityModel;
using Es.Riam.Gnoss.AD.EntityModelBASE;
using Es.Riam.Gnoss.AD.Virtuoso;
using Es.Riam.Gnoss.CL;
using Es.Riam.Gnoss.Util.Configuracion;
using Es.Riam.Gnoss.Util.General;
using Es.Riam.Util;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Serilog.Core;
using System;
using System.Collections.Generic;
using System.Web;

namespace Gnoss.Web.Login
{
    [Controller]
    [Route("[controller]")]
    /// <summary>
    /// Página para crear la cookie del usuario actual que se acaba de loguear en un dominio
    /// </summary>
    public class CrearCookieController : ControllerBaseLogin
    {
        private ILogger mlogger;
        private ILoggerFactory mLoggerFactory;
        public CrearCookieController(LoggingService loggingService, IHttpContextAccessor httpContextAccessor, EntityContext entityContext, ConfigService configService, RedisCacheWrapper redisCacheWrapper, GnossCache gnossCache, VirtuosoAD virtuosoAD, IHostingEnvironment env, EntityContextBASE entityContextBASE, IServicesUtilVirtuosoAndReplication servicesUtilVirtuosoAndReplication, ILogger<CrearCookieController> logger, ILoggerFactory loggerFactory)
            : base(loggingService, httpContextAccessor, entityContext, configService, redisCacheWrapper, gnossCache, virtuosoAD, env, entityContextBASE, servicesUtilVirtuosoAndReplication, logger, loggerFactory)
        {
            mlogger = logger;
            mLoggerFactory = loggerFactory;
        }

        #region Métodos de eventos

        /// <summary>
        /// Método page load
        /// </summary>
        /// <param name="sender">sender</param>
        /// <param name="e">e</param>
        [HttpGet, HttpPost]
        public void Index()
        {
            if (!string.IsNullOrEmpty(Request.QueryString.Value))
            {
                string redirect = "";

                try
                {
                    //Hay que crear la cookie en este dominio
                    string query = Request.QueryString.Value.Substring(1);

                    Dictionary<string, string> hashQuery = UtilDominios.QueryStringToDictionary(query);

                    if (hashQuery["usuarioID"] != null)
                    {
                        //Obtengo el usuarioID a partir del parametro GET
                        string usuarioID = hashQuery["usuarioID"];
                        string idioma = hashQuery["idioma"];
                        string login = "";
                        Guid personaID = Guid.Empty;
                        string nombreCorto = "";

                        try
                        {
                            if (hashQuery.ContainsKey("personaID"))
                            {
                                personaID = new Guid(hashQuery["personaID"]);
                            }
                            if (hashQuery.ContainsKey("nombreCorto"))
                            {
                                nombreCorto = hashQuery["nombreCorto"];
                            }
                        }
                        catch (Exception) { }

                        //Login del usuario
                        if (hashQuery.ContainsKey("loginUsuario"))
                        {
                            login = hashQuery["loginUsuario"];
                        }

                        //Creo la cookie del usuario actual
                        bool existeCookie = CrearCookieUsuarioActual(usuarioID, login, idioma, DominioAplicacion);

                        //Creo la cookie de perfiles y rewrite
                        CrearCookiePerfiles(personaID, nombreCorto, "");

                        if (hashQuery.ContainsKey("redirect"))
                        {
                            redirect = HttpUtility.UrlDecode(hashQuery["redirect"]);
                        }
                    }
                }
                catch (Exception ex)
                {
                    mLoggingService.GuardarLogError(ex, mlogger);
                }

                if (!string.IsNullOrEmpty(redirect))
                {
                    Response.Redirect(redirect);
                }
            }
        }

        #endregion

        #region Métodos generales
        [NonAction]
        private void AgregarIframe(string pUrl, string pQuery)
        {
            if ((!string.IsNullOrEmpty(pUrl)) && (!string.IsNullOrEmpty(pQuery)))
            {
                if (!pUrl.EndsWith("/"))
                {
                    pUrl = pUrl + "/";
                }

                Response.WriteAsync("<IFRAME style='WIDTH:1px;HEIGHT:1px' src='" + pUrl + "/crearCookie" + pQuery + "' frameBorder='0'></IFRAME>");
            }
        }

        #endregion
    }
}
