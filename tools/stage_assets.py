"""Stage LibreQuake assets into the Unity project (Assets/LQ/...).
Usage: python3 stage_assets.py <librequake_repo> <unity_project> [--music]
"""
import os, sys, shutil, glob, json, re
from PIL import Image

def tex_asset_name(png_name):
    """LibreQuake texture-wads PNG file name -> asset name (mirrors qpakman: plus_ -> +, star_ -> *, strip _fbr).
    We keep the file-system safe form (plus_/star_) and strip only the _fbr suffix; C# maps '+'->'plus_' and '*'->'star_'."""
    n = png_name[:-4].lower()
    if n.endswith('_fbr'):
        n = n[:-4]
    return n

def stage_textures(repo, proj):
    dst = os.path.join(proj, 'Assets/LQ/Textures'); os.makedirs(dst, exist_ok=True)
    seen = {}
    for wad_dir in sorted(glob.glob(os.path.join(repo, 'texture-wads/*/'))):
        for png in sorted(glob.glob(os.path.join(wad_dir, '*.png'))):
            name = tex_asset_name(os.path.basename(png))
            if name in seen:
                continue
            seen[name] = png
            im = Image.open(png).convert('RGBA')
            im.save(os.path.join(dst, name + '.png'))
    json.dump({k: os.path.relpath(v, repo) for k, v in seen.items()}, open(os.path.join(proj, 'MapSources/texture_index.json'), 'w'), indent=0)
    print('textures', len(seen))

def stage_models(repo, proj):
    dst = os.path.join(proj, 'Assets/LQ/Resources/progs'); os.makedirs(dst, exist_ok=True)
    n = 0
    for mdl in glob.glob(os.path.join(repo, 'lq1/progs/*.mdl')):
        shutil.copy(mdl, os.path.join(dst, os.path.basename(mdl) + '.bytes')); n += 1
    for spr in glob.glob(os.path.join(repo, 'lq1/progs/*.spr')):
        shutil.copy(spr, os.path.join(dst, os.path.basename(spr) + '.bytes')); n += 1
    gfx = os.path.join(proj, 'Assets/LQ/Resources/gfx'); os.makedirs(gfx, exist_ok=True)
    shutil.copy(os.path.join(repo, 'lq1/gfx/palette.lmp'), os.path.join(gfx, 'palette.lmp.bytes'))
    print('models', n)

def stage_sounds(repo, proj):
    dst = os.path.join(proj, 'Assets/LQ/Resources/sound')
    if os.path.exists(dst): shutil.rmtree(dst)
    n = 0
    for wav in glob.glob(os.path.join(repo, 'lq1/sound/**/*.wav'), recursive=True):
        rel = os.path.relpath(wav, os.path.join(repo, 'lq1/sound'))
        out = os.path.join(dst, rel); os.makedirs(os.path.dirname(out), exist_ok=True)
        shutil.copy(wav, out); n += 1
    print('sounds', n)

def stage_gfx(repo, proj):
    dst = os.path.join(proj, 'Assets/LQ/Resources/hud'); os.makedirs(dst, exist_ok=True)
    n = 0
    for png in glob.glob(os.path.join(repo, 'lq1/gfx-wad/*.png')):
        name = os.path.basename(png).lower()
        if '.bak' in name: continue
        Image.open(png).convert('RGBA').save(os.path.join(dst, name)); n += 1
    for name in ['conback.png', 'loading.png', 'complete.png', 'inter.png', 'finale.png']:
        p = os.path.join(repo, 'lq1/gfx', name)
        if os.path.exists(p):
            Image.open(p).convert('RGBA').save(os.path.join(dst, name)); n += 1
    print('hud gfx', n)

def stage_maps(repo, proj):
    dst = os.path.join(proj, 'MapSources'); os.makedirs(dst, exist_ok=True)
    n = 0
    for ep in ['e1', 'e2', 'e3', 'e4', 'e0', 'dm', 'misc', 'brushmodels']:
        for m in glob.glob(os.path.join(repo, 'lq1/maps/src', ep, '*.map')):
            shutil.copy(m, os.path.join(dst, os.path.basename(m))); n += 1
    print('maps', n)

def stage_anims(repo, proj):
    sys.path.insert(0, os.path.dirname(__file__))
    pairs = {'grunt': 'soldier', 'rottweiler': 'dog', 'ogre': 'ogre', 'knight': 'knight', 'zombie': 'zombie', 'scrag': 'wizard',
             'fiend': 'demon', 'shambler': 'shambler', 'hellknight': 'hknight', 'enforcer': 'enforcer', 'rotfish': 'fish',
             'vore': 'shalrath', 'spawn': 'tarbaby', 'chthon': 'boss', 'shub': 'oldone'}
    out = {}
    for qc, mdl in pairs.items():
        names = []
        for line in open(os.path.join(repo, 'qcsrc/monsters', qc + '.qc'), encoding='latin1'):
            if line.startswith('$frame'):
                names += line.split('//')[0].split()[1:]
        out[mdl] = names
    names = []
    for line in open(os.path.join(repo, 'qcsrc/player.qc'), encoding='latin1'):
        if line.startswith('$frame'):
            names += line.split('//')[0].split()[1:]
    out['player'] = names
    dst = os.path.join(proj, 'Assets/LQ/Resources'); os.makedirs(dst, exist_ok=True)
    # Unity's JsonUtility can't do dictionaries -> emit list of {model, frames}
    json.dump({'models': [{'model': k, 'frames': v} for k, v in out.items()]}, open(os.path.join(dst, 'anims.json'), 'w'))
    print('anims', len(out))

def stage_music(repo, proj):
    dst = os.path.join(proj, 'Assets/LQ/Resources/music'); os.makedirs(dst, exist_ok=True)
    for ogg in glob.glob(os.path.join(repo, 'lq1/music/*.ogg')):
        shutil.copy(ogg, dst)

def main():
    repo, proj = sys.argv[1], sys.argv[2]
    os.makedirs(os.path.join(proj, 'MapSources'), exist_ok=True)
    stage_textures(repo, proj); stage_models(repo, proj); stage_sounds(repo, proj)
    stage_gfx(repo, proj); stage_maps(repo, proj); stage_anims(repo, proj)
    if '--music' in sys.argv: stage_music(repo, proj)
    shutil.copy(os.path.join(repo, 'docs/COPYING'), os.path.join(proj, 'LICENSE-LibreQuake-assets.txt'))

if __name__ == '__main__':
    main()
