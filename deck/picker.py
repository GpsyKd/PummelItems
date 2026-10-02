#!/usr/bin/env python3
"""
The launch picker for Pummel Party on a Steam Deck: "Играть с модом / Играть без мода / Отмена",
shown as the game starts, in the style of Steam's own launch-option menu.

Steam's real menu cannot be used: it only lists the launch options the game's developer
registered with Steam. So launch.sh runs this first and starts the game according to what it
prints: "modded", "vanilla" or "cancel". Exit status 2 means it could not show itself at all,
and launch.sh falls back to the last choice.

It needs nothing but Python and libX11, which every SteamOS has - no zenity (absent), no
toolkit. All lettering comes pre-rendered from picker-art.txt; see picker-art.cs.

In Game Mode the screen belongs to gamescope, which shows a window only if it carries the
STEAM_GAME property; given the game's id, the picker is shown as part of the game launching.
It answers to the touchscreen, the controller (D-pad or stick, A, B) and the keyboard.

    picker.py --last modded --wait 12 --appid 880940 [--fullscreen]
    picker.py --preview out.png [--last vanilla] [--size 1280x800]    draw it into a PNG instead
"""
import argparse, base64, ctypes, ctypes.util, glob, math, os, select, struct, sys, time, zlib

ART = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'picker-art.txt')

BG = (16, 22, 30)
TITLE = (255, 255, 255)
ROW, ROW_TEXT = (43, 58, 75), (220, 227, 234)
ROW_ON, ROW_ON_TEXT = (223, 227, 232), (27, 38, 51)
HINT = (139, 146, 154)
FOOTER = (220, 222, 223)

ITEM_W, ITEM_H, ITEM_STEP = 520, 62, 66
CHOICES = ('modded', 'vanilla', 'cancel')


def log(msg):
    sys.stderr.write('picker: %s\n' % msg)
    sys.stderr.flush()


# ---------------------------------------------------------------------------- drawing

class Art:
    def __init__(self, path):
        self.masks = {}
        with open(path, encoding='utf-8') as f:
            for line in f:
                if line.startswith('#') or not line.strip():
                    continue
                name, w, h, data = line.split()
                w, h = int(w), int(h)
                mask = zlib.decompress(base64.b64decode(data), -15)
                if len(mask) != w * h:
                    raise ValueError('art %s is %d bytes, expected %d' % (name, len(mask), w * h))
                if name.startswith(('hint_', 'digit')):
                    w, mask = trim(w, h, mask)
                self.masks[name] = (w, h, mask)
        self._tinted = {}

    def size(self, name):
        w, h, _ = self.masks[name]
        return w, h

    def tinted(self, name, fg, bg):
        """The mask as BGRA pixels: fg where it is fully covered, bg where it is empty."""
        key = (name, fg, bg)
        if key not in self._tinted:
            w, h, mask = self.masks[name]
            out = bytearray(w * h * 4)
            for ch, (f, b) in enumerate(zip(reversed(fg), reversed(bg))):   # BGR order
                table = bytes(int(round(b + (f - b) * a / 255.0)) for a in range(256))
                out[ch::4] = mask.translate(table)
            out[3::4] = b'\xff' * (w * h)
            self._tinted[key] = (w, h, bytes(out))
        return self._tinted[key]


def trim(w, h, mask):
    """Drops the empty columns either side, so pieces of one line can be spaced exactly."""
    cols = [x for x in range(w) if any(mask[y * w + x] for y in range(h))]
    if not cols:
        return w, mask
    a, b = cols[0], cols[-1] + 1
    return b - a, b''.join(mask[y * w + a:y * w + b] for y in range(h))


class Frame:
    def __init__(self, w, h):
        self.w, self.h = w, h
        self.px = bytearray(bytes((BG[2], BG[1], BG[0], 255)) * (w * h))

    def fill(self, x, y, w, h, rgb):
        x0, y0, x1, y1 = max(x, 0), max(y, 0), min(x + w, self.w), min(y + h, self.h)
        if x1 <= x0 or y1 <= y0:
            return
        run = bytes((rgb[2], rgb[1], rgb[0], 255)) * (x1 - x0)
        for row in range(y0, y1):
            o = (row * self.w + x0) * 4
            self.px[o:o + len(run)] = run

    def blit(self, x, y, img):
        w, h, data = img
        if x < 0 or y < 0 or x + w > self.w or y + h > self.h:
            return                      # off the edge of a tiny window: leave it out
        for row in range(h):
            o = ((y + row) * self.w + x) * 4
            self.px[o:o + w * 4] = data[row * w * 4:(row + 1) * w * 4]


class Layout:
    """Where everything goes in a window of the given size: one column, centred."""
    def __init__(self, art, w, h):
        tw, th = art.size('title')
        total = th + 16 + ITEM_STEP * 3 + 12 + 24
        top = max((h - total) // 2 - 20, 8)
        self.title = ((w - tw) // 2, top)
        self.items = [((w - ITEM_W) // 2, top + th + 16 + i * ITEM_STEP) for i in range(3)]
        self.hint_y = top + th + 16 + ITEM_STEP * 3 + 12
        fw, fh = art.size('footer')
        self.footer = (w - fw - 36, h - fh - 28)

    def hit(self, px, py):
        for i, (x, y) in enumerate(self.items):
            if x <= px < x + ITEM_W and y <= py < y + ITEM_H:
                return i
        return None


def compose(art, w, h, selected, seconds, last):
    """The whole picture, as BGRA pixels. seconds=None hides the countdown."""
    f, lay = Frame(w, h), Layout(art, w, h)
    f.blit(lay.title[0], lay.title[1], art.tinted('title', TITLE, BG))
    for i, (x, y) in enumerate(lay.items):
        on = (i == selected)
        bg, fg = (ROW_ON, ROW_ON_TEXT) if on else (ROW, ROW_TEXT)
        f.fill(x, y, ITEM_W, ITEM_H, bg)
        f.blit(x, y, art.tinted('item%d' % i, fg, bg))
    if seconds is not None:
        # "Без выбора через 12 с — ...": a word gap either side of the number, digits close.
        digits = [art.tinted('digit' + c, HINT, BG) for c in str(max(seconds, 0))]
        pieces = [(art.tinted('hint_pre', HINT, BG), 5)]
        pieces += [(img, 1) for img in digits[:-1]] + [(digits[-1], 5)]
        pieces += [(art.tinted('hint_post_' + last, HINT, BG), 0)]
        x = (w - sum(img[0] + gap for img, gap in pieces)) // 2
        for img, gap in pieces:
            f.blit(x, lay.hint_y, img)
            x += img[0] + gap
    f.blit(lay.footer[0], lay.footer[1], art.tinted('footer', FOOTER, BG))
    return f


def write_png(path, frame):
    raw = bytearray()
    stride = frame.w * 4
    for y in range(frame.h):
        row = frame.px[y * stride:(y + 1) * stride]
        rgba = bytearray(row)
        rgba[0::4], rgba[2::4] = row[2::4], row[0::4]
        raw += b'\x00' + rgba

    def chunk(kind, data):
        return (struct.pack('>I', len(data)) + kind + data +
                struct.pack('>I', zlib.crc32(kind + data) & 0xffffffff))

    with open(path, 'wb') as f:
        f.write(b'\x89PNG\r\n\x1a\n' +
                chunk(b'IHDR', struct.pack('>IIBBBBB', frame.w, frame.h, 8, 6, 0, 0, 0)) +
                chunk(b'IDAT', zlib.compress(bytes(raw), 9)) + chunk(b'IEND', b''))


# ---------------------------------------------------------------------------- controllers

EV_KEY, EV_ABS = 1, 3
BTN_SOUTH, BTN_EAST, BTN_DPAD_UP, BTN_DPAD_DOWN = 304, 305, 544, 545
ABS_Y, ABS_HAT0Y = 1, 17


class Pad:
    """A game controller read straight from evdev. Steam's virtual pad for the game is one."""
    def __init__(self, fd, name):
        self.fd, self.name = fd, name
        self.stick = 0                  # -1 / 0 / 1: which way the stick was last pushed
        self.lo, self.hi = None, None
        try:
            import fcntl
            info = bytearray(24)
            fcntl.ioctl(fd, 0x80184541, info)          # EVIOCGABS(ABS_Y)
            _, mn, mx = struct.unpack_from('iii', info)
            if mx > mn:
                mid, half = (mn + mx) / 2.0, (mx - mn) / 2.0
                self.lo, self.hi = mid - half * 0.6, mid + half * 0.6
        except Exception:
            pass

    def events(self):
        """Yields 'up', 'down', 'ok' and 'back'."""
        try:
            data = os.read(self.fd, 24 * 64)
        except (BlockingIOError, InterruptedError):
            return
        except OSError:
            return
        for i in range(0, len(data) - 23, 24):
            _, _, kind, code, value = struct.unpack_from('qqHHi', data, i)
            if kind == EV_KEY and value == 1:
                if code == BTN_SOUTH: yield 'ok'
                elif code == BTN_EAST: yield 'back'
                elif code == BTN_DPAD_UP: yield 'up'
                elif code == BTN_DPAD_DOWN: yield 'down'
            elif kind == EV_ABS and code == ABS_HAT0Y:
                if value < 0: yield 'up'
                elif value > 0: yield 'down'
            elif kind == EV_ABS and code == ABS_Y and self.lo is not None:
                way = -1 if value < self.lo else (1 if value > self.hi else 0)
                if way != self.stick:
                    self.stick = way
                    if way: yield 'up' if way < 0 else 'down'


def open_pads():
    pads = []
    for sysdir in sorted(glob.glob('/sys/class/input/event*')):
        try:
            with open(sysdir + '/device/capabilities/key') as f:
                bits = 0
                for word in f.read().split():
                    bits = (bits << 64) | int(word, 16)
        except (OSError, ValueError):
            continue
        if not (bits >> BTN_SOUTH) & 1:
            continue
        try:
            with open(sysdir + '/device/name') as f:
                name = f.read().strip()
        except OSError:
            name = '?'
        try:
            fd = os.open('/dev/input/' + os.path.basename(sysdir), os.O_RDONLY | os.O_NONBLOCK)
        except OSError as e:
            log('controller "%s" not readable: %s' % (name, e.strerror))
            continue
        pads.append(Pad(fd, name))
    log('controllers: %s' % (', '.join(p.name for p in pads) or 'none'))
    return pads


# ---------------------------------------------------------------------------- X11

def xlib():
    x = ctypes.CDLL(ctypes.util.find_library('X11') or 'libX11.so.6')
    V, UL, L, I, UI = ctypes.c_void_p, ctypes.c_ulong, ctypes.c_long, ctypes.c_int, ctypes.c_uint
    sig = {
        'XOpenDisplay': (V, [ctypes.c_char_p]),
        'XDefaultScreen': (I, [V]),
        'XRootWindow': (UL, [V, I]),
        'XDefaultDepth': (I, [V, I]),
        'XDefaultVisual': (V, [V, I]),
        'XDisplayWidth': (I, [V, I]),
        'XDisplayHeight': (I, [V, I]),
        'XCreateSimpleWindow': (UL, [V, UL, I, I, UI, UI, UI, UL, UL]),
        'XInternAtom': (UL, [V, ctypes.c_char_p, I]),
        'XChangeProperty': (I, [V, UL, UL, UL, I, I, V, I]),
        'XStoreName': (I, [V, UL, ctypes.c_char_p]),
        'XSetWMProtocols': (I, [V, UL, V, I]),
        'XSelectInput': (I, [V, UL, L]),
        'XMapRaised': (I, [V, UL]),
        'XCreateGC': (V, [V, UL, UL, V]),
        'XCreateImage': (V, [V, V, UI, I, I, V, UI, UI, I, I]),
        'XPutImage': (I, [V, UL, V, V, I, I, I, I, UI, UI]),
        'XPending': (I, [V]),
        'XNextEvent': (I, [V, V]),
        'XConnectionNumber': (I, [V]),
        'XLookupKeysym': (UL, [V, I]),
        'XFlush': (I, [V]),
        'XDestroyWindow': (I, [V, UL]),
        'XCloseDisplay': (I, [V]),
        'XSetErrorHandler': (V, [V]),
    }
    for name, (res, args) in sig.items():
        fn = getattr(x, name)
        fn.restype, fn.argtypes = res, args
    return x


KeyPress, ButtonPress, ButtonRelease, Expose, ConfigureNotify, ClientMessage = 2, 4, 5, 12, 22, 33
MASKS = (1 << 0) | (1 << 2) | (1 << 3) | (1 << 15) | (1 << 17)   # key, button down/up, expose, structure
KEYS = {0xff52: 'up', 0xff97: 'up', 0xff54: 'down', 0xff99: 'down', 0xff09: 'down',
        0xff0d: 'ok', 0xff8d: 'ok', 0x20: 'ok', 0xff1b: 'back'}

_keep = []   # ctypes objects the X server-side structures point into


def run(args, art):
    x = xlib()
    errors = []
    handler = ctypes.CFUNCTYPE(ctypes.c_int, ctypes.c_void_p, ctypes.c_void_p)(
        lambda d, e: errors.append(1) or 0)
    _keep.append(handler)
    x.XSetErrorHandler(ctypes.cast(handler, ctypes.c_void_p))

    d = x.XOpenDisplay(None)
    if not d:
        log('cannot open the X display %r' % os.environ.get('DISPLAY'))
        return None
    scr = x.XDefaultScreen(d)
    root, depth, visual = x.XRootWindow(d, scr), x.XDefaultDepth(d, scr), x.XDefaultVisual(d, scr)
    sw, sh = x.XDisplayWidth(d, scr), x.XDisplayHeight(d, scr)
    if depth not in (24, 32):
        log('unsupported colour depth %d' % depth)
        return None
    if args.fullscreen:
        w, h, wx, wy = sw, sh, 0, 0
    else:
        w, h = 680, 440
        wx, wy = (sw - w) // 2, (sh - h) // 2
    log('display %s %dx%d depth %d, window %dx%d' % (os.environ.get('DISPLAY'), sw, sh, depth, w, h))

    bg_pixel = (BG[0] << 16) | (BG[1] << 8) | BG[2]
    win = x.XCreateSimpleWindow(d, root, wx, wy, w, h, 0, 0, bg_pixel)

    # Without this gamescope never puts the window on screen; with the game's id it is shown
    # as part of the game being launched.
    steam_game = (ctypes.c_long * 1)(args.appid)
    _keep.append(steam_game)
    x.XChangeProperty(d, win, x.XInternAtom(d, b'STEAM_GAME', 0), 6, 32, 0,
                      ctypes.cast(steam_game, ctypes.c_void_p), 1)
    x.XStoreName(d, win, b'Pummel Party')
    wm_delete = x.XInternAtom(d, b'WM_DELETE_WINDOW', 0)
    protocols = (ctypes.c_ulong * 1)(wm_delete)
    _keep.append(protocols)
    x.XSetWMProtocols(d, win, ctypes.cast(protocols, ctypes.c_void_p), 1)
    x.XSelectInput(d, win, MASKS)
    x.XMapRaised(d, win)
    gc = x.XCreateGC(d, win, 0, None)

    state = {'w': w, 'h': h, 'buf': None, 'img': None}

    def surface(w, h):
        buf = ctypes.create_string_buffer(w * h * 4)
        img = x.XCreateImage(d, visual, depth, 2, 0, ctypes.cast(buf, ctypes.c_void_p), w, h, 32, w * 4)
        _keep.append(buf)
        state.update(w=w, h=h, buf=buf, img=img)

    surface(w, h)
    pads = open_pads()
    xfd = x.XConnectionNumber(d)
    ev = (ctypes.c_long * 24)()

    selected = 0 if args.last == 'modded' else 1
    started = time.monotonic()
    deadline = started + args.wait
    counting = True
    pressed = None
    shown = [None]
    recent = {}

    def draw():
        secs = int(math.ceil(deadline - time.monotonic())) if counting else None
        frame = compose(art, state['w'], state['h'], selected, secs, args.last)
        ctypes.memmove(state['buf'], bytes(frame.px), len(frame.px))
        if state['img']:
            x.XPutImage(d, win, gc, state['img'], 0, 0, 0, 0, state['w'], state['h'])
        x.XFlush(d)
        shown[0] = secs

    def finish(choice):
        x.XDestroyWindow(d, win)
        x.XFlush(d)
        x.XCloseDisplay(d)
        for p in pads:
            os.close(p.fd)
        log('chose %s%s' % (choice, ' (X errors: %d)' % len(errors) if errors else ''))
        return choice

    draw()
    while True:
        now = time.monotonic()
        if counting:
            if now >= deadline:
                return finish(args.last)
            if int(math.ceil(deadline - now)) != shown[0]:
                draw()

        try:
            select.select([xfd] + [p.fd for p in pads], [], [], 0.1)
        except InterruptedError:
            pass

        actions = []
        while x.XPending(d):
            x.XNextEvent(d, ctypes.byref(ev))
            raw = ctypes.string_at(ctypes.addressof(ev), 96)
            kind = struct.unpack_from('i', raw, 0)[0]
            if kind == Expose:
                if struct.unpack_from('i', raw, 56)[0] == 0:
                    actions.append(('redraw', None))
            elif kind == ConfigureNotify:
                cw, ch = struct.unpack_from('ii', raw, 56)
                if (cw, ch) != (state['w'], state['h']) and cw > 0 and ch > 0:
                    surface(cw, ch)
                    actions.append(('redraw', None))
            elif kind == KeyPress:
                key = KEYS.get(x.XLookupKeysym(ctypes.byref(ev), 0))
                if key:
                    actions.append((key, None))
            elif kind in (ButtonPress, ButtonRelease):
                bx, by = struct.unpack_from('ii', raw, 64)
                button = struct.unpack_from('I', raw, 84)[0]
                if button == 1:
                    actions.append(('press' if kind == ButtonPress else 'release',
                                    Layout(art, state['w'], state['h']).hit(bx, by)))
            elif kind == ClientMessage:
                if struct.unpack_from('l', raw, 56)[0] == wm_delete:
                    actions.append(('back', None))

        # A press still held from starting the game in Steam must not count. And one controller
        # can show up as two devices, Steam's virtual pad and the hardware, so the same press
        # arriving twice within a moment counts once.
        now = time.monotonic()
        for p in pads:
            for a in p.events():
                if now - started > 0.4 and now - recent.get(a, 0.0) > 0.12:
                    actions.append((a, None))
                recent[a] = now

        for what, where in actions:
            if what == 'redraw':
                draw()
                continue
            counting = False
            if what == 'up':
                selected = (selected - 1) % 3
            elif what == 'down':
                selected = (selected + 1) % 3
            elif what == 'ok':
                return finish(CHOICES[selected])
            elif what == 'back':
                return finish('cancel')
            elif what == 'press':
                pressed = where
                if where is not None:
                    selected = where
            elif what == 'release':
                if where is not None and where == pressed:
                    return finish(CHOICES[where])
                pressed = None
            draw()


def main(argv):
    ap = argparse.ArgumentParser(description=__doc__.split('\n\n')[0])
    ap.add_argument('--last', choices=('modded', 'vanilla'), default='modded')
    ap.add_argument('--wait', type=int, default=12)
    ap.add_argument('--appid', type=int, default=880940)
    ap.add_argument('--fullscreen', action='store_true')
    ap.add_argument('--preview')
    ap.add_argument('--size', default='1280x800')
    ap.add_argument('--selected', type=int, default=None)
    args = ap.parse_args(argv)

    try:
        art = Art(ART)
    except Exception as e:
        log('no lettering: %s' % e)
        return 2

    if args.preview:
        w, h = (int(v) for v in args.size.split('x'))
        sel = args.selected if args.selected is not None else (0 if args.last == 'modded' else 1)
        write_png(args.preview, compose(art, w, h, sel, args.wait, args.last))
        return 0

    try:
        choice = run(args, art)
    except Exception as e:
        log('failed: %s: %s' % (type(e).__name__, e))
        return 2
    if choice is None:
        return 2
    print(choice)
    return 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
