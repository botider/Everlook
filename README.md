# Everlook
[![Latest Download](https://img.shields.io/badge/Latest-Download-blue.svg)](https://ci.appveyor.com/api/projects/majorcyto/everlook/artifacts/) [![DoxygenDoc](https://img.shields.io/badge/Docs-Doxygen-red.svg)](http://everlookdocs.wowdev.info/)
[![Bountysource](https://www.bountysource.com/badge/tracker?tracker_id=34637447)](https://www.bountysource.com/trackers/34637447-wowdevtools-everlook?utm_source=44433103&utm_medium=shield&utm_campaign=TRACKER_BADGE)

<a href='https://ko-fi.com/H2H176VD' target='_blank'><img height='36' style='border:0px;height:36px;' src='https://az743702.vo.msecnd.net/cdn/kofi2.png?v=0' border='0' alt='Buy Me a Coffee at ko-fi.com' /></a>

## Build Status

CI | Build | Defects
:------------: | :------------: | :------------:
AppVeyor | [![Build status](https://ci.appveyor.com/api/projects/status/lf5swhbglpcuni33/branch/master?svg=true)](https://ci.appveyor.com/project/majorcyto/everlook/branch/master) | Coverity Badge Soon
Travis | [![Build Status](https://travis-ci.org/WowDevTools/Everlook.svg?branch=master)](https://travis-ci.org/WowDevTools/Everlook) | 

# About #
Everlook is a cross-platform, open-source World of Warcraft model viewer, created to showcase the capabilities of libwarcraft.

Everlook will be capable of browsing, exporting and converting most World of Warcraft formats up until 
Wrath of the Lich King, and is under active development. The current goal is to act as an open, simple
and feature-complete replacement for World of Warcraft Model Viewer.

Currently, Everlook is in early development and may not be usable in your day-to-day activities.

### Features
* Explore multiple game versions in one application (even old applications like Warcraft 3!)
* Explore games on an archive-by-archive basis, or as a unified virtual file tree
* Export files from the archives
* View textures stored in most major image formats, as well as BLP
* View WMO models

![Everlook](https://i.imgur.com/Y34yY3R.jpg)

### Known Isssues
* No format-specific export functions have been implemented.
* The export queue does not work beyond the UI.
* Everlook lacks any testing on Windows-based systems.
* Model rendering is not fully fleshed out.
* Animations are not yet supported.
* The audio controls do nothing.
* Wireframe rendering does not work on NVIDIA's proprietary drivers.

### Useful Information
* Everlook will mark files that have been deleted in the archives with red colour and an exclamation mark icon in the file explorer. However, it will still allow you to access the underlying file.
* The file trees are pregenerated by Everlook on first load of any folder which contains supported archive formats. This may take a long time (several minutes), but subsequent loads will be far quicker.
* Files shown in the primary top-level tree are versioned in the same way as is visible to the game. If you want a file from a specific archive, look in the "Packages" node.
* Whole directories can be saved to disk. This may take a long time for high-level directories, so be patient. A loading indicator is coming.

### Planned Features
* Fully compliant M2 rendering
* Fully compliant WMO rendering
* More fine-grained audio playback controls
* Collada exporter
* glTF exporter
* Animation controls

### Compiling
In order to compile Everlook, you will need a Nuget-capable IDE that supports the C# language. The most commonly used ones are Visual Studio, MonoDevelop and more recently Project Rider.
Everlook uses .NET Standard 2.0 libraries and tooling, so you need at least one of the following:

* Visual Studio 15.3
* JetBrains Rider 2017.1
* Mono >= 5.2.0.215
* .NET Framework 4.6.1

You'll also need two additional nuget package feeds - [OpenTK-develop](https://www.myget.org/gallery/opentk-develop) and [ImageSharp-develop](https://www.myget.org/gallery/imagesharp). 
These should be configured automatically via the NuGet.config file in the repository, but you may need to add them manually. Refer to your
IDE on how to do this.

You can also add them via the terminal:

	$ nuget sources add -Name OpenTK-Development -Source https://www.myget.org/F/opentk-develop/api/v2
	$ nuget sources add -Name ImageSharp-Development -Source https://www.myget.org/F/imagesharp/api/v2

Beyond that, downloading and compiling Everlook is as simple as the following commands:

    $ git clone git@github.com:WowDevTools/Everlook.git
    $ cd Everlook
    $ git submodule update --init --recursive
    $ nuget restore
    
If you're running Mono, you'll also need to .NET Core 2.0 SDK, and you should substitude any nuget restore commands with

	$ dotnet restore

All required GTK+ binaries are bundled for Windows.

For Debian-based Linux distributions, the following package should suffice:
* mono-complete (>= 4.4.2.11-0xamarin1)
* libgtk-3-0 (>= 3.16)

If you find that other dependencies are required, please open an issue here on Github.

If you are not using an IDE, Everlook can be built by invoking

	$ msbuild Everlook.sln

### Binary Packages
There are a number of ways you could get Everlook. For Windows users, the current method is, unfortunately, limited to downloading and compiling from source. You get the latest version, but it's a bit more of a hassle. In the future, Everlook may become available as an installer.

Ubuntu (and Ubuntu derivations) can simply add this PPA to get the application and a number of other helper packages, such as mime types and the underlying libraries used in the development of Everlook.

* [[PPA] blizzard-development-tools](https://launchpad.net/~jarl-gullberg/+archive/ubuntu/blizzard-dev-tools)

Debian users can manually download packages from the PPA, or add it manually to their sources.list file. Maybe someday it'll be in the main repo? We can hope!

Other Linux users can get tarballs of the binaries from the PPA as well. I plan on figuring out some better format for you soon. If someone who uses Arch sees this, I'd love some help getting it onto the AUR.Currently, Everlook does not provide any binary packages or installers due to its early state.

### Why?
World of Warcraft modding and development in general relies on a number of different command-line utilities, halfway finished applications and various pieces of abandonware, many which lack any source code. Furthermore, most of them are written for a specific operating system (most commonly Windows), which limits their use for developers on other systems.

libwarcraft (and, by extension, Everlook) is intended to solve at least a few of these problems, providing a common library for all Warcraft file formats upon which more specialized applications can be built - such as Everlook. 

Everlook itself stems from my frustration with WMV and its utter inability to compile on any of my systems, as well as its broken model export functions which were more harm than help. I have naught but respect for the creator of WMV, but it does not meet my requirements for a model viewer and exporter. Thus, Everlook.
