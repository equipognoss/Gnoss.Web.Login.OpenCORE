using Es.Riam.AbstractsOpen;
using Es.Riam.Gnoss.AD.EncapsuladoDatos;
using Es.Riam.Gnoss.AD.EntityModel;
using Es.Riam.Gnoss.AD.EntityModel.Models.IdentidadDS;
using Es.Riam.Gnoss.AD.EntityModel.Models.UsuarioDS;
using Es.Riam.Gnoss.AD.EntityModelBASE;
using Es.Riam.Gnoss.AD.ServiciosGenerales;
using Es.Riam.Gnoss.AD.Usuarios;
using Es.Riam.Gnoss.AD.Virtuoso;
using Es.Riam.Gnoss.CL;
using Es.Riam.Gnoss.CL.Identidad;
using Es.Riam.Gnoss.CL.Live;
using Es.Riam.Gnoss.CL.ServiciosGenerales;
using Es.Riam.Gnoss.CL.Usuarios;
using Es.Riam.Gnoss.Elementos.Identidad;
using Es.Riam.Gnoss.Elementos.Peticiones;
using Es.Riam.Gnoss.Elementos.ServiciosGenerales;
using Es.Riam.Gnoss.Logica.Identidad;
using Es.Riam.Gnoss.Logica.Peticion;
using Es.Riam.Gnoss.Logica.ServiciosGenerales;
using Es.Riam.Gnoss.Logica.Usuarios;
using Es.Riam.Gnoss.Util.Configuracion;
using Es.Riam.Gnoss.Util.General;
using Es.Riam.Util;
using Es.Riam.Web.Util;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Xml;

namespace Gnoss.Web.Login
{
    public class ControllerBaseLoginRedesSociales : ControllerBaseLogin
    {

        #region Miembros

        /// <summary>
        /// TokenLogin
        /// </summary>
        public string mTokenLogin = null;

        /// <summary>
        /// Id del proyecto en el que loguearse
        /// </summary>
        public string mProyectoIDSeleccionado = null;

        /// <summary>
        /// Id de invitacion a comunidad
        /// </summary>
        public string mInvitacionAComunidadID = null;

        /// <summary>
        /// Url de origen
        /// </summary>
        public string mUrlOrigen = null;

        /// <summary>
        /// indica si es simplificado
        /// </summary>
        public bool mSimplificado = false;

        public string mEventoID = string.Empty;

        /// <summary>
        /// Proyecto en el que loguearse
        /// </summary>
        public Proyecto mProyectoSeleccionado = null;

        /// <summary>
        /// Invitacion a comunidad
        /// </summary>
        public PeticionInvComunidad mInvitacionAComunidad = null;

        /// <summary>
        /// Parámetros de un proyecto.
        /// </summary>
        private Dictionary<string, string> mParametroProyecto;

        #endregion


        public ControllerBaseLoginRedesSociales(LoggingService loggingService, IHttpContextAccessor httpContextAccessor, EntityContext entityContext, ConfigService configService, RedisCacheWrapper redisCacheWrapper, GnossCache gnossCache, VirtuosoAD virtuosoAD, IHostingEnvironment env, EntityContextBASE entityContextBASE, IServicesUtilVirtuosoAndReplication servicesUtilVirtuosoAndReplication) : base(loggingService, httpContextAccessor, entityContext, configService, redisCacheWrapper, gnossCache, virtuosoAD, env, entityContextBASE, servicesUtilVirtuosoAndReplication)
        {
            //Recogemos los parámetros (token, proyecto e invitacion)
            string state = mHttpContextAccessor.HttpContext.Request.Query["state"];
            if (!string.IsNullOrWhiteSpace(mHttpContextAccessor.HttpContext.Request.Query["proyectoID"]))
            {
                ProyectoIDSeleccionado = mHttpContextAccessor.HttpContext.Request.Query["proyectoID"];
            }
            if (state != null)
            {
                string[] allkeys = state.Split(new string[] { "AND_AND" }, StringSplitOptions.None);

                tokenLogin = allkeys[0];
                ProyectoIDSeleccionado = allkeys[1];
                InvitacionAComunidadID = allkeys[2];
                UrlOrigen = allkeys[3];
                Simplificado = allkeys[4].ToLower() == "true";

                if (allkeys.Length > 4)
                {
                    EventoID = allkeys[5];
                }
            }
            else if (!string.IsNullOrWhiteSpace(mHttpContextAccessor.HttpContext.Request.Query["token"]))
            {
                if (!string.IsNullOrWhiteSpace(mHttpContextAccessor.HttpContext.Request.Query["token"]))
                {
                    tokenLogin = mHttpContextAccessor.HttpContext.Request.Query["token"];
                }
                if (!string.IsNullOrWhiteSpace(mHttpContextAccessor.HttpContext.Request.Query["proyectoID"]))
                {
                    ProyectoIDSeleccionado = mHttpContextAccessor.HttpContext.Request.Query["proyectoID"];
                }
                if (!string.IsNullOrWhiteSpace(mHttpContextAccessor.HttpContext.Request.Query["peticionID"]))
                {
                    InvitacionAComunidadID = mHttpContextAccessor.HttpContext.Request.Query["peticionID"];
                }
                else if (!string.IsNullOrWhiteSpace(mHttpContextAccessor.HttpContext.Request.Query["eventoComID"]))
                {
                    EventoID = mHttpContextAccessor.HttpContext.Request.Query["eventoComID"];
                }

                if (!string.IsNullOrWhiteSpace(mHttpContextAccessor.HttpContext.Request.Query["urlOrigen"]))
                {
                    UrlOrigen = mHttpContextAccessor.HttpContext.Request.Query["urlOrigen"];
                }
                if (!string.IsNullOrWhiteSpace(mHttpContextAccessor.HttpContext.Request.Query["simplificado"]))
                {
                    Simplificado = mHttpContextAccessor.HttpContext.Request.Query["simplificado"] == "true";
                }
            }
            else if (mHttpContextAccessor.HttpContext.Session.GetString("paramsLoginOauth") != null)
            {
                string[] allkeys = mHttpContextAccessor.HttpContext.Session.GetString("paramsLoginOauth").Split(new string[] { "AND_AND" }, StringSplitOptions.None);

                tokenLogin = allkeys[0];
                ProyectoIDSeleccionado = allkeys[1];
                InvitacionAComunidadID = allkeys[2];
                UrlOrigen = allkeys[3];
                Simplificado = allkeys[4].ToLower() == "true";

                if (allkeys.Length > 4)
                {
                    EventoID = allkeys[5];
                }
            }
            mHttpContextAccessor.HttpContext.Session.Remove("paramsLoginOauth");
        }

        #region Metodos Login de usuarios
        /// <summary>
        /// Procede con el inicio de sesión del usuario, realizando lo que corresponda para cada caso
        /// </summary>
        /// <param name="pTipoRedSocial">Tipo de red social de la que procede el Login</param>
        /// <param name="pIDenRedSocial">Identificador en esa red social</param>
        /// <param name="pNombre">Nombre del usuario (extraido de la red social)</param>
        /// <param name="pApellidos">Apellidos del usuario (extraido de la red social)</param>
        /// <param name="pCorreo">Correo del usuario (extraido de la red social)</param>
        /// <param name="pFechaNacimiento">Fecha de nacimiento</param>
        /// <param name="pHombre">Sexo del usuario True si es hombre (extraido de la red social)</param>
        [NonAction]
        public void ProcesarInicioDeSesionDeUsuario(TipoRedSocialLogin pTipoRedSocial, string pIDenRedSocial, string pNombre, string pApellidos, string pCorreo, DateTime? pFechaNacimiento, bool? pHombre)
        {
            //Si Ya esta vinculada iniciamos la vinculada
            UsuarioCN usuarioCN = new UsuarioCN("acid",mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication);
            Guid usuarioGnossID = usuarioCN.ObtenerUsuarioPorLoginEnRedSocial(pTipoRedSocial, pIDenRedSocial);

            DataWrapperUsuario usuarioValidadoDW = usuarioCN.ObtenerUsuarioPorLoginOEmail(pCorreo, false);
            if (usuarioValidadoDW.ListaUsuario != null && usuarioValidadoDW.ListaUsuario.Count > 0)
            {
                usuarioValidadoDW.ListaUsuario.First().Validado = (short)ValidacionUsuario.Verificado;
                usuarioCN.ActualizarUsuario(false);
            }

            //URL por si tenemos que redirigir al paso 1 del registro (santillana);
            string urlRegistro = "";

            //Si no está vinculada pero el login es de GMAIL o santillana y ya existe el correo le asociamos y le logueamos
            if ((pTipoRedSocial == TipoRedSocialLogin.Google || pTipoRedSocial == TipoRedSocialLogin.Santillana) && Guid.Empty.Equals(usuarioGnossID))
            {
                DataWrapperUsuario usuarioDW = usuarioCN.ObtenerUsuarioPorLoginOEmail(pCorreo, false);

                // TODO Javi: Si no hay usuarios, comprobar la cookie userId y obtener ese usuario

                if (usuarioDW.ListaUsuario != null && usuarioDW.ListaUsuario.Count > 0)
                {
                    usuarioGnossID = usuarioDW.ListaUsuario.First().UsuarioID;

                    if (pTipoRedSocial == TipoRedSocialLogin.Google)
                    {
                        //Insertamos en la tabla  UsuarioVinculadoLoginRedesSociales      
                        Es.Riam.Gnoss.AD.EntityModel.Models.UsuarioDS.UsuarioVinculadoLoginRedesSociales filaUsuarioVinculadoLoginRedesSociales = new Es.Riam.Gnoss.AD.EntityModel.Models.UsuarioDS.UsuarioVinculadoLoginRedesSociales();
                        filaUsuarioVinculadoLoginRedesSociales.UsuarioID = usuarioGnossID;
                        filaUsuarioVinculadoLoginRedesSociales.TipoRedSocial = (short)pTipoRedSocial;
                        filaUsuarioVinculadoLoginRedesSociales.IDenRedSocial = pIDenRedSocial;
                        usuarioDW.ListaUsuarioVinculadoLoginRedesSociales.Add(filaUsuarioVinculadoLoginRedesSociales);
                        mEntityContext.UsuarioVinculadoLoginRedesSociales.Add(filaUsuarioVinculadoLoginRedesSociales);

                        usuarioCN.ActualizarUsuario(false);


                        // TODO Javi integrar desde linea 149
                    }
                }
            }
            usuarioCN.Dispose();
            if (!Guid.Empty.Equals(usuarioGnossID))
            {

                if (!string.IsNullOrEmpty(ProyectoIDSeleccionado))
                {
                    Guid proyectoID = new Guid(ProyectoIDSeleccionado);
                    ProyectoCL proyectoCL = new ProyectoCL(mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mVirtuosoAD, mServicesUtilVirtuosoAndReplication);
                    GestionProyecto gestorProy = new GestionProyecto(proyectoCL.ObtenerProyectoPorID(proyectoID),mLoggingService, mEntityContext);

                    Proyecto proyecto = gestorProy.ListaProyectos[proyectoID];
                    TipoProyectoEventoAccion tipoAccion = TipoProyectoEventoAccion.Login;
                    switch (pTipoRedSocial)
                    {
                        case TipoRedSocialLogin.Facebook:
                            tipoAccion = TipoProyectoEventoAccion.LoginFacebook;
                            break;
                        case TipoRedSocialLogin.Google:
                            tipoAccion = TipoProyectoEventoAccion.LoginGoogle;
                            break;
                        case TipoRedSocialLogin.Twitter:
                            tipoAccion = TipoProyectoEventoAccion.LoginTwitter;
                            break;
                        case TipoRedSocialLogin.Santillana:
                            tipoAccion = TipoProyectoEventoAccion.LoginSantillana;
                            break;
                    }
                    if (proyecto.ListaTipoProyectoEventoAccion.ContainsKey(tipoAccion))
                    {
                        proyectoCL.AgregarEventosAccionProyectoPorProyectoYUsuarioID(proyectoID, usuarioGnossID, tipoAccion);
                    }
                    proyectoCL.Dispose();
                }
                //Logueamos al usuario vinculado
                LoginUsuario(usuarioGnossID.ToString(), urlRegistro);
                return;
            }
            Dictionary<string, object> listaDatosUsuario = new Dictionary<string, object>();
            listaDatosUsuario.Add("tipored", (short)pTipoRedSocial);
            listaDatosUsuario.Add("id", pIDenRedSocial);
            listaDatosUsuario.Add("nombre", pNombre);
            listaDatosUsuario.Add("apellidos", pApellidos);
            listaDatosUsuario.Add("correo", pCorreo);
            listaDatosUsuario.Add("hombre", pHombre);
            listaDatosUsuario.Add("nacimiento", pFechaNacimiento);
            listaDatosUsuario.Add("token", "");
            listaDatosUsuario.Add("grupos", new Dictionary<string, List<string>>());

            UsuarioCL usuarioCL = new UsuarioCL(mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mServicesUtilVirtuosoAndReplication);
            usuarioCL.GuardarDatosRedSocial(pIDenRedSocial, listaDatosUsuario);
            usuarioCL.Dispose();

            string UrlComunidad = BaseURLIdioma;
            if (ProyectoSeleccionado.Clave != ProyectoAD.MetaProyecto)
            {

                ProyectoCN proyCN = new ProyectoCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication);
                UrlComunidad = new GnossUrlsSemanticas(mLoggingService, mEntityContext, mConfigService, mServicesUtilVirtuosoAndReplication).GetURLHacerseMiembroComunidad(BaseURLIdioma, UtilIdiomas, proyCN.ObtenerNombreCortoProyecto(new Guid(ProyectoIDSeleccionado)), true);
                proyCN.Dispose();
            }
            else
            {
                UrlComunidad += "/" + UtilIdiomas.GetText("URLSEM", "LOGIN");
            }

            UrlComunidad += "/reload?loginid=" + pIDenRedSocial + "&proyID=" + ProyectoIDSeleccionado + "&invitacionID=" + InvitacionAComunidadID + "&urlOrigen=" + UrlOrigen + "&simplificado=" + Simplificado;

            if (!string.IsNullOrEmpty(EventoID))
            {
                UrlComunidad += "&eventoComID=" + EventoID;
            }

            if (!string.IsNullOrEmpty(InvitacionAComunidadID))
            {
                UrlComunidad += "&peticionID=" + InvitacionAComunidadID;
            }

            try
            {
                Response.Redirect(UrlComunidad);
            }
            catch (System.Threading.ThreadAbortException) { }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        // public static Dictionary<string, string> ObtenerParametrosLoginExterno(TipoRedSocialLogin pTipoRedSocial, Dictionary<string, string> pParametrosProyecto, ParametroAplicacionDS pParametrosAplicacion)
        public static Dictionary<string, string> ObtenerParametrosLoginExterno(TipoRedSocialLogin pTipoRedSocial, Dictionary<string, string> pParametrosProyecto, List<Es.Riam.Gnoss.AD.EntityModel.ParametroAplicacion> pParametrosAplicacion)
        {
            string key = "login" + pTipoRedSocial.ToString();

            string parametros = "";
            if (pParametrosProyecto.ContainsKey(key))
            {
                parametros = pParametrosProyecto[key];
            }
            //else if (pParametrosAplicacion.Select("parametro = '" + key + "'").Length > 0)
            else if (pParametrosAplicacion.Where(parametro => parametro.Parametro.Equals(key)).ToList().Count > 0)
            {
                parametros = pParametrosAplicacion.Where(parametro => parametro.Parametro.Equals(key)).ToList().First().Valor;
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
        /// 
        /// </summary>
        /// <param name="pCorreoOIDUsuario"></param>
        private void LoginUsuario(string pCorreoOIDUsuario, string pUrlRegistro)
        {
            Configuracion.ObtenerDesdeFicheroConexion = true;

            UsuarioCN usuarioCN = new UsuarioCN("acid", mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication);
            DataWrapperUsuario dataWrapperUsuario = new DataWrapperUsuario();

            Es.Riam.Gnoss.AD.EntityModel.Models.UsuarioDS.Usuario filaUsuario = null;

            try
            {
                //Si es un Guid obtenemos el usuario por el ID
                filaUsuario = usuarioCN.ObtenerUsuarioPorID(new Guid(pCorreoOIDUsuario));
                dataWrapperUsuario.ListaUsuario.Add(filaUsuario);
            }
            catch
            {
                //Si no es un Guid obtenemos el usuario por el correo
                dataWrapperUsuario = usuarioCN.ObtenerUsuarioPorLoginOEmail(pCorreoOIDUsuario, false);
            }

            if (dataWrapperUsuario.ListaUsuario.Count > 0)
            {
                filaUsuario = dataWrapperUsuario.ListaUsuario.First();
            }

            usuarioCN.Dispose();

            PersonaCN personaCN = new PersonaCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication);
            Es.Riam.Gnoss.AD.EntityModel.Models.PersonaDS.Persona filaPersona = personaCN.ObtenerPersonaPorUsuario(filaUsuario.UsuarioID).ListaPersona.FirstOrDefault();

            LoguearUsuario(filaUsuario.UsuarioID, filaPersona.PersonaID, filaUsuario.NombreCorto, filaUsuario.Login, filaPersona.Idioma);

            string UrlComunidad = BaseURLIdioma;
            if (ProyectoSeleccionado.Clave != ProyectoAD.MetaProyecto)
            {
                ProyectoCN proyCN = new ProyectoCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication);
                UrlComunidad = new GnossUrlsSemanticas(mLoggingService, mEntityContext, mConfigService, mServicesUtilVirtuosoAndReplication).GetURLHacerseMiembroComunidad(BaseURLIdioma, UtilIdiomas, proyCN.ObtenerNombreCortoProyecto(new Guid(ProyectoIDSeleccionado)), true);
                proyCN.Dispose();
            }
            else
            {
                UrlComunidad += "/" + UtilIdiomas.GetText("URLSEM", "LOGIN");
            }
            UrlComunidad += "/reload";

            string dominioDeVuelta = UtilDominios.ObtenerDominioUrl(new Uri(UrlComunidad), true);
            if (!string.IsNullOrEmpty(pUrlRegistro))
            {
                UrlComunidad += "?urlRegistro=" + pUrlRegistro;
            }
            else
            {
                UrlComunidad = UrlComunidad.Replace(dominioDeVuelta + "/", "");
            }

            AgregarFilaUsuarioContadores(filaUsuario.UsuarioID);
            EnviarCookies(dominioDeVuelta, UrlComunidad, tokenLogin);
        }

        private void AgregarFilaUsuarioContadores(Guid pUsuarioID)
        {
            UsuarioCN usuarioCN = new UsuarioCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication);
            usuarioCN.ActualizarContadorUsuarioNumAccesos(pUsuarioID);
            usuarioCN.Dispose();
        }

        private void EnviarCookies(string pDominioDeVuelta, string pRedirect, string pToken)
        {
            string query = $"urlVuelta={pDominioDeVuelta}&redirect={HttpUtility.UrlEncode(pRedirect)}&token={pToken}";

            string dominio = mConfigService.ObtenerUrlServicioLogin();

            Response.Redirect(dominio + "/obtenerCookie?" + query);
        }

        #endregion


        #region Métodos auxiliares
        /// <summary>
        /// Obtiene la respuesta en forma de texto de una URL
        /// </summary>
        /// <param name="pUrl">URL</param>
        /// <returns></returns>
        [NonAction]
        public string ObtenerDatos(string pUrl)
        {
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Add("UserAgent", UtilWeb.GenerarUserAgent());
            Stream dataStream = client.GetStreamAsync($"{pUrl}").Result;
            StreamReader reader = new StreamReader(dataStream);
            string responseFromServer = reader.ReadToEnd();
            reader.Close();
            dataStream.Close();
            return responseFromServer;
        }
        #endregion

        #region Propiedades


        /// <summary>
        /// Parámetros de un proyecto.
        /// </summary>
        public Dictionary<string, string> ParametroProyecto
        {
            get
            {
                if (mParametroProyecto == null)
                {
                    ProyectoCL proyectoCL = new ProyectoCL(mEntityContext, mLoggingService,mRedisCacheWrapper, mConfigService, mVirtuosoAD, mServicesUtilVirtuosoAndReplication);
                    mParametroProyecto = proyectoCL.ObtenerParametrosProyecto(ProyectoSeleccionado.Clave);
                    proyectoCL.Dispose();
                }

                return mParametroProyecto;
            }
        }

        /// <summary>
        /// Token de Login
        /// </summary>
        public string tokenLogin
        {
            get
            {
                return mTokenLogin;
            }
            set
            {
                mTokenLogin = value;
            }
        }

        /// <summary>
        /// ProyectoID en el que se hace login
        /// </summary>
        public string ProyectoIDSeleccionado
        {
            get
            {
                return mProyectoIDSeleccionado;
            }
            set
            {
                mProyectoIDSeleccionado = value;
            }
        }

        /// <summary>
        /// Proyecto en el que se hace login
        /// </summary>
        public Proyecto ProyectoSeleccionado
        {
            get
            {
                if (mProyectoSeleccionado == null)
                {
                    Guid proyectoID = new Guid(ProyectoIDSeleccionado);

                    ProyectoCN proyCN = new ProyectoCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication);
                    GestionProyecto gestorProy = new GestionProyecto(proyCN.ObtenerProyectoPorID(proyectoID), mLoggingService, mEntityContext);
                    proyCN.Dispose();

                    mProyectoSeleccionado = gestorProy.ListaProyectos[proyectoID];
                }
                return mProyectoSeleccionado;
            }
        }


        /// <summary>
        /// ID de la invitación a la comunidad
        /// </summary>
        public string InvitacionAComunidadID
        {
            get
            {
                return mInvitacionAComunidadID;
            }
            set
            {
                mInvitacionAComunidadID = value;
            }
        }


        /// <summary>
        /// Invitación a la comunidad (si existe)
        /// </summary>
        public PeticionInvComunidad InvitacionAComunidad
        {
            get
            {
                if (mInvitacionAComunidad == null && !string.IsNullOrEmpty(InvitacionAComunidadID))
                {
                    Guid peticionID = new Guid(InvitacionAComunidadID);

                    PeticionCN peticionCN = new PeticionCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication);
                    GestionPeticiones gestionPeticion = new GestionPeticiones(peticionCN.ObtenerPeticionPorID(peticionID),mLoggingService, mEntityContext);
                    mInvitacionAComunidad = (PeticionInvComunidad)gestionPeticion.ListaPeticiones[peticionID];

                    peticionCN.Dispose();
                }
                return mInvitacionAComunidad;
            }
        }

        /// <summary>
        /// Indica sui es simplificado
        /// </summary>
        public bool Simplificado
        {
            get
            {
                return mSimplificado;
            }
            set
            {
                mSimplificado = value;
            }
        }


        public string EventoID
        {
            get
            {
                return mEventoID;
            }
            set
            {
                mEventoID = value;
            }
        }

        /// <summary>
        /// URL origen
        /// </summary>
        public string UrlOrigen
        {
            get
            {
                return mUrlOrigen;
            }
            set
            {
                mUrlOrigen = value;
            }
        }
        #endregion

    }
}
