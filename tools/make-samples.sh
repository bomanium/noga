#!/usr/bin/env bash
# Generates the test files in samples/ (ignored by git, never committed) with ffmpeg's lavfi sources only: no
# customer media, no downloads. Each file has a known answer (where its black, bars, tone and silence are, and at
# what levels), so a check can be tested against what was put in as well as against FFmpeg's own filters.
#
#   tools/make-samples.sh [output folder, default samples/]
#
# Needs ffmpeg on PATH (any recent build; libx264 for the H.264 files, which are made only when it has it). The
# encoders run here only to make test files; nothing from this ffmpeg is shipped.
set -euo pipefail

out="${1:-samples}"
mkdir -p "$out"
command -v ffmpeg > /dev/null || { echo "make-samples: ffmpeg isn't on PATH" >&2; exit 1; }

ff() { ffmpeg -hide_banner -loglevel error -nostdin -y "$@"; }
# Not "ffmpeg ... | grep -q": grep stops reading at the first match, and pipefail then fails on ffmpeg's broken pipe.
encoders=$(ffmpeg -hide_banner -encoders 2> /dev/null)
has_encoder() { grep -q " $1 " <<< "$encoders"; }

size=1920x1080
rate=25
# testsrc2 drawn in RGB and converted with BT.709's matrix, as an HD master is: drawn straight into YUV it takes
# BT.601's, and read as BT.709 most of its colours are then outside RGB gamut (docs/decisions/2026-11-06-rgb-gamut.md).
# Straight to 4:2:2: through 4:4:4, the 4:2:2 encoders' bicubic chroma resampling overshoots its saturated edges
# outside broadcast range. The programme's other parts are 4:2:2 too, so that concat converts none of them.
hd="format=rgb24,scale=out_color_matrix=bt709:out_range=tv,format=yuv422p10le"
# EBU line-up tone: 1 kHz at -18 dBFS (EBU R 68), as an exact amplitude rather than sine's default of 1/8.
tone="aevalsrc=exprs=pow(10\,-18/20)*sin(2*PI*1000*t):s=48000:c=stereo"
silence="anullsrc=r=48000:cl=stereo"

# A delivery-shaped programme, 20 s: bars and tone 0-5 s, black and silence 5-8 s, picture and pink noise
# 8-17 s, black and silence 17-20 s; its timecode starts at 10:00:00:00, as a broadcast master's does. The gaps are
# 3 s, clear of the profile's 2 s minimum: AAC smears the tone's end into the silence after it.
# programme FILE PICTURE [ffmpeg output options]: PICTURE is the filter chain after the parts are joined ("null" for
# none).
programme() {
  local file="$1" picture="$2"; shift 2
  ff -f lavfi -i "smptehdbars=s=$size:r=$rate:d=5,format=yuv422p10le" \
     -f lavfi -i "color=c=black:s=$size:r=$rate:d=3,format=yuv422p10le" \
     -f lavfi -i "testsrc2=s=$size:r=$rate:d=9,$hd" \
     -f lavfi -i "color=c=black:s=$size:r=$rate:d=3,format=yuv422p10le" \
     -f lavfi -t 5 -i "$tone" \
     -f lavfi -t 3 -i "$silence" \
     -f lavfi -t 9 -i "anoisesrc=r=48000:c=pink:a=0.1,aformat=channel_layouts=stereo" \
     -f lavfi -t 3 -i "$silence" \
     -filter_complex "[0:v][4:a][1:v][5:a][2:v][6:a][3:v][7:a]concat=n=4:v=1:a=1[joined][a];[joined]$picture[v]" \
     -map "[v]" -map "[a]" -timecode 10:00:00:00 "$@" "$file"
}

# ProRes 422 HQ in QuickTime with PCM: the usual professional delivery (ManagedCodecs can't decode it).
programme "$out/programme_prores.mov" null -c:v prores_ks -profile:v 3 -pix_fmt yuv422p10le -c:a pcm_s24le

# DNxHR SQ in MXF OP1a with PCM.
programme "$out/programme_dnxhr.mxf" null -c:v dnxhd -profile:v dnxhr_sq -pix_fmt yuv422p -c:a pcm_s24le

# H.264 and AAC in MP4: the web delivery (what ManagedCodecs decodes). To 4:2:0 bilinear, as above: bicubic
# overshoots testsrc2's saturated edges outside broadcast range.
if has_encoder libx264; then
  programme "$out/programme_h264.mp4" "scale=flags=bilinear,format=yuv420p" -c:v libx264 -preset fast -pix_fmt yuv420p -g 50 -c:a aac -b:a 192k -movflags +faststart
fi

# Pixels outside broadcast range: a 10-bit luma ramp over every code value (ProRes keeps 4-1019, so 4-63 and
# 941-1019 lie outside 64-940) on neutral chroma, tagged limited range, 5 s. (smptehdbars itself has some too: its PLUGE and
# +I/-I patches go from 4 to 1016.)
ff -f lavfi -i "nullsrc=s=$size:r=$rate:d=5,format=yuv422p10le,geq=lum='X*1023/(W-1)':cb=512:cr=512" \
   -f lavfi -t 5 -i "$silence" -c:v prores_ks -profile:v 3 -color_range tv -c:a pcm_s24le "$out/out_of_range.mov"

# Letterboxed 2.39:1 picture in a 16:9 frame (black bars of 138 lines top and bottom), 5 s.
ff -f lavfi -i "testsrc2=s=1920x804:r=$rate:d=5,$hd,pad=1920:1080:0:138:black" \
   -f lavfi -t 5 -i "$tone" -c:v prores_ks -profile:v 3 -pix_fmt yuv422p10le -c:a pcm_s24le "$out/letterbox.mov"

# Which version of this script made them: the tests make them again when it changes.
sha256sum "$0" | cut -d' ' -f1 > "$out/.make-samples.sha256"
echo "make-samples: wrote $(ls "$out" | wc -l) files to $out/"
