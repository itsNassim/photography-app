# Photo Studio App

A Windows desktop app for a photo studio: counts daily production per category by
watching folders, calculates revenue, manages stock and a price list, and closes
the day with a password-protected archive-and-rollover action.

Runs on **Windows 7 SP1 and above** (32-bit and 64-bit).

## Tech stack (and why)

- **C# WinForms on .NET Framework 4.6** - the last Framework version, fully
  supported on Windows 7 SP1. Do **not** retarget this project to .NET 5/6/7/8/9 -
  those dropped Windows 7 support entirely.
- **SQLite** via the `Stub.System.Data.SQLite.Core.NetFramework` NuGet package
  (the actual assets - `System.Data.SQLite.Core` itself is an empty
  meta-package as of version 1.0.116+) - a single local
  `studio.db` file holds categories, prices (with full price history), stock,
  custom charges, and every closed day's history, so reports stay queryable.
- No internet/cloud dependency. Everything runs locally.

## Project layout

```
PhotoStudioApp.sln
PhotoStudioApp/
  Models/       Plain data classes (Category, StockItem, CustomCharge, DailyClose, AppSettings)
  Data/         SQLite access - one repository class per table, plus schema creation
  Services/     Business logic - folder counting/moving, closing workflow, password
                hashing, workday/skip-weekday calculation, print/PDF export
  UI/           WinForms screens - MainForm (tab host) + one class per tab
  UI/Dialogs/   Add/Edit dialogs, password prompt, breakdown/report screen
sql/schema.sql  Reference copy of the DB schema (created automatically at runtime -
                you never need to run this by hand)
build/publish.ps1          PowerShell script to build a portable Release folder
build/build-installer.ps1  Builds the app, then compiles the installer below
installer/PhotoStudioApp.iss  Inno Setup script -> installer\Output\PhotoStudioApp-Setup.exe
```

## Building it without installing anything (recommended if you don't have Visual Studio)

This repo includes a GitHub Actions workflow (`.github/workflows/build.yml`)
that builds the app **and** the installer on a free Windows cloud machine -
you don't install Visual Studio, .NET, or Inno Setup on your own PC at all.

1. Create a free account at <https://github.com> if you don't have one.
2. Create a new repository (Private is fine) and upload this entire project
   folder into it - easiest way: on the repo's page, click **Add file →
   Upload files**, drag the whole `PhotoStudioApp` folder contents in, and
   commit. (If you're comfortable with git: `git init`, `git add .`,
   `git commit -m init`, then push to the new repo instead - faster for a
   folder this size.)
3. Click the **Actions** tab at the top of the repo. GitHub should already be
   running the workflow (it triggers automatically on push). If it asks you
   to enable Actions for the repo, click the button to enable it, then click
   **"Run workflow"** if it didn't start automatically.
4. Wait 2-3 minutes for the run to go green.
5. Open the completed run, scroll down to **Artifacts**, and download
   **PhotoStudioApp-Installer** - it's a zip containing one file,
   `PhotoStudioApp-Setup.exe`. That's your real, compiled Windows installer.
6. Copy `PhotoStudioApp-Setup.exe` to the target Windows 7+ machine and run
   it - Start Menu shortcut, optional desktop icon, and a normal uninstaller
   entry, exactly as described below.

(There's also a **PhotoStudioApp-Portable** artifact - the raw, uncompressed
build folder - if you'd rather xcopy-deploy instead of using the installer.)

## Building it locally (if you do have, or don't mind installing, Visual Studio)

You need a **Windows machine** (or a Windows VM) with **Visual Studio 2019 or 2022**
(Community edition is fine) and the **".NET desktop development"** workload
installed - that workload includes the .NET Framework 4.6 targeting pack.

1. Open `PhotoStudioApp.sln` in Visual Studio.
2. This project restores its one NuGet dependency
   (`Stub.System.Data.SQLite.Core.NetFramework`) by direct download rather than
   Visual Studio's automatic restore (that flow proved unreliable in CI) - run
   `.\build\publish.ps1` once first (it downloads and unpacks the package), or
   see that script's restore step if you'd rather do it by hand.
3. Set the solution platform to **x86** (top toolbar) - this matches the
   `PlatformTarget` in the project and keeps the native SQLite interop DLL
   selection simple across 32-bit and 64-bit Windows 7.
4. Build → Rebuild Solution (Release).
5. Your build is in `PhotoStudioApp\bin\Release\`. This whole folder is
   **portable** - zip it up or copy it directly to the target Windows 7 machine
   and run `PhotoStudioApp.exe`. No installer or extra runtime needed (.NET 4.6
   ships with, or is easily added to, any supported Windows 7 SP1 install).

Alternatively, from a "Developer PowerShell for VS" prompt:

```powershell
.\build\publish.ps1
```

## Building a real installer (setup.exe)

The portable Release folder above works fine as-is (just copy/zip it), but if
you want a proper double-clickable installer with a Start Menu entry, an
optional desktop icon, and a normal Windows uninstaller entry, this repo
includes an **Inno Setup** script - Inno Setup is free, widely used for exactly
this kind of WinForms app, and its installers run fine on Windows 7 SP1+.

1. Build the app in Release mode first (see "Building it locally" above, or
   just use the GitHub Actions path above which does this for you) - the
   installer just packages whatever is in `PhotoStudioApp\bin\Release`.
2. Install Inno Setup 6 (free): <https://jrsoftware.org/isinfo.php>
3. Compile `installer\PhotoStudioApp.iss` - either:
   - Open it in the Inno Setup IDE and click **Compile**, or
   - From a command prompt: `"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\PhotoStudioApp.iss`

That produces a single file: `installer\Output\PhotoStudioApp-Setup.exe`.
Copy just that one file to the target machine and run it - it installs to
Program Files, adds Start Menu / optional desktop shortcuts, registers a
normal "Uninstall a program" entry, and offers to launch the app when done.

Or do both steps (build the app, then build the installer) in one command:

```powershell
.\build\build-installer.ps1
```

**Note on upgrades and data:** the installer's `AppId` is fixed, so running a
newer installer later upgrades in place rather than installing side-by-side.
The studio's database lives outside the install folder (under
`%ProgramData%\PhotoStudioApp`, falling back to `%LocalAppData%\PhotoStudioApp`
if that's not writable - see "First run / configuration" below), so
installing, upgrading, or uninstalling never touches studio history or
settings. If you ever want to fully wipe a machine's data, delete those
folders manually.

**Note on admin rights:** installing to Program Files requires an
administrator to approve the UAC prompt once, same as any other Windows
installer. The app itself does not need to run as administrator afterwards.

## Interface language

The app's UI is entirely in **French** (menus, buttons, messages, and the
printed/PDF report). This README stays in English for the dev-facing parts
(build steps, architecture), but screenshots/labels in the running app will
read "Accueil", "Cloturer la journee", etc.

## First run / configuration

On first launch the app creates its SQLite database in
`%ProgramData%\PhotoStudioApp\studio.db` and seeds the 6 fixed categories from the
spec (`8 identite`, `4 identite`, `4 visa`, `8 visa`, and the two `amature`
sub-categories), each starting at price 0.

Go to **Parametres** (Settings) and configure, in order:

1. **Dossier racine du studio** (studio root folder) - where the category
   subfolders live day-to-day (e.g. `D:\PhotoStudio\studio`). The app creates
   this folder and the category subfolders for you.
2. **Dossier d'archive** (archive folder) - the *parent* folder that will
   contain one subfolder per year (e.g. `D:\PhotoStudio\archive`). The app
   automatically builds `archive\2026\septembre\07 septembre\...` underneath
   it at close time - you never create the year/month folders yourself.
3. **Format du nom de dossier** - controls how the daily working folder and
   the day-level archive folder are named. Default `dd MMMM` → `07 septembre`
   (in French, regardless of the machine's Windows display language).
4. **Jours de fermeture** (closed weekdays) - which day(s) the studio is
   closed. When closing the day and computing "the next work day," the app
   skips forward over any checked day(s). Vendredi (Friday) is checked by
   default, but you can change this at any time.
5. **Mot de passe administrateur** - click "Definir / Changer" and set the
   password. This single password protects: closing the day, all Stock and
   Categories add/edit/delete actions, and opening the History tab. It's
   stored as a salted PBKDF2 hash, never in plain text.

Then go to **Categories / Tarifs** and set a real price for each of the 6
fixed categories (they start at 0), and add any custom categories you need.
Each category is either:
- **Folder-backed** - pick its folder with the "Parcourir..." button (it must
  be inside the studio root; the app computes the relative path for you), or
- **Sans dossier / prix fixe** (no folder, fixed price) - tick the checkbox
  instead. Use this for services with no folder to count, e.g. printing
  photos straight from a customer's phone: no dossier to watch, just a fixed
  price per print. These show up in "Ajouter une charge" (add extra charge)
  with a quantity field, and the amount is computed automatically
  (quantite x prix).

## Day-to-day workflow

- Staff drop photos straight into each folder-backed category's folder under
  the studio root all day. The **Accueil** tab auto-refreshes every 10
  seconds (or click Actualiser) to show live counts and unit prices per
  category. (There's deliberately no running total shown here - just the
  live counts.)
- Use **"+ Ajouter"** under "Charges additionnelles" for one-off extras
  (reprints, rush fees, frames, phone-photo prints, etc.). Pick a Stock item
  or a fixed-price category from the dropdown and the description auto-fills
  with its name (you can keep typing after it); entering a quantity computes
  the amount automatically, and picking a Stock item deducts that quantity
  from stock.
- Manage consumables in the **Stock** tab, and the category list in
  **Categories / Tarifs** - both require the administrator password for
  every add/edit/delete action.
- **Historique** (History) opens locked - click "Deverrouiller" and enter the
  administrator password to view or reprint any past closed day.
- At the end of the day, click **"🔒 Cloturer la journee"** on Accueil.
  You'll be asked for the administrator password (lockout after the
  configured number of wrong attempts), then asked to confirm. The app then:
  1. For each category folder, **deletes any file whose name starts with
     "re" or "GSW"** (case-insensitive) - *before* counting or copying
     anything, so rejects/duplicates never make it into the count, the
     total, or the archive.
  2. Counts what's left in each folder and moves it into that day's working
     folder (named from the date, e.g. `07 septembre` - there's no more
     static "today" folder name).
  3. Moves that working folder into
     `archive\<annee>\<mois en francais>\<meme nom>` - creating the year
     and/or month folder only if they don't already exist.
  4. Re-creates empty category folders ready for the next working day
     (skipping any closed weekday(s) when deciding what date that is).
  5. Saves a permanent record of counts, prices, custom charges, and the
     grand total to the database.
  6. Shows a breakdown screen you can **Imprimer** (Print, via the standard
     Windows print dialog) or **Exporter en PDF** (uses the built-in
     "Microsoft Print to PDF" driver if present).

## Editing prices / categories safely

Editing a category's price or deactivating a category never rewrites history:
every price change is stored as a new, dated row, and every closed day snapshots
the exact count and price it used at close time into its own permanent record.
Deactivating a category (soft-delete) hides it from the Home dashboard going
forward but leaves every past report exactly as it was.

## Known limitations / things to double-check before going live

- This code was written and reviewed carefully, including a partial compile
  check of the non-UI logic, but the WinForms UI layer itself could not be
  compiled in the environment that produced it (Linux, no Windows/.NET Framework
  SDK available) - a full C# WinForms toolchain isn't available in that sandbox.
  **Build it once in Visual Studio and click through every tab before relying on
  it in production.** Nothing here is exotic (standard WinForms controls, no
  visual designer files - controls are laid out in code), but please verify.
- "Microsoft Print to PDF" ships by default on Windows 8.1+ but is **not
  guaranteed to exist on a stock Windows 7 SP1 install** - PDF export will show
  a clear message if it's missing and suggest installing a free PDF printer
  driver (e.g. CutePDF, PDFCreator), which restores PDF export immediately.
- Recognized photo extensions are `.jpg .jpeg .png .bmp .tif .tiff .gif .raw
  .cr2 .nef .webp` - add more in `Services/FolderCountService.cs` if your studio
  cameras produce something else.
- The app currently recognizes photos placed directly inside each category
  folder (not in sub-subfolders). If staff nest folders further, adjust
  `FolderCountService` to recurse.
- The purged filename prefixes ("re", "GSW") are hardcoded in
  `Services/ClosingService.cs` (`PurgedPrefixes`) - edit that array if you need
  to add/remove a prefix.
- The single administrator password protects closing the day, all Stock and
  Categories CRUD, and History access. There's no separate "staff" vs "admin"
  distinction - anyone who knows that one password can do all of the above.
