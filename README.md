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

## Development

### Requirements

- Git
- .NET 9
- Visual Studio 2022 or 2026

### Setting up the development environment

1. Clone the repository
2. Initialize git submodules ``` git submodule update --init --recursive ```
3. Compile the required submodules
	- Run `dotnet build --configuration Release` on **./Arrowgene.DragonsDogmaOnline/Arrowgene.Ddon.Client**
4. Open the project in Visual Studio

### Building

To build the launcher in a single .exe:

**Not depend** on .net runtime download (~130Mb): <br />
```dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true```

**Depend** on .net runtime installation like the releases (~8Mb): <br />
```dotnet publish -c Release -r win-x64 --no-self-contained -p:PublishSingleFile=true```

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
