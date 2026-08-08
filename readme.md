ClipReader
==========

Super simple but very useful application written in C# using Microsoft's Speech API (SAPI).
The application docks in system tray and reads any text you copy to clipboard (CTRL+C)
It's very helpful with reading large amounts of text like science papers, tech documentation etc. 

* You can change the speed of reading by moving the slider. 
* You can change the voice / language of the reader in the system's Control Panel -> Speech Recognition -> Text to Speech.
* Here's how you can install more voices / languages: http://superuser.com/a/872573


System requirements
---------
This application should work in every Windows operating system starting from Windows XP (tested up to Windows 8.1). Requires Microsoft .NET framework 3.5 installed.


History
---------
v1.1.1 (2026-08-08) — Packaging fix (VaultedVentures fork)
  * Release zip now ships a ClipReader.exe.config that pins the .NET Framework 4.x
    runtime (supportedRuntime v4.0). The v1.1 zip's config was empty, so Windows
    selected CLR 2.0 which cannot bind System.Drawing 4.0 and the app crashed at
    launch (CLR20r3 FileNotFoundException). Same exe/dll as v1.1, config only.

v1.1 (2026-08-08) — Long text support (VaultedVentures fork)
  * Removed the "stops after a few hundred characters" limitation.
    - SAPI's Speak() is told explicitly that the text is NOT XML (SVSFIsNotXML),
      so '<', '>', '&' etc. in copied text are read literally instead of being
      parsed as markup and silently dropping the rest of the text.
    - Long clipboard content is split into small chunks (max 4000 chars, cut at
      sentence/line boundaries) and queued, so there is no length limit at all.
  * New clipboard content interrupts whatever is currently being read (queue is
    purged on new copy), so a long article can't block new copies for minutes.
  * Clipboard polling is now crash-safe: a temporarily locked clipboard (e.g.
    while another app is copying) is skipped and retried instead of killing the app.
  * Build is reproducible: Interop.SpeechLib.dll is referenced from the repo
    (bin\Release) instead of a machine-specific SDK path.

v1.0 (2011) — Original release.
I created this piece of software many years ago in just a few hours, but it's still a super useful tool for fast learning so I decided to share it with the world. 
