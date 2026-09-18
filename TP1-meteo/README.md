# TP1 — API Météo

`GET /forecast?address=<adresse>` renvoie les prévisions du lieu, en enchaînant deux
services externes : **Nominatim** (adresse → lat/lon) puis **Open-Meteo** (lat/lon →
prévisions). Ce README est la pièce à charge du TP : il justifie chaque choix
architectural face au support du Jour 1 (`Support J1.md`), et doit permettre de défendre
le projet sans relire le code.

## Lancer et tester

```bash
dotnet build                     # 0 avertissement : TreatWarningsAsErrors=true
dotnet test                      # 51 tests, hors-ligne, déterministes

# L'app refuse de démarrer sans User-Agent Nominatim (politique d'usage de Nominatim) :
Nominatim__UserAgent="MonNom/1.0 (contact: mon.email@exemple.fr)" \
  dotnet run --project src/Meteo.Api

curl "http://localhost:5000/forecast?address=Alès"
curl -i "http://localhost:5000/forecast"                # 400
curl -i "http://localhost:5000/forecast?address=zzzzzz" # 404
curl -i "http://localhost:5000/health"                  # 200

docker build -t meteo-api .
docker run -p 8080:8080 -e Nominatim__UserAgent="..." meteo-api
```

## Architecture en couches

```
Présentation        Meteo.Api             UI, contrôleur d'API, mapping HTTP
      │
Application          Meteo.Application     Orchestration du cas d'usage
      │
Domaine               Meteo.Domain          Modèle, règles, ports (interfaces)
      ▲
Infrastructure       Meteo.Infrastructure   Nominatim, Open-Meteo, cache, horloge
```

Directement la slide "L'architecture en couches" de Support J1 : chaque couche dépend de
celle du dessous, jamais de celle du dessus. La flèche remontant de l'Infrastructure vers
le Domaine (et non l'inverse) est le point du TP — voir ci-dessous.

### Deux graphes, deux directions

**À l'exécution**, la dépendance va vers l'extérieur : le cas d'usage appelle Nominatim et
Open-Meteo via le réseau.

```
Meteo.Application → (réseau) → Nominatim / Open-Meteo
```

**À la compilation**, la flèche est inversée — c'est l'inversion de dépendance :

```
Meteo.Api ──────────► Meteo.Application ──────────► Meteo.Domain
    │                                                    ▲
    └──────────► Meteo.Infrastructure ───────────────────┘
                     (implémente IGeocoder, IWeatherProvider, ICache<T>, IClock)
```

`Meteo.Infrastructure` référence `Meteo.Domain`, **pas** `Meteo.Application` : elle
implémente les ports du Domaine sans rien connaître du cas d'usage qui les consomme.
`Meteo.Domain.csproj` ne référence **aucun** projet ni paquet NuGet — vérifiable :

```
$ dotnet list src/Meteo.Domain/Meteo.Domain.csproj package
Project 'Meteo.Domain' has the following package references
   [net10.0]: No packages were found for this framework.
```

Et testé, pas seulement affirmé : `Meteo.Domain.Tests.ArchitectureTests` vérifie par
réflexion que l'assembly du Domaine ne référence que la BCL, et que celui de l'Application
ne référence ni ASP.NET Core ni Polly.

## Table des durées de vie (IoC / DI)

| Service | Durée de vie | Pourquoi |
|---|---|---|
| `IGetForecastUseCase` → `GetForecastUseCase` | **Scoped** | Unité de travail par requête HTTP, sans état entre deux requêtes. |
| `IGeocoder` → `GeocodingClient` | **Transient** (`AddHttpClient`) | Voir "La question du singleton" ci-dessous. |
| `IWeatherProvider` → `WeatherClient` | **Transient** (`AddHttpClient`) | Idem. |
| `IClock` → `SystemClock` | **Singleton** | Sans état, aucune dépendance scoped → zéro risque de dépendance captive. |
| `ICache<T>` → `MemoryCache<T>` | **Singleton** | Un cache par requête ne cache rien. |
| `IOptions<NominatimOptions>` / `OpenMeteoOptions` / `ResilienceOptions` | **Singleton** | Configuration lue une fois au démarrage. |
| Pipeline Polly (retry/breaker/timeout/limiteur) | **Singleton** (géré par le framework) | Un disjoncteur réinitialisé à chaque requête ne s'ouvre jamais. |
| Pool de `HttpMessageHandler` | **Singleton** (géré par `IHttpClientFactory`) | Sockets réutilisés, rotation DNS toutes les 2 min. |

### La question du singleton

L'intuition de départ était que l'endpoint de prévision serait câblé en **Singleton**.
C'est le mauvais réflexe, et c'est précisément le piège que Support J1 nomme **dépendance
captive** : *"Un singleton qui reçoit un service scoped le garde pour toujours."*

`AddHttpClient<IGeocoder, GeocodingClient>()` enregistre le client typé en **Transient** —
par conception du framework, pas par oubli. `IHttpClientFactory` sépare l'objet en deux
durées de vie : le `HttpClient`/client typé, jetable, et la chaîne de
`HttpMessageHandler` (sockets, DNS), mutualisée par la fabrique — elle-même singleton. Le
bénéfice recherché avec "singleton" (pas d'épuisement de sockets) est donc déjà là, sans
son coût : un `GeocodingClient` réellement singleton figerait le handler à vie (DNS jamais
rafraîchi — le bug classique du `HttpClient` statique) et capturerait tout service scoped
qu'on lui injecterait un jour.

Ce qui est légitimement singleton : le cache, l'horloge, les options, les pipelines Polly
— l'**état qui doit survivre aux requêtes**, jamais les objets sur le chemin de la requête.

**Vérifié, pas seulement argumenté** : `Program.cs` active
`ValidateScopes`/`ValidateOnBuild` sans condition d'environnement, et
`Meteo.Api.E2ETests.ForecastEndpointTests.The_DI_container_builds_without_any_captive_dependency`
le revérifie à chaque exécution de la suite de tests.

## Composition root et injection

`src/Meteo.Api/Program.cs` est le **seul** fichier qui relie une implémentation à une
abstraction (`AddApplication()` + `AddInfrastructure(configuration)`). Cette règle est
mécanique, pas seulement documentée :

1. Les adaptateurs d'Infrastructure (`GeocodingClient`, `WeatherClient`, `MemoryCache<T>`,
   `SystemClock`) sont `internal` : le seul type public de l'assembly est
   `InfrastructureServiceCollectionExtensions`. `Program.cs` ne peut **pas compiler**
   `new GeocodingClient(...)`.
2. `Meteo.Application` ne référence pas `Meteo.Infrastructure` : elle ne peut même pas
   *nommer* `GeocodingClient`.

**Injection par constructeur partout** (la forme "RECOMMANDÉE" de Support J1). Par
propriété : nulle part. Par méthode : un seul cas, volontairement — le
`CancellationToken` est passé à chaque appel plutôt qu'injecté, parce que c'est *"un besoin
lié à un appel"* (la forme "PONCTUELLE"). Les trois formes de la slide sont ainsi couvertes
honnêtement, pas cochées de façade.

## SOLID, ancré dans ce code

| Principe | Où |
|---|---|
| **S**RP | `GeocodingClient` ne fait que parler HTTP à Nominatim et traduire panne/JSON en `Result<GeoLocation>`. `GetForecastUseCase` orchestre sans aucune E/S directe. |
| **O**CP | Ajouter un second géocodeur (stratégie "dédoubler" de Support J1) : une classe `IGeocoder` de plus + une ligne dans `AddInfrastructure`. Zéro fichier existant modifié. |
| **L**SP | `GeocoderContractTests` (dans `Meteo.TestSupport`) est héritée par les tests du fake et du vrai client : aucune implémentation ne peut laisser fuir une exception pour un échec attendu. |
| **I**SP | `IGeocoder` et `IWeatherProvider` : un port, une méthode. L'endpoint ne dépend que d'`IGetForecastUseCase`, jamais des ports du Domaine directement. |
| **D**IP | `IGeocoder`/`IWeatherProvider`/`ICache<T>`/`IClock` sont déclarés dans `Meteo.Domain`, implémentés dans `Meteo.Infrastructure`. Le compilateur rend impossible au cas d'usage de nommer une implémentation concrète. |

## Gestion des erreurs

`Result<T>` pour les échecs métier attendus (adresse introuvable, service externe en
panne) — jamais d'exception. Les vrais bugs continuent de lever et finissent en 500. La
traduction `ForecastErrorKind` → code HTTP vit dans un seul fichier
(`Meteo.Api/Errors/ForecastErrorMapper.cs`).

| `ForecastErrorKind` | HTTP |
|---|---|
| `InvalidAddress` | 400 |
| `AddressNotFound` | 404 (jamais dégradé : pas transitoire) |
| `GeocodingUnavailable` / `WeatherUnavailable` | 503 + `Retry-After`, ou 200 dégradé si un cache périmé est utilisable |
| `UpstreamTimeout` | 504 |

Un `switch` C# sur un enum n'est jamais réellement exhaustif (CS8524) : l'exhaustivité
"toute valeur gérée" est vérifiée par `ForecastErrorMapperTests`, pas seulement par le
compilateur.

## Résilience et mode dégradé

Pipeline Polly par client typé : timeout total → retry (exponentiel + jitter, jamais sur
4xx ni sur un disjoncteur déjà ouvert) → circuit breaker → timeout par tentative. Nominatim
reçoit en plus un limiteur à 1 req/s (sa politique d'usage l'exige) — en pratique le cache
de géocodage (24h) le rend presque inutile : `DegradedModeTests` prouve que deux requêtes
identiques ne contactent Nominatim qu'une fois.

`GetForecastUseCase` compare l'horodatage des entrées de cache à `IClock.UtcNow` (jamais
`DateTimeOffset.UtcNow` en dur) pour décider : cache frais → pas d'appel réseau ; échec +
cache périmé → réponse `200` avec `degraded: true` ; échec sans rien d'utilisable → `503`.
C'est la stratégie SPOF de Support J1 : *"circuit breaker et cache pour un mode dégradé"*.

## Dépendances cachées neutralisées

| Dépendance cachée (Support J1) | Parade |
|---|---|
| Variable d'environnement | Aucun `Environment.GetEnvironmentVariable` dans le code ; tout passe par `IOptions<T>`, validé au démarrage. |
| Singleton / état global | Les seuls singletons du graphe (cache, horloge, options, pipelines) sont sans dépendance scoped — pas de dépendance captive, vérifié en test. |
| Horloge système | `IClock`, jamais `DateTimeOffset.UtcNow` en dehors de `SystemClock`. |
| Culture / séparateur décimal | `CultureInfo.InvariantCulture` explicite à chaque parsing/formatage de nombre ; `WeatherClientTests` le vérifie sous `fr-FR`. |

## Conventions de code

- **Central Package Management** (`Directory.Packages.props`) : une seule version par
  paquet pour toute la solution — réponse directe à la slide du conflit transitif
  (`Lib B v1.2` vs `v2.0`).
- `TreatWarningsAsErrors=true` partout, y compris les tests.
- `Nullable=enable`, `records` pour le modèle/DTO, `internal sealed class` pour les
  services, `sealed` par défaut.
- Fakes écrits à la main (`Meteo.TestSupport`), pas de librairie de mock — chaque paquet
  NuGet est une dépendance à justifier, et les ports n'ont qu'une méthode chacun.

### Dépendances transitives (l'inventaire que Support J1 demande de savoir faire)

```
$ dotnet list src/Meteo.Api/Meteo.Api.csproj package --include-transitive
   Transitive Package                                             Resolved
   > Microsoft.Extensions.AmbientMetadata.Application             10.10.0
   > Microsoft.Extensions.Compliance.Abstractions                 10.10.0
   > Microsoft.Extensions.DependencyInjection.AutoActivation      10.10.0
   > Microsoft.Extensions.Diagnostics.ExceptionSummarization      10.10.0
   > Microsoft.Extensions.Http.Diagnostics                        10.10.0
   > Microsoft.Extensions.Http.Resilience                         10.10.0
   > Microsoft.Extensions.Resilience                              10.10.0
   > Microsoft.Extensions.Telemetry                                10.10.0
   > Microsoft.Extensions.Telemetry.Abstractions                  10.10.0
   > Polly.Core                                                   8.4.2
   > Polly.Extensions                                             8.4.2
   > Polly.RateLimiting                                           8.4.2
```

Douze paquets jamais mentionnés dans un `PackageReference` : **le seul paquet demandé
directement pour la résilience est `Microsoft.Extensions.Http.Resilience`, et il en
apporte douze avec lui.** C'est la slide "directes ou transitives" de Support J1, rendue
concrète sur ce projet précis.

## Tests

| Projet | Nature | Compte |
|---|---|---|
| `Meteo.Domain.Tests` | unitaire + architecture | 11 |
| `Meteo.Application.Tests` | unitaire (fakes) | 13 |
| `Meteo.Infrastructure.Tests` | unitaire HTTP (sans réseau) + résilience | 14 |
| `Meteo.Api.E2ETests` | e2e en mémoire (transport simulé) | 13 |

**51 tests, aucun appel réseau, aucun ne dépasse quelques centaines de millisecondes.**

Le point le plus démonstratif : remplacer les APIs externes en test ne demande de changer
**aucune** ligne de code de production — seul le `HttpMessageHandler` primaire est
substitué (`Meteo.Api.E2ETests/FakeUpstream.cs`). Tout le reste — client typé,
désérialisation, pipeline Polly, conteneur DI, routage, mapping `ProblemDetails` — reste
réel. C'est la preuve, exécutable, que l'IoC/DI rend le code testable.

## Ce qui a été volontairement simplifié

- Un seul appel réseau réel a été fait pendant le développement (vérification manuelle de
  la chaîne complète) ; tout le reste tourne hors-ligne.
- `ForecastResult` ne distingue que dégradé/non-dégradé, pas les trois états
  live/cache-fresh/cache-stale — aucun scénario testé n'a besoin de la nuance.
- Les deux clients HTTP partagent le même pipeline de résilience composé à la main (seuls
  les seuils diffèrent), plutôt que de montrer aussi le handler standard du framework.
- Le limiteur de débit Nominatim n'a pas de test de minutage dédié (un test temps réel
  serait lent ou fragile) ; son effet pratique est couvert indirectement par le test du
  cache de géocodage.
- Le `Dockerfile` suit le patron multi-étapes standard .NET mais n'a pas pu être vérifié
  par un `docker build` réel dans cet environnement de développement.
