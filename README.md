# DDO-Launcher (Windows Presentation Foundation)
### What is it?
A remade launcher for Dragon's Dogma Online to communicate with the private server account API developed [here](https://github.com/sebastian-heinz/Arrowgene.DragonsDogmaOnline "here") 
with a better visual resource and UX (User Experience) aimed.

!! Place the launcher .exe alongside the client's DDO.exe !!

### Why?
To modernize it.<br/>
The base code remains mostly the same, and the visual features grow a lot.

### Images used:

https://www.steamgriddb.com/logo/96344

## For server hosts
### Custom background and logo
- Create a folder called `launcher` inside `Server\Files\www`<br />
<img width="540" height="271" alt="image" src="https://github.com/user-attachments/assets/ef34fd4a-fdf5-4598-b4fe-25e8b2b007be" />

<br /><br />

- Place the images you want inside it and name them `background.png` and `logo.png` (Background and logo must be PNG)<br />
<img width="360" height="236" alt="image" src="https://github.com/user-attachments/assets/ba9c5da4-3be6-4302-940b-a08edbdab779" />


<br /><br />

Optimal resolutions (they're not a limit)
- For background is `1000x500`
- For logo is `300x150`

!! If no images are specified, launcher will use default ones !!

### News panel
This panel actually supports one new at time.<br />
<br />

- Create a folder called `news` inside `Server\Files\www`<br />
<img width="540" height="271" alt="image" src="https://github.com/user-attachments/assets/629bd885-1bdc-4862-85f6-1798792e405f" />

<br /><br />

- Place the image banner you want inside it and name it `newsbanner.png` (Must be PNG - 665x200)
- Create a `news.html` too <br />
<img width="629" height="177" alt="image" src="https://github.com/user-attachments/assets/5f5d0837-9fe9-42c6-b585-b3798f57bcd6" />

<br /><br />

### For html
News are shown to players using custom HTML tags inside `news.html`, as an example:
<br />
```
<type>
    MAINTENANCE
</type>

<date>
    01 jan 2026
</date>

<title>
    The Maintenance Title
</title>

<content>
    The text about what maintenance will do, how long 
</content>
```
<br />
The "Type" tag changes it's color to match the text inside of it:<br /><br />

![UNAVAILABILITY](https://img.shields.io/static/v1?message=UNAVAILABILITY&color=B1224A)<br />
![MAINTENANCE](https://img.shields.io/static/v1?message=MAINTENANCE&color=845E14)<br />
![UPDATE](https://img.shields.io/static/v1?message=UPDATE&color=615D9F)<br />
![INFORMATION](https://img.shields.io/static/v1?message=INFORMATION&color=247CAA)<br />
![EVENT](https://img.shields.io/static/v1?message=EVENT&color=348B3A)<br /><br />

Any other text will have EVENT green color<br /><br />

!! Other tags are just text, you can input whatever you want !! 

## Local Play Build

This launcher can expose the Arrowgene.DragonsDogmaOnline server as a local single-player-style experience. The published launcher stays lightweight: the server is downloaded from the Arrowgene.DragonsDogmaOnline GitHub releases page only when Local Play needs it.

### Requirements

- Git
- .NET 10 SDK
- Visual Studio 2022/2026 or the .NET CLI

### Publishing the local build

From the launcher repository root, run:

```sh
dotnet publish DDO_Launcher.csproj -c Release
```

The publish output is created at:

```text
bin\Release\net10.0-windows\win-x64\publish
```

Copy the contents of that `publish` folder into the Dragon's Dogma Online game folder, next to `ddo.exe`, then run `DDO_Launcher.exe`.

The normal release files are `DDO_Launcher.exe` and `DDO_Launcher.ini`. `DDO_Launcher.ini` contains user-editable launcher URLs:

```ini
[Launcher]
TranslationPatchUrl = https://raw.githubusercontent.com/Sapphiratelaemara/DDON-translation/refs/heads/main/English/gmd.csv

[LocalPlay]
ServerReleaseUrl = https://github.com/sebastian-heinz/Arrowgene.DragonsDogmaOnline/releases
AutoUpdateServer = true
ServerStartupTimeoutSeconds = 90
```

When a user opens Local Play, the launcher checks the latest GitHub release and compares it against `LocalServer\server-release.json`. If the server is missing or out of date, the Local Play screen offers a download/update button. Pressing Play will also install/update the local server when needed, then start it automatically.

Set `AutoUpdateServer = false` to keep an installed local server frozen. Pressing Play will still download the server if it is missing, but it will not automatically replace an installed server with a newer release. The Local Play update icon can still be used to update manually.

Set `ServerStartupTimeoutSeconds` higher if the local server needs more time to start on slower machines. Values below 30 seconds or above 300 seconds are clamped by the launcher.

The downloaded release is extracted under:

```text
LocalServer\Server
```

When Local Play starts, the launcher automatically runs the server's `dbmigration` command before starting the local server. Normal users do not need to run database migration commands manually. If an existing local SQLite database is present, the launcher creates a timestamped backup under:

```text
LocalServer\Server\Files\Database\Backups
```

If the launcher starts the local server itself, it stays hidden while the game is running and shuts that server down when the game exits. If a local server was already running before Play was pressed, the launcher leaves it running.

When the server is updated, the installer preserves the local SQLite database, database backups, and `Files\Arrowgene.Ddon.config.json`.

### Updating the server dependency

Runtime Local Play updates come from the release page configured in `DDO_Launcher.ini`. Publish a new `win-x64-*.tar.gz` release asset in Arrowgene.DragonsDogmaOnline, then the launcher can discover and install it without rebuilding the launcher.

For the best end-user experience, server release assets should be self-contained so players do not need to install .NET separately. The launcher can fall back to running framework-dependent server releases through `dotnet` when needed, but that requires the matching .NET runtime to be installed on the player's machine.

The launcher still references Arrowgene client/shared projects at build time. After cloning the launcher repository, initialize the submodule before building:

```sh
git submodule update --init --recursive
```

To update the build-time submodule pointer:

```sh
git -C Arrowgene.DragonsDogmaOnline fetch origin
git -C Arrowgene.DragonsDogmaOnline checkout develop
git -C Arrowgene.DragonsDogmaOnline pull --ff-only origin develop
```

Then rebuild/publish the launcher when you intentionally want newer client/shared build-time logic:

```sh
dotnet publish DDO_Launcher.csproj -c Release
```

If you want to commit the launcher repository so everyone else uses the same server revision, commit the updated submodule pointer:

```sh
git add Arrowgene.DragonsDogmaOnline
git commit -m "Update bundled Arrowgene server"
```

After someone else pulls that commit, they should run:

```sh
git submodule update --init --recursive
```

## Development

### Requirements

- Git
- .NET 10 SDK
- Visual Studio 2022 or 2026

### Setting up the development environment

1. Clone the repository
2. Initialize git submodules ``` git submodule update --init --recursive ```
3. Open the project in Visual Studio

### Building

To build the launcher locally:

```sh
dotnet build DDO_Launcher.csproj
```

To create the local-play publish output:

```sh
dotnet publish DDO_Launcher.csproj -c Release
```

## Extra features

### Translation patch

Download the latest translation and use it to patch the client with one click.

### Mod installer

Patch your game files with the ones in a mod zip file.

#### Creating a mod

Place in a folder the files you want to pack into the game files and create a text file named "manifest.json"

```
    files/
        button_hud_win_00_ID_HQ.tex
        button_win_00_ID_HQ.tex
    manifest.json
```

Edit the manifest.json file and specify in there the name of the mod, its author, and a list of the ARC files that will be patched.

For each arc:
- Specify the path to it, relative to the rom folder (e.g. `ui/gui_cmn.arc`, instead of `nativePC/rom/ui/gui_cmn.arc`)
- or leave it null to indicate this block is about actions that affect files not contained in arc files.
- Add an action list, indicating what will be done with each file.

The possible actions are:
- `replace`: Copies the contents of the modded file `src` into the game file `dst` as is.
```json
{
	"action": "replace",
	"src": "files/button_win_00_ID_HQ.tex",
    "dst": "ui\\00_font\\button_win_00_ID_HQ.tex",
	"create": true // Optional. False by default. Creates the file if it doesn't exist
}
```

- `convert`: Copies the contents of the modded file `src` into the game file `dst`, but converts it to the adequate format. Currently the following conversions are supported:
	- `.dds`/`.txt` pair to `.tex`: Converts a DDS texture to a TEX texture using the information of an acompanying TXT file
```json
{
	"action": "convert",
	"src": "files/button_win_00_ID_HQ.dds",
	"txt": "files/button_win_00_ID_HQ.txt",
	"dst": "ui\\00_font\\button_win_00_ID_HQ.tex",
	"create": true // Optional. False by default. Creates the file if it doesn't exist
}
```

- `packGmd`: Patches GMD files with the texts in the provided file. This action can only be used in a block where `arc` is null, as the target arc files are specified inside the CSV file.
```json
{
	"action": "packGmd",
	"gmd": "gmd.csv",
	"romLang": "English" // Optional. English by default. At the moment can only be English or Japanese, chooses which language column to use.
}
```

The paths inside the ARC file MUST use escaped backward slashes (`\\`). Extension is optional but **recommended**, it might be required to disambiguate files that have the same basename. (e.g. `ui\\00_font\\button_win_00_ID_HQ.tex` instead of `ui/00_font/button_win_00_ID_HQ`)
If arc is null, the paths can be either backward or forward slashes. The paths are relative to nativePC.

#### Example manifest

```json
{
	"name": "XBox Button Layout Mod",
	"author": "Genolka",
	"arcs": [
		{
			"arc": null,
			"actions": [
				{
					"action": "replace",
					"src": "bgm_s_001.sngw",
					"dst": "sound/stream/bgm/bgm_system/wave/bgm_s_001.sngw"
				},
				{
					"action": "packGmd",
					"gmd": "gmd.csv"
				}
			]
		},
		{
			"arc": "ui/gui_cmn_win.arc",
			"actions": [
				{
					"action": "replace",
					"src": "files/button_win_00_ID_HQ.tex",
					"dst": "ui\\00_font\\button_win_00_ID_HQ.tex"
				}
			]
		},
		{
			"arc": "ui/gui_cmn.arc",
			"actions": [
				{
					"action": "convert",
					"src": "files/button_hud_win_00_ID_HQ.dds",
					"txt": "files/button_hud_win_00_ID_HQ.txt",
					"dst": "ui\\00_font\\button_hud_win_00_ID_HQ.241F5DEB"
				}
			]
		}
	]
}
```

# Special thanks to
- Rumi ([@Najelith](https://github.com/Najelith)), for the Cici smile launcher that made me work on my own
- Laith ([@alborrajo](https://github.com/alborrajo)), for the server selection and mod/translation install features
- Kythera ([@KytheLXI](https://github.com/KytheLXI)), for all contribution and tips on UI/UX
- All devs, translators and server owner from The White Dragon Temple [discord](https://discord.gg/jS3bNDmSCr) for all the support and patience
- And all Dragon's Dogma Online players who used my launcher, i never thought this would be used by someone other than me hahah 😅
