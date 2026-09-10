<p align="center">
    <h1 align="center">
        OpenTPW
    </h1>
    <p align="center">
        OpenTPW is an open-source re-implementation of <a href="https://en.wikipedia.org/wiki/Theme_Park_World">Sim Theme Park / Theme Park World</a>.
        <br>
        <a href="https://opentpw.org/">Website</a> |
        <a href="https://docs.opentpw.org/">Documentation</a>
    </p>
</p>

![image](https://github.com/user-attachments/assets/be81a5d3-f99c-4f46-8200-7ea5d9a652e8)

## About

OpenTPW is a re-implementation of Theme Park World, requiring an installation the original game and its assets in order to run. OpenTPW aims to re-create the same experience as the original game. While OpenTPW was initially created as it is quite difficult to get Sim Theme Park to run on modern hardware and on a modern operating system, it also aims to somewhat re-introduce the original online aspect of the game - the servers of which have since been shut down.

**In order to run OpenTPW, you must have a full legal copy of any version of the original game.**

## Status

OpenTPW is currently in a very early stage of development, and is not yet playable.

### File Formats

- ❌ - Not Implemented
- ⚠️ - Partially Implemented
- ✅ - Implemented

| Format                                                  | Status |
|---------------------------------------------------------|--------|
| Textures ([.WCT](https://opentpw.gu3.me/formats/wct.html))                    | ✅     |
| Settings ([.SAM](https://opentpw.gu3.me/formats/sam.html))                    | ✅     |
| Sounds ([.SDT](https://opentpw.gu3.me/formats/sdt.html), .MP2) \*\*            | ✅     |
| Strings ([.BFMU](https://opentpw.gu3.me/formats/bfmu.html), [.BFST](https://opentpw.gu3.me/formats/bfst.html), [.BFUM](https://opentpw.gu3.me/formats/bfum.html)) | ✅     |
| Models ([.MD2](https://opentpw.gu3.me/formats/m3d2.html)) \*                   | ⚠️     |
| Map Data ([.MAP](https://opentpw.gu3.me/formats/map.html))                    | ⚠️     |
| Ride Scripts ([.RSE](https://opentpw.gu3.me/formats/rsse.html))                | ⚠️     |
| Save Files ([.TPWS](https://opentpw.gu3.me/formats/tpws-ints-lays.html))                | ⚠️     |
| Fonts ([.BF4](https://opentpw.gu3.me/formats/bf4.html))                      | ❌     |
| Lip Sync ([.LIPS](https://opentpw.gu3.me/formats/lips.html))                  | ❌     |
| Materials ([.MTR](https://opentpw.gu3.me/formats/mtr.html))                   | ❌     |
| Video ([.TQI](https://opentpw.gu3.me/formats/tqi.html))                       | ❌     |
| Sound categories (cat_\*.map) \*\*                                             | ⚠️     |
| Park signs (.SGN) \*\*\*                                                        | ⚠️     |

\* **Models (.MD2)**: static mesh geometry (verts/faces/materials) loads reliably. The same extension is also used for a structurally distinct keyframe animation format, of which three channel kinds are decoded and played: per-vertex morph animation (752 files), per-mesh quaternion rotation (686 files) and UV scrolling (324 files) - together 1151 of the game's 1279 animation files (90%). Of the rest, 38 carry only channel kinds that aren't decoded yet and 90 contain no animation data at all.

\*\* **Sounds (.SDT, .MP2, cat_\*.map)**: banks are read, and their audio decodes and plays. Despite the .mp2 extension on every name inside a bank, the audio is not always MPEG Layer II - 2,646 of the game's 3,739 streams are Layer I - so both layers are decoded, and Layer III does not occur. Nothing in the game addresses a sound by file name: sounds are grouped into categories and code plays a numbered effect within one, which is what the cat_\*.map pair holds. Its bank half is fully decoded. Of its effect half, the header, the effect table and the sample records are decoded, but the variable-size header in front of each effect's sample list is not, so the records are located by validating them against the banks rather than by offset.

\*\*\* **Park signs (.SGN)**: a park's name board renders with the fonts, colours and artwork the file asks for. Two regions of its header - a 64x64 image at 0x03C5 and 36 bytes at 0x03A1 - are not identified.

### Documentation

File format information is available at the [OpenTPW formats](https://opentpw.gu3.me/formats/) website. Keep in mind that this information is a work-in-progress, and therefore might not be of incredible detail - however, upon completion, it still aims to be as useful, detailed, and as in-depth as possible.

## Contributing

Contributions to this project are greatly appreciated; please follow these steps in order to submit your contribution to the project:

1. Fork the [Original Project](https://github.com/ThemeParkWorld/OpenTPW)
2. Create a branch under the name `YourName/FeatureName`
3. Once you've made all the changes you need to make, go ahead and submit a Pull Request.

## License

This project is licensed under the MIT license; a copy of this license is available at [LICENSE.md](https://github.com/ThemeParkWorld/OpenTPW/blob/main/LICENSE.md).
