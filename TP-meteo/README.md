# TP1, TP2 & TP3 — API Météo

`GET /forecast?address=<adresse>` renvoie les prévisions du lieu, en enchaînant deux
services externes : un **géocodeur** (adresse → lat/lon) puis un **fournisseur météo**
(lat/lon → prévisions).

- **TP1** (`documentation/TP1.md`) : Nominatim + Open-Meteo, avec couplage faible, IoC et DI.
- **TP2** (`documentation/TP2.md`) : ajout de la BAN et de MET Norway derrière les mêmes ports, choix du
  fournisseur par configuration, sans recompilation ni redémarrage — voir
  [TP2 — changement d'API](#tp2--changement-dapi).
- **TP3** (`documentation/TP3.md`) : mode démo, cache de géocodage prouvé, format de sortie unifié — voir
  [TP3](#tp3--mode-démo-cache-et-format-unifié).

Ce README est la pièce à charge des deux TP : il justifie chaque choix architectural face
au support du Jour 1 (`../Support J1.md`), et doit permettre de défendre le projet sans
relire le code. Les commits sont préfixés `[TP1]` ou `[TP2]` selon le TP qu'ils servent.

## Lancer et tester

```bash
dotnet build                     # 0 avertissement : TreatWarningsAsErrors=true
dotnet test                      # 106 tests, hors-ligne, déterministes

# User-Agent identifiable fourni par appsettings.json (Nominatim et MET Norway renvoient
# 403 sans lui). L'app refuse de démarrer si l'un d'eux est vide ; surchargeable :
dotnet run --project src/Meteo.Api
MetNo__UserAgent="MonApp/1.0 mon.email@exemple.fr" dotnet run --project src/Meteo.Api

# Choix des fournisseurs (voir TP2) — au démarrage ou à chaud dans appsettings.json :
Providers__Geocoder=ban Providers__Weather=met-no dotnet run --project src/Meteo.Api

curl "http://localhost:5000/forecast?address=Alès"
curl -i "http://localhost:5000/forecast"                # 400
curl -i "http://localhost:5000/forecast?address=zzzzzz" # 404
curl -i "http://localhost:5000/health"                  # 200

docker build -t meteo-api .
docker run -p 8080:8080 -e Providers__Weather=met-no meteo-api
```

## Architecture en couches

```
Présentation        Meteo.Api             UI, contrôleur d'API, mapping HTTP
      │
Application          Meteo.Application     Orchestration du cas d'usage
      │
Domaine               Meteo.Domain          Modèle, règles, ports (interfaces)
      ▲
Infrastructure       Meteo.Infrastructure   Nominatim, BAN, Open-Meteo, MET Norway, cache, horloge
```

Directement la slide "L'architecture en couches" de Support J1 : chaque couche dépend de
celle du dessous, jamais de celle du dessus. La flèche remontant de l'Infrastructure vers
le Domaine (et non l'inverse) est le point du TP — voir ci-dessous.

### Deux graphes, deux directions

**À l'exécution**, la dépendance va vers l'extérieur : le cas d'usage appelle le
géocodeur et le fournisseur météo actifs via le réseau.

```
Meteo.Application → (réseau) → Nominatim | BAN  puis  Open-Meteo | MET Norway
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
| `IGeocoder` → `GeocoderSelector` | **Transient** | Sans état : relit `Providers:Geocoder` à chaque appel (TP2). |
| `IWeatherProvider` → `WeatherProviderSelector` | **Transient** | Idem avec `Providers:Weather`. |
| `IGeocoder` keyed `nominatim`/`ban` → `NominatimGeocodingClient`/`BanGeocodingClient` | **Transient** (`AddHttpClient`) | Voir "La question du singleton" ci-dessous. |
| `IWeatherProvider` keyed `open-meteo`/`met-no` → `OpenMeteoWeatherClient`/`MetNoWeatherClient` | **Transient** (`AddHttpClient`) | Idem. |
| `IClock` → `SystemClock` | **Singleton** | Sans état, aucune dépendance scoped → zéro risque de dépendance captive. |
| `ICache<T>` → `MemoryCache<T>` | **Singleton** | Un cache par requête ne cache rien. |
| `IOptions<NominatimOptions>` / `BanOptions` / `OpenMeteoOptions` / `MetNoOptions` / `ResilienceOptions` | **Singleton** | Configuration lue une fois ; les options validées au démarrage ne sont jamais revalidées à chaud (voir TP2). |
| Pipeline Polly (retry/breaker/timeout/limiteur) | **Singleton** (géré par le framework) | Un disjoncteur réinitialisé à chaque requête ne s'ouvre jamais. |
| Pool de `HttpMessageHandler` | **Singleton** (géré par `IHttpClientFactory`) | Sockets réutilisés, rotation DNS toutes les 2 min. |

### La question du singleton

L'intuition de départ était que l'endpoint de prévision serait câblé en **Singleton**.
C'est le mauvais réflexe, et c'est précisément le piège que Support J1 nomme **dépendance
captive** : *"Un singleton qui reçoit un service scoped le garde pour toujours."*

`AddHttpClient<NominatimGeocodingClient>()` enregistre le client typé en **Transient** —
par conception du framework, pas par oubli. `IHttpClientFactory` sépare l'objet en deux
durées de vie : le `HttpClient`/client typé, jetable, et la chaîne de
`HttpMessageHandler` (sockets, DNS), mutualisée par la fabrique — elle-même singleton. Le
bénéfice recherché avec "singleton" (pas d'épuisement de sockets) est donc déjà là, sans
son coût : un `NominatimGeocodingClient` réellement singleton figerait le handler à vie (DNS jamais
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

1. Les adaptateurs d'Infrastructure (`NominatimGeocodingClient`, `BanGeocodingClient`,
   `OpenMeteoWeatherClient`, `MetNoWeatherClient`, les deux sélecteurs, `MemoryCache<T>`,
   `SystemClock`) sont `internal` : le seul type public de l'assembly est
   `InfrastructureServiceCollectionExtensions`. `Program.cs` ne peut **pas compiler**
   `new NominatimGeocodingClient(...)` (vérifié par `AdapterIsolationTests`).
2. `Meteo.Application` ne référence pas `Meteo.Infrastructure` : elle ne peut même pas
   *nommer* un adaptateur.

**Injection par constructeur partout** (la forme "RECOMMANDÉE" de Support J1). Par
propriété : nulle part. Par méthode : un seul cas, volontairement — le
`CancellationToken` est passé à chaque appel plutôt qu'injecté, parce que c'est *"un besoin
lié à un appel"* (la forme "PONCTUELLE"). Les trois formes de la slide sont ainsi couvertes
honnêtement, pas cochées de façade.

## SOLID, ancré dans ce code

| Principe | Où |
|---|---|
| **S**RP | `NominatimGeocodingClient` ne fait que parler HTTP à Nominatim et traduire panne/JSON en `Result<GeoLocation>`. `GetForecastUseCase` orchestre sans aucune E/S directe. |
| **O**CP | Mis à l'épreuve par le TP2 : la BAN et MET Norway sont chacun une classe `IGeocoder`/`IWeatherProvider` de plus, enregistrée sous une clé dans `AddInfrastructure`. Ports, Application et Présentation n'ont pas été modifiés pour les accueillir. |
| **L**SP | `GeocoderContractTests`/`HttpGeocoderContractTests` et leurs équivalents météo (dans `Meteo.TestSupport`) sont hérités par les tests du fake et de chaque vrai client : aucune implémentation ne peut laisser fuir une exception pour un échec attendu. |
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
| Variable d'environnement / fichier de configuration | Aucun `Environment.GetEnvironmentVariable` dans le code ; tout passe par `IOptions<T>` validé au démarrage, ou par `IConfiguration` injectée pour le seul choix de fournisseur. |
| Singleton / état global | Les seuls singletons du graphe (cache, horloge, options, pipelines) sont sans dépendance scoped — pas de dépendance captive, vérifié en test. |
| Horloge système | `IClock`, jamais `DateTimeOffset.UtcNow` en dehors de `SystemClock`. |
| Culture / séparateur décimal | `CultureInfo.InvariantCulture` explicite à chaque parsing/formatage de nombre ; `HttpWeatherProviderContractTests` le vérifie sous `fr-FR` pour chaque fournisseur météo. |

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

## TP2 — changement d'API

Mesure du TP : le coût du changement. Chaque demande a été résolue en **ajoutant** du code
dans `Meteo.Infrastructure`. Hors Infrastructure, seuls deux changements : un ajout au
Domaine (le vocabulaire canonique `WeatherVariables`, sans toucher aux ports ni au modèle
existants) et la configuration (`appsettings.json`). Application et code de Présentation :
aucune ligne modifiée.

### 1. Deux nouveaux adaptateurs derrière les ports existants

| Port | TP1 | TP2 | Piège propre au fournisseur, absorbé dans l'adaptateur |
|---|---|---|---|
| `IGeocoder` | `NominatimGeocodingClient` | `BanGeocodingClient` | GeoJSON ordonne `[longitude, latitude]`, l'inverse de Nominatim. |
| `IWeatherProvider` | `OpenMeteoWeatherClient` | `MetNoWeatherClient` | User-Agent identifiable obligatoire (sinon 403), unité en toutes lettres (`celsius` → `°C`). |

Le User-Agent (`TP2-MeteoApi/1.0 <e-mail>`) vient de la configuration, jamais d'une chaîne
en dur ; `MetNoOptions`/`NominatimOptions` refusent le démarrage s'il est absent, et
`ProviderSwitchingTests.Switching_to_MetNo_sends_a_descriptive_User_Agent` vérifie qu'il
est bien envoyé.

### 2. Fournisseur configurable, sans recompilation ni redémarrage

```json
"Providers": { "Geocoder": "nominatim", "Weather": "open-meteo" }
```

Valeurs : `nominatim` | `ban` et `open-meteo` | `met-no` (casse indifférente). Chaque
client est enregistré sous une **clé DI** ; le port lui-même n'a qu'une implémentation,
un **sélecteur** (`GeocoderSelector`, `WeatherProviderSelector`) qui relit la clé dans
`IConfiguration` **à chaque appel** et délègue au client correspondant. Modifier
`appsettings.json` (rechargé à chaud par ASP.NET Core) bascule donc le fournisseur du
processus en cours d'exécution — prouvé par
`ProviderSwitchingTests.Weather_provider_switches_between_two_requests_without_recreating_the_host`.

- Clé inconnue **au démarrage** : l'app refuse de démarrer (`ProvidersOptionsValidator` +
  `ValidateOnStart`).
- Clé inconnue **après un rechargement à chaud** : repli sur le fournisseur par défaut,
  avec un avertissement dans les logs. Une faute de frappe ne coupe pas le service.
- Les options validées au démarrage sont liées **une seule fois** (`BindOnce`, dans
  `InfrastructureServiceCollectionExtensions`) : `OptionsBuilder.Bind` combiné à
  `ValidateOnStart` les ferait revalider à chaque rechargement, et une valeur invalide
  lèverait alors depuis le callback de rechargement, hors de toute requête.
- **Délai de bascule dû au cache** : les clés de cache ne contiennent pas le fournisseur
  (l'Application ne le connaît pas, c'est le point de l'inversion de dépendance). Une
  prévision déjà en cache reste servie jusqu'à 10 min après la bascule, un lieu déjà
  géocodé jusqu'à 24 h ; toute nouvelle entrée vient du nouveau fournisseur.

### 3. Une suite de contrat unique par port

`HttpGeocoderContractTests` et `HttpWeatherProviderContractTests` (`Meteo.TestSupport`)
sont écrites une fois et héritées par le test de chaque implémentation, qui ne fournit
que ses corps de réponse simulés (`StubHttpMessageHandler`).

| Cas exigé | Géocodeurs (Nominatim, BAN) | Fournisseurs météo (Open-Meteo, MET Norway) |
|---|---|---|
| Adresse valide | coordonnées + libellé | points ordonnés, variable et unité canoniques |
| Adresse introuvable | `AddressNotFound` | sans objet (un point lat/lon existe toujours) → remplacé par : panne amont persistante → `WeatherUnavailable` |
| Réponse vide | échec, jamais d'exception | échec, jamais d'exception |
| Caractères accentués | `Alès` encodé `Al%C3%A8s`, libellé restitué | sans objet (aucun texte envoyé) → remplacé par : URL invariante sous culture `fr-FR` |

### 4. Les formats propriétaires ne sortent pas de leur adaptateur

- `AdapterIsolationTests` : tout type `*Dto` est `internal sealed`, la surface publique de
  `Meteo.Infrastructure` se limite à la composition et aux options, aucune signature de
  port n'expose un DTO.
- `ArchitectureTests.Domain_and_Application_declare_no_provider_DTO_type`.
- `ProviderSwitchingTests.Forecast_has_the_same_public_shape_for_every_provider_combination` :
  les 4 combinaisons de fournisseurs produisent la même réponse publique, sans aucun nom
  de champ propriétaire (`features`, `timeseries`, `temperature_2m`, `display_name`...).
- Les noms de variable sont traduits vers un vocabulaire canonique
  (`WeatherVariables`) : `temperature_2m` (Open-Meteo) et `air_temperature` (MET Norway)
  sortent tous deux en `air_temperature`. `OpenMeteo:HourlyVariable` accepte aussi
  `shortwave_radiation` (l'exemple du TP1), exposé comme tel ; une variable sans nom
  canonique empêche le démarrage plutôt que d'être servie sous une étiquette fausse.
  Seule `temperature_2m` existe chez les deux fournisseurs météo : c'est la valeur par
  défaut, à garder pour qu'une bascule ne change pas le sens de la réponse.

## Tests

| Projet | Nature | Compte |
|---|---|---|
| `Meteo.Domain.Tests` | unitaire + architecture | 12 |
| `Meteo.Application.Tests` | unitaire (fakes) | 13 |
| `Meteo.Infrastructure.Tests` | unitaire HTTP (sans réseau), contrats par port, sélection de fournisseur, isolation des DTO, résilience | 56 |
| `Meteo.Api.E2ETests` | e2e en mémoire (transport simulé), dont bascule de fournisseur à chaud | 25 |

**106 tests, aucun appel réseau, aucun ne dépasse quelques centaines de millisecondes.**

Le point le plus démonstratif : remplacer les APIs externes en test ne demande de changer
**aucune** ligne de code de production — seul le `HttpMessageHandler` primaire est
substitué (`Meteo.Api.E2ETests/FakeUpstream.cs`). Tout le reste — client typé,
désérialisation, pipeline Polly, conteneur DI, routage, mapping `ProblemDetails` — reste
réel. C'est la preuve, exécutable, que l'IoC/DI rend le code testable.

## Ce qui a été volontairement simplifié

- Les tests tournent hors-ligne : les appels réels aux quatre APIs ne font pas partie de
  la suite automatisée (les corps simulés de la BAN ont été capturés sur l'API réelle).
- `ForecastResult` ne distingue que dégradé/non-dégradé, pas les trois états
  live/cache-fresh/cache-stale — aucun scénario testé n'a besoin de la nuance.
- Les clients HTTP partagent le même pipeline de résilience composé à la main (seuls
  les seuils diffèrent), plutôt que de montrer aussi le handler standard du framework.
- Le limiteur de débit Nominatim n'a pas de test de minutage dédié (un test temps réel
  serait lent ou fragile) ; son effet pratique est couvert indirectement par le test du
  cache de géocodage.
- Le délai de bascule dû au cache (voir TP2, point 2) est assumé plutôt que corrigé :
  inclure le fournisseur dans la clé de cache ferait connaître l'Infrastructure à
  l'Application.
- Le `Dockerfile` suit le patron multi-étapes standard .NET ; il est construit par le CI
  (`.github/workflows/ci.yml`), pas vérifié localement (Docker absent de l'environnement
  de développement).
