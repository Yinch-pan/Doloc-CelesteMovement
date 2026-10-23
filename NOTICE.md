# Attribution and distribution boundaries

The MIT license applies to original code and documentation contributed to this project. It does not relicense third-party games, source material, assets, trademarks, or dependencies.

- **Celeste**: movement design and behavior reference. Local development consulted the user-provided Celeste/Monocle reference project and compared selected scripted movement scenarios. This project is not represented as a clean-room implementation or an official Celeste product. The reference source, assemblies and assets are not distributed in this repository. https://www.celestegame.com/
- **Doloc Town**: the host game. The mod refers to host APIs and uses the installed game's existing character textures and animation data at runtime. No game DLLs, extracted textures or decompiled research files are included. https://store.steampowered.com/app/2285550/
- **DTMAPI**: required mod framework and authoring SDK, obtained separately. Refer to its own license and third-party notices. https://github.com/yakumo-hx/DTMAPI
- **Harmony, Unity and .NET**: development/runtime dependencies subject to their respective licenses; no third-party binary redistribution is made through this source repository.

The Steam Workshop cover for version 0.2.10 uses an official Celeste Steam store screenshot. That image and the Doloc Town screenshots are intentionally excluded from this code repository and are not licensed under this project's MIT license. Image source: https://store.steampowered.com/app/504230/Celeste/

The optional local comparison harness depends on a private reference lab and is not part of the public, independently runnable test suite. The published tests exercise this repository's own movement and adapter state logic.

This is an unofficial fan mod. No affiliation or endorsement by the game developers is claimed. Contributors must not add game binaries, decompiled source, extracted assets, credentials or personal save files.
