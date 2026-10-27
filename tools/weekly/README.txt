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

   Windows:  if the game is in its usual place,
               C:\Program Files (x86)\Bullfrog\Theme Park World
             just double-click OpenTPW.exe. Otherwise, in a terminal
             in the OpenTPW folder, run
               OpenTPW.exe --game "D:\Games\Theme Park World"
   Linux:    ./OpenTPW --game "/path/to/Theme Park World"
   macOS:    ./OpenTPW --game "/path/to/Theme Park World"

   Or copy the OpenTPW folder's files into the game's folder and
   start OpenTPW from there, with nothing else to type.

OpenTPW saves into the game's "save" folder, the same one the
original game uses. Keep a copy of that folder before you play.


The first time you start it
---------------------------

The build is not signed, so your computer will warn you once.

Windows:  SmartScreen says "Windows protected your PC". Choose
          "More info", then "Run anyway".
macOS:    THE MAC BUILD IS UNTESTED: nobody has run it on a Mac yet.
          macOS says the app "cannot be opened". In Terminal, run
            xattr -dr com.apple.quarantine <the OpenTPW folder>
          then start it again. It needs a Mac with Apple silicon
          (M1 or newer).
Linux:    you need a Vulkan driver for your graphics card (Mesa's
          on AMD and Intel, the NVIDIA driver on NVIDIA). If the
          window does not open, install your system's
          "vulkan" or "mesa-vulkan-drivers" package.


If something goes wrong
-----------------------

Every start writes a log, console.log. It is in the game's folder,
beside "save". If that folder cannot be written, it is here instead:

   Windows:  %AppData%\OpenTPW
   Linux:    ~/.local/share/OpenTPW
   macOS:    ~/Library/Application Support/OpenTPW

The log names the build, your system and graphics card, and whether
the game's files look complete. It does not hold your name or your
computer's name: folder paths in it are cut down to "<game folder>"
and "~".

To report a problem, open an issue at
   https://github.com/maexah/OpenTPW/issues
and choose "Weekly build problem". Say which build you have (the
name of this zip), your system, and attach console.log.


Licence
-------

OpenTPW's code is under the licence in LICENSE.md. Theme Park World
belongs to its owners; OpenTPW is not made or approved by them.
