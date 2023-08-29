using DotNetOpenAuth.OAuth2;
using Es.Riam.AbstractsOpen;
using Es.Riam.Gnoss.AD.EntityModel;
using Es.Riam.Gnoss.AD.EntityModelBASE;
using Es.Riam.Gnoss.AD.Virtuoso;
using Es.Riam.Gnoss.CL;
using Es.Riam.Gnoss.Util.Configuracion;
using Es.Riam.Gnoss.Util.General;
using Es.Riam.Web.Util;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;
using Serilog.Sinks.Http;
using System.Threading.Tasks;
using Es.Riam.Util;

namespace Gnoss.Web.Login
{

    /// <summary>
    /// Página que elimina las cookies de todos los dominios en los que el usuario ha estado
    /// </summary>
    [Controller]
    [Route("[controller]")]
    public class EliminarCookieController : ControllerBaseLogin
    {

        public EliminarCookieController(LoggingService loggingService, IHttpContextAccessor httpContextAccessor, EntityContext entityContext, ConfigService configService, RedisCacheWrapper redisCacheWrapper, GnossCache gnossCache, VirtuosoAD virtuosoAD, IHostingEnvironment env, EntityContextBASE entityContextBASE, IServicesUtilVirtuosoAndReplication servicesUtilVirtuosoAndReplication)
            : base(loggingService, httpContextAccessor, entityContext, configService, redisCacheWrapper, gnossCache, virtuosoAD, env, entityContextBASE, servicesUtilVirtuosoAndReplication)
        {
        }

        #region Métodos de eventos

        /// <summary>
        /// Método page load
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        [HttpGet, HttpPost]
        public ActionResult Index()
        {
            string cookieUsuarioKey = "_UsuarioActual";
            string cookieEnvioKey = "_Envio";
            List<string> listaSrcIframes = new List<string>();
            if (!Request.Headers.ContainsKey("eliminar") && !Request.Query.ContainsKey("eliminar"))
            {
                string dominio = "";

                if (Request.Headers.ContainsKey("dominio"))
                {
                    dominio = Request.Headers["dominio"];
                }
                else if (Request.Query.ContainsKey("dominio"))
                {
                    dominio = Request.Query["dominio"];
                }

                if (Request.Cookies.ContainsKey(cookieEnvioKey) || Request.Headers.ContainsKey("nuevoEnvio"))
                {
                    //Creo una cookie para saber que el resto de dominios ya han sido notificados
                    mHttpContextAccessor.HttpContext.Response.Cookies.Append(cookieEnvioKey, "true", new CookieOptions { Expires = DateTime.Now.AddDays(1) });

                    //El usuario se acaba de conectar, si habia estado en otros dominios, elimino su sesión
                    listaSrcIframes.AddRange(EliminarCookieRestoDominios(dominio));
                }

            }
            else
            {
                //Elimino la cookie del usuario actual conectado
                if (Request.Cookies.ContainsKey(cookieUsuarioKey))
                {
                    Response.Cookies.Append(cookieUsuarioKey, "0", new CookieOptions { Expires = DateTime.Now.AddDays(-1) });
                }

                string cookieRewriteKey = "_rewrite";

                //Elimino la cookie de rewrite
                if (Request.Cookies.ContainsKey(cookieRewriteKey))
                {
                    Response.Cookies.Append(cookieRewriteKey, "0", new CookieOptions { Expires = DateTime.Now.AddDays(-1) });
                }

                if (Request.Cookies.ContainsKey(cookieEnvioKey))
                {
                    Response.Cookies.Append(cookieEnvioKey, "0", new CookieOptions { Expires = DateTime.Now.AddDays(-1) });
                }

                if (Request.Cookies.ContainsKey("redireccion"))
                {
                    Response.Cookies.Append("redireccion", "0", new CookieOptions { Expires = DateTime.Now.AddDays(-1) });
                }

                string cookieTokenKey = "tokenDeVuelta";
                if (Request.Cookies.ContainsKey(cookieTokenKey))
                {
                    Response.Cookies.Append(cookieTokenKey, "0", new CookieOptions() { Expires = DateTime.Now.AddDays(-1) });
                }

                string usuarioLogueadoKey = "UsuarioLogueado";
                if (Request.Cookies.ContainsKey(usuarioLogueadoKey))
                {
                    CookieOptions cookieUsuarioLogueadoOptions = new CookieOptions
                    {
                        Expires = DateTime.Now.AddDays(-1)
                    };

                    string dominio = DominioAplicacion;
                    if (DominioAplicacion.IndexOf('.', 1) >= 0)
                    {
                        dominio = DominioAplicacion.Substring(DominioAplicacion.IndexOf('.', 1));
                    }
                    cookieUsuarioLogueadoOptions.Domain = dominio;

                    if (mHttpContextAccessor.HttpContext.Request.Scheme.Equals("https"))
                    {
                        cookieUsuarioLogueadoOptions.Secure = true;
                    }

                    Response.Cookies.Append(usuarioLogueadoKey, "0", cookieUsuarioLogueadoOptions);
                }

                Request.Path.ToString();
                string dominioPeticion = "";
                if (Request.Headers.ContainsKey("dominio"))
                {
                    dominioPeticion = Request.Headers["dominio"];
                }
                else if (Request.Query.ContainsKey("dominio"))
                {
                    dominioPeticion = Request.Query["dominio"];
                }
                listaSrcIframes.AddRange(EliminarCookieRestoDominios(dominioPeticion));
            }

            //si esta configurado para usar Keycloak
            try
            {
                string accessToken = HttpContext.Session.GetString("KeycloakTK");
                if (!string.IsNullOrEmpty(accessToken))
                {
                    string urlKeycloak = mHttpContextAccessor.HttpContext.Request.Cookies["_DominioLogoutExterno"];
                    string urlPeticion = $"{urlKeycloak}/Logout?keycloakToken={accessToken}&redirect={Request.Query["redirect"]}";
                    
                    Response.Redirect(urlPeticion);
                    
                    string content = AgregarContent(listaSrcIframes);

                    return Content(content, "text/html");
                }     
            }
            catch (Exception ex)
            {
                mLoggingService.GuardarLogError($"Ha habido un error al intentar cerrar la sesion de Keycloak. KEYCLOAK_ERROR: {ex.Message}");
            }

            if ((Request.Query.ContainsKey("redirect") || Request.Headers.ContainsKey("redirect") )&& listaSrcIframes.Count == 0)
            {
                if (Request.Headers.ContainsKey("redirect"))
                {
                    Response.Redirect(Request.Headers["redirect"]);
                }
                else if (Request.Query.ContainsKey("redirect"))
                {
                    Response.Redirect(Request.Query["redirect"]);
                }
            }

            string contenido = AgregarContent(listaSrcIframes);

            return Content(contenido, "text/html");
            //return View(listaSrcIframes);
        }

        #endregion

        #region Métodos generales

        /// <summary>
        /// Método que elimina las cookies de todos los dominios
        /// </summary>
        [NonAction]
        private List<string> EliminarCookieRestoDominios(string pDominio)
        {
            Dictionary<string, string> dominios = UtilCookies.FromLegacyCookieString(Request.Cookies["_Dominios"], mEntityContext);
            List<string> listaSrcIframes = new List<string>();
            if (pDominio.Contains("//www."))
            {
                pDominio = pDominio.Replace("//www.", "//");
            }
            string dominioSinHTTPS = pDominio.Replace("https://", "http://");
            string dominioConHTTPS = pDominio.Replace("http://", "https://");

            if ((dominios != null) && (dominios.Values.Count > 0))
            {
                //Recorre todos los dominios que hay en la cookie dominios y accede a la página eliminarCookie.aspx de cada uno de ellos, que elimina sus cookies
                foreach (string dominio in dominios.Keys)
                {
                    if (string.IsNullOrEmpty(pDominio) || !(dominio.Equals(dominioSinHTTPS) || dominio.Equals(dominioConHTTPS)))
                    {
                        listaSrcIframes.Add(MontarSrcIframe(dominio));
                    }
                }
            }

            if (Request.Headers.ContainsKey("eliminar") && Request.Headers["eliminar"].Equals("true"))
            {
                string cookieDominioLogoutExternoKey = "_DominioLogoutExterno";
                if (Request.Cookies.ContainsKey(cookieDominioLogoutExternoKey) && !string.IsNullOrEmpty(Request.Cookies[cookieDominioLogoutExternoKey]) && Uri.IsWellFormedUriString(Request.Cookies[cookieDominioLogoutExternoKey], UriKind.Absolute))
                {
                    listaSrcIframes.Add(Request.Cookies[cookieDominioLogoutExternoKey]);
                }
            }
            return listaSrcIframes;
        }

        /// <summary>
        /// Genera el src del dominio del cual se va a elimanar la cookie en la vista con el IFRAME
        /// </summary>
        /// <param name="pUrl">Dominio a generar el src del IFRAME</param>
        /// <returns></returns>
        [NonAction]
        private string MontarSrcIframe(string pUrl)
        {
            if (!string.IsNullOrEmpty(pUrl))
            {
                if (!pUrl.EndsWith("/"))
                {
                    pUrl += "/";
                }

                string parametros = "";
                string separador = "?";
                if (!Request.Headers.ContainsKey("eliminar"))
                {
                    parametros += "?login=1";
                    separador = "&";
                }

                if (Request.Headers.ContainsKey("usuarioID"))
                {
                    parametros += separador + "usuarioID=" + Request.Headers["usuarioID"];
                }
     
                return $"{pUrl}eliminarCookie{parametros}";
            }

            return string.Empty;
        }

        #endregion
        private string AgregarContent(List<string> pListaSrc)
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("<!DOCTYPE html PUBLIC \" -//W3C//DTD XHTML 1.0 Transitional//EN\" \"http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd\">");
            stringBuilder.AppendLine("<html xmlns=\"http://www.w3.org/1999/xhtml\">");
            stringBuilder.AppendLine("<head>");
            stringBuilder.AppendLine("<title>");
            stringBuilder.AppendLine(" Loading...");
            stringBuilder.AppendLine("</title>");
            stringBuilder.AppendLine($"<meta id=\"metaRefresh\" http-equiv=\"refresh\" content=\"2; url = {Request.Query["redirect"]}\" />");
            stringBuilder.AppendLine("</head>");
            stringBuilder.AppendLine("<body>");
            stringBuilder.AppendLine("<div>");
            stringBuilder.AppendLine("</div>");
            foreach (string src in pListaSrc)
            {
                stringBuilder.AppendLine($"<IFRAME style=\"WIDTH: 1px; HEIGHT: 1px\" src=\"{src}\" frameBorder=\"0\"></IFRAME>");
            }
            stringBuilder.AppendLine("<div>");
            stringBuilder.AppendLine("</div>");
            stringBuilder.AppendLine("<div>");
            stringBuilder.AppendLine("<p>Loading...</p>");
            stringBuilder.AppendLine("</div>");
            stringBuilder.AppendLine("</body>");
            stringBuilder.AppendLine("</html>");

            return stringBuilder.ToString();
        }
    }
}
