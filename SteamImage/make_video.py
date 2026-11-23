# -*- coding: utf-8 -*-
"""Gameplay preview video for the Steam store page, built from in-game frame runs.

    python SteamImage/make_video.py

Frames: Validation/ChatProbe/shots-videorun/v-<shot>-NNNN.jpg  (PlayerChatProbe request: `videorun2`, 1920x1080, 15 per game second = 2x speed)
Sound:  the game's own ambience loops (Assets/Resources/Audio/Ambience)
Output: SteamImage/StorePage/video/StockWhaleSimulator_Preview_1080p.mp4   H.264 + AAC, 30 fps
"""
import glob, os, subprocess, sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__))).replace('\\', '/') + '/'
FRAMES = ROOT + 'Validation/ChatProbe/shots-videorun/'
AUDIO = ROOT + 'Assets/Resources/Audio/Ambience/'
LOGO = ROOT + 'SteamImage/Review/Round2/Final/Library/library_logo_1280w.png'
OUT = ROOT + 'SteamImage/StorePage/video/'
os.makedirs(OUT, exist_ok=True)
FPS = 30
# Background music (must be at least as long as the video). When the file is missing, only the game's ambience is used.
MUSIC = ROOT + 'SteamImage/StorePage/video/music/Market_Pressure.wav'

# The frames are shot at 15 per game second (PlayerChatProbe request: `videorun2`), so playing them at 30 fps is exactly 2x speed.
# The video is cut to the length of the music. Fixed shots are used whole; the chart shot (surge -> crash) is used whole unless it is
# longer than CHART_MAX frames, in which case only its climb is thinned out; the chat and room shots share whatever length is left.
TARGET_FRAMES = 44 * FPS
CHART_MAX = 870
LOGO_SHOT = 'f-end'      # the title fades in over this shot
SURGE_SHOT = 'b-chart'   # the surge ambience plays under this shot


def shot(name):
    return sorted(glob.glob(FRAMES + 'v-' + name + '-*.jpg'))


def main():
    meta = {}
    if os.path.exists(FRAMES + 'video-meta.txt'):
        for part in open(FRAMES + 'video-meta.txt').read().split():
            key, value = part.split('='); meta[key] = int(value)
    desk, chart, chat, room, calendar, end = (shot(n) for n in ('a-desk', 'b-chart', 'c-chat', 'd-room', 'e-calendar', 'f-end'))
    if not (desk and chart and end):
        print('frames are missing'); sys.exit(1)
    crash = meta.get('crash_at', -1)
    if len(chart) > CHART_MAX and 0 < crash < len(chart):
        climb, fall = chart[:crash], chart[crash:]
        keep = max(60, CHART_MAX - len(fall))
        chart = [climb[int(k * (len(climb) - 1) / (keep - 1))] for k in range(keep)] + fall
        print('chart climb thinned from %d to %d frames (%.1fx on top of 2x)' % (len(climb), keep, len(climb) / float(keep)))
    left = max(0, TARGET_FRAMES - len(desk) - len(chart) - len(calendar) - len(end))
    chat = chat[:min(len(chat), int(round(left * .55)))]
    room = room[:min(len(room), left - len(chat))]
    lines, start, at = [], {}, 0
    for name, files in (('a-desk', desk), ('b-chart', chart), ('c-chat', chat), ('d-room', room), ('e-calendar', calendar), ('f-end', end)):
        start[name] = at / float(FPS)
        for f in files:
            lines.append("file '%s'\nduration %.6f\n" % (f.replace(chr(92), '/'), 1.0 / FPS))
        at += len(files)
        print('%-12s %4d frames  %5.1f s' % (name, len(files), len(files) / float(FPS)))
    if crash >= 0:
        print('crash starts at %.1f s of the video' % (start['b-chart'] + (len(chart) - (meta.get('chart_frames', len(chart)) - crash)) / float(FPS)))
    total = at / float(FPS)
    listing = OUT + 'frames.txt'
    open(listing, 'w', encoding='utf-8').write(''.join(lines))
    logo_at, surge_at = start[LOGO_SHOT] + 1.0, start[SURGE_SHOT] + 1.0
    video = ('[1:v]scale=1000:-1,format=rgba,fade=t=in:st=%.2f:d=1.2:alpha=1[logo];'
             '[6:v]format=rgba,fade=t=in:st=%.2f:d=1.2:alpha=1,colorchannelmixer=aa=0.55[dim];'
             '[0:v]fps=%d,format=yuv420p[raw];[raw][dim]overlay=enable=gte(t\,%.2f),fade=t=in:st=0:d=0.6,fade=t=out:st=%.2f:d=0.8[base];'
             '[base][logo]overlay=(W-w)/2:(H-h)/2-40:enable=\'gte(t,%.2f)\',format=yuv420p[v]') % (logo_at, logo_at, FPS, logo_at, total - 0.8, logo_at)
    audio = ('[2:a]volume=0.9[a0];[3:a]volume=0.6[a1];[4:a]volume=0.5[a2];'
             '[5:a]adelay=%d|%d,volume=0.8[a3];'
             '[a0][a1][a2][a3]amix=inputs=4:normalize=0:duration=longest,atrim=0:%.3f,'
             'volume=8dB,alimiter=limit=0.9,afade=t=in:st=0:d=1,afade=t=out:st=%.2f:d=1.5[amb]') % (surge_at * 1000, surge_at * 1000, total, total - 1.5)
    music = os.path.exists(MUSIC)
    # With music, the ambience sits well underneath it (-14 dB) and the music keeps its own ending.
    audio += (';[amb]volume=-14dB[low];[7:a]atrim=0:%.3f[mus];[mus][low]amix=inputs=2:normalize=0:duration=first,alimiter=limit=0.95,afade=t=out:st=%.2f:d=0.4[a]' % (total, total - 0.4)) if music else ';[amb]anull[a]'
    out = OUT + ('StockWhaleSimulator_Preview_1080p_BGM.mp4' if music else 'StockWhaleSimulator_Preview_1080p.mp4')
    command = ['ffmpeg', '-y', '-v', 'error', '-f', 'concat', '-safe', '0', '-i', listing,
               '-loop', '1', '-i', LOGO,
               '-stream_loop', '-1', '-i', AUDIO + 'RoomTone.wav', '-stream_loop', '-1', '-i', AUDIO + 'Monitors.wav',
               '-stream_loop', '-1', '-i', AUDIO + 'Trading.wav', '-i', AUDIO + 'Surge.wav', '-f', 'lavfi', '-i', 'color=black:s=1920x1080:r=%d' % FPS,
               ] + (['-i', MUSIC] if music else []) + [
               '-filter_complex', video + ';' + audio, '-map', '[v]', '-map', '[a]', '-t', '%.3f' % total,
               '-c:v', 'libx264', '-preset', 'slow', '-crf', '15', '-profile:v', 'high', '-r', str(FPS), '-pix_fmt', 'yuv420p',
               '-c:a', 'aac', '-b:a', '256k', '-ar', '48000', '-movflags', '+faststart', out]
    subprocess.check_call(command)
    os.remove(listing)
    print('%s  %.1f s  %.1f MB' % (out, total, os.path.getsize(out) / 1048576.0))


if __name__ == '__main__':
    main()
