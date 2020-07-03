# NScumm.Audio
[![License: GPL v3](https://img.shields.io/badge/License-GPL%20v3-blue.svg)](https://www.gnu.org/licenses/gpl-3.0)
[![Build status](https://ci.appveyor.com/api/projects/status/x8we7f55wpdepu6c?svg=true)](https://ci.appveyor.com/project/scemino/nscumm-audio)
![.NET Core](https://github.com/scemino/NScumm.Audio/workflows/.NET%20Core/badge.svg)

**NScumm.Audio** is a pure C# library, it contains these OPL emulators:

* DOSBox
* Woody

**NScumm.Audio.Players** is a C# library containing several players decoding different adlib music formats (see Supported formats).

**NScumm.Audio.Player** is a small console frontend, it uses these libraries to play adlib musics.

## Supported formats

* A2M: AdLib Tracker 2 by subz3ro
* ADL: Westwood ADL File Format
* BAM: Bob's Adlib Music Format
* BMF: Easy AdLib 1.0 by The Brain
* CMF: Creative Music File Format by Creative Technology
* DRO: DOSBox Raw OPL Format
* HSC: HSC Adlib Composer by Hannes Seifert, HSC-Tracker by Electronic Rats
* IMF: Apogee IMF File Format
* KSM: Ken Silverman's Music Format
* LAA: LucasArts AdLib Audio File Format by LucasArts
* MID: MIDI Audio File Format
* MKJ: MKJamz by M \ K Productions (preliminary)
* S3M: Screamtracker 3 by Future Crew
* SCI: Sierra's AdLib Audio File Format
* SNG: Adlib Tracker 1.0 by TJ
* XSM: eXtra Simple Music by Davey W Taylor

## License

DosBox OPL3 emulator is licensed under GPL v2+.

WoodyOPL from the DOSBox team is licensed under LGPL v2.1.

AdPlug is licensed under LGPL v2.1.

Mono.Options is licensed under the MIT License (MIT).
