using AngleSharp.Common;
using Es.Riam.AbstractsOpen;
using Es.Riam.Gnoss.AD.EncapsuladoDatos;
using Es.Riam.Gnoss.AD.EntityModel;
using Es.Riam.Gnoss.AD.EntityModel.Models.Peticion;
using Es.Riam.Gnoss.AD.EntityModel.Models.Solicitud;
using Es.Riam.Gnoss.AD.EntityModelBASE;
using Es.Riam.Gnoss.AD.Notificacion;
using Es.Riam.Gnoss.AD.Organizador.Correo.Model;
using Es.Riam.Gnoss.AD.ParametroAplicacion;
using Es.Riam.Gnoss.AD.Peticion;
using Es.Riam.Gnoss.AD.ServiciosGenerales;
using Es.Riam.Gnoss.AD.Usuarios;
using Es.Riam.Gnoss.AD.Virtuoso;
using Es.Riam.Gnoss.CL;
using Es.Riam.Gnoss.CL.ParametrosAplicacion;
using Es.Riam.Gnoss.CL.Seguridad;
using Es.Riam.Gnoss.CL.ServiciosGenerales;
using Es.Riam.Gnoss.CL.Usuarios;
using Es.Riam.Gnoss.Elementos.Identidad;
using Es.Riam.Gnoss.Elementos.Notificacion;
using Es.Riam.Gnoss.Elementos.Organizador.Correo;
using Es.Riam.Gnoss.Elementos.ParametroAplicacion;
using Es.Riam.Gnoss.Elementos.ServiciosGenerales;
using Es.Riam.Gnoss.Logica.Identidad;
using Es.Riam.Gnoss.Logica.Notificacion;
using Es.Riam.Gnoss.Logica.ParametroAplicacion;
using Es.Riam.Gnoss.Logica.ParametrosProyecto;
using Es.Riam.Gnoss.Logica.ServiciosGenerales;
using Es.Riam.Gnoss.Logica.Usuarios;
using Es.Riam.Gnoss.Util.Configuracion;
using Es.Riam.Gnoss.Util.General;
using Es.Riam.Gnoss.Web.Controles.ServiciosGenerales;
using Es.Riam.Util;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Amqp.Framing;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Unicode;
using System.Web;
using System.Web.UI;

namespace Gnoss.Web.Login
{
    [Controller]
    [Route("[controller]")]
    [EnableCors("_myAllowSpecificOrigins")]
    /// <summary>
    /// P�gina para loguear al usuario
    /// </summary>
    public class LoginController : ControllerBaseLogin
    {
        /// <summary>
        /// DataSet con los par�metros de la aplicaci�n.
        /// </summary> 
        private List<ParametroAplicacion> mParametroAplicacion;

        private string mUrlIntragnoss;
        private string mUrlIntragnossServicios;

        public LoginController(LoggingService loggingService, IHttpContextAccessor httpContextAccessor, EntityContext entityContext, ConfigService configService, RedisCacheWrapper redisCacheWrapper, GnossCache gnossCache, VirtuosoAD virtuosoAD, IHostingEnvironment env, EntityContextBASE entityContextBASE, IServicesUtilVirtuosoAndReplication servicesUtilVirtuosoAndReplication)
             : base(loggingService, httpContextAccessor, entityContext, configService, redisCacheWrapper, gnossCache, virtuosoAD, env, entityContextBASE, servicesUtilVirtuosoAndReplication)
        {
        }


        #region Metodos de eventos

        /// <summary>
        /// Evento que se produce al cargar la p�gina
        /// </summary>
        [HttpGet, HttpPost]
        public ActionResult Index([FromHeader] string token, [FromHeader] string redirect, Guid proyectoID, string idioma, string baseURL, bool esProyectoPrivado, [FromForm] string usuario, [FromForm] string password)
        {          
            string urlFormulario = this.Request.Path.ToString();

            string redirect1 = UrlAplicacionPrincipal;

            if (Request.Headers.ContainsKey("Referer") && !Request.Headers["Referer"].ToString().Contains(UtilIdiomas.GetText("URLSEM", "DESCONECTAR")))
            {
                redirect1 = Request.Headers["Referer"].ToString();
                try
                {
                    //Comprobamos que el parametro redirect que pueda venir desde el Referer pertenece al host del proyecto
                    Uri uriRedirect = new Uri(redirect1);
                    string query = uriRedirect.Query;
                    if (!string.IsNullOrEmpty(query))
                    {
                        redirect1 = ComprobarQueryString(query, proyectoID, idioma);
                        NameValueCollection queryString = HttpUtility.ParseQueryString(redirect1);
                        if (queryString["redirect"] != null)
                        {
                            redirect1 = queryString["redirect"];
                        }
                        else
                        {
                            redirect1 = "";
                        }
                    }
                    
                }
                catch (Exception ex) 
                {
                    mLoggingService.GuardarLogError(ex.Message);
                }
            }
            if (!string.IsNullOrEmpty(Request.Query["redirect"]))
            {
                //Comprobamos que el parametro redirect que se est� enviando explicitamente sea valido
                string redirectQuery = Request.Query["redirect"];  
                if(redirectQuery.Equals(ComprobarRedirectValido(redirectQuery, proyectoID, idioma)))
                {
                    redirect1 = Request.Query["redirect"];
                }
                if (!redirect1.StartsWith("http"))
                {
                    if (!redirect1.StartsWith("/"))
                    {
                        redirect1 = "/" + redirect1;
                    }
                    if (Request.Headers.ContainsKey("Referer"))
                    {
                        redirect1 = UtilDominios.ObtenerDominioUrl(Request.Headers["Referer"], true) + redirect1;
                    }
                    else
                    {
                        redirect1 = UrlAplicacionPrincipal + redirect1;
                    }

                    string caracterCorte = "?";
                    if (urlFormulario.Contains("&redirect"))
                    {
                        caracterCorte = "&";
                    }

                    int indiceCorte = urlFormulario.IndexOf(caracterCorte + "redirect");
                    int indiceFin = urlFormulario.IndexOf("&", indiceCorte + 1) - indiceCorte;
                    if (indiceFin > 0)
                    {
                        urlFormulario = urlFormulario.Remove(indiceCorte + 1, indiceFin);
                        caracterCorte = "&";
                    }
                    else
                    {
                        urlFormulario = urlFormulario.Remove(indiceCorte);
                    } 
                    urlFormulario += caracterCorte + "redirect=" + redirect1;
                }
            }           
            
            bool CapchaActivo = true;
            int NumMaxIntentosIP = 3;


            CapchaActivo = mConfigService.CaptchaActive();
            if (mConfigService.ObtenerCaptchaNumIntentos() != 0)
            {
                NumMaxIntentosIP = mConfigService.ObtenerCaptchaNumIntentos();
            }
            string token1 = null;
            if (token != null)
            {
                token1 = token;
            }
            else
            {
                token1 = Request.Query["token"];
            }


            string ip = ObtenerIP();

            int numIntentosIP = ObtenerNumIntentosIP(ip);

            if (!urlFormulario.Contains("&redirect=") && !Request.Headers.ContainsKey("redirect") && Request.Headers.ContainsKey("Referer") && !Request.Path.ToString().Equals(Request.Headers["Referer"].ToString()) && !Request.Headers["Referer"].ToString().Contains(UtilIdiomas.GetText("URLSEM", "DESCONECTAR")))
            {
                urlFormulario += "&redirect=" + HttpUtility.UrlEncode(Request.Headers["Referer"].ToString());
            }

            if (Request.Method.Equals("POST") && Request.Form.Count > 1 && (Request.Form.ContainsKey("usuario")) && (Request.Form.ContainsKey("password")) && Request.Headers.ContainsKey("Referer"))
            {
                string login = Request.Form["usuario"];
                string passwd = Request.Form["password"];
                string codigoVerificacion = null;

                if (!string.IsNullOrEmpty(Request.Form["codigoverificacion"]))
                {
                    codigoVerificacion = Request.Form["codigoverificacion"];
                }

                if (!ComprobarSiUsuarioPuedeHacerLogin(login))
                {
                    RedirigirConError(redirect1, "#UsuarioBloqueado");
                    return Content("");
                }

                string captcha = Request.Headers["captcha"];

                string almohadillaerror = "#error";

                if (!Request.Form.ContainsKey("accion") || Request.Form["accion"] == "login")
                {
                    string errorLoginExterno = LoginConServicioExterno(ref login, ref passwd, ref redirect1, ref token1);
                    bool mostrarPanelActivacionExterno = false;
                    if (!string.IsNullOrEmpty(errorLoginExterno))
                    {
                        if (errorLoginExterno.StartsWith("error="))
                        {
                            //Guardo el error en el log
                            mLoggingService.GuardarLogError("Se ha producido un error en el servicio de login externo:" + errorLoginExterno);
                            almohadillaerror = "#errorAutenticacionExterna";
                        }
                        else if (errorLoginExterno.StartsWith("sinactivar"))
                        {
                            mostrarPanelActivacionExterno = true;
                        }
                    }

                    bool loguear = true;
                    string mensajeErrorServicioLoginExterno = "";
                    string urlServicioLoginExterno = "";
                    List<ParametroAplicacion> filaUrlServicioLoginExt = ParametrosAplicacionDS.Where(parametroApp => parametroApp.Parametro.Equals("urlServicioLoginExterno")).ToList();

                    if (filaUrlServicioLoginExt != null && filaUrlServicioLoginExt.Count > 0)
                    {
                        urlServicioLoginExterno = filaUrlServicioLoginExt[0].Valor;
                    }

                    if (!string.IsNullOrEmpty(urlServicioLoginExterno))
                    {
                        Dictionary<string, string> parametros = new Dictionary<string, string>();
                        string Usuario = login;
                        parametros.Add("Usuario", Usuario);
                        string Password = passwd;
                        parametros.Add("Password", Password);

                        /*Hacemos la llamada con usuario y contrase�a y nos devuelve el JSON con*/
                        //OK+usuarioID -->logueamos al usuario
                        //KO+Si loguear
                        //KO+No loguear
                        //llamada al servicio externo
                        JsonLoginExterno jsonLogin = null;

                        try
                        {
                            jsonLogin = PeticionServicioLoginExterno(urlServicioLoginExterno, UtilWeb.Metodo.POST, parametros);
                        }
                        catch (Exception ex)
                        {
                            mLoggingService.GuardarLogError(ex);
                        }

                        if (jsonLogin != null)
                        {
                            if (jsonLogin.EstadoLogin.Equals(TipoEstadoLoginExterno.LoginOK))
                            {
                                //Obtener login por usuarioID
                                UsuarioCN usuarioCN = new UsuarioCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication);
                                string loginUsuario = usuarioCN.ObtenerLoginUsuarioPorNombreCorto(usuarioCN.ObtenerNombreCortoUsuarioPorID(jsonLogin.UsuarioID));
                                usuarioCN.Dispose();

                                bool mismoUsuarioLogueadoJson = ComprobarMismoUsuarioLogueado(loginUsuario);

                                ValidarUsuario(loginUsuario, "", null, true);

                                if (Request.Cookies.ContainsKey("_Envio"))
                                {
                                    CookieOptions options = new CookieOptions();
                                    options.Expires = DateTime.Now.AddDays(-1);
                                    Response.Cookies.Append("_Envio", "0", options);
                                }
                                string dominioDeVuelta = UtilDominios.ObtenerDominioUrl(new Uri(redirect1), true);

                                if (!string.IsNullOrEmpty(Request.Query["proyectoID"]))
                                {
                                    Guid proyectoID1 = new Guid(Request.Query["proyectoID"]);
                                    ProyectoCL proyectoCL = new ProyectoCL(mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mVirtuosoAD, mServicesUtilVirtuosoAndReplication);
                                    GestionProyecto gestorProy = new GestionProyecto(proyectoCL.ObtenerProyectoPorID(proyectoID1), mLoggingService, mEntityContext);

                                    Proyecto proyecto = gestorProy.ListaProyectos[proyectoID1];
                                    if (proyecto.ListaTipoProyectoEventoAccion.ContainsKey(TipoProyectoEventoAccion.Login))
                                    {
                                        proyectoCL.AgregarEventosAccionProyectoPorProyectoYUsuarioID(proyectoID1, mUsuarioID, TipoProyectoEventoAccion.Login);
                                    }
                                    proyectoCL.Dispose();
                                }
                                EnviarCookies(dominioDeVuelta, redirect1, token1, mismoUsuarioLogueadoJson);
                            }
                            else if (jsonLogin.EstadoLogin.Equals(TipoEstadoLoginExterno.LoginKO) && !jsonLogin.Loguear)
                            {
                                loguear = false;
                                mensajeErrorServicioLoginExterno = jsonLogin.InfoExtra;
                                almohadillaerror += "&" + mensajeErrorServicioLoginExterno;
                            }
                        }
                    }

                    bool mismoUsuarioLogueado = ComprobarMismoUsuarioLogueado(login);
                    //loguearSinValidar
                    bool estaValidado = false;
                    if (string.IsNullOrEmpty(Request.Headers["loguearSinValidar"]))
                    {
                        estaValidado = ValidadoUsuario(login);
                    }
                    else
                    {
                        estaValidado = true;
                    }

                    if (CapchaActivo && numIntentosIP > 2 && !ValidarTokenCaptcha(token1, captcha))
                    {
                        RedirigirConError(redirect1, almohadillaerror);
                    }
                    else if (loguear && ValidarUsuario(login, passwd, codigoVerificacion, false, estaValidado) && !mostrarPanelActivacionExterno && estaValidado)
                    {
                        UsuarioCN usuarioCN = new UsuarioCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication);                       
                        Guid usuarioID = (Guid)usuarioCN.ObtenerUsuarioIDPorLoginOEmail(login);
                      

                        //Comprobar si el usuario tiene activada la autenticacion de doble factor
                        if (usuarioCN.ComprobarDobleAutenticacionUsuario(usuarioID))
                        {
                            AutenticarUsuarioConDobleFactor(login, baseURL, idioma, proyectoID, usuarioID);

                            return Content("");
                        }

                        InvalidarTokenCaptcha(token1);

                        if (Request.Cookies.ContainsKey("_Envio"))
                        {
                            CookieOptions options = new CookieOptions();
                            options.Expires = DateTime.Now.AddDays(-1);
                            Response.Cookies.Append("_Envio", "0", options);
                        }

                        if (Request.Form.ContainsKey("clausulasRegistro"))
                        {
                            redirect1 += "/clausulasRegistro/" + Request.Form["clausulasRegistro"].FirstOrDefault().Replace('|', '&').Replace(',', '&');
                        }

                        if (!string.IsNullOrEmpty(Request.Query["proyectoID"]))
                        {
                            Guid proyectoID1 = new Guid(Request.Query["proyectoID"]);
                            ProyectoCL proyectoCL = new ProyectoCL(mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mVirtuosoAD, mServicesUtilVirtuosoAndReplication);
                            GestionProyecto gestorProy = new GestionProyecto(proyectoCL.ObtenerProyectoPorID(proyectoID1), mLoggingService, mEntityContext);

                            Proyecto proyecto = gestorProy.ListaProyectos[proyectoID1];
                            if (proyecto.ListaTipoProyectoEventoAccion.ContainsKey(TipoProyectoEventoAccion.Login))
                            {
                                proyectoCL.AgregarEventosAccionProyectoPorProyectoYUsuarioID(proyectoID1, mUsuarioID, TipoProyectoEventoAccion.Login);
                            }
                            proyectoCL.Dispose();
                        }

                        ParametroAplicacion filaParametro = ParametroAplicacionDS.Find(parametroApp => parametroApp.Parametro.Equals(TiposParametrosAplicacion.UrlHomeConectado));

                        if (filaParametro != null && !string.IsNullOrEmpty(filaParametro.Valor) && filaParametro.Valor.ToLower().Equals("##comunidadorigen##"))
                        {
                            SolicitudCN solicitudCN = new SolicitudCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication);
                            Es.Riam.Gnoss.AD.EntityModel.Models.UsuarioDS.Usuario filaUsuario = usuarioCN.ObtenerFilaUsuarioPorLoginOEmail(login);
                            if (filaUsuario != null)
                            {
                                ProyectoCN proyCN = new ProyectoCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication);
                                Guid proyOrigenUsuario = solicitudCN.ObtenerProyectoOrigenUsuario(filaUsuario.UsuarioID);
                                string nombreCortoProyOrigen = proyCN.ObtenerNombreCortoProyecto(proyOrigenUsuario);
                                if (!string.IsNullOrEmpty(nombreCortoProyOrigen) && !proyOrigenUsuario.Equals(ProyectoAD.MetaProyecto))
                                {
                                    redirect1 = mControladorBase.UrlsSemanticas.ObtenerURLComunidad(UtilIdiomas, BaseURLIdioma, nombreCortoProyOrigen);
                                }
                                proyCN.Dispose();
                            }

                            solicitudCN.Dispose();
                            usuarioCN.Dispose();
                        }

                        Uri uriRedirect = null;
                        string dominioDeVuelta = redirect1;

                        if (!Uri.TryCreate(redirect1, UriKind.RelativeOrAbsolute, out uriRedirect))
                        {
                            redirect1 = HttpUtility.UrlDecode(redirect1);
                        }

                        if (Uri.TryCreate(redirect1, UriKind.RelativeOrAbsolute, out uriRedirect))
                        {
                            dominioDeVuelta = UtilDominios.ObtenerDominioUrl(new Uri(redirect1), true);
                        }
                        else
                        {
                            mLoggingService.GuardarLogError($"La url {redirect1} no es v�lida. Imposible obtener el dominio. ");
                        }

                        EnviarCookies(dominioDeVuelta, redirect1, token1, mismoUsuarioLogueado);
                    }
                    else
                    {
                        UsuarioCN usuarioCN = new UsuarioCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication);
                        Es.Riam.Gnoss.AD.EntityModel.Models.UsuarioDS.Usuario filaUsuario = null;

                        if (loguear && ValidarUsuario(login, passwd, codigoVerificacion, false, estaValidado))
                        {
                            filaUsuario = usuarioCN.ObtenerFilaUsuarioPorLoginOEmail(login);
                        }

                        bool redirigirConError = false;

                        if (filaUsuario != null)
                        {
                            bool registroCompleto = true;                          
                            SolicitudCN solicitudCN = new SolicitudCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication);
                            DataWrapperSolicitud solicitudDW = solicitudCN.ObtenerSolicitudesDeUsuario(filaUsuario.UsuarioID);
                            SolicitudNuevoUsuario filaSolicitud = null;
                            if (solicitudDW.ListaSolicitudNuevoUsuario.Count > 0)
                            {
                                filaSolicitud = solicitudDW.ListaSolicitudNuevoUsuario.First();

                                if (filaSolicitud.Solicitud.Estado.Equals(0))
                                {
                                    registroCompleto = !filaSolicitud.FaltanDatos;
                                    if (!registroCompleto)
                                    {
                                        Guid proyectoID1 = solicitudDW.ListaSolicitud.Find(solicitud => solicitud.SolicitudID.Equals(filaSolicitud.SolicitudID)).ProyectoID;
                                        if (proyectoID1 != ProyectoAD.MetaProyecto)
                                        {
                                            string urlCompletarRegistro = BaseURLIdioma + "/" + UtilIdiomas.GetText("URLSEM", "COMUNIDAD") + "/" + new ProyectoCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication).ObtenerNombreCortoProyecto(proyectoID1) + "/" + UtilIdiomas.GetText("URLSEM", "COMPLETARREGISTRO") + "/" + filaSolicitud.UsuarioID.ToString();
                                            Response.Redirect(urlCompletarRegistro);
                                        }
                                    }
                                }
                            }

                            PersonaCN persCN = new PersonaCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication);
                            Es.Riam.Gnoss.AD.EntityModel.Models.PersonaDS.Persona filaPersona = persCN.ObtenerFilaPersonaPorUsuario(filaUsuario.UsuarioID);

                            if (registroCompleto && (mostrarPanelActivacionExterno || usuarioCN.ValidarPasswordUsuarioSinActivar(filaUsuario, passwd)) && (filaPersona == null || !filaPersona.Eliminado) || filaUsuario.Validado == 0)
                            {
                                ProyectoCN proyCN = new ProyectoCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication);
                                string urlProy = mControladorBase.UrlsSemanticas.ObtenerURLComunidad(UtilIdiomas, BaseURLIdioma, proyCN.ObtenerNombreCortoProyecto(new Guid(Request.Query["proyectoID"])));
                                proyCN.Dispose();
                                string urlRedireccion = $"{urlProy}/{UtilIdiomas.GetText("URLSEM", "REENVIAREMAILVERIFICACION")}/{filaSolicitud.SolicitudID.ToString()}";
                                Response.Redirect(urlRedireccion);
                            }
                            else
                            {
                                AumentarNumIntentosIP(ip);

                                if (!string.IsNullOrEmpty(Request.Query["proyectoID"]) && new Guid(Request.Query["proyectoID"]) != ProyectoAD.MyGnoss)
                                {
                                    redirigirConError = true;
                                }
                            }

                            persCN.Dispose();
                        }
                        else
                        {
                            redirigirConError = true;
                        }
                        if (redirigirConError)
                        {
                            RedirigirConError(redirect1, almohadillaerror);
                        }
                    }
                }
            }

            return Content("");
        }

        [NonAction]
        private bool ComprobarSiUsuarioPuedeHacerLogin(string pLoginUsuario)
        {
            bool loginPermitido = true;
            try
            {
                UsuarioCN usuarioCN = new UsuarioCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication);
                Guid usuarioID = (Guid)usuarioCN.ObtenerUsuarioIDPorLoginOEmail(pLoginUsuario);
                
                if (!usuarioID.Equals(Guid.Empty))
                {
                    UsuarioCL usuarioCL = new UsuarioCL(mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mServicesUtilVirtuosoAndReplication);
                    EstadoLoginUsuario estadoLogin = usuarioCL.ComprobarSiUsuarioPuedeHacerLogin(usuarioID);
                    if (estadoLogin.Equals(EstadoLoginUsuario.Bloqueado))
                    {
                        loginPermitido = false;
                    }
                    usuarioCL.Dispose();
                }
                usuarioCN.Dispose();
            }
            catch (Exception ex)
            {
                mLoggingService.GuardarLogError(ex, $"Error al comprobar el numero de peticiones del usuario {pLoginUsuario}. Error: {ex.Message}");
            }

            return loginPermitido;
        }

        [NonAction]
        private bool ValidadoUsuario(string login)
        {
            bool validado = false;
            UsuarioCN usuarioCN = new UsuarioCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication);
            Es.Riam.Gnoss.AD.EntityModel.Models.UsuarioDS.Usuario filaUsuario = null;
            filaUsuario = usuarioCN.ObtenerFilaUsuarioPorLoginOEmail(login);
            if (filaUsuario == null || filaUsuario.Validado != (short)ValidacionUsuario.SinVerificar)
            {
                validado = true;
            }
            return validado;
        }

        [NonAction]
        private void RedirigirConError(string pRedirect, string pAlmohadillaerror)
        {
            if (pRedirect.Contains("/" + UtilIdiomas.GetText("URLSEM", "LOGIN") + "/") || pRedirect.Contains("/" + UtilIdiomas.GetText("URLSEM", "ACEPTARINVITACION") + "/") || pRedirect.Contains("/" + UtilIdiomas.GetText("URLSEM", "HAZTEMIEMBRO")))
            {
                pRedirect = pRedirect.TrimEnd('/');
                if (Request.Headers.ContainsKey("Referer") && !Request.Headers["Referer"].ToString().Contains(UtilIdiomas.GetText("URLSEM", "DESCONECTAR")))
                {
                    // Redirigimos a la p�gina original con el error, no a la que hay que redirigir, no tiene sentido.
                    Response.Redirect(Request.Headers["Referer"].ToString() + pAlmohadillaerror);
                }
                else
                {
                    Response.Redirect(pRedirect + pAlmohadillaerror);
                }
            }
            else
            {
                string urlOrigen = pRedirect;
                if (Request.Headers.ContainsKey("Referer"))
                {
                    urlOrigen = Request.Headers["Referer"].ToString();
                }

                string language = "es";
                // Busco la primera / a partir del �ndice 8 para quitar https://
                int indicePrimerFragmento = urlOrigen.IndexOf('/', 8) + 1;
                int indiceFinPrimerFragmento = urlOrigen.IndexOf('/', indicePrimerFragmento);
                int longitudFragmento = indiceFinPrimerFragmento - indicePrimerFragmento;

                if (longitudFragmento == 2)
                {
                    language = Request.Headers["Referer"].ToString().Substring(indicePrimerFragmento, longitudFragmento);
                }

                Es.Riam.Gnoss.Recursos.UtilIdiomas utilIdiomas = new Es.Riam.Gnoss.Recursos.UtilIdiomas(language, mLoggingService, mEntityContext, mConfigService, mRedisCacheWrapper);

                ProyectoCN proyCN = new ProyectoCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication);

                Guid proyectoID = ProyectoAD.MetaProyecto;
                if (!string.IsNullOrEmpty(Request.Query["proyectoID"]))
                {
                    proyectoID = new Guid(Request.Query["proyectoID"]);
                }

                string urlProy = mControladorBase.UrlsSemanticas.ObtenerURLComunidad(utilIdiomas, BaseURLIdioma, proyCN.ObtenerNombreCortoProyecto(proyectoID));

                if (string.IsNullOrEmpty(urlProy))
                {
                    Uri uriOrigen = new Uri(urlOrigen);
                    urlProy = $"{uriOrigen.Scheme}://{uriOrigen.Host}";
                }

                proyCN.Dispose();
                string urlRedireccion = $"{urlProy}/{utilIdiomas.GetText("URLSEM", "LOGIN")}?redirect={HttpUtility.UrlEncode(pRedirect)}{pAlmohadillaerror}";
                Response.Redirect(urlRedireccion);
            }
        }

        private bool ComprobarMismoUsuarioLogueado(string pLoginUsuario)
        {
            //Compruebo si se est� logueando el mismo usuario que ya estaba logueado
            string cookieValue = Request.Cookies["_UsuarioActual"];
            return Request.Cookies.ContainsKey("_UsuarioActual") && pLoginUsuario.ToLower().Equals(cookieValue);
        }

        #endregion

        #region Metodos captcha
        [NonAction]
        private string ObtenerIP()
        {
            HttpContext context = mHttpContextAccessor.HttpContext;
            string ipAddress = context?.Features?.Get<IServerVariablesFeature>()?["HTTP_X_FORWARDED_FOR"];

            if (!string.IsNullOrEmpty(ipAddress))
            {
                string[] addresses = ipAddress.Split(',');
                if (addresses.Length != 0)
                {
                    return addresses[0];
                }
            }

            string ip = context?.Features?.Get<IServerVariablesFeature>()?["REMOTE_ADDR"];

            if (ip == "::1")
            {
                return "127.0.0.1";
            }

            return ip;
        }
        [NonAction]
        private int ObtenerNumIntentosIP(string IP)
        {
            SeguridadCL seguridadCL = new SeguridadCL(mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mServicesUtilVirtuosoAndReplication);
            return seguridadCL.ObtenerNumIntentosIP(IP);
        }
        [NonAction]
        private bool ValidarTokenCaptcha(string token, string captcha)
        {
            if (string.IsNullOrEmpty(captcha))
            {
                return false;
            }
            SeguridadCL seguridadCL = new SeguridadCL(mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mServicesUtilVirtuosoAndReplication);
            return seguridadCL.ValidarTokenCaptcha(token, captcha);
        }
        [NonAction]
        private void InvalidarTokenCaptcha(string token)
        {
            SeguridadCL seguridadCL = new SeguridadCL(mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mServicesUtilVirtuosoAndReplication);
            seguridadCL.EliminarCaptchaToken(token);
        }
        [NonAction]
        private void AumentarNumIntentosIP(string IP)
        {
            SeguridadCL seguridadCL = new SeguridadCL(mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mServicesUtilVirtuosoAndReplication);
            seguridadCL.AumentarNumIntentosIP(IP);
        }

        #endregion

        #region Metodos generales
        
        /// <summary>
        /// Envia las cookies al dominio que hizo la petici�n por medio de una redirecci�n a obtenercookie.aspx
        /// </summary>
        /// <param name="pDominioDeVuelta">Dominio que hizo la petici�n</param>
        /// <param name="pRedirect">URL a la que hay que redirigir al usuario una vez finalice el login</param>
        /// <param name="pToken">Token generado por el dominio de origen</param>
        /// <param name="pMismoUsuarioLogueado">Verdad si el usuario ya estaba logueado previamente</param>
        [NonAction]
        private void EnviarCookies(string pDominioDeVuelta, string pRedirect, string pToken, bool pMismoUsuarioLogueado)
        {
            string dominio = mConfigService.ObtenerUrlServicioLogin();

            if (string.IsNullOrEmpty(dominio))
            {
                dominio = ObtenerDominioIP();
            }

            string proyectoID = Request.Query["proyectoID"];
            string queryProyectoID = string.Empty;

            if (!string.IsNullOrEmpty(proyectoID))
            {
                queryProyectoID = $"&proyectoID={proyectoID}";
            }

            string mismoUsuarioLogueado = "";
            if (pMismoUsuarioLogueado)
            {
                mismoUsuarioLogueado = "&eliminarCookie=false";
            }
            
            string query = $"urlVuelta={pDominioDeVuelta}&redirect={HttpUtility.UrlEncode(pRedirect)}&token={pToken}{mismoUsuarioLogueado}{queryProyectoID}";

            Response.Redirect($"{dominio}/obtenerCookie?{query}");
        }

        #endregion

        #region Metodos auxiliares Acciones externas


        /// <summary>
        /// Intenta realizar el login con el servicio externo en caso de que exista
        /// </summary>
        /// <param name="pLogin"></param>
        /// <param name="pPasswd"></param>
        /// <param name="pRedirect"></param>
        /// <param name="pToken"></param>
        ///<returns>'error=' en caso de que de un error 'sinactivar' en caso de que este sin activar '' en caso de que no este configurado</returns>
        [NonAction]
        public string LoginConServicioExterno(ref string pLogin, ref string pPasswd, ref string pRedirect, ref string pToken)
        {
            Guid proyectoSeleccionado = ProyectoAD.MetaProyecto;
            if (!string.IsNullOrEmpty(Request.Query["proyectoID"]))
            {
                proyectoSeleccionado = new Guid(Request.Query["proyectoID"]);
            }
            GestorParametroAplicacion gestorParametroApp = new GestorParametroAplicacion();
            gestorParametroApp.ParametroAplicacion = ParametroAplicacionDS;
            JsonEstado jsonEstadoLogin = ControladorIdentidades.AccionEnServicioExternoEcosistema(TipoAccionExterna.LoginConRegistro, proyectoSeleccionado, Guid.Empty, "", "", pLogin, pPasswd, gestorParametroApp, null, null, null, null);

            //1.- Login OK (se loguea correctamente en el servicio externo)
            //jsonEstadoLogin.Correcto=true y jsonEstadoLogin.InfoExtra con JsonUsuario
            //2.- Login KO (no existe el usuario en el servicio externo)
            //jsonEstadoLogin.Correcto=false y jsonEstadoLogin.InfoExtra vacio
            //3.- Login sin activar (el login es correcto pero no est� activado, hay que mostrar el popup)
            //jsonEstadoLogin.Correcto=false y jsonEstadoLogin.InfoExtra con usuario

            if (jsonEstadoLogin != null)
            {
                if (!jsonEstadoLogin.Correcto)
                {
                    bool loginErroneo = false;
                    if (string.IsNullOrEmpty(jsonEstadoLogin.InfoExtra))
                    {
                        loginErroneo = true;
                    }
                    try
                    {
                        //Si no se loguea en el servicio pero devuelve usuario (est� sin activar)
                        JsonUsuario jsonNuevoUsuario = JsonConvert.DeserializeObject<JsonUsuario>(jsonEstadoLogin.InfoExtra);
                    }
                    catch (Exception)
                    {
                        loginErroneo = true;
                    }

                    if (loginErroneo)
                    {
                        //Si no se loguea en el servicio y no devuelve usuario ponemos vacios el login y el pass para que no se loguee
                        pLogin = "";
                        pPasswd = "";

                        if (!string.IsNullOrEmpty(jsonEstadoLogin.InfoExtra))
                        {
                            return "error=" + jsonEstadoLogin.InfoExtra;
                        }
                    }
                    else
                    {
                        return "sinactivar";
                    }
                }
                else
                {
                    //Login
                    UsuarioCN usuarioCN = new UsuarioCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication);
                    DataWrapperUsuario usuarioDWLogin = usuarioCN.ObtenerUsuarioPorLoginOEmail(pLogin, false);

                    if (usuarioDWLogin.ListaUsuario.Count == 0)
                    {
                        JsonUsuario jsonNuevoUsuario = JsonConvert.DeserializeObject<JsonUsuario>(jsonEstadoLogin.InfoExtra);

                        if (jsonNuevoUsuario != null)
                        {
                            //Creamos el ususario en la aplicaci�n
                            string loginUsuario = UtilCadenas.LimpiarCaracteresNombreCortoRegistro(jsonNuevoUsuario.name) + '-' + UtilCadenas.LimpiarCaracteresNombreCortoRegistro(jsonNuevoUsuario.last_name);

                            if (loginUsuario.Length > 12)
                            {
                                loginUsuario = loginUsuario.Substring(0, 12);
                            }

                            int hashNumUsu = 1;
                            while (usuarioCN.ExisteUsuarioEnBD(loginUsuario))
                            {
                                loginUsuario = loginUsuario.Substring(0, loginUsuario.Length - hashNumUsu.ToString().Length) + hashNumUsu.ToString();
                                hashNumUsu++;
                            }

                            string nombreCortoUsuario = loginUsuario;
                            hashNumUsu = 1;
                            while (usuarioCN.ExisteNombreCortoEnBD(nombreCortoUsuario))
                            {
                                nombreCortoUsuario = nombreCortoUsuario.Substring(0, nombreCortoUsuario.Length - hashNumUsu.ToString().Length) + hashNumUsu.ToString();
                                hashNumUsu++;
                            }

                            //Usuario
                            GestionUsuarios gestorUsuarios = new GestionUsuarios(usuarioDWLogin, mLoggingService, mEntityContext, mConfigService);

                            UsuarioGnoss usuario = gestorUsuarios.AgregarUsuario(loginUsuario, nombreCortoUsuario, HashHelper.CalcularHash(pPasswd, true), true);


                            Es.Riam.Gnoss.AD.EntityModel.Models.UsuarioDS.Usuario filaUsuario = usuario.FilaUsuario;
                            filaUsuario.EstaBloqueado = false;

                            //Cargamos datos del proyecto
                            Guid ProyectoSeleccionadoID = new Guid(Request.Query["proyectoID"]);
                            ProyectoCN proyCN = new ProyectoCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication);
                            GestionProyecto gestorProy = new GestionProyecto(proyCN.ObtenerProyectoPorID(ProyectoSeleccionadoID), mLoggingService, mEntityContext);
                            proyCN.Dispose();
                            Proyecto ProyectoSeleccionado = gestorProy.ListaProyectos[ProyectoSeleccionadoID];

                            ParametroGeneralCN paramCN = new ParametroGeneralCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication);
                            Es.Riam.Gnoss.AD.EntityModel.Models.ParametroGeneralDS.ParametroGeneral ParametrosGeneralesRow = paramCN.ObtenerFilaParametrosGeneralesDeProyecto(ProyectoSeleccionadoID);
                            paramCN.Dispose();


                            DataWrapperSolicitud solicitudDW = new DataWrapperSolicitud();

                            Solicitud filaSolicitud = new Solicitud();
                            filaSolicitud.Estado = (short)EstadoSolicitud.Aceptada;

                            filaSolicitud.FechaSolicitud = DateTime.Now;
                            filaSolicitud.FechaProcesado = filaSolicitud.FechaSolicitud;
                            filaSolicitud.OrganizacionID = ProyectoSeleccionado.FilaProyecto.OrganizacionID;
                            filaSolicitud.ProyectoID = ProyectoSeleccionado.Clave;
                            filaSolicitud.SolicitudID = Guid.NewGuid();
                            solicitudDW.ListaSolicitud.Add(filaSolicitud);
                            mEntityContext.Solicitud.Add(filaSolicitud);

                            SolicitudNuevoUsuario filaNuevoUsuario = new SolicitudNuevoUsuario();
                            filaNuevoUsuario.SolicitudID = filaSolicitud.SolicitudID;
                            filaNuevoUsuario.UsuarioID = filaUsuario.UsuarioID;
                            filaNuevoUsuario.NombreCorto = nombreCortoUsuario;
                            filaNuevoUsuario.Nombre = jsonNuevoUsuario.name;
                            filaNuevoUsuario.Apellidos = jsonNuevoUsuario.last_name;
                            filaNuevoUsuario.Email = jsonNuevoUsuario.email;
                            filaNuevoUsuario.EsBuscable = true;
                            filaNuevoUsuario.EsBuscableExterno = false;

                            if (!ParametrosGeneralesRow.PrivacidadObligatoria)
                            {
                                filaNuevoUsuario.EsBuscable = false;
                                filaNuevoUsuario.EsBuscableExterno = false;
                            }

                            filaNuevoUsuario.FaltanDatos = false;
                            filaNuevoUsuario.Idioma = UtilIdiomas.LanguageCode;
                            filaNuevoUsuario.Provincia = "";
                            solicitudDW.ListaSolicitudNuevoUsuario.Add(filaNuevoUsuario);
                            mEntityContext.SolicitudNuevoUsuario.Add(filaNuevoUsuario);


                            JsonEstado jsonEstadoRegistroTrasLoginConRegistro = ControladorIdentidades.AccionEnServicioExternoEcosistema(TipoAccionExterna.RegistroTrasLoginConRegistro, ProyectoSeleccionadoID, filaNuevoUsuario.UsuarioID, jsonNuevoUsuario.name, jsonNuevoUsuario.last_name, pLogin, pPasswd, gestorParametroApp, null, null, null, null);

                            if (jsonEstadoRegistroTrasLoginConRegistro == null || jsonEstadoRegistroTrasLoginConRegistro.Correcto)
                            {
                                GuardarDatosExtra(solicitudDW, filaSolicitud, jsonNuevoUsuario.extra_data, ProyectoSeleccionado);

                                ControladorIdentidades.AceptarUsuario(filaSolicitud.SolicitudID, solicitudDW, usuarioDWLogin, ProyectoSeleccionado, ProyectoSeleccionado.FilaProyecto.URLPropia, UrlIntragnoss, UrlIntragnossServicios);
                                pRedirect = mControladorBase.UrlsSemanticas.ObtenerURLComunidad(UtilIdiomas, BaseURLIdioma, ProyectoSeleccionado.NombreCorto) + "/" + UtilIdiomas.GetText("URLSEM", "REGISTROUSUARIO") + "/1?prefer=1";
                            }
                            else
                            {
                                pLogin = "";
                                pPasswd = "";
                            }
                        }
                        else
                        {
                            //Si no hay que registrarlo ponemos vacios el login y el pass para que no se loguee
                            pLogin = "";
                            pPasswd = "";
                        }
                    }

                    if (usuarioDWLogin.ListaUsuario.Count > 0)
                    {
                        string loginUsuario = usuarioDWLogin.ListaUsuario.First().Login;

                        bool mismoUsuarioLogueado = ComprobarMismoUsuarioLogueado(loginUsuario);

                        ValidarUsuario(pLogin, pPasswd, null, true);

                        if (Request.Cookies.ContainsKey("_Envio"))
                        {
                            CookieOptions options = new CookieOptions();
                            options.Expires = DateTime.Now.AddDays(-1);
                            Response.Cookies.Append("_Envio", "0", options);
                        }
                        string dominioDeVuelta = UtilDominios.ObtenerDominioUrl(new Uri(pRedirect), true);


                        if (!string.IsNullOrEmpty(Request.Query["proyectoID"]))
                        {
                            Guid proyectoID = new Guid(Request.Query["proyectoID"]);
                            ProyectoCL proyectoCL = new ProyectoCL(mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mVirtuosoAD, mServicesUtilVirtuosoAndReplication);
                            GestionProyecto gestorProy = new GestionProyecto(proyectoCL.ObtenerProyectoPorID(proyectoID), mLoggingService, mEntityContext);

                            Proyecto proyecto = gestorProy.ListaProyectos[proyectoID];
                            if (proyecto.ListaTipoProyectoEventoAccion.ContainsKey(TipoProyectoEventoAccion.Login))
                            {
                                proyectoCL.AgregarEventosAccionProyectoPorProyectoYUsuarioID(proyectoID, mUsuarioID, TipoProyectoEventoAccion.Login);
                            }
                            proyectoCL.Dispose();
                        }

                        EnviarCookies(dominioDeVuelta, pRedirect, pToken, mismoUsuarioLogueado);
                    }

                    usuarioCN.Dispose();
                }
            }
            return "";
        }

        /// <summary>
        /// Guarda los datos extra
        /// </summary>
        [NonAction]
        private void GuardarDatosExtra(DataWrapperSolicitud pSolicitudDW, Solicitud pSolicitudRow, List<JsonDatosExtraUsuario> pDatosExtra, Proyecto pProyecto)
        {
            ProyectoCN proyectoCN = new ProyectoCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication);
            DataWrapperProyecto dataWrapperProyecto = proyectoCN.ObtenerDatosExtraProyectoPorID(pProyecto.Clave);
            proyectoCN.Dispose();

            foreach (JsonDatosExtraUsuario datoextraUsuario in pDatosExtra)
            {
                foreach (Es.Riam.Gnoss.AD.EntityModel.Models.ProyectoDS.DatoExtraEcosistemaVirtuoso filaEcosistema in dataWrapperProyecto.ListaDatoExtraEcosistemaVirtuoso)
                {
                    if (!string.IsNullOrEmpty(filaEcosistema.NombreCampo) && !string.IsNullOrEmpty(datoextraUsuario.value) && filaEcosistema.NombreCampo == datoextraUsuario.name)
                    {
                        DatoExtraEcosistemaVirtuosoSolicitud datoExtraEcosistemaVirtuosoSolicitud = new DatoExtraEcosistemaVirtuosoSolicitud();
                        datoExtraEcosistemaVirtuosoSolicitud.DatoExtraID = filaEcosistema.DatoExtraID;
                        datoExtraEcosistemaVirtuosoSolicitud.Solicitud = pSolicitudRow;
                        datoExtraEcosistemaVirtuosoSolicitud.SolicitudID = pSolicitudRow.SolicitudID;
                        datoExtraEcosistemaVirtuosoSolicitud.Opcion = datoextraUsuario.value;
                        pSolicitudDW.ListaDatoExtraEcosistemaVirtuosoSolicitud.Add(datoExtraEcosistemaVirtuosoSolicitud);
                        mEntityContext.DatoExtraEcosistemaVirtuosoSolicitud.Add(datoExtraEcosistemaVirtuosoSolicitud);
                    }
                }

                foreach (Es.Riam.Gnoss.AD.EntityModel.Models.ProyectoDS.DatoExtraProyectoVirtuoso filaProyecto in dataWrapperProyecto.ListaDatoExtraProyectoVirtuoso)
                {
                    if (!string.IsNullOrEmpty(filaProyecto.NombreCampo) && !string.IsNullOrEmpty(datoextraUsuario.value) && filaProyecto.NombreCampo == datoextraUsuario.name)
                    {
                        DatoExtraEcosistemaVirtuosoSolicitud datoExtraEcosistemaVirtuosoSolicitud = new DatoExtraEcosistemaVirtuosoSolicitud();
                        datoExtraEcosistemaVirtuosoSolicitud.DatoExtraID = filaProyecto.DatoExtraID;
                        datoExtraEcosistemaVirtuosoSolicitud.Solicitud = pSolicitudRow;
                        datoExtraEcosistemaVirtuosoSolicitud.SolicitudID = pSolicitudRow.SolicitudID;
                        datoExtraEcosistemaVirtuosoSolicitud.Opcion = datoextraUsuario.value;
                        pSolicitudDW.ListaDatoExtraEcosistemaVirtuosoSolicitud.Add(datoExtraEcosistemaVirtuosoSolicitud);
                        mEntityContext.DatoExtraEcosistemaVirtuosoSolicitud.Add(datoExtraEcosistemaVirtuosoSolicitud);
                    }
                }
            }
        }

        /// <summary>
        /// Genera el token para la doble autenticacion, redirige al usuario a la paguina para autenticarse y le envia un correo con el token
        /// </summary>
        private void AutenticarUsuarioConDobleFactor(string pLogin, string pBaseURL, string pIdioma, Guid pProyectoID, Guid pUsuarioID)
        {
            //Genera el token para la doble autenticacion
            string tokenAutenticacion = GenerarToken();
            ProyectoCL proyectoCL = new ProyectoCL(mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mVirtuosoAD, mServicesUtilVirtuosoAndReplication);
            //Agrega el token a cache durante 10 minutos
            proyectoCL.AgregarObjetoCache($"DobleAutenticacion_{tokenAutenticacion}", pLogin, 600);

            ProyectoCN proyectoCN = new ProyectoCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication);
            GestionProyecto gestionProyecto = new GestionProyecto(new DataWrapperProyecto(), mLoggingService, mEntityContext);
            Proyecto proyecto = new Proyecto(proyectoCN.ObtenerProyectoPorIDCargaLigera(pProyectoID), gestionProyecto, mLoggingService, mEntityContext);

            GnossUrlsSemanticas gnossUrlsSemanticas = new GnossUrlsSemanticas(mLoggingService, mEntityContext, mConfigService, mServicesUtilVirtuosoAndReplication);
            string urlBase = gnossUrlsSemanticas.ObtenerURLComunidad(new Es.Riam.Gnoss.Recursos.UtilIdiomas(pIdioma, mLoggingService, mEntityContext, mConfigService, mRedisCacheWrapper), pBaseURL, proyecto.NombreCorto);

            //Envia el correo al usuario con el token para autenticarse
            EnviarCorreoAutenticacionDobleFactor(pUsuarioID, proyecto, tokenAutenticacion);

            string redireccion = Request.Query["redirect"];
            
            if (!string.IsNullOrEmpty(redireccion))
            {
                if (redireccion.StartsWith("http://") || redireccion.StartsWith("https://"))
                {
                    string urlPropia = proyectoCN.ObtenerURLPropiaProyecto(pProyectoID).Replace("http://", string.Empty).Replace("https://", string.Empty);
                    
                    redireccion = $"/redirect{redireccion.Replace("http://", string.Empty).Replace("https://", string.Empty).Replace(urlPropia, string.Empty)}";
                }
                else
                {
                    int inicioRedireccion = redireccion.LastIndexOf("comunidad");
                    if (inicioRedireccion > 0)
                    {
                        redireccion = $"/redirect/{redireccion.Substring(inicioRedireccion)}";
                    }
                    else
                    {
                        if (redireccion.StartsWith('/'))
                        {
                            redireccion = redireccion.Substring(1);
                        }

                        redireccion = $"/redirect/{redireccion}";
                    }
                }
            }

            //Redirige al usuario a la p�gina de autenticacion
            Response.Redirect($"{urlBase}/{UtilIdiomas.GetText("URLSEM", "AUTENTICACIONDOBLEFACTOR")}{redireccion}");
        }

        /// <summary>
        /// Genera un toke aleatorio de 6 digitos
        /// </summary>
        /// <returns>Token</returns>
        private string GenerarToken()
        {
            string characters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            StringBuilder token = new StringBuilder();
            Random random = new Random();

            for (int i = 0; i < 6; i++)
            {
                int indice = random.Next(characters.Length);
                token.Append(characters[indice]);
            }

            return token.ToString();
        }

        /// <summary>
        /// Envia un correo con el token necesario para autenticarse con el doble factor
        /// </summary>
        private void EnviarCorreoAutenticacionDobleFactor(Guid pUsuarioID, Proyecto proyecto, string token)
        {
            DataWrapperPeticion peticionDW = new DataWrapperPeticion();
            Peticion filaPeticion = new Peticion();
            filaPeticion.Estado = (short)EstadoPeticion.Pendiente;
            filaPeticion.FechaPeticion = DateTime.Now;
            filaPeticion.PeticionID = Guid.NewGuid();
            filaPeticion.UsuarioID = pUsuarioID;
            filaPeticion.Tipo = (int)TipoPeticion.AutenticacionDobleFactor;

            peticionDW.ListaPeticion.Add(filaPeticion);
            mEntityContext.Peticion.Add(filaPeticion);

            PersonaCN personaCN = new PersonaCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication);
            DataWrapperPersona dataWrapperPersona = personaCN.ObtenerPersonaPorUsuario(pUsuarioID);

            if (dataWrapperPersona.ListaPersona.Count > 0)
            {
                GestionPersonas gestorPersonas = new GestionPersonas(dataWrapperPersona, mLoggingService, mEntityContext);
                Es.Riam.Gnoss.AD.EntityModel.Models.PersonaDS.Persona filaPersona = dataWrapperPersona.ListaPersona.FirstOrDefault();

                NotificacionCN notificacionCN = new NotificacionCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication);

                GestionNotificaciones gestorNotificaciones = new GestionNotificaciones(new DataWrapperNotificacion(), mLoggingService, mEntityContext, mConfigService, mServicesUtilVirtuosoAndReplication);

                gestorNotificaciones.AgregarNotificacionPeticionAutenticacionDobleFactor(gestorPersonas.ListaPersonas[filaPersona.PersonaID], token, proyecto, UtilIdiomas.LanguageCode);

                notificacionCN.ActualizarNotificacion();
            }
        }

        #endregion

        #region M�todos auxiliares Servicio Login Externo

        [NonAction]
        private JsonLoginExterno PeticionServicioLoginExterno(string pUrl, UtilWeb.Metodo pMetodo, Dictionary<string, string> pParametros)
        {
            string urlCreacion = pUrl;
            string parametros = "";
            if (pParametros != null)
            {
                foreach (string claveParametro in pParametros.Keys)
                {
                    if (!string.IsNullOrEmpty(parametros))
                    {
                        parametros += "&";
                    }
                    parametros += claveParametro + "=" + HttpUtility.UrlEncode(pParametros[claveParametro]);
                }
            }
            JsonLoginExterno respuesta = WebRequest(pMetodo, urlCreacion, parametros, "application/x-www-form-urlencoded", "");
            return respuesta;
        }

        /// <summary>
        /// Env�a una petici�n web
        /// </summary>
        /// <param name="pMetodo">M�todo Http (GET, POST, PUT, etc)</param>
        /// <param name="pUrl">Url completa del recurso web</param>
        /// <param name="pPostData">Datos para enviar en la petici�n (en formato querystring)</param>
        /// <returns>Respuesta del servidor</returns>
        [NonAction]
        private static JsonLoginExterno WebRequest(UtilWeb.Metodo pMetodo, string pUrl, string pPostData, string pContentType, string pAccept)
        {
            HttpWebRequest webRequest = null;
            StreamWriter requestWriter = null;
            JsonLoginExterno responseData = null;

            webRequest = System.Net.WebRequest.Create(pUrl) as HttpWebRequest;
            webRequest.Method = pMetodo.ToString();
            webRequest.ServicePoint.Expect100Continue = false;
            webRequest.Timeout = 200000;
            webRequest.UserAgent = UtilWeb.GenerarUserAgent();

            if (!string.IsNullOrEmpty(pContentType))
            {
                webRequest.ContentType = pContentType;
            }
            if (!string.IsNullOrEmpty(pAccept))
            {
                webRequest.Accept = pAccept;
            }


            if (pMetodo == UtilWeb.Metodo.POST || pMetodo == UtilWeb.Metodo.PUT || pMetodo == UtilWeb.Metodo.DELETE)
            {
                //Enviamos los datos
                requestWriter = new StreamWriter(webRequest.GetRequestStream());
                try
                {
                    requestWriter.Write(pPostData);
                }
                catch
                {
                    throw;
                }
                finally
                {
                    requestWriter.Close();
                    requestWriter = null;
                }
            }
            responseData = WebResponseGet(webRequest);

            webRequest = null;

            return responseData;
        }

        /// <summary>
        /// Procesa la respuesta del servidor a una petici�n
        /// </summary>
        /// <param name="pWebRequest">Petici�n Http</param>
        /// <returns>Datos de la respuesta del servidor</returns>
        [NonAction]
        private static JsonLoginExterno WebResponseGet(HttpWebRequest pWebRequest)
        {
            StreamReader responseReader = null;
            string responseData = "";
            JsonLoginExterno loginUsuario = null;

            try
            {
                responseReader = new StreamReader(pWebRequest.GetResponse().GetResponseStream(), Encoding.UTF8);
                responseData = responseReader.ReadToEnd();
                loginUsuario = JsonConvert.DeserializeObject<JsonLoginExterno>(responseData);
            }
            catch (WebException webEx)
            {
                StreamReader reader = new StreamReader(webEx.Response.GetResponseStream());
                string error = reader.ReadToEnd();
                throw;
            }
            finally
            {
                pWebRequest.GetResponse().GetResponseStream().Close();
                responseReader.Close();
                responseReader = null;
            }
            return loginUsuario;
        }

        #endregion

        #region Propiedades

        public string UrlIntragnoss
        {
            get
            {
                if (string.IsNullOrEmpty(mUrlIntragnoss))
                {
                    mUrlIntragnoss = (string)ParametroAplicacionDS.Find(parametroApp => parametroApp.Equals("UrlIntragnoss")).Valor;
                }
                return mUrlIntragnoss;
            }
        }

        public string UrlIntragnossServicios
        {
            get
            {
                if (string.IsNullOrEmpty(mUrlIntragnossServicios))
                {
                    mUrlIntragnossServicios = (string)ParametroAplicacionDS.Find(parametroApp => parametroApp.Parametro.Equals("UrlIntragnossServicios")).Valor;
                }
                return mUrlIntragnossServicios;
            }
        }


        /// <summary>
        /// DataSet con los par�metros de la aplicaci�n.
        /// </summary>
        public List<ParametroAplicacion> ParametroAplicacionDS
        {
            get
            {
                if (mParametroAplicacion == null)
                {
                    ParametroAplicacionCL paramCL = new ParametroAplicacionCL(mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mServicesUtilVirtuosoAndReplication);
                    mParametroAplicacion = paramCL.ObtenerParametrosAplicacionPorContext();
                }
                return mParametroAplicacion;
            }
        }

        #endregion

    }
}
