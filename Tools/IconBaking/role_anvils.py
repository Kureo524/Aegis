# Legendary anvils by role: Items/Anvil_Legendary with the role glyph (UI/Roles) in a medallion,
# bottom right. Run after make_icons.py. Usage: role_anvils.py
import os
from PIL import Image, ImageDraw, ImageOps
S = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'Assets', 'GraphicAssets', 'Sprites')
base = Image.open(f'{S}/Items/Anvil_Legendary.png').convert('RGBA')
n = base.width
for role in ('Marksman', 'Mage', 'Fighter', 'Assassin', 'Tank'):
    out = base.copy()
    r = int(n * 0.24); cx = cy = n - r - int(n * 0.04)
    d = ImageDraw.Draw(out)
    d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=(250, 240, 214, 255), outline=(150, 105, 35, 255), width=max(2, n // 40))
    glyph = Image.open(f'{S}/UI/Roles/Role_{role}.png').convert('RGBA').resize((int(r * 1.45),) * 2, Image.LANCZOS)
    alpha = glyph.getchannel('A')
    tint = Image.new('RGBA', glyph.size, (150, 82, 20, 255)); tint.putalpha(alpha)
    out.alpha_composite(tint, (cx - glyph.width // 2, cy - glyph.height // 2))
    out.save(f'{S}/Items/Anvil_Legendary_{role}.png')
