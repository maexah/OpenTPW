# Play the original Sim Theme Park on Linux

> **What has been tested:** 2026-09-29, on Ubuntu 24.04 (KDE, X11, AMD graphics): the setup script with a disc image
> and with a disc folder, on a computer with Steam and on one without, then the game up to the player screen and into
> a park.
> Not tested yet: other Linux versions, Wayland, NVIDIA, sound through speakers, and the "Add it to Steam" part.

This gets the original 1999 game (*Sim Theme Park*, or *Theme Park World* outside North America) running on Linux. A
setup script does all the fiddly parts. You answer two questions, and then there's a **Sim Theme Park** icon in your
app menu.

## What you need

- Your game disc, or a disc image (`.iso`) of it. A raw image works too: a CloneCD `.img` or a single-track BIN/CUE
  `.bin` (drag in the `.img` or `.bin`, not the `.ccd` or `.cue`). The script turns it into an `.iso` first. This
  has been tried with a raw image made from an `.iso`, not yet with one made from a real disc.
- **A no-CD version of the game's `.exe`. You have to get this yourself.** The `TP.exe` on the disc has old copy
  protection that doesn't work on modern computers.
- About 3.5 GB of free space.

You don't need Steam.

**This is for Linux only.** Proton is Wine for Linux. On Windows the game runs by itself, but it still needs a
no-CD `.exe` and has the same clock bug. On a Mac, CrossOver is the nearest thing to Proton. Neither has been tried.

## Set it up

> ⚠️ **Before you run any script from the internet:** a script can do anything you can do on your computer. Only run
> one if you trust where it came from, and look at it first. This one is plain text, so open it in a text editor. The
> part at the top says what it does. It never asks for your password, and it only downloads two things, GE-Proton and
> MangoHud, from their official pages, and checks both against fixed checksums.

1. Download [**tpw-setup.sh**](https://raw.githubusercontent.com/maexah/OpenTPW/main/tools/play-the-original/tpw-setup.sh)
   (right-click the link, then **Save Link As…**) into your **Downloads** folder.
2. Put the disc in, or have your `.iso` file ready. Have your no-CD `.exe` ready too.
3. Open a **Terminal** (press **Ctrl + Alt + T**, or find *Terminal* in your app menu).
4. Type this and press **Enter**:

   ```
   bash ~/Downloads/tpw-setup.sh
   ```

5. It asks two questions. For each one, **drag the file or folder from your file manager into the Terminal window**,
   then press **Enter**:
   - first, the disc (or your `.iso` file),
   - then, your no-CD `.exe`.
6. Wait until it says **All done**. It takes a few minutes.

If it stops with a red message, the message says what's wrong. Fix that and run it again. Running it again is
safe, and it skips anything that's already done.

## Play

1. Click **Sim Theme Park** in your app menu.
2. The first time, some short films play. Click to skip them. If it sits on the *"Welcome to Sim Theme Park"*
   picture, click once.
3. Click **Create New Player**, type a name, and click the green tick. Then click the island.

## Good to know

- **Restart your computer before you play if it's been on for days.** The original game has a clock bug: the longer
  your computer has been on, the less accurate the park's clock gets. After about six days the park runs too fast,
  and later it can freeze. If your computer has been on for five days or more, the game pops up a reminder when it
  starts.
- The game is capped at **30 frames a second** on purpose, because it keeps that clock bug away for longer.
- **Your saves** are in **Home → Games → TPW → save → users**. Copy that folder somewhere safe now and then.
- **A bigger picture:** in **Home → Games → TPW → data**, make a copy of **_Resolution.sam** called **Resolution.sam**.
  Open it in a text editor and change the number on the last line: **3** = 800 × 600, **4** = 1024 × 768,
  **5** = 1280 × 1024. (Only 4 has been tested.)
- **To remove the game:** delete the **TPW** folder in **Home → Games**, and the file
  `~/.local/share/applications/sim-theme-park.desktop`.

## Add it to Steam too (optional, not tested yet)

In Steam: **Games → Add a Non-Steam Game to My Library… → Browse…**. Set the file type to **All Files**, pick
**Home → Games → TPW → play.sh**, and click **Add Selected Programs**. Don't choose a Proton version for it:
`play.sh` already uses one.

## If something goes wrong

- **The guests don't move, or the park runs too fast.** It's the clock bug. Quit, restart your computer, and play
  again.
- **The game closes by itself right after it starts loading.** The game needs working sound. Check that sound works in
  other apps and that an output device is picked.
- **Nothing happens when you click the icon.** Open a Terminal, run `~/Games/TPW/play.sh`, and look at what it
  prints.

## Would rather not run a script?

Every step is in the script, in order, with a comment saying what it does. You can follow it by hand.

## About the clock bug (for the curious)

The game keeps its park clock as "milliseconds since the computer was switched on", and adds each frame's time to it
with reduced precision. The bigger the number gets, the coarser the clock's smallest step. Each frame's time is
rounded to that step: if a frame is much shorter than the step, its time is lost and the park stops, and if it's a
bit shorter, it's rounded up and the park runs fast.

What we measured on a computer that had been on for 6½ days: with no frame cap, **the park froze**. Capped at 30 frames
a second, **it ran about 1½ times too fast**. At a few frames a second, it ran at the right speed. After a restart,
30 frames a second should stay close to the right speed for about six days.

It comes from the original game's own code, not from Linux or Proton.

## The European *Theme Park World* disc

Not tested. It may need a different language setting.
