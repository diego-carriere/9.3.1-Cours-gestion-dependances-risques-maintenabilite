# TP4 — Audit de conformité des licences

Projet audité : **TP-meteo**, c'est-à-dire la solution `Meteo.slnx` des TP1 à TP3 (9 projets : 4
dans `src/`, 5 dans `tests/`). Aucune dépendance n'a été ajoutée : l'audit porte sur l'existant.
Brief : [`../documentation/TP4.md`](../documentation/TP4.md). Cours : Support J2, parties 8 et 9.

| Livrable demandé | Où |
|---|---|
| Fichier de scan brut | [`licenses.json`](licenses.json), régénéré par `./licenses/audit.sh` |
| Tableau de classification | [Classification](#classification) |
| Configuration CI ajoutée | [Intégration en CI](#intégration-en-ci) — job `license-audit` de `.github/workflows/ci.yml` |

## Méthode

- **Outil** : `dotnet-project-licenses` 2.7.1, l'outil .NET imposé par le brief, épinglé dans
  [`../dotnet-tools.json`](../dotnet-tools.json). `dotnet tool restore` installe la même version
  en local et en CI.
- **Une seule commande**, `./licenses/audit.sh`, identique en local et en CI. Elle restaure la
  solution, puis lance l'outil avec `--include-transitive --use-project-assets-json --unique`.
  `project.assets.json` est le graphe résolu par `dotnet restore` : chaque paquet transitif y
  figure, à la version réellement retenue (Central Package Management et pinning transitif,
  voir le README).
- **Périmètre** : les 9 projets, tests compris, puisque le brief demande l'intégralité des
  dépendances. La colonne « distribués » du tableau fait la part de ce qui part dans l'image
  Docker.
- **Profondeur** : nombre de liens depuis le projet dans l'arbre de `dotnet nuget why`,
  références de projet comprises. Une référence directe est de profondeur 1.

## Scan brut

[`licenses.json`](licenses.json) contient 62 paquets distincts. C'est la sortie JSON de l'outil,
seulement réindentée par `jq` pour que la revue d'une PR montre la ligne qui change. Résumé
affiché par `./licenses/audit.sh` :

```
Licences des paquets NuGet, directs et transitifs :
  Apache-2.0  8
  BSD-3-Clause  3
  MIT  51
```

## Classification

Trois familles (Support J2, « Trois familles de licences ») : permissive, copyleft, propriétaire.

| Licence (SPDX) | Famille | Paquets | Dont directs | Distribués dans l'image | Obligations principales |
|---|---|---|---|---|---|
| MIT | permissive | 51 | 10 : 7 `Microsoft.Extensions.*`, `Microsoft.AspNetCore.Mvc.Testing`, `Microsoft.NET.Test.Sdk`, `coverlet.collector` | 9 | joindre l'avis de copyright et le texte de licence à toute copie |
| Apache-2.0 | permissive | 8, dont `xunit.abstractions` après revue (voir Signalements) | 2 : `xunit`, `xunit.runner.visualstudio` | 0 | idem, plus le fichier NOTICE s'il existe et la mention des modifications ; licence de brevets explicite |
| BSD-3-Clause | permissive | 3 : `Polly.Core`, `Polly.Extensions`, `Polly.RateLimiting` | 0 | 3 | reproduire l'avis de copyright dans la documentation d'une distribution binaire ; ne pas utiliser le nom des auteurs pour promouvoir le produit |
| GPL, AGPL, LGPL | **copyleft** | **0** | — | — | — |
| — | **propriétaire** | **0** | — | — | — |

« Distribués » : `dotnet list src/Meteo.Api/Meteo.Api.csproj package --include-transitive`
compte 12 paquets NuGet, les seuls que le `Dockerfile` restaure et publie. Les autres
`Microsoft.Extensions.*` référencés par Application et Infrastructure sont fournis, côté API, par
le framework partagé ASP.NET Core (MIT). Tout le reste ne sert qu'aux tests.

## Signalements

### Copyleft (GPL, AGPL, LGPL) : aucun

Aucun des 62 paquets n'est sous licence copyleft. Comme le prévoit le brief, les fiches de
décision portent donc sur des cas hors projet.

### Licence non identifiée par l'outil : `xunit.abstractions` 2.0.3

- **Constat** : premier lancement de `./licenses/audit.sh`, avant toute revue :
  `Package(xunit.abstractions-2.0.3) LicenseUrl(https://raw.githubusercontent.com/xunit/xunit/master/license.txt) License Type ()`.
  Le paquet ne déclare qu'une URL de licence, sans expression SPDX : l'outil ne sait pas la
  classer, et le gate échoue.
- **Position** : transitive, de profondeur 4 depuis chaque projet de test (`xunit` →
  `xunit.core` → `xunit.extensibility.core` → `xunit.abstractions`), 5 via `Meteo.TestSupport`.
  Non distribuée.
- **Revue** : le texte servi par cette URL est l'Apache License 2.0 (vérifié le 2026-09-28).
  Famille permissive.
- **Décision** : l'URL est déclarée dans [`reviewed-license-urls.json`](reviewed-license-urls.json)
  comme `Apache-2.0`. Le gate reste fermé par défaut : toute autre licence non identifiée fera
  échouer la CI jusqu'à sa propre revue.
- **Pourquoi pas `--manual-package-information`** (une entrée épinglée à une version) : constaté
  pendant l'audit, l'outil *ajoute* ces entrées au scan même quand le paquet est absent du
  projet. Un projet jetable sans xunit listait `xunit.abstractions`, et le scan brut aurait menti
  dès la mise à jour de xunit. La correspondance d'URL ne s'applique qu'aux paquets présents, et
  décrit exactement ce qui a été relu : le texte derrière cette URL.

## Liste blanche

[`allowed-licenses.json`](allowed-licenses.json) : `MIT`, `Apache-2.0`, `BSD-3-Clause`, soit
exactement les licences présentes et revues, rien d'anticipé. Accepter une nouvelle licence,
même permissive (BSD-2-Clause, ISC…), passe par une PR qui modifie ce fichier **et** ce rapport.
C'est la trace écrite de la décision.

Limite : la liste blanche de l'outil porte sur une licence, pas sur un paquet. Y ajouter une
licence copyleft après une fiche de décision l'autoriserait pour tout paquet.

## Intégration en CI

Job `license-audit` de `.github/workflows/ci.yml` (répertoire de travail : `TP-meteo/`) :

```yaml
  license-audit:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          global-json-file: TP-meteo/global.json

      # TP4 : échoue si une dépendance NuGet, directe ou transitive, porte une licence
      # absente de licenses/allowed-licenses.json ou non identifiée par l'outil.
      - run: ./licenses/audit.sh

      # Le scan brut versionné est celui du code actuel : un paquet ajouté ou mis à jour
      # sans relancer ./licenses/audit.sh fait échouer la CI.
      - run: git diff --exit-code -- licenses/licenses.json

      # Contrôle négatif permanent : le même gate, sur un projet jetable qui référence un
      # paquet GPL-3.0, doit refuser. S'il accepte, le gate ne protège plus rien.
      - run: ./licenses/audit-canary.sh
```

Le job `docker` a désormais `needs: [build-test, license-audit]` : aucune image n'est construite
avec une licence non revue.

Quand le job échoue :

| Message | Action |
|---|---|
| `License Type ()` (licence non identifiée) | lire le texte à l'URL citée, l'ajouter à `reviewed-license-urls.json`, et consigner la revue dans Signalements |
| `License Type (<licence>)` hors liste | copyleft ou propriétaire : fiche de décision d'abord. Permissive : l'ajouter à `allowed-licenses.json` et au tableau de classification |
| `git diff` sur `licenses.json` | relancer `./licenses/audit.sh` et committer le fichier |
| `audit-canary.sh` | le gate a été affaibli : annuler le changement qui l'a causé |

## Limites de l'audit

- **Framework partagé** : ASP.NET Core et le runtime .NET (MIT) ne sont pas des paquets NuGet du
  projet, et l'outil ne les voit pas.
- **Image Docker** : la couche système de `mcr.microsoft.com/dotnet/aspnet:10.0` contient ses
  propres composants, dont du copyleft (la glibc est sous LGPL). Les scanner demanderait un SBOM
  d'image (Syft, Trivy), sujet de la partie « Le SBOM » du Support J2.
- **Obligations des licences permissives** : MIT, BSD et Apache exigent de joindre avis et textes
  de licence à toute distribution. L'image n'est aujourd'hui que construite en CI, jamais
  diffusée. Avant de la diffuser, il faudrait y joindre les textes des 12 paquets distribués
  (`dotnet-project-licenses --export-license-texts`).
- **Métadonnées déclaratives** : l'outil lit la licence déclarée dans le `.nuspec`, pas le code.
  Un paquet mal déclaré passerait.
- **L'outil est lui-même une dépendance** (de build). 2.7.1 est la dernière version stable
  (2023-03-15) et ne cible que jusqu'à net7.0 : le manifeste l'autorise à tourner sur .NET 10
  (`"rollForward": true`). Sortie prévue s'il cassait : `nuget-license` 4.x, son successeur, qui
  cible net8.0 à net11.0.
- **Scripts bash** : Linux, macOS et CI. Sous Windows, passer par Git Bash ou WSL.
