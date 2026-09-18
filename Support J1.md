# Gestion des dépendances, risques et maintenabilité 

Jour 1 - 18/09/26 



### Qui suis-je ? 



#### Philippe AUSSEL 

- Début de codeur à 14 ans sur CPC 464 

- 10 ans en tant que développeur 

- 18 ans en tant qu’architecte/manager/CTO 

- - Mais toujours développeur dans l'âme 



<!-- Start of picture text -->
mlelelalel<br><!-- End of picture text -->

## Programme 



- Jour 1 — Comprendre et maîtriser ses dépendances. 

- Jour 2 — Découpler, packager et sécuriser ses choix. 

- Jour 3 — Appliquer, puis évaluer. 

JOUR 1 — Comprendre et maîtriser ses dépendances. 





Partie 1 : C'est quoi une dépendance ? 





### Définition 



Une dépendance est tout élément dont un module a besoin pour fonctionner. Cela peut être une fonction, une classe, une bibliothèque, un service externe… Sans cette dépendance, le module ne peut pas exécuter sa tâche. 

“A dépend de B” signifie : si B change, A peut casser. 

### Des dépendances, il y en a partout 



- Une librairie, un framework, une package 

- Une API, un service cloud, .. 

- Une base de données, un partage de fichier 

- Un protocole de communication 

### Dépendances internes 



- Un module Front qui dépend d'un module Services 

- - Une classe qui dépend d'une autre classe 

- Un projet partagé (Common, Shared…) 

### Dépendances externes 



- Librairies : NuGet, npm, pip… 

- - Frameworks 

- API tierces 

- Bases de données 

Les externes sont souvent plus risquées : on les maîtrise moins. 

### Directes ou transitives 



#### **Directes** 

Vous l'appelez explicitement dans votre code. **Transitives** 

Jamais mentionnée : elle arrive avec une autre. Votre projet utilise A, A utilise B, donc votre projet dépend aussi de B. Invisibles, elles sont souvent sources de bugs. 



<!-- Start of picture text -->
Mon projet<br>directe directe<br>Package A Package C<br>'!<br>11<br>! transitive ' transitive<br>' !<br>conflit<br>Lib B - v1.2 aa ais LibB - v2.0<br><!-- End of picture text -->

Directes ou transitives 

### Explicites ou implicites 



#### **Explicites : Visibles dans le code** 

- import, using, #include 

- new MaClasse() 

- Un appel d'API écrit en clair 

**Implicites : Rarement documentées** 

- Variables d'environnement 

- Fichiers de configuration 

- Format de données attendu 

### Couplage 



<!-- Start of picture text -->
Couplage faible : c'est bon Couplage fort : danger<br>Un changement dans un module casse peu le reste. Effet domino, maintenance difficile.<br><!-- End of picture text -->

L'objectif : minimiser le couplage, pas éliminer les dépendances. 

### Cohésion 



Faible cohésion : Un module qui fait tout et n'importe quoi. 

Haute cohésion : Des modules clairs, stables, faciles à maintenir. 



<!-- Start of picture text -->
class ClientManager<br>{<br>bool ValiderSiret(string s);<br>void EnvoyerEmail(Client c);<br>byte[] FacturePdf(Facture f);<br>void ExporterCsv(Client[] 1);<br>}<br><!-- End of picture text -->



<!-- Start of picture text -->
class SiretValidator { .. }<br>class ClientMailer { .. }<br>class InvoicePdfGenerator { .. }<br>class ClientCsvExporter { .. }<br><!-- End of picture text -->

### Couplage faible + cohésion forte = architecture saine 



||Couplage faible|Couplage fort|
|---|---|---|
|Cohésion forte|Architecture saine,<br>modules clairs,<br>changements<br>localisés.|Modules rigides,<br>chacun est clair, mais<br>tout bouge ensemble.|
|Cohésion faible|Code éparpillé et<br>Indépendant, mais<br>responsabilités<br>diluées.|Tout dépend de tout,<br>rien n'est clair.|



### Pourquoi les dépendances fragilisent un projet 



- **Propagation** : Si la dépendance casse, tout ce qui repose dessus casse aussi. 

- **Mise à jour** : Une nouvelle version introduit une incompatibilité. 

- **Disponibilité** : L'API externe est lente ou indisponible. 

- **Sécurité** : Une vulnérabilité est découverte dans un package. 

- **Humain** : Une seule personne comprend la dépendance. 

Une dépendance mal gérée = dette technique + instabilité. 

### Quand une dépendance devient-elle un problème ? 



- **Elle change souvent** : API instable, ruptures de compatibilité fréquentes 

- **Plus maintenue** : Ni correctifs de bugs, ni correctifs de sécurité 

- **Fortement couplée** : Ses types et ses appels sont partout dans votre code 

- **Utilisée partout** : Un SPOF en puissance 

- **Non isolée** : Aucune interface entre elle et le métier 

- **Licence risquée** : GPL ou AGPL dans un produit propriétaire 

   - Le problème n'est pas la dépendance : c'est l'absence de contrôle. 

### Une dépendance n'est pas que du code 



- **Vendor lock-in (Fournisseur)** — Sortir coûte cher : formats fermés, services managés, compétences spécifiques 

- - **Pricing (Prix)** — Le modèle tarifaire ou la licence peuvent changer du jour au lendemain 

- **Souveraineté (Juridiction)** — Où sont les données, et quel droit s'applique à l'opérateur ? 

- **Bus factor (Personnes)** — Un mainteneur unique, une compétence rare dans l'équipe 

Approfondi durant le jour 2 

### Exercice 



#### Inventoriez les dépendances d'un projet que vous connaissez 

- Listez au moins 10 dépendances : code, infrastructure, services, configuration. 

- Classez-les : interne ou externe, directe ou transitive, explicite ou implicite. 

- - Notez leur couplage avec votre code : faible, moyen ou fort. 

- Entourez celle qui vous inquiète le plus, et dites pourquoi. 

### A retenir 



- Une dépendance est tout ce dont votre code a besoin, et pas seulement du code. 

- Moins vous contrôlez une dépendance, plus elle est risquée. 

- - Transitives et implicites sont les plus dangereuses : on ne les voit pas. 

- Visez un couplage faible et une cohésion forte. 

Partie 2 : Identifier les dépendances dans un projet 





### Quatre endroits où chercher 



- **Le code** : import, using, #include, require() 

- **Le réseau** : appels HTTP, SDK cloud, files de messages 

- **Les manifestes** : .csproj, package.json, requirements.txt 

- **Les ressources** : bases, stockage, cache, secrets 

Puis ce qui ne se voit nulle part : les dépendances cachées. 

Méthode 1 : Repérer les imports 



**Comment faire :** Parcourir les fichiers source ou utiliser un outil d'analyse statique pour ces mots-clés. 

**Ce que ça révèle** : Les dépendances directes et explicites de chaque fichier. 

**Premier réflexe d'analyse** : lire la liste des imports. 



<!-- Start of picture text -->
MyProject.Data<br>Newtonsoft.Json;<br>pandas DataFrame<br><!-- End of picture text -->

Méthode 2 : Traquer les appels réseau 



Ce qu'on cherche 

- API REST et GraphQL 

- SDK cloud (Azure, AWS…) 

- Microservices internes et externes 



- Files de messages : Kafka, RabbitMQ 

Comment : Rechercher fetch, axios.get, HttpClient.SendAsync… ou analyser les logs de trafic sortant. 

### Méthode 3 : Lire les manifestes 



|.NET|*.csproj|packages.lock.json|NuGet|
|---|---|---|---|
|JavaScript|package.json|package-lock.json|npm, Yarn|
|Python|requirements.txt,<br>pyproject.toml|poetry.lock|pip, Poetry|
|Java|pom.xml, build.gradle||Maven, Gradle|
|PHP|composer.json|composer.lock|Composer|



Méthode 4 : Suivre l'accès aux ressources 



- Bases de données SQL et NoSQL 

- Systèmes de fichiers 

- Stockage objet : S3, Azure Blob 

- Caches : Redis 

- Secrets et identité : Key Vault, annuaire 

Chercher les chaînes de connexion, les configurations d'accès et les drivers ou clients utilisés. 



### Les dépendances cachées 



- **Configuration et environnement** : process.env.DB_HOST, fichiers YAML ou JSON qui changent le comportement 

- **Singletons et état global** : Une instance unique et globale : couplage fort, impossible à remplacer en test 

- **Outils externes** : Le code lance un binaire (ffmpeg, gzip, un compilateur) qu'il ne gère pas 

- **Contexte d'exécution** : Horloge, fuseau horaire, culture (séparateur décimal), système d'exploitation 

Source majeure de dette technique et de régressions surprises. 

### Combien de dépendances dans cette méthode ? 



<!-- Start of picture text -->
Au moins 8<br>1. Variable d‘environnement DB_HOST<br>espers? 2. Singleton Database.Instance<br>host = Environaent.GetEnvironmantVar tablet ) 3. Schéma de la table Clients<br>rows = Database. Instance .Query( CT 5 ) 4, Chemin Windows C:\exports<br>file i 0 ; :<br>File.WriteALlText(file, ToCsv(rows)) 5. Horioge systeme<br>Process. Start file) 6. Systéme de fichiers<br>J 7. Binaire gzip installé<br>8. Culture utilisée par ToCsv<br><!-- End of picture text -->

### Les outils pour lister et auditer 



|.NET|dotnet list package --include-transitive|dotnet list package --vulnerable<br>dotnet list package --outdated|
|---|---|---|
|JavaScript|npm ls --all|npm audit<br>npm outdated|
|Python|pipdeptree|pip-audit<br>pip list --outdated|
|Graphe de code|NDepend (.NET) · dependency-cruiser,<br>madge (JS)|Visualiser le couplage et les cycles|





<!-- Start of picture text -->
Front<br>Un listing ne montre pas les<br>interconnexions. Le graphe donne une<br>vue macro (qui dépend de qui) et micro<br>(quelles classes).<br>©) Module interne<br>() Externe : package ou API<br>Ba Infrastructure GeocodingClient WeatherClient Lib de logs<br>— «dépend de»<br>API Nominatim API Open-Meteo<br><!-- End of picture text -->

Cartographier : de la liste au graphe 



<!-- Start of picture text -->
Couplage fort Dépendances circulaires Chemins critiques et SPOF<br>Un noeud relié 4 beaucoup d'autres : A-B-C-—A: difficile a tester et a Le point par ou transite la majorité<br>chaque changement s'y propage. décomposer. des flux.<br><!-- End of picture text -->

Ce que le graphe révèle 

### Détecter les dépendances critiques 



Les 3 critères : 

- **Fréquence** : Combien de modules l'utilisent ? - **Rôle métier** : Est-elle indispensable au cœur de métier ? - **Contrôle** : Pouvez-vous la corriger ou la remplacer ? 

### SPOF : Single Point of Failure 



##### Exemples classiques 



<!-- Start of picture text -->
Cae) (one) Cae<br>Base SQL unique<br>Elle tombe : tout s'arréte<br><!-- End of picture text -->

   - Une base de données unique 

   - Un service d'authentification centralisé 

   - Une librairie de logs utilisée par 100 % des modules 

- Stratégies 

   - Isoler derrière une interface 

   - Dédoubler : réplication, failover 

   - Circuit breaker et cache pour un mode dégradé 

### A retenir 



- Imports et manifestes révèlent les dépendances directes et les librairies externes. 

- Appels réseau et ressources montrent ce que vous ne contrôlez pas.. 

- Configuration, singletons et contexte d'exécution cachent des dépendances.. 

- Le graphe rend visibles couplage, cycles, dépendances critiques et SPOF.. 

Partie 3 : Architecture, IoC et injection de dépendances ? 





### L'architecture décide de vos dépendances 



Selon l'organisation des modules, on peut : 

- réduire ou augmenter le couplage 

- faciliter ou compliquer la maintenance 

- isoler ou propager les dépendances externes 

- rendre le code testable… ou non 

- limiter les effets domino d'un changement 



<!-- Start of picture text -->
Une bonne architecture<br>les dependances.<br>Une mauvaise architecture<br>les subit.<br><!-- End of picture text -->

### L'architecture en couches 





<!-- Start of picture text -->
Présentation<br>UI, contréleurs d'API<br>Application / Services<br>Orchestration des cas d'usage<br>Domaine / Meétier<br>Régles de gestion, entités<br>Infrastructure<br><!-- End of picture text -->

#### Objectifs : 

- Séparer les responsabilités 

- - Protéger le métier de la technique 

- Limiter la propagation des dépendances externes 

- Simplifier les tests 

- Isoler base et API externes 

Chaque couche dépend de celle du dessous, jamais de celle du dessus. 

### Le problème du new partout 





<!-- Start of picture text -->
OrderService<br>{<br>Confirm(Order order)<br>{<br>email = ;<br>email.Send(order.CustomerEmail,<br>):<br>}<br>}<br><!-- End of picture text -->

- **Fortement couplé** : Lié à SmtpEmailService pour toujours. 

- **Impossible à tester** : Chaque test envoie un vrai e-mail. 

- **Fragile** : Si le constructeur change, l'appelant casse. 

- **Rigide** : Passer au SMS impose de modifier la classe. 

Le code choisit lui-même ses dépendances : c'est ce que l'IoC évite. 

Inversion de contrôle : ce n'est plus le module qui va chercher ses dépendances 





<!-- Start of picture text -->
Sans loC Avec loC<br>Conteneur_ loC. crée SmtpEmailService.<br>OrderService new SmtpEmailService injecte t implémente<br>. utilise pore Senay<br>Le service crée et choisit sa dépendance OrderService ; lEmailService ;<br>Nii: scecariepee eneecawersomversxeemeel?<br><!-- End of picture text -->

- **Réutilisable** : Le module marche avec toute implémentation. 

- **Testable** : On lui fournit un fake en test. 

- **Remplaçable** : On change la dépendance sans toucher au module. 

L'injection de dépendances : trois formes 





<!-- Start of picture text -->
RECOMMANDEE OPTIONNELLE PONCTUELLE<br>Par constructeur Par propriété Par méthode<br>Claire, obligatoire, testable. Moins stricte : peut rester vide. Pour un besoin lié a un appel.<br>OrderService( ILogger? Logger Export(<br>IPaymentGateway gateway) { : ees I€xportFormat format)<br>{ _gateway = gateway; } 5 a<br><!-- End of picture text -->

DI : le module ne crée plus ses dépendances, il les reçoit. 

### Le conteneur IoC fait le travail 



Ce que fait le conteneur : 

- Il crée les objets 

- Il gère leur durée de vie 

- Il résout les dépendances en cascade 

- Il permet de remplacer une implémentation en un point 



<!-- Start of picture text -->
‘NET Microsoft.Extensions.<br>Dependencylnjection<br>Java Spring<br>Nodejs InversifyJS<br>Python dependency-injector<br><!-- End of picture text -->

Durées de vie : Transient, Scoped, Singleton 





<!-- Start of picture text -->
AddTransient @ chaque résolution Services légers, sans état<br>AddScoped par requéte HTTP DbContext, unité de travail<br>AddSingleton pour toute l'application Cache, configuration<br><!-- End of picture text -->

**Piège** : la dépendance captive 

Un singleton qui reçoit un service scoped le garde pour toujours : état partagé entre requêtes. 

### Pourquoi l'IoC rend le code testable 



<!-- Start of picture text -->
Avant Avec loC<br>repo = : FakeUserRepository : IUserRepository<br>service = UserService (repo); {<br>User? Find( email)<br>> User(email);<br>}<br>service = UserService(<br>FakeUserRepository());<br>Assert. True(service.Exists("a@b.fr"));<br><!-- End of picture text -->

Tests unitaires simples - Aucune dépendance lourde - Aucun effet de bord - Tests rapides 

### A retenir 



- L'architecture en couches limite la propagation des dépendances. 

- Le new dispersé crée un couplage fort et un code intestable. 

- - IoC : le module ne choisit plus ses dépendances. La DI les lui fournit. 

- Le conteneur gère création et durée de vie ; les fakes rendent les tests simples. 





Partie 4 : TP1 : API Météo ? 

### L'objectif 



Construire une API qui reçoit une adresse postale en GET et renvoie les prévisions météo du lieu, avec les bonnes pratiques vues cet après-midi : couplage faible, IoC, DI. 



<!-- Start of picture text -->
GET /forecast?address=Alés<br>{<br>: 44.13, : 4.08,<br>va ee<br>}<br><!-- End of picture text -->

### Deux services externes à enchaîner 



**GEOCODING : Nominatim (Nom de lieu → latitude / longitude)** nominatim.openstreetmap.org/search?q=Alès&format=json 

**MÉTÉO : Open-Meteo (Latitude / longitude → prévisions)** api.open-meteo.com/v1/forecast?latitude=48.85&longitude=2.35&hourly=shortwav e_radiation 

