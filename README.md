# AGUI WebChat

Application de chat Blazor Server (.NET 10), avec Fluent UI, un agent Ollama exposé via AG-UI et une télémétrie SignalR.

## Structure

- `Client/Components/Pages/Chat.razor` : page de chat et annulation des réponses.
- `Client/Components/Chat/` : messages, réglages, raisonnement et métriques.
- `Client/Services/` : échanges AG-UI, paramètres et connexion SignalR par circuit.
- `Middleware/` : bibliothèque `AGUIWebChat.Middleware` partagée (interface de journalisation, logger AG-UI, décodeur SSE UTF-8, flux de lecture et d'écriture).
- `Client/Middleware/` et `Server/Middleware/` : intégrations HTTP propres à chaque application et adaptateurs conservant les catégories de logs existantes.
- `Server/Agents/ChatAgentFactory.cs` : création et instrumentation de l'agent.
- `Server/Inference/` : paramètres d'inférence et validation.
- `Server/Telemetry/` : observation des réponses et publication des métriques.
- `Server/Hubs/` : connexion SignalR.
- `Contracts/` : DTO partagés entre serveur et client.
- `tests/AGUIWebChat.Tests/` : tests automatisés.

## Démarrage

Installer le SDK .NET 10 et disposer d'un serveur Ollama accessible avec le modèle choisi déjà installé.

Dans un premier terminal PowerShell :

```powershell
$env:OLLAMA_ENDPOINT="http://localhost:11434"
$env:OLLAMA_MODEL="granite4.2:8b"
dotnet run --project Server --launch-profile http
```

`OLLAMA_ENDPOINT` est obligatoire ; `OLLAMA_MODEL` utilise `granite4.2:8b` par défaut. Choisir un modèle compatible avec les paramètres de raisonnement utilisés.

Dans un second terminal :

```powershell
$env:AGUI_SERVER_URL="http://localhost:5100"
dotnet run --project Client --launch-profile http
```

Ouvrir `http://localhost:5245`. Le profil HTTPS du client utilise `https://localhost:7219` et nécessite un certificat de développement approuvé.

Le serveur expose `/ag-ui` et `/telemetry`. Le client utilise `AGUI_SERVER_URL` pour les deux connexions. Le bouton **Stop** annule la requête en cours ; quitter la page déclenche aussi l'annulation.

## Paramètres et télémétrie

Les paramètres invalides reviennent aux valeurs par défaut. Bornes serveur : température 0–2, top-p 0–1, top-k 1–1000, contexte 1024–131072 ; effort `low`, `medium` ou `high`. Ces bornes applicatives ne garantissent pas la capacité du modèle ou de la machine.

Chaque circuit Blazor possède une connexion et un canal de télémétrie aléatoire, conservé lors des reconnexions SignalR. Le serveur publie seulement dans ce canal, sans diffusion globale. Ce mécanisme sépare les circuits ; il ne remplace pas une authentification utilisateur. Les métriques ne sont pas rejouées après une déconnexion. Une erreur de publication ne doit pas interrompre la réponse du modèle.

Les paramètres communs se trouvent dans `appsettings*.json` et les profils locaux dans `Properties/launchSettings.json`. Conserver les secrets dans les variables d'environnement ou les user secrets .NET. La journalisation HTTP/SSE peut contenir les conversations : adapter son niveau et sa conservation avant une utilisation partagée.

## Compilation et tests

```powershell
dotnet restore AGUIWebChat.slnx
dotnet build AGUIWebChat.slnx --configuration Release --no-restore
dotnet test AGUIWebChat.slnx --configuration Release --no-build
```

Les tests ne nécessitent pas Ollama. Ils couvrent notamment les paramètres invalides, le décodage SSE fragmenté, l'annulation et les erreurs de lecture. Le workflow `.github/workflows/ci.yml` exécute compilation et tests sur GitHub à chaque push et pull request.

## Git

`.gitignore` exclut les sorties .NET, les fichiers utilisateur Visual Studio et les logs. `.gitattributes` normalise les textes en LF et les scripts Windows en CRLF.

```powershell
git status
git add .
git diff --cached
git commit -m "Describe the change"
```
