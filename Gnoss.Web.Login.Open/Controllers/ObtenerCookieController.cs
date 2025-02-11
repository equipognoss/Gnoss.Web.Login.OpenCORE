using Es.Riam.AbstractsOpen;
using Es.Riam.Gnoss.AD.EntityModel;
using Es.Riam.Gnoss.AD.EntityModelBASE;
using Es.Riam.Gnoss.AD.Virtuoso;
using Es.Riam.Gnoss.CL;
using Es.Riam.Gnoss.Util.Configuracion;
using Es.Riam.Gnoss.Util.General;
using Es.Riam.Util;
using Es.Riam.Web.Util;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;

namespace Gnoss.Web.Login
{

    /// <summary>
    /// P�gina que lee la cookie del usuario (si la tiene) y se la env�a a otro dominio
    /// </summary>
    [Controller]
    [Route("[controller]")]
    public class ObtenerCookieController : ControllerBaseLogin
    {

        public ObtenerCookieController(LoggingService loggingService, IHttpContextAccessor httpContextAccessor, EntityContext entityContext, ConfigService configService, RedisCacheWrapper redisCacheWrapper, GnossCache gnossCache, VirtuosoAD virtuosoAD, IHostingEnvironment env, EntityContextBASE entityContextBASE, IServicesUtilVirtuosoAndReplication servicesUtilVirtuosoAndReplication)
            : base(loggingService, httpContextAccessor, entityContext, configService, redisCacheWrapper, gnossCache, virtuosoAD, env, entityContextBASE, servicesUtilVirtuosoAndReplication)
        {
        }

        #region M�todos de eventos

        /// <summary>
        /// M�todo page load
        /// </summary>
        /// <param name="sender">sender</param>
        /// <param name="e">e</param>
        [HttpGet, HttpPost]
        public void Index()
        {
            string url = Request.Headers["urlVuelta"];

            //obtengo las cookies
            Dictionary<string, string> cookie = UtilCookies.FromLegacyCookieString(Request.Cookies["_UsuarioActual"], mEntityContext);
            Dictionary<string, string> cookieRewrite = UtilCookies.FromLegacyCookieString(Request.Cookies["_rewrite"], mEntityContext);

            DateTime caduca = DateTime.Now.AddDays(1);
            List<ParametroAplicacion> filas = ParametrosAplicacionDS.Where(parametroApp => parametroApp.Parametro.Equals("TiposParametrosAplicacion.DuracionCookieUsuario")).ToList();
            bool extenderFechaCookie = false;
            if (filas == null)
            {
                extenderFechaCookie = true;
            }

            string dominio = "*";

            if (Request.Headers.ContainsKey("Referer"))
            {
                dominio = UtilDominios.ObtenerDominioUrl(Request.Headers["Referer"], true);
            }

            Response.Headers.Add("Access-Control-Allow-Origin", dominio);
            Response.Headers.Add("Access-Control-Allow-Credentials", "true");

            if (cookieRewrite != null && cookieRewrite.Count > 0)
            {
                mPersonaID = new Guid(cookieRewrite["personaID"]);
                mNombreCorto = cookieRewrite["nombreCorto"];
            }
            if (cookie != null && cookieRewrite.Count > 0)
            {
                mUsuarioID = new Guid(cookie["usuarioID"]);
                mMantenerConectado = bool.Parse(cookie["MantenerConectado"]);
                mLogin = cookie["loginUsuario"];
                mIdioma = cookie["idioma"];

                if (extenderFechaCookie)
                {
                    if (mMantenerConectado)
                    {
                        //As� la cookie nunca caduca
                        caduca = DateTime.MaxValue;
                    }
                    CookieOptions cookieUsuarioOptions = new CookieOptions();
                    cookieUsuarioOptions.Expires = caduca;
                    cookieUsuarioOptions.HttpOnly = true;
                    cookieUsuarioOptions.SameSite = SameSiteMode.Lax;
                    if (mConfigService.PeticionHttps())
                    {                    
                        cookieUsuarioOptions.Secure = true;   
                    }
                    Response.Cookies.Append("_UsuarioActual", UtilCookies.ToLegacyCookieString(cookie, mEntityContext), cookieUsuarioOptions);
                }
            }

            if (cookieRewrite != null && cookieRewrite.Count > 0 && extenderFechaCookie)
            {
                Response.Cookies.Append("_rewrite", UtilCookies.ToLegacyCookieString(cookieRewrite, mEntityContext), new CookieOptions { Expires = caduca });
            }

            if (Request.Cookies.ContainsKey("_Envio") && extenderFechaCookie)
            {
                Response.Cookies.Append("_Envio", Request.Cookies["_Envio"], new CookieOptions { Expires = caduca });
            }
            
            string redirect = "";
            string token = "";
            string eliminarCookie = "";
            if (Request.Headers.ContainsKey("Referer") && !Request.Headers["Referer"].ToString().Contains(UtilIdiomas.GetText("URLSEM", "DESCONECTAR")))
            {
                redirect = Request.Headers["Referer"].ToString();
            }
            if (Request.Query.Count > 1)
            {
                string queryRequest = Request.QueryString.ToString().Substring(1);


                Dictionary<string, string> hashQuery = UtilDominios.QueryStringToDictionary(queryRequest);

                url = hashQuery["urlVuelta"];

                if (hashQuery.ContainsKey("redirect"))
                {
                    redirect = HttpUtility.UrlDecode(hashQuery["redirect"]);
                }
                token = hashQuery["token"];

                if (hashQuery.ContainsKey("eliminarCookie"))
                {
                    eliminarCookie = "&eliminarCookie=" + hashQuery["eliminarCookie"];
                }
            }

            RedireccionarADominioDeOrigen(url, redirect, token, eliminarCookie);
        }

        #endregion

    }
}
