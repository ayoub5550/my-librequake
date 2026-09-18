"""Quake MDL (IDPO v6) parser — used to validate the C# loader and to export skin PNGs."""
import struct, sys, os

def read_mdl(data):
    off = 0
    def rd(fmt):
        nonlocal off
        v = struct.unpack_from('<' + fmt, data, off); off += struct.calcsize('<' + fmt); return v
    ident, version = rd('4si')
    assert ident == b'IDPO' and version == 6, (ident, version)
    scale = rd('3f'); translate = rd('3f'); radius, = rd('f'); eye = rd('3f')
    nskins, skinw, skinh, nverts, ntris, nframes, synctype, flags, size = rd('9i')[:9] if False else (rd('i')[0], rd('i')[0], rd('i')[0], rd('i')[0], rd('i')[0], rd('i')[0], rd('i')[0], rd('i')[0], rd('f')[0])
    skins = []
    for _ in range(nskins):
        group, = rd('i')
        if group == 0:
            skins.append([data[off:off + skinw * skinh]]); off += skinw * skinh
        else:
            nb, = rd('i'); off += 4 * nb
            grp = []
            for _ in range(nb):
                grp.append(data[off:off + skinw * skinh]); off += skinw * skinh
            skins.append(grp)
    tex = [rd('3i') for _ in range(nverts)]        # onseam, s, t
    tris = [rd('4i') for _ in range(ntris)]        # facesfront, v0, v1, v2
    frames = []
    for _ in range(nframes):
        ftype, = rd('i')
        if ftype == 0:
            frames.append([read_simple(data, off, nverts)]); off = frames[-1][0]['end']
        else:
            nb, = rd('i'); off += 8  # bboxmin/max
            off += 4 * nb            # intervals
            grp = []
            for _ in range(nb):
                grp.append(read_simple(data, off, nverts)); off = grp[-1]['end']
            frames.append(grp)
    if off != len(data): print("  WARN trailing bytes", len(data)-off)
    return dict(scale=scale, translate=translate, eye=eye, skinw=skinw, skinh=skinh, skins=skins,
                tex=tex, tris=tris, frames=frames, flags=flags)

def read_simple(data, off, nverts):
    bmin = data[off:off + 4]; bmax = data[off + 4:off + 8]
    name = data[off + 8:off + 24].split(b'\0')[0].decode('latin1')
    off += 24
    verts = [tuple(data[off + i * 4: off + i * 4 + 4]) for i in range(nverts)]
    return dict(name=name, verts=verts, end=off + nverts * 4)

if __name__ == '__main__':
    for p in sys.argv[1:]:
        m = read_mdl(open(p, 'rb').read())
        fr = [f[0]['name'] for f in m['frames']]
        print(os.path.basename(p), 'skins', len(m['skins']), m['skinw'], m['skinh'], 'verts', len(m['tex']), 'tris', len(m['tris']), 'frames', len(m['frames']), fr[:6], '...', fr[-2:])
