# SUPer CLI [Command Line Interface]
SUPer is a HDMV PGS ("BD SUP") encoder. It supports typesetting effects and generates fully compliant Presentation Graphic Streams from BDN XML + PNG assets.

## Input file format
SUPer only accepts Sony BDN + PNG assets as input. These may be generated via [ass2bdnxml](https://github.com/cubicibo/ass2bdnxml) or avs2bdnxml.

## Output file format
Two output formats are supported: SUP and PES+MUI. The later is commonly used in professional authoring suites.

## config.ini
The config.ini file contains permanent configuration items. Notably, a relative or absolute path to an image quantizer (either pngquant[.exe] or libimagequant[.dll, .so]) and options can be specified. If pngquant is in PATH, the name is sufficient. An external quantizer will offer higher quality than the internal one.

### Command line interface

`SUPer_CLI.exe [PARAMETERS] outputfile`

```
 -i, --input         Input BDNXML file [Mandatory]
 -c, --compression   Set the time margin required to perform an acquisition, affects stream compression. [int, 0-100, def: 65]
 -a, --acqrate       Set the acquisition rate, lower values will compress the stream but lower quality. [int, 0-100, def: 100]
 -q, --quantizer     Set the image quantization mode. [0: Qtzr, 1: Pillow, 2: HexTree, 3: libimagequant/pngquant, def: 3]
 -n, --allow-normal  Flag to allow normal case object redefinition, can reduce the number of dropped events on complex animations.
 -k, --prefer-normal Flag to always prefer normal case object redefinition, can reduce the overall bitrate.
 -t, --threads       Set the number of concurrent threads to use. Default is 0 (autoselect), maximum is 8.
 -b, --bt            Set the target BT matrix [601, 709, 2020, def: 709]
 -p, --palette       Flag to always write the full palette (enforced for PES).
 -d, --ahead         Flag to enable display set buffering to drop fewer events.
 -y, --yes           Flag to overwrite output file if it already exists.
 -w, --withsup       Flag to write both SUP and PES+MUI files.
 -m, --max-kbps      Set the maximum bitrate to test the output against. Recommended range: [500-16000].
 -e, --extra-acq     Set the min count of palette update after which acquisitions should be inserted [0: off, default: 2]
 -l, --log-to-file   Set (enable) logging to file and set logging level: [-10: main report, 10: debug, 20: info, 25: iinf, 30: warnings]
 -v, --version       Print the version and exit.
 --layout            Set the epoch and window definition mode. 2 is the preferred default [2: opportunist, 1: normal, 0: basic].
 --ssim-tol          Adjust the SSIM tolerance threshold. This threshold is used for bitmaps classification in effects [-100; 100, def: 0]
 --redraw-period     Set the period to redraw the screen, useful for streams with long lasting events [1.0+, def: 0 (off)]
```

### CLI options
Here are some additional informations on selected options, especially those that shape the datastream and affect the output:
- Compression rate: minimum time margin (in %) between two events to perform an acquisition.
- Acquisition rate: Secondary compression parameter. Leave it untouched at 100 unless a lower bitrate is needed.
- Quantization: [Image quantizer](#image-quantizers) to use. pngquant/libimagequant (3) is recommended.
- Allow normal case object redefinition: whenever possible, update a single object out of two. This may reduce the count of dropped events.
- Palette update buffering: decode palette updates early to drop fewer events, right before decoding of new bitmaps.
- Insert acquisition: perform a screen refresh after a palette animation. Some palette animations may cause artifacts that remain on the display, a refresh will hide them. This can impact the bitrate as new bitmaps are inserted in the stream.
- Logging: logging level -10 creates a single file listing solely the filtering decisions, handy for inspection after batch processing.
- Layout mode: steer the epoch definition algorithm way of defining windows. Opportunist (2) is the best and coded buffer-safe, but may be disliked by Scenarist "Build/Rebuild" operation. 1 is an acceptable fall-back. 0 should be avoided.

!!! If `--allow-normal`, `--prefer-normal` or `--ahead` is used in a Scenarist(R) BD project, one must not "Encode->Build" or "Encode->Rebuild" the PES assets. (Re-)Building the .PES is not mandatory to mux a project. !!!

### Image quantizers
The different methods (values for `--qmode`) are enumerated here:

0. **Qtzr**: High quality and reasonably fast quantizer, albeit generates the largest file sizes.
1. **Pillow**: Fastest but low quality. Excellent for low bitrates and files with solely dialogue and karaoke. Glows and gradients will be hideous.
2. **HexTree**: Fast and good quality. Suits nicely files with moderate typesetting effects (no heavy gradients + large glows).
3. **PILIQ** (libimagequant): delivers the best quality, acceptably fast.
    
WARNING: Moving the executable in another location without `config.ini`, `lib` or `_internal` will break the app or libimagequant.

Higher quality quantizers will generally consume more bandwidth:
- Quality: (highest) *PILIQ > Qtzr > HexTree >> Pillow* (lowest).
- Bandwidth usage: (highest) *Qtzr > PILIQ > HexTree >> Pillow* (lowest).

### Example
`SUPer_CLI.exe -i ./subtitle01/bdn.xml -c 80 -a 100 --qmode 3 --allow-normal --ahead --palette --bt 709 -m 10000 --threads 6 --withsup --yes ./output/01/out.pes`

## Special Thanks
Masstock, TheScorpius666, jcdr
