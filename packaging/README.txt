mediaqc: file-based quality control of video deliveries (a working name)

  mediaqc check FILE [--out FOLDER] [--warnings-fail] [--profile PROFILE]
  mediaqc serve [--port PORT | --listen ADDRESS:PORT] [--root FOLDER]... [--watch FOLDER] [--token TOKEN] [--profile PROFILE] [--no-open]

writes FILE.qc.html and FILE.qc.json next to FILE (or in FOLDER), prints the findings and exits 0 passed, 1 failed,
2 usage or read error, 3 warnings (1 with --warnings-fail). serve runs the web UI on this machine only
(http://localhost:8722): pick files in its folders, and each is checked where it is, its reports written beside it. With --watch, the media
files in FOLDER (and its subfolders) that have no report yet are checked too, new ones once their copy has finished.
--listen 0.0.0.0:8722 --root FOLDER shares it with your team on a trusted network: only the --root folders can be seen
and checked, and only by who opens the link it prints (it carries a random token). It is plain HTTP: use it on a
network you trust.
mediaqc --help says the same.

Profiles: ebu-r128 (the default), atsc-a85, op-59, arib-tr-b32, or your own in a .json file (a "name", "basedOn" one
of those, and the thresholds it changes, e.g. "maxTruePeak": -3); mediaqc --help lists them with their figures.

The desktop app: mediaqc-app (mediaqc-app.exe on Windows) opens a window to drop video files or folders on; each is
checked where it is, with the profile chosen in the window, and its reports open from the list. "mediaqc-app FILE..."
queues files at start. On Linux it needs a desktop (X11) and fontconfig, which desktop systems have; to have it in the
desktop's menu (and "Open with" for video files), run ./install-desktop-entry.sh in this folder: it writes
mediaqc-app.desktop into ~/.local/share/applications pointing at this folder's mediaqc-app and mediaqc-app.png. Run it
again after moving the folder; ./install-desktop-entry.sh --uninstall takes the entry out.

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
