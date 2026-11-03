#!/usr/bin/env bash
# How fast a check is, and how a queue of files goes one at a time or several at once
# (docs/decisions/2026-11-03-benchmark.md; the figures: docs/notes/performance.md). Builds the command in Release,
# then, each in its own process:
#   - one check of each make-samples.sh file (made first when missing), and of longer programmes looped from the
#     20 s ProRes and H.264 ones (ffmpeg -stream_loop, copied, without the timecode track): wall and CPU seconds,
#     peak memory, seconds a programme minute;
#   - a queue of the 20 s ProRes programme, QUEUE copies (links), run 1, 2, 4 ... at a time (xargs -P): what
#     Jobs would gain from running checks side by side.
# Writes a Markdown table to stdout and to FOLDER/benchmark.md. Needs dotnet, ffmpeg/ffprobe (with libx264 for the
# H.264 files) and python3 (for peak memory). The long ProRes is ~280 MB a minute.
#
#   tools/benchmark.sh [--minutes N] [--queue N] [--jobs "1 2 4"] [FOLDER]   defaults: 10, 8, "1 2 4",
#                                                                             artifacts/benchmark
set -euo pipefail
cd "$(dirname "$0")/.."

minutes=10
queue=8
jobs="1 2 4"
while [ $# -gt 0 ]; do
  case "$1" in
    --minutes) minutes="$2"; shift 2 ;;
    --queue) queue="$2"; shift 2 ;;
    --jobs) jobs="$2"; shift 2 ;;
    -*) echo "benchmark: unknown option $1" >&2; exit 2 ;;
    *) break ;;
  esac
done
work="${1:-artifacts/benchmark}"
for tool in dotnet ffmpeg ffprobe python3; do
  command -v "$tool" > /dev/null || { echo "benchmark: $tool isn't on PATH" >&2; exit 1; }
done

mkdir -p "$work/media" "$work/reports"
cli="$work/cli"
echo "benchmark: building the command (Release)" >&2
dotnet build -c Release src/Noga.Cli/Noga.Cli.csproj -o "$cli" -v quiet -nologo > /dev/null

samples=samples
[ -f "$samples/programme_prores.mov" ] || tools/make-samples.sh "$samples" >&2

# The 20 s programmes looped to MINUTES minutes: the same content over and over, so the same work a second.
loops=$((minutes * 3 - 1))
for kind in prores.mov h264.mp4; do
  src="$samples/programme_$kind"
  dst="$work/media/programme_${minutes}min_$kind"
  [ -f "$src" ] || continue
  if [ ! -f "$dst" ]; then
    echo "benchmark: making $dst" >&2
    ffmpeg -hide_banner -loglevel error -nostdin -y -stream_loop "$loops" -i "$src" -map 0:v -map 0:a -c copy "$dst"
  fi
done

# One process measured: "wall cpu peak_mb exit" (seconds, seconds, MB of the largest resident set).
measure() {
  python3 -I -c '
import resource, subprocess, sys, time
start = time.monotonic()
code = subprocess.run(sys.argv[1:], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL).returncode
wall = time.monotonic() - start
use = resource.getrusage(resource.RUSAGE_CHILDREN)
print(f"{wall:.1f} {use.ru_utime + use.ru_stime:.1f} {use.ru_maxrss / 1024:.0f} {code}")
' "$@"
}

duration() { ffprobe -v error -show_entries format=duration -of csv=p=0 "$1"; }

cpus=$(nproc)
cpu_name=$(sed -n 's/^model name[[:space:]]*: //p' /proc/cpuinfo | head -1)
report="$work/benchmark.md"
{
  echo "Noga $(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' Directory.Build.props), $(git rev-parse --short HEAD), $(date -u +%Y-%m-%d);"
  echo "$cpus CPUs ($cpu_name), $(free -g | awk '/^Mem:/ {print $2}') GB; Release build, one process per check."
  echo
  echo "| File | Length | Wall | CPU | Cores busy | Wall a programme minute | Peak memory | Exit |"
  echo "| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |"
} | tee "$report"

files=("$samples"/programme_prores.mov "$samples"/programme_dnxhr.mxf "$samples"/programme_h264.mp4
  "$samples"/out_of_range.mov "$samples"/letterbox.mov "$work"/media/programme_"${minutes}"min_*)
for file in "${files[@]}"; do
  [ -f "$file" ] || continue
  echo "benchmark: checking $file" >&2
  len=$(duration "$file")
  read -r wall cpu peak code < <(measure "$cli/noga" check "$file" --out "$work/reports")
  awk -v f="$(basename "$file")" -v len="$len" -v wall="$wall" -v cpu="$cpu" -v peak="$peak" -v code="$code" \
    'BEGIN { printf "| %s | %.0f s | %.1f s | %.1f s | %.1f | %.1f s | %d MB | %d |\n",
             f, len, wall, cpu, cpu / wall, wall / (len / 60), peak, code }'
done | tee -a "$report"

# The queue: QUEUE links to the 20 s ProRes programme, each its own name so the reports don't collide.
qdir="$work/queue"
rm -rf "$qdir" && mkdir -p "$qdir/reports"
for i in $(seq 1 "$queue"); do ln -s "$(realpath "$samples/programme_prores.mov")" "$qdir/programme_$i.mov"; done
{
  echo
  echo "Queue: $queue checks of programme_prores.mov (20 s each), N at a time (separate processes)."
  echo
  echo "| At a time | Wall | Wall a file | Against one at a time | Peak memory (one process) |"
  echo "| ---: | ---: | ---: | ---: | ---: |"
} | tee -a "$report"
base=""
for n in $jobs; do
  echo "benchmark: queue, $n at a time" >&2
  read -r wall _ peak _ < <(measure bash -c 'ls "$1"/*.mov | xargs -P "$2" -I{} "$3" check {} --out "$1/reports"' _ \
    "$qdir" "$n" "$cli/noga")
  base=${base:-$wall}
  awk -v n="$n" -v wall="$wall" -v q="$queue" -v base="$base" -v peak="$peak" \
    'BEGIN { printf "| %d | %.1f s | %.1f s | %.2fx | %d MB |\n", n, wall, wall / q, base / wall, peak }'
done | tee -a "$report"
echo "benchmark: wrote $report" >&2
