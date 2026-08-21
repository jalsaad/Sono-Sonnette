# Sono-Sonnette

Application Windows 10/11 qui joue des fichiers audio à des heures programmées, en excluant certains jours de la
semaine. Elle tourne en arrière-plan (icône dans la zone de notification) et peut démarrer automatiquement avec
Windows.

## Fonctionnalités

- Chargement de fichiers audio (mp3, wav, wma, m4a, flac, ogg) et création de plannings : nom, heure de
  déclenchement, jours actifs (Lun–Dim).
- Un planning ne se déclenche que les jours cochés — décochez un jour pour qu'il ne se lance pas ce jour-là.
- Icône dans la zone de notification Windows : fermer la fenêtre ne quitte pas l'application, elle continue de
  surveiller les plannings en arrière-plan. Menu clic droit : Ouvrir / Quitter.
- Choix du périphérique de sortie audio : haut-parleurs de l'ordinateur, sortie Jack / Line-out de la carte son, ou
  tout autre périphérique de lecture actif détecté par Windows (via WASAPI).
- Réglage du volume et bouton « Tester le son ».
- Bouton rouge **ON AIR** : maintenu enfoncé, il diffuse en direct le micro de l'ordinateur vers la sortie audio
  choisie dans les paramètres (haut-parleurs ou sortie Jack), pour des annonces live indépendantes des plannings.
  Le relâcher coupe immédiatement la diffusion.
- Modification des plannings et des paramètres protégée par mot de passe (haché en PBKDF2-SHA256, jamais stocké en
  clair). Le mot de passe est demandé une fois par session, à la première modification.
- Démarrage automatique à l'ouverture de session Windows (optionnel, activable dans les paramètres).

## Architecture

```
src/
  SonoSonnette.Core/    Logique métier, indépendante de l'UI
    Models/              ScheduleEntry, AppSettings, AudioOutputDevice
    Services/
      SettingsService     Persistance JSON (%AppData%\SonoSonnette\settings.json)
      PasswordService      Hachage / vérification PBKDF2-SHA256
      AudioPlaybackService WASAPI (NAudio) : liste des périphériques, lecture, bip de test
      MicPassthroughService Diffusion live du micro vers la sortie choisie (bouton ON AIR)
      SchedulerService      Minuteur de fond, déclenche la lecture au bon jour/heure
      AutoStartService      Clé de registre HKCU\...\Run

  SonoSonnette.App/     Interface Avalonia (multiplateforme, ciblée Windows)
    App.axaml(.cs)       Icône de la zone de notification, cycle de vie de l'application
    MainWindow           Liste des plannings + actions Ajouter/Modifier/Supprimer/Paramètres
    Dialogs/             Fenêtres modales : édition de planning, paramètres, mot de passe, confirmation

installer/
  SonoSonnette.iss      Script Inno Setup pour générer l'installateur Windows
```

Le mot de passe protège uniquement l'ajout/modification/suppression des plannings et l'accès aux paramètres. Une
fois déverrouillé, il le reste jusqu'à la fermeture complète de l'application (Quitter dans le menu de la zone de
notification).

## Compiler et exécuter

Prérequis : [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
dotnet build
```

L'exécution graphique nécessite Windows (Avalonia s'appuie sur Win32/WASAPI côté Windows). Sous Windows :

```powershell
dotnet run --project src\SonoSonnette.App
```

## Créer l'installateur Windows

1. Publier un binaire autonome (aucune installation de .NET requise sur la machine cible) :

   ```powershell
   dotnet publish src\SonoSonnette.App\SonoSonnette.App.csproj -c Release -r win-x64 --self-contained true -o publish\win-x64
   ```

2. Installer [Inno Setup](https://jrsoftware.org/isinfo.php), puis compiler le script :

   ```powershell
   iscc installer\SonoSonnette.iss
   ```

   L'installateur est généré dans `dist\SonoSonnette-Setup.exe`. Il installe l'application pour l'utilisateur
   courant (aucun droit administrateur requis), ce qui correspond au démarrage automatique basé sur la clé de
   registre `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.

   L'installateur n'étant pas signé numériquement, Windows SmartScreen peut afficher un avertissement au premier
   lancement (« Informations complémentaires » → « Exécuter quand même »). Pour l'éviter, il faudrait signer
   l'exécutable avec un certificat de signature de code.

## Limites connues / pistes d'amélioration

- Le mot de passe se réinitialise (redemande de déverrouillage) à chaque redémarrage de l'application ; il n'y a
  pas de session persistante entre deux lancements.
- Pas de récupération de mot de passe oublié : il faudrait supprimer `%AppData%\SonoSonnette\settings.json` (perd
  aussi les plannings enregistrés) pour en redéfinir un.
- La liste des sorties audio est celle exposée par Windows (WASAPI) : si votre carte son ne présente pas
  « Haut-parleurs » et « Sortie Jack » comme deux périphériques distincts dans le Panneau de configuration Son de
  Windows, ils n'apparaîtront pas comme deux choix séparés ici non plus.
