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
    public class TestController : Controller
    {


        /// <summary>
        /// Método page load
        /// </summary>
        /// <param name="sender">sender</param>
        /// <param name="e">e</param>
        [HttpGet]
        public IActionResult Index()
        {
            return Content($"PathBase: {Request.PathBase} Path: {Request.Path}");
        }

        
    }
}
