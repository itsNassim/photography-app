# Photo Studio App

A Windows desktop app for a photo studio: counts daily production per category by
watching folders, calculates revenue, manages stock and a price list, and closes
the day with a password-protected archive-and-rollover action.

Runs on **Windows 7 SP1 and above** (32-bit and 64-bit).

## Tech stack (and why)

- **C# WinForms on .NET Framework 4.8** - the last Framework version, fully
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
installed - that workload includes the .NET Framework 4.8 targeting pack.

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
   and run `PhotoStudioApp.exe`. No installer or extra runtime needed (.NET 4.8
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

## First run / configuration

On first launch the app creates its SQLite database in
`%ProgramData%\PhotoStudioApp\studio.db` and seeds the 6 fixed categories from the
spec (`8 identite`, `4 identite`, `4 visa`, `8 visa`, and the two `amature`
sub-categories), each starting at price 0.

Go to the **Settings** tab and configure, in order:

1. **Studio root folder** - where the category subfolders and the working
   "today" folder live day-to-day (e.g. `D:\PhotoStudio\studio`). The app will
   create this folder and the category subfolders for you.
2. **Archive/destination folder** - where each closed day's photos get moved to,
   one dated subfolder per day (e.g. `D:\PhotoStudio\archive\07-September`).
3. **Date folder format** - controls how the archived-day folder (and, if you
   choose a format containing the year, disambiguates across years) is named.
   Examples: `dd-MMMM` → `07-September`, `yyyy-MM-dd` → `2026-09-07`.
4. **Closed weekdays** - which day(s) the studio is closed. When Close Day
   computes "tomorrow," it skips forward over any checked day(s). Friday is
   checked by default per the spec, but you can change this at any time.
5. **Closing password** - click "Set / Change password" and set the password
   staff will need to close each day. It's stored as a salted PBKDF2 hash, never
   in plain text.

Then go to **Categories / Price List** and set a real price for each of the 6
fixed categories (they start at 0), and add any custom categories you need -
each custom category needs a name, a price, and a folder path *relative to the
studio root* (e.g. `10x15` or `amature\20 identite` for a nested folder).

## Day-to-day workflow

- Staff drop photos straight into each category's folder under the studio root
  all day. The **Home / Today** tab auto-refreshes every 10 seconds (or click
  Refresh) to show live counts, per-category subtotals, and the grand total.
- Use **"+ Add charge"** on Home for one-off extras (reprints, rush fees,
  frames, etc.), optionally linked to a Stock item so the quantity used is
  deducted automatically.
- Manage consumables in the **Stock** tab - search, filter by low/out-of-stock,
  add/edit/delete, and manually restock. Rows go red when a item hits 0, and
  linked-item dropdowns in "Add charge" flag out-of-stock items too.
- At the end of the day, click **"🔒 Close Day"** on Home. You'll be asked for
  the closing password (lockout after the configured number of wrong attempts),
  then asked to confirm. The app then:
  1. Moves every category folder's photos into the day's working folder.
  2. Moves that folder into the archive, named with the actual date.
  3. Creates fresh, empty category folders ready for the next working day
     (skipping any closed weekday(s) when deciding what "next day" means).
  4. Saves a permanent record of counts, prices, custom charges, and the grand
     total to the database.
  5. Shows a breakdown screen you can **Print** (via the standard Windows print
     dialog) or **Export as PDF** (uses the built-in "Microsoft Print to PDF"
     driver if present - install a third-party PDF printer driver such as
     CutePDF on machines where it's missing, which is common on Windows 7).
- Look back at any previous day, and reprint its report, from the **History**
  tab.

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
