noga: file-based quality control of video deliveries (Noga: Swedish for careful, thorough)

  noga check FILE [--out FOLDER] [--warnings-fail] [--profile PROFILE]
  noga serve [--port PORT | --listen ADDRESS:PORT] [--root FOLDER]... [--watch FOLDER] [--token TOKEN]
                [--cert FILE [--key FILE] | --http] [--profile PROFILE] [--no-open]

writes FILE.qc.html and FILE.qc.json next to FILE (or in FOLDER), prints the findings and exits 0 passed, 1 failed,
2 usage or read error, 3 warnings (1 with --warnings-fail). serve runs the web UI on this machine only
(http://localhost:8722): pick files in its folders, and each is checked where it is, its reports written beside it. With --watch, the media
files in FOLDER (and its subfolders) that have no report yet are checked too, new ones once their copy has finished.
--listen 0.0.0.0:8722 --root FOLDER shares it with your team: only the --root folders can be seen and checked, and
only by who opens the link it prints (it carries a random token). Shared, it is HTTPS: with your certificate
(--cert server.pfx, its password in NOGA_CERT_PASSWORD if it has one, or --cert server.crt --key server.key), else
a self-signed one it makes once and keeps in your profile. Browsers warn about a self-signed certificate once: accept
it when the fingerprint they show is the one noga printed. --http shares it as plain HTTP instead, for a network you
trust.
noga --help says the same.

Profiles: ebu-r128 (the default), ebu-r128-s1 (ads and promos), atsc-a85, op-59, arib-tr-b32, or your own in a .json file (a "name", "basedOn" one
of those, and the thresholds it changes, e.g. "maxTruePeak": -3); noga --help lists them with their figures.

The line-up tone's level is judged by the profile: -18 dBFS for ebu-r128, -20 dBFS for atsc-a85, within 0.5 dB (a
warning outside it; "toneLevel" and "toneTolerance" in a profile file). A profile file can also ask for the head and
tail: "headBars" and "headTone" (seconds of bars and of tone from the start), "headBlack" and "tailBlack" (seconds of
black with silence before the programme and at the end); a file short of one fails. The HD templates ask for 30 s of
bars and tone, 10 s of black before and 5 s after.

Interlacing is read from the picture too (FFmpeg's idet): the report says whether the picture looks interlaced, and
which field first, and warns when the stream's flag says otherwise.

A picture frozen for 2 s or more (freezedetect's measure; black, colour bars and constant colour left out) is a
warning mid-programme and reported at the head or tail; a profile file's "freezeNoise" and "minFreeze" change what
counts.

Runs of 2 s or more of one flat colour other than black (a slate or a test colour left in: signalstats' Y, U and V
10th to 90th percentiles within 4 codes) are a warning mid-programme and reported at the head or tail, with the colour
(#RRGGBB); a profile file's "colourSpread" and "minConstantColour" change what counts.

Pixels outside RGB gamut (EBU R 103: R, G or B below -5% or above 105%, by the file's matrix, else BT.709, or BT.601
under 720 lines) on over 1% of a frame are a warning, as pixels outside broadcast range are; a profile file's
"maxOutOfGamut", "gamutLow" and "gamutHigh" change what counts.

Each audio track's channels are checked one by one: a channel silent throughout while another has sound fails the
file; a channel silent for 2 s or more while the others have sound, a stereo pair out of phase (its phase correlation
below -0.3) and clipping (3 or more samples in a row at full scale) are warnings; dual mono is reported. A profile
file's "minPhase" and "clipRun" change the last two.

Delivery formats: every report lists the file's format (codec and profile, frame size and rate, scan, pixel format,
bit depth, colour, aspect ratio, each audio track, duration) as ffprobe reads it. A profile file's "format" section
names what the delivery asks for, and each value given fails the file when it differs: "container", "videoCodec",
"videoProfile", "width", "height", "frameRate" ("25", "30000/1001"), "scan" ("progressive" or "interlaced"),
"fieldOrder" (ffprobe's field_order), "pixelFormat", "bitDepth", "colourPrimaries", "transfer", "matrix",
"colourRange", "displayAspect", "audioTracks", "audioCodec", "sampleRate", "audioChannels", "audioBitDepth",
"minDuration", "maxDuration" (seconds) and "startTimecode". The profiles folder here has templates to start from
(hd-1080i25-prores-hq, hd-1080p25-prores-hq, web-1080p-h264-aac): --profile takes their names, or copy one, rename it
and change what your delivery spec says.

The desktop app: noga-app (noga-app.exe on Windows) opens a window to drop video files or folders on; each is
checked where it is, with the profile chosen in the window, and its reports open from the list. "noga-app FILE..."
queues files at start. On Linux it needs a desktop (X11) and fontconfig, which desktop systems have; to have it in the
desktop's menu (and "Open with" for video files), run ./install-desktop-entry.sh in this folder: it writes
noga-app.desktop into ~/.local/share/applications pointing at this folder's noga-app and noga-app.png. Run it
again after moving the folder; ./install-desktop-entry.sh --uninstall takes the entry out.

The installers: the release also has noga-VERSION-win-x64-setup.exe, noga-VERSION-x86_64.AppImage and, for
Linux on ARM (64-bit: Raspberry Pi 4 and 5 with a 64-bit system, ARM servers), noga-VERSION-aarch64.AppImage with
the linux-arm64 .tar.gz; each holds the same files as its platform's folder. The setup.exe installs for your user only, without administrator rights, into
%LOCALAPPDATA%\Programs\noga, with a Start-menu shortcut (and a desktop one, and noga on your PATH, if you
choose); Settings, Apps uninstalls it. The AppImage runs as it is (chmod +x it first): it opens the desktop app, or runs
the command when its first argument is check, serve, --help or --version, or when it is run through a link named
noga (ln -s ~/Applications/noga-VERSION-x86_64.AppImage ~/.local/bin/noga). It needs FUSE, which desktop
systems have; elsewhere set APPIMAGE_EXTRACT_AND_RUN=1. Its runtime's licences (libfuse's LGPL among them) are in
noga-VERSION-ARCH.AppImage.licences.txt and inside it, in licences/appimage-runtime/.

A file with sound only (a WAV or AIFF stem, an M4A) is checked for its sound: loudness, true peak, tone, silence,
each channel, its format; the report says the picture's checks aren't made (nor the head's bars and black a profile
asks for), and its timecodes count at 25 fps.

A file cut short or damaged fails: in a MOV, MP4 or MXF, a picture that ends before the frames the file lists, frames
that don't decode, or a track whose sound ends more than a frame before its stream's duration (decode.frames,
decode.errors, decode.sound).

Every figure says what it is checked against in FFmpeg (which filter, which options, what tolerance).

noga is under the MIT licence (LICENSE.txt). The libraries it ships keep their own licences, below.

This folder holds everything noga needs: the .NET runtime and FFmpeg's libraries come with it.

FFmpeg's libraries
  libavcodec, libavformat, libavfilter, libavutil, libswresample and libswscale (.so files on Linux, .dll files on
  Windows) are FFmpeg 8.1, under the GNU Lesser General Public License 2.1 or later. They are separate files, loaded
  when noga runs, so you can replace them with your own build of the same FFmpeg version (8.1, the same library
  versions and file names). The licence, the build's exact configure line and source URLs, and an SPDX bill of
  materials are in licences/ffmpeg/. FFmpeg's source: https://ffmpeg.org/releases/ffmpeg-8.1.3.tar.xz.

ShotDetector.FastYuv
  ShotDetector.FastYuv.dll converts the H.264 path's frames for the shot list. It ports FFmpeg's code, so it is under
  the GNU Lesser General Public License 2.1 or later (licences/shotdetector-fastyuv/COPYING.LGPL). It is a separate
  assembly, loaded when noga runs, so you can replace it with your own build of the same version (3.25.0). Its
  source: https://github.com/bisforboman/shotdetector (src/ShotDetector.FastYuv; the release is commit
  97be0548db06a8388a03e8ffc26ac92bf428480e).

Other licences
  licences/THIRD-PARTY-NOTICES.txt: the libraries noga is built on (MIT).
  licences/dotnet/: the .NET runtime's licence and third-party notices.
  licences/aspnetcore/: ASP.NET Core's (the web UI's server), the same.
