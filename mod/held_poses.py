# -*- coding: utf-8 -*-
"""
Works out where each item sits in the player's hand.

PoseStudio (src/PoseStudio.cs) logs both hand bones in the carrying pose - the pose every item
of ours switches on while it is held. This script takes those frames plus a plain statement of
intent per item - which hand, which point of the model goes into it, which way the model faces,
how big it should be in the world - and turns it into the bone-local position, rotation and
scale the game wants.

    python held_poses.py                  print the poses
    python held_poses.py <held_poses.txt> also write them where PoseStudio picks them up

The numbers that ship are copied from the output into HeldPoses.cs.

Body frame: x right, y up, z forward, in world units, from the character's feet.
"""
import math, io, sys

# ---------------------------------------------------------------- vector helpers
def add(a, b): return [a[i] + b[i] for i in range(3)]
def sub(a, b): return [a[i] - b[i] for i in range(3)]
def mul(a, k): return [a[i] * k for i in range(3)]
def dot(a, b): return sum(a[i] * b[i] for i in range(3))
def cross(a, b): return [a[1]*b[2] - a[2]*b[1], a[2]*b[0] - a[0]*b[2], a[0]*b[1] - a[1]*b[0]]
def norm(a):
    l = math.sqrt(dot(a, a)); return [a[i] / l for i in range(3)]
def mm(A, B): return [[sum(A[i][k] * B[k][j] for k in range(3)) for j in range(3)] for i in range(3)]
def mv(A, v): return [sum(A[i][k] * v[k] for k in range(3)) for i in range(3)]
def T(A): return [[A[j][i] for j in range(3)] for i in range(3)]
def cols(a, b, c): return [[a[i], b[i], c[i]] for i in range(3)]

def q2m(x, y, z, w):
    return [[1 - 2*(y*y + z*z), 2*(x*y - z*w), 2*(x*z + y*w)],
            [2*(x*y + z*w), 1 - 2*(x*x + z*z), 2*(y*z - x*w)],
            [2*(x*z - y*w), 2*(y*z + x*w), 1 - 2*(x*x + y*y)]]

def rot(axis, deg):
    """Rotation matrix about an axis, the way Quaternion.AngleAxis turns it."""
    x, y, z = norm(axis); a = math.radians(deg)
    s, c = math.sin(a / 2), math.cos(a / 2)
    return q2m(x * s, y * s, z * s, c)

def to_euler(R):
    """Unity's Quaternion.Euler(x, y, z) is Ry * Rx * Rz; invert that."""
    sx = max(-1.0, min(1.0, -R[1][2]))
    x = math.asin(sx)
    if abs(math.cos(x)) > 1e-6:
        y = math.atan2(R[0][2], R[2][2])
        z = math.atan2(R[1][0], R[1][1])
    else:
        y = math.atan2(-R[2][0], R[0][0]); z = 0.0
    return [math.degrees(x), math.degrees(y), math.degrees(z)]

def euler(x, y, z):
    return mm(rot([0, 1, 0], y), mm(rot([1, 0, 0], x), rot([0, 0, 1], z)))

def frame(ma, ba, mb, bb):
    """The rotation taking model direction ma onto body direction ba, and mb as near bb as it can."""
    def ortho(a, b):
        a = norm(a); b = norm(sub(b, mul(a, dot(a, b)))); return a, b, cross(a, b)
    m = ortho(ma, mb); w = ortho(ba, bb)
    return mm(cols(*w), T(cols(*m)))

# ---------------------------------------------------------------- the hands, as PoseStudio logged them
SCALE = 0.42750
LP = [-0.0754, 0.3880, 0.1265]; LR = q2m(0.61026, 0.61429, 0.47405, 0.15973)
RP = [0.1540, 0.3904, 0.0241];  RR = q2m(0.14960, -0.48454, -0.67809, 0.53200)

def on_hand(P, R, local): return add(P, mul(mv(R, local), SCALE))

LX, LY, LZ = [list(c) for c in zip(*LR)]      # thumb side, fingers, palm normal
RX, RY, RZ = [list(c) for c in zip(*RR)]
L_PALM = on_hand(LP, LR, [0.01, 0.055, 0.02])
R_PALM = on_hand(RP, RR, [-0.005, 0.045, 0.02])
MID = mul(add(L_PALM, R_PALM), 0.5)
GAP = math.sqrt(dot(sub(R_PALM, L_PALM), sub(R_PALM, L_PALM)))
U = norm(sub(R_PALM, L_PALM)); U[1] = 0.0; U = norm(U)        # along the hands, level
UP = [0.0, 1.0, 0.0]; FWD = [0.0, 0.0, 1.0]; RIGHT = [1.0, 0.0, 0.0]
F2 = norm(cross(U, UP))                                          # forward, square to the hands
if F2[2] < 0: F2 = mul(F2, -1)

def solve(key, hold, grip, ma, ba, mb, bb, size, dim, push=0.0, shift=(0, 0, 0), extra=None, raise_=None):
    """
    hold  'L' one hand (left), 'B' between both hands (hung off the left one)
    grip  model point that goes to the hand (L) or to the middle between the hands (B)
    size  world size of the model dimension 'dim'
    push  how far out of the palm the grip point sits, along the palm's normal (L)
    shift extra body-space offset of the target
    extra extra body-space rotation applied after the frame
    """
    R = frame(ma, ba, mb, bb)
    if extra is not None: R = mm(extra, R)
    s = size / (dim * SCALE)
    if hold == 'L':
        # Compact things sit up out of the hand, the way the game's cactus does: inside the
        # palm they hide behind the hand from most angles.
        if raise_ is None: raise_ = 0.0
        target = add(add(L_PALM, mul(LZ, push)), mul(LX, raise_))
    else:
        target = list(MID)
    target = add(target, list(shift))
    local_rot = mm(T(LR), R)
    p = sub(mul(mv(T(LR), sub(target, LP)), 1.0 / SCALE), mv(local_rot, mul(grip, s)))
    e = to_euler(local_rot)
    return '%-18s LeftHand %8.4f %8.4f %8.4f  %8.2f %8.2f %8.2f  %.3f' % (key, p[0], p[1], p[2], e[0], e[1], e[2], s)

lines = []
def item(*a, **k): lines.append(solve(*a, **k))

TILT_UP = norm(add(FWD, mul(UP, 0.55)))     # a face turned to the viewer and a little up
ROD = norm(add(LX, mul(FWD, 0.15)))         # through the fist: the thumb-up hand's grip line

# ---- between both hands, like the game's present
item('PCI_ShuffleItem', 'B', [0, 0, 0],               [0, 1, 0], UP, [0, 0, 1], F2, 0.22, 0.904)
item('PCI_I_SwapInv',   'B', [0.0024, 0.0687, -0.009], [0, 1, 0], UP, [1, 0, 0], U, GAP + 0.03, 0.689)
item('PCI_I_Copier',    'B', [-0.05, 0.183, 0.0085],  [0, 1, 0], UP, [1, 0, 0], U, GAP, 0.52, shift=(0, -0.03, 0.02))
item('PCI_I_Junk',      'B', [0, 0.066, 0],           [0, 1, 0], UP, [1, 0, 0], U, GAP, 0.508, shift=(0, -0.01, 0.02),
     extra=rot(U, -18))
item('PCI_I_Glass',     'B', [0, -0.011, -0.025],     [0, 1, 0], UP, [0, 0, 1], F2, 0.34, 0.9095, shift=(0, 0, 0.13))
item('PCI_I_Pinata',    'B', [0.032, 0.074, 0],       [0, 1, 0], UP, [1, 0, 0], U, GAP + 0.03, 0.633, shift=(0, 0.02, 0.02))
# the lid stands on the model's -x side: put it at the back, against the chest
item('PCI_I_Generosity','B', [-0.03, 0.06, 0],        [0, 1, 0], UP, [1, 0, 0], F2, 0.18, 0.36, shift=(0, -0.01, 0.03),
     extra=rot(U, -12))
# the fire trail runs along the model's +z: up and away from the face, not through the head
item('PCI_I_Armageddon','B', [0, 0, 0],               [0, 0, 1], norm(add(mul(UP, 0.65), mul(FWD, 0.75))), [1, 0, 0], U, 0.30, 0.9306,
     shift=(0, 0, 0.03))
item('PCI_I_Piggy',     'B', [-0.0198, 0.0237, 0],    [0, 1, 0], UP, [1, 0, 0], U, GAP + 0.02, 0.4835, shift=(0, 0, 0.02))

# ---- in the left hand: compact things perched on it, long things through the fist
item('PCI_LoadedDiceItem', 'L', [0, 0, 0],            [0, 1, 0], TILT_UP, [1, 0, 0], norm(add(RIGHT, mul(FWD, -0.5))), 0.12, 0.508,
     push=0.045, raise_=0.05)
item('PCI_I_DoubleMove',   'L', [0.074, 0.116, -0.05], [0, 1, 0], UP, [0, 0, 1], FWD, 0.15, 0.479, push=0.05, raise_=0.06)
item('PCI_I_Grenade', 'L', [0.009, 0.032, 0],    [0, 1, 0], UP, [1, 0, 0], FWD, 0.14, 0.494, push=0.045, raise_=0.055)
item('PCI_I_Sticky',  'L', [-0.013, 0.078, 0.004], [0, 1, 0], UP, [0, 0, 1], FWD, 0.14, 0.403, push=0.05, raise_=0.05)
item('PCI_I_Curse',   'L', [0, 0.013, 0.05],     [0, 1, 0], UP, [0, 0, 1], FWD, 0.17, 0.5534, push=0.04, raise_=0.06)
item('PCI_I_Freeze',  'L', [0.017, 0.062, -0.004], [0, 1, 0], UP, [0, 0, 1], FWD, 0.15, 0.4749, push=0.05, raise_=0.055)
item('PCI_I_Poison',  'L', [0, -0.03, 0],        [0, 1, 0], UP, [0, 0, 1], FWD, 0.16, 0.45, push=0.04, raise_=0.04)
item('PCI_I_Mine',    'L', [0, 0.049, 0],        [0, 1, 0], TILT_UP, [0, 0, 1], norm(add(FWD, mul(UP, -0.5))), 0.15, 0.3191,
     push=0.06, raise_=0.04)
item('PCI_I_Kick',    'L', [-0.05, 0.12, 0],     [0, 1, 0], UP, [1, 0, 0], FWD, 0.16, 0.50, push=0.015)
item('PCI_I_Tax',     'L', [0.12, 0, -0.17],     [0, 1, 0], TILT_UP, [0, 0, 1], norm(add(UP, mul(FWD, -0.55))), 0.17, 0.397, push=0.008,
     raise_=0.02)
item('PCI_I_Ticket',  'L', [0.15, 0, -0.08],     [0, 1, 0], TILT_UP, [0, 0, 1], norm(add(UP, mul(FWD, -0.55))), 0.15, 0.38, push=0.008,
     raise_=0.02)
item('PCI_I_Banana',  'L', [0.375, -0.063, 0],   [-0.887, 0.462, 0], ROD, [0.462, 0.887, 0], mul(RIGHT, -1), 0.20, 1.0565, push=0.02)
item('PCI_I_Icicle',  'L', [0, 0, -0.16],        [0, 0, 1], norm(add(ROD, mul(FWD, 0.5))), [0, 1, 0], UP, 0.22, 0.68, push=0.025)
item('PCI_I_Boomerang','L', [0.29, 0, 0.24],     norm([-0.29, 0, -0.20]), ROD, [0, 1, 0], TILT_UP, 0.22, 0.6689, push=0.012)
item('PCI_I_Ricochet','L', [-0.12, -0.02, 0],    [1, 0, 0], FWD, [0, 1, 0], UP, 0.30, 0.958, push=0.02)
item('PCI_I_DeathWand','L', [0, -0.12, 0],       [0, 1, 0], ROD, [0, 0, 1], FWD, 0.32, 0.655, push=0.015)
item('PCI_I_Vacuum',  'L', [-0.176, 0.15, 0],    [0, 1, 0], UP, [1, 0, 0], FWD, 0.30, 0.8556, push=0.01, shift=(0, 0.01, 0))
item('PCI_I_Grapple', 'L', [0, -0.05, 0],        [0, 1, 0], norm(add(ROD, mul(FWD, 0.4))), [0, 0, 1], FWD, 0.22, 0.7036, push=0.015)
item('PCI_I_Signpost','L', [0, -0.10, 0],        [0, 1, 0], UP, [0, 0, 1], FWD, 0.25, 0.5671, push=0.012)
item('PCI_I_LifeMagnet','L', [0, 0.34, 0],       [0, -1, 0], FWD, [0, 0, 1], UP, 0.22, 0.8699, push=0.02)

if __name__ == '__main__':
    text = '# key  bone  px py pz  rx ry rz  scale   (from held_poses.py)\n' + '\n'.join(lines) + '\n'
    sys.stdout.write(text)
    if len(sys.argv) > 1:
        io.open(sys.argv[1], 'w', encoding='utf-8', newline='\n').write(text)
