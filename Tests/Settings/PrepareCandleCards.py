from pathlib import Path
import re, uuid
from PIL import Image

folder=Path('Assets/Resources/UI/SystemMenu')
template=Path('Assets/Resources/UI/TabletKit/Icons.png.meta').read_text(encoding='utf-8-sig')
for name in ['CandleThemeRedBlue','CandleThemeGreenRed']:
    image=Image.open(folder/(name+'.png')).convert('RGBA')
    assert image.getpixel((0,0))[3]==0 and image.getpixel((image.width-1,image.height-1))[3]==0
    assert image.getchannel('A').getbbox() is not None
    meta=folder/(name+'.png.meta')
    if not meta.exists():
        text=re.sub(r'(?m)^guid: .+$','guid: '+uuid.uuid4().hex,template)
        text=re.sub(r'spriteID: \w+','spriteID: '+uuid.uuid4().hex,text)
        text=text.replace('maxTextureSize: 1024','maxTextureSize: 512')
        meta.write_text(text,encoding='utf-8')
    print(name,image.size,'transparent alpha verified; single sprite, max 512, preserve original PNG')
