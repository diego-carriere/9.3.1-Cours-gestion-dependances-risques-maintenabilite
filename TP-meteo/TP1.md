# **TP1 — API Météo** 

## **Prérequis** 

- **Langage** : Langage de votre choix. 

- **Gestionnaire de versions** : Dépôt Git. 

## **Objectif** 

Construire une API qui reçoit une adresse postale en GET et renvoie les prévisions météo du lieu. L'implémentation doit obligatoirement respecter les bonnes pratiques d'architecture logicielle : couplage faible, Inversion de Contrôle IoC et Injection de Dépendances DI. Le code doit être testable et testé (unitairement et end-to-end). 

## **Services externes à intégrer** 

L'application s'appuiera sur deux services externes à enchaîner : 

- **Géocodage** : Nominatim Nom de lieu → latitude / longitude) 

   - Exemple d'appel : 

      - nominatim.openstreetmap.org/search?q=Alès&format=json 

- **Météo** : Open-Meteo Latitude / longitude → prévisions) 

   - Exemple d'appel : 

api.open-meteo.com/v1/forecast?latitude=48.85&longitud e=2.35&hourly=shortwave_radiation 

