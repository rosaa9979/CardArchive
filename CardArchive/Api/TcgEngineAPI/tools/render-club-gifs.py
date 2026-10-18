"""Encode newly captured club replays as fading, looping website GIFs.

Example: python tools/render-club-gifs.py --ffmpeg /path/to/ffmpeg.exe
Source clips and verification files are produced by ClubShowcaseTool in Unity.
"""
import argparse
import json
import re
import subprocess
from pathlib import Path


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--ffmpeg', required=True)
    parser.add_argument('--width', type=int, default=800)
    parser.add_argument('--fps', type=int, default=15)
    parser.add_argument('--timings', type=Path, default=Path(__file__).with_name('club-showcase-timings.json'),
                        help='JSON mapping club IDs to [start_seconds, end_seconds] in the new captures')
    args = parser.parse_args()
    project = Path(__file__).resolve().parents[3]
    source = project / '.ai-work/club-media/generated'
    public = Path(__file__).resolve().parents[1] / 'public'
    catalog = public / 'clubs.json'
    groups = json.loads(catalog.read_text(encoding='utf-8'))
    clubs = [club for group in groups for club in group['clubs']]
    timings = json.loads(args.timings.read_text(encoding='utf-8')) if args.timings else {}
    # Never publish stale, failed, or unverified captures.
    for club in clubs:
        for suffix in ['.mp4', '-validation.txt', '-playback.txt']:
            file = source / (club['id'] + suffix)
            if not file.is_file():
                raise RuntimeError('Missing verified capture: ' + str(file))
            if suffix.endswith('.txt') and not file.read_text().startswith('PASS:'):
                raise RuntimeError('Failed verification: ' + str(file))
    output = public / 'media/clubs'
    output.mkdir(parents=True, exist_ok=True)
    for club in clubs:
        clip = source / (club['id'] + '.mp4')
        info = subprocess.run([args.ffmpeg, '-hide_banner', '-i', str(clip)], capture_output=True, text=True)
        match = re.search(r'Duration: (\d+):(\d+):(\d+\.\d+)', info.stderr)
        if not match:
            raise RuntimeError('Cannot read capture duration: ' + str(clip))
        hours, minutes, seconds = map(float, match.groups())
        full_duration = hours * 3600 + minutes * 60 + seconds
        start, end = timings.get(club['id'], [1.0, full_duration])
        if not 0 <= start < end <= full_duration:
            raise RuntimeError('Invalid trim range: ' + club['id'])
        duration = end - start
        if duration < 2:
            raise RuntimeError('Capture too short: ' + str(clip))
        # Fade into the media panel color; the final faded frame is held for 0.8s.
        filters = (f'fps={args.fps},scale={args.width}:-2:flags=lanczos,'
                   f'fade=t=in:st=0:d=0.5:color=0xEEF5FA,'
                   f'fade=t=out:st={duration - 0.6:.3f}:d=0.5:color=0xEEF5FA,'
                   'tpad=stop_mode=clone:stop_duration=0.8,split[a][b];'
                   '[a]palettegen=max_colors=128:reserve_transparent=0[p];'
                   '[b][p]paletteuse=dither=bayer:bayer_scale=5:diff_mode=rectangle')
        gif = output / (club['id'] + '.gif')
        subprocess.run([args.ffmpeg, '-hide_banner', '-loglevel', 'error', '-ss', str(start), '-t', str(duration), '-i', str(clip),
                        '-filter_complex', filters, '-an', '-loop', '0', '-y', str(gif)], check=True)
        club['media'] = {'type': 'image', 'src': '/media/clubs/' + gif.name}
        print(club['id'], f'{duration + 0.8:.2f}s', f'{gif.stat().st_size / 1024:.0f} KiB')
    catalog.write_text(json.dumps(groups, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


if __name__ == '__main__':
    main()
