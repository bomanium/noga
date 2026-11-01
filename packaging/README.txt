mediaqc: file-based quality control of video deliveries (a working name)

  mediaqc check FILE [--out FOLDER] [--warnings-fail]

writes FILE.qc.html and FILE.qc.json next to FILE (or in FOLDER), prints the findings and exits 0 passed, 1 failed,
2 usage or read error, 3 warnings (1 with --warnings-fail). mediaqc --help says the same.

Every figure says what it is checked against in FFmpeg (which filter, which options, what tolerance).

This folder holds everything mediaqc needs: the .NET runtime and FFmpeg's libraries come with it.

FFmpeg's libraries
  libavcodec, libavformat, libavfilter, libavutil, libswresample and libswscale (.so files on Linux, .dll files on
  Windows) are FFmpeg 8.1, under the GNU Lesser General Public License 2.1 or later. They are separate files, loaded
  when mediaqc runs, so you can replace them with your own build of the same FFmpeg version (8.1, the same library
  versions and file names). The licence, the build's exact configure line and source URLs, and an SPDX bill of
  materials are in licences/ffmpeg/. FFmpeg's source: https://ffmpeg.org/releases/ffmpeg-8.1.3.tar.xz.

Other licences
  licences/THIRD-PARTY-NOTICES.txt: the libraries mediaqc is built on (MIT).
  licences/dotnet/: the .NET runtime's licence and third-party notices.
