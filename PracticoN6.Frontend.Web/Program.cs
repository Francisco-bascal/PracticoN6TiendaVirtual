using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using PracticoN6.Frontend.Shared;
using PracticoN6.Frontend.Shared.Extensions;
using PracticoN6.Frontend.Shared.Services;
using PracticoN6.Frontend.Web.Servicios;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Dirección de la API: se puede cambiar en wwwroot/appsettings.json (o desde la pantalla "Servidor" de la app).
string urlApi = builder.Configuration["ApiUrl"] ?? "http://localhost:5276";

builder.Services.AddTiendaShared(opciones => opciones.UrlApiPorDefecto = urlApi);

// En el navegador, tanto los datos sensibles como las preferencias van al almacenamiento local (localStorage).
builder.Services.AddScoped<IAlmacenamientoSeguro, AlmacenamientoNavegador>();
builder.Services.AddScoped<IPreferencias, AlmacenamientoNavegador>();
builder.Services.AddScoped<IGestorArchivos, GestorArchivosNavegador>();
builder.Services.AddScoped<IConectividad, ConectividadNavegador>();
builder.Services.AddScoped<ICompartir, CompartirNavegador>();

await builder.Build().RunAsync();
