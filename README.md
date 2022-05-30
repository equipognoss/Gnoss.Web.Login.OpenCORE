![](https://content.gnoss.ws/imagenes/proyectos/personalizacion/7e72bf14-28b9-4beb-82f8-e32a3b49d9d3/cms/logognossazulprincipal.png)

# Gnoss.Web.Login.OpenCORE

![](https://github.com/equipognoss/Gnoss.Web.Login/workflows/BuildLogin/badge.svg)

Aplicación Web que se encarga de autenticar al usuario, validar su contraseña y enviar las credenciales a la Web. 

Si en una misma plataforma existen varios dominios (ej: community.gnoss.com, forum.gnoss.com, myorg.gnoss.com, …), el servicio de login es también un Single Sign On y se encarga de conectar y desconectar al usuario en todos los dominios de la plataforma en los que el usuario acceda. 

Configuración estandar de esta aplicación en el archivo docker-compose.yml: 

```yml
login:
    image: login
    env_file: .env
    ports:
     - ${puerto_login}:80
    environment:
     virtuosoConnectionString: ${virtuosoConnectionString}
     acid: ${acid}
     base: ${base}
     redis__redis__ip__master: ${redis__redis__ip__master}
     redis__redis__ip__read: ${redis__redis__ip__read}
     redis__redis__bd: ${redis__redis__bd}
     redis__redis__timeout: ${redis__redis__timeout}
     redis__recursos__ip__master: ${redis__recursos__ip__master}
     redis__recursos__ip__read: ${redis__recursos__ip__read}
     redis__recursos__bd: ${redis__recursos__bd}
     redis__recursos__timeout: ${redis__redis__timeout}
     idiomas: ${idiomas}
     Servicios__urlBase: ${Servicios__urlBase}
     connectionType: ${connectionType} 
    volumes:
      - ./logs/login:/app/logs
```

Se pueden consultar los posibles valores de configuración de cada parámetro aquí: https://github.com/equipognoss/Gnoss.Platform.Deploy
