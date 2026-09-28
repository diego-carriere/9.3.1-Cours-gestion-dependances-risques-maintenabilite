using Meteo.Api.Endpoints;
using Meteo.Application;
using Meteo.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Détecte au build du conteneur toute dépendance captive (un singleton qui capture un
// service scoped) sans attendre qu'elle se manifeste en production — Support J1, "durées
// de vie : le piège de la dépendance captive". Activé sans condition d'environnement : la
// suite e2e le revérifie à chaque exécution (voir Meteo.Api.E2ETests).
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

// Le composition root : les deux seules lignes de tout le projet qui relient une
// implémentation à une abstraction. Rien d'autre, nulle part, ne doit appeler `new` sur
// une dépendance (Support J1, "le problème du new partout").
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapForecastEndpoint();
app.MapHealthChecks("/health");

app.Run();

// Permet à WebApplicationFactory<Program> (Meteo.Api.E2ETests) de trouver le point d'entrée.
public partial class Program;
