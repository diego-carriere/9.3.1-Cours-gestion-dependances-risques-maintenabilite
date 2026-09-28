## TP3 — API Météo : mode démo, cache et format

## Objectif

Ce TP consolide l'architecture construite aux TP1 et TP2. Il ajoute deux fonctionnalités transverses (mode démo, cache) et impose une contrainte supplémentaire sur le format de sortie de l'API, quel que soit le fournisseur actif. Comme au TP2, ces ajouts doivent se résoudre en ajoutant du code aux abstractions existantes, presque sans modifier le code existant.

## Mode démo

Si le paramètre demo=true est présent dans la requête, l'API doit renvoyer des données

simulées et ne jamais appeler les services externes réels (ni géocodage, ni météo).

Exemple d'appel : GET /forecast?address=Alès&demo=true

## Cache sur le geocoding

Deux appels successifs portant sur la même adresse ne doivent déclencher qu'un seul appel réseau réel vers le service de géocodage actif. Pas de static et bien avoir à l’esprit que la solution de cache peut évoluer dans le futur.

## Format de sortie unifié

Quel que soit le fournisseur actif — Nominatim, BAN, Open-Meteo, MET Norway — ou le

mode démo, la réponse JSON renvoyée par l'API doit avoir exactement la même structure

et les mêmes noms de champs

```
{
"address": "Alès",
"latitude": 44.1279,
"longitude": 4.0817,
"hourly": [
{ "time": "2025-06-10T14:00:00Z", "temperatureCelsius": 24.3 }
```


]

}

## Critère de réussite

Changer de fournisseur (variable d'environnement) ou activer le mode démo ne doit jamais changer la forme de la réponse observée par le client de l'API — seul son contenu change.
