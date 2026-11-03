mediaqc: file-based quality control of video deliveries (a working name)

  mediaqc check FILE [--out FOLDER] [--warnings-fail] [--profile PROFILE]
  mediaqc serve [--port PORT | --listen ADDRESS:PORT] [--root FOLDER]... [--watch FOLDER] [--token TOKEN]
                [--cert FILE [--key FILE] | --http] [--profile PROFILE] [--no-open]

writes FILE.qc.html and FILE.qc.json next to FILE (or in FOLDER), prints the findings and exits 0 passed, 1 failed,
2 usage or read error, 3 warnings (1 with --warnings-fail). serve runs the web UI on this machine only
(http://localhost:8722): pick files in its folders, and each is checked where it is, its reports written beside it. With --watch, the media
files in FOLDER (and its subfolders) that have no report yet are checked too, new ones once their copy has finished.
--listen 0.0.0.0:8722 --root FOLDER shares it with your team: only the --root folders can be seen and checked, and
only by who opens the link it prints (it carries a random token). Shared, it is HTTPS: with your certificate
(--cert server.pfx, its password in MEDIAQC_CERT_PASSWORD if it has one, or --cert server.crt --key server.key), else
a self-signed one it makes once and keeps in your profile. Browsers warn about a self-signed certificate once: accept
it when the fingerprint they show is the one mediaqc printed. --http shares it as plain HTTP instead, for a network you
trust.
mediaqc --help says the same.

Profiles: ebu-r128 (the default), atsc-a85, op-59, arib-tr-b32, or your own in a .json file (a "name", "basedOn" one
of those, and the thresholds it changes, e.g. "maxTruePeak": -3); mediaqc --help lists them with their figures.

The line-up tone's level is judged by the profile: -18 dBFS for ebu-r128, -20 dBFS for atsc-a85, within 0.5 dB (a
warning outside it; "toneLevel" and "toneTolerance" in a profile file). A profile file can also ask for the head and
tail: "headBars" and "headTone" (seconds of bars and of tone from the start), "headBlack" and "tailBlack" (seconds of
black with silence before the programme and at the end); a file short of one fails. The HD templates ask for 30 s of
bars and tone, 10 s of black before and 5 s after.

A picture frozen for 2 s or more (freezedetect's measure; black and colour bars left out) is a warning mid-programme
and reported at the head or tail; a profile file's "freezeNoise" and "minFreeze" change what counts.

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

The desktop app: mediaqc-app (mediaqc-app.exe on Windows) opens a window to drop video files or folders on; each is
checked where it is, with the profile chosen in the window, and its reports open from the list. "mediaqc-app FILE..."
queues files at start. On Linux it needs a desktop (X11) and fontconfig, which desktop systems have; to have it in the
desktop's menu (and "Open with" for video files), run ./install-desktop-entry.sh in this folder: it writes
mediaqc-app.desktop into ~/.local/share/applications pointing at this folder's mediaqc-app and mediaqc-app.png. Run it
again after moving the folder; ./install-desktop-entry.sh --uninstall takes the entry out.

The installers: the release also has mediaqc-VERSION-win-x64-setup.exe, mediaqc-VERSION-x86_64.AppImage and, for
Linux on ARM (64-bit: Raspberry Pi 4 and 5 with a 64-bit system, ARM servers), mediaqc-VERSION-aarch64.AppImage with
the linux-arm64 .tar.gz; each holds the same files as its platform's folder. The setup.exe installs for your user only, without administrator rights, into
%LOCALAPPDATA%\Programs\mediaqc, with a Start-menu shortcut (and a desktop one, and mediaqc on your PATH, if you
choose); Settings, Apps uninstalls it. The AppImage runs as it is (chmod +x it first): it opens the desktop app, or runs
the command when its first argument is check, serve, --help or --version, or when it is run through a link named
mediaqc (ln -s ~/Applications/mediaqc-VERSION-x86_64.AppImage ~/.local/bin/mediaqc). It needs FUSE, which desktop
systems have; elsewhere set APPIMAGE_EXTRACT_AND_RUN=1. Its runtime's licences (libfuse's LGPL among them) are in
mediaqc-VERSION-ARCH.AppImage.licences.txt and inside it, in licences/appimage-runtime/.

Every figure says what it is checked against in FFmpeg (which filter, which options, what tolerance).

This folder holds everything mediaqc needs: the .NET runtime and FFmpeg's libraries come with it.

FFmpeg's libraries
  libavcodec, libavformat, libavfilter, libavutil, libswresample and libswscale (.so files on Linux, .dll files on
  Windows) are FFmpeg 8.1, under the GNU Lesser General Public License 2.1 or later. They are separate files, loaded
  when mediaqc runs, so you can replace them with your own build of the same FFmpeg version (8.1, the same library
  versions and file names). The licence, the build's exact configure line and source URLs, and an SPDX bill of
  materials are in licences/ffmpeg/. FFmpeg's source: https://ffmpeg.org/releases/ffmpeg-8.1.3.tar.xz.

ShotDetector.FastYuv
  ShotDetector.FastYuv.dll converts the H.264 path's frames for the shot list. It ports FFmpeg's code, so it is under
  the GNU Lesser General Public License 2.1 or later (licences/shotdetector-fastyuv/COPYING.LGPL). It is a separate
  assembly, loaded when mediaqc runs, so you can replace it with your own build of the same version (3.25.0). Its
  source: https://github.com/bisforboman/shotdetector (src/ShotDetector.FastYuv; the release is commit
  97be0548db06a8388a03e8ffc26ac92bf428480e).

Other licences
  licences/THIRD-PARTY-NOTICES.txt: the libraries mediaqc is built on (MIT).
  licences/dotnet/: the .NET runtime's licence and third-party notices.
  licences/aspnetcore/: ASP.NET Core's (the web UI's server), the same.
