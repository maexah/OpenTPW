OpenTPW - an experimental weekly build
=======================================

OpenTPW is a fan remake of Theme Park World (1999). This is an
experimental build: it is unfinished and it will have bugs.

You need your own copy of Theme Park World. This download holds no
part of the game. OpenTPW reads the game's own files from where you
installed it, or from a copy of the disc.

Lost Kingdom is the land that is furthest along. The other three
lands are not ready yet.


How to start it
---------------

1. Unzip this file anywhere you like.
2. Find the folder Theme Park World is installed in: the one that
   holds TP.exe and a folder called "data".
3. Start OpenTPW and give it that folder:

   Windows:  OpenTPW asks Windows where the game was installed (its
             uninstall entry) and tries the usual places,
               C:\Program Files (x86)\Bullfrog\Theme Park World
               C:\Program Files (x86)\SimTheme Park
             so if the game is installed, just double-click
             OpenTPW.exe. Otherwise, in a terminal in the OpenTPW
             folder, run
               OpenTPW.exe --game "D:\Games\Theme Park World"
   Linux:    ./OpenTPW --game "/path/to/Theme Park World"
   macOS:    ./OpenTPW --game "/path/to/Theme Park World"

   Or copy the OpenTPW folder's files into the game's folder and
   start OpenTPW from there, with nothing else to type.

OpenTPW saves into the game's "save" folder, the same one the
original game uses. Keep a copy of that folder before you play.
Where that folder cannot be written (a game installed under
Program Files that your account may not change), OpenTPW keeps its
saves in a "save" folder in your own data folder instead
(%AppData%\OpenTPW\save on Windows) and plays as usual, saying in
console.log which one it chose. Players already in the game's own
save folder are not copied there; the game says so once each start.


The first time you start it
---------------------------

The build is not signed, so your computer will warn you once.

Windows:  SmartScreen says "Windows protected your PC". Choose
          "More info", then "Run anyway".
macOS:    THE MAC BUILD IS UNTESTED: nobody has run it on a Mac yet.
          macOS says the app "cannot be opened". In Terminal, run
            xattr -dr com.apple.quarantine <the OpenTPW folder>
          then start it again. The osx-arm64 zip is for Macs with
          Apple silicon (M1 or newer), the osx-x64 zip for older
          Macs with an Intel processor.
Linux:    you need a Vulkan driver for your graphics card (Mesa's
          on AMD and Intel, the NVIDIA driver on NVIDIA). If the
          window does not open, install your system's
          "vulkan" or "mesa-vulkan-drivers" package. On Wayland
          it runs through XWayland, and without XWayland the
          window does not open: install XWayland, or start it
          with OPENTPW_SYSTEM_SDL=1 to use your system's SDL2.
          If it stops at once naming ICU or libicu, install your
          system's ICU package (not tested: no machine without
          it was tried).
          The check for a newer weekly build needs OpenSSL (libssl,
          version 3 or 1.1). Where none is installed the check is
          not made and console.log says so; the game starts as usual.


Does it use the internet?
-------------------------

Yes, once, as it starts: this build asks GitHub (api.github.com,
with no account and nothing sent but what any web request carries)
whether a newer weekly build has been released. If one has, the
lobby and the Escape menu say "Update available" on the line
above the name of the build you are running, and clicking those
words opens that release's page in your browser. Nothing is
downloaded or installed by the game. If the check fails (no
internet, GitHub down) console.log says so once and nothing else
happens.

To stop the request, put the line

   updatecheck off

in opentpw.cfg, in the "save" folder the game uses (the game
makes that file itself once you have played; or make it yourself,
with that one line). A build you made yourself ("local") never asks.


If something goes wrong
-----------------------

Every start writes a log, console.log. It is in the game's folder,
beside "save". If that folder cannot be written, it is here instead:

   Windows:  %AppData%\OpenTPW
   Linux:    ~/.local/share/OpenTPW
   macOS:    ~/Library/Application Support/OpenTPW

The start before is kept beside it as console-prev.log.
The log names the build, your system and graphics card, and whether
the game's files look complete. It does not hold your name or your
computer's name: folder paths in it are cut down to "<game folder>"
and "~", and each player's name to "<player 1>" and so on.

If OpenTPW cannot find Theme Park World, or stops before the game
begins, a message box says why and where console.log is. It still
writes console.log. When the game was not found there is no game
folder to put it in, so it is in the folder listed above, and its
first line says "no game folder was found".

To report a problem, open an issue at
   https://github.com/maexah/OpenTPW/issues
and choose "Weekly build problem". Say which build you have (the
name of this zip), your system, and attach console.log.


Licence
-------

OpenTPW's code is under the licence in LICENSE.md. The libraries and
the font in this download are other people's work, each under its own
licence: THIRD-PARTY-NOTICES.md names every one, and the font's licence
is also in content/fonts. Theme Park World belongs to its owners;
OpenTPW is not made or approved by them.
