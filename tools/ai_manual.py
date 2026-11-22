# -*- coding: utf-8 -*-
"""Claude が操作した画面から、操作性改善アプリと同じ見た目の「手順書.html」を作る（AI向けの道具）

使い方:
  python3 ai_manual.py 手順.json 出力フォルダ [app.cs のパス]
  → 出力フォルダに「yyyy-MM-dd 題名.html」を1つだけ作る（画像は中に入れるので別ファイルは作らない）

手順.json の形:
  {"title": "手順書の題名",
   "steps": [{"image": "画面の画像ファイル", "act": "何をしたか（見出しの赤字の所）",
              "where": "どの画面か（ウィンドウ名など。省略可）",
              "marks": [[x, y], ...],      # 押した所（画像の座標）。2つ以上なら番号付き
              "desc": "説明（省略可）"}]}

・見た目（テンプレート）は app.cs の ManualTemplate をそのまま使う（アプリが作る手順書と同じ）
・赤丸の描き方もアプリと同じ（半径22・太さ4。2つ以上なら右上に白い番号）
"""
import base64, datetime, html, io, json, os, re, sys
from PIL import Image, ImageDraw, ImageFont

def template(app_cs):
    t = open(app_cs, encoding='utf-8-sig').read()
    m = re.search(r'class ManualTemplate\s*\{\s*public const string Html = @"(.*?)";\s*\}', t, re.S)
    if not m: raise SystemExit('app.cs に ManualTemplate が見つかりません')
    return m.group(1).replace('""', '"').replace('\r\n', '\n')

def font(size):
    for f in ['/usr/share/fonts/opentype/noto/NotoSansCJK-Bold.ttc', '/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf']:
        if os.path.exists(f): return ImageFont.truetype(f, size)
    return ImageFont.load_default()

def draw_marks(img, marks):
    d = ImageDraw.Draw(img)
    multi = len(marks) >= 2
    f = font(15)
    for i, (x, y) in enumerate(marks):
        r = 22
        d.ellipse([x - r, y - r, x + r, y + r], outline=(255, 0, 0), width=4)
        if multi:
            bx, by, br = x + 20, y - 20, 13
            d.ellipse([bx - br, by - br, bx + br, by + br], fill=(255, 0, 0))
            d.text((bx, by), str(i + 1), fill=(255, 255, 255), font=f, anchor='mm')

def main():
    spec = json.load(open(sys.argv[1], encoding='utf-8'))
    out = sys.argv[2]
    here = os.path.dirname(os.path.abspath(__file__))
    app_cs = sys.argv[3] if len(sys.argv) > 3 else os.path.join(here, '..', 'app', 'app.cs')
    os.makedirs(out, exist_ok=True)
    parts = []
    for i, s in enumerate(spec['steps'], 1):
        img = Image.open(s['image']).convert('RGB')
        draw_marks(img, s.get('marks') or [])
        buf = io.BytesIO()
        img.save(buf, 'PNG', optimize=True)
        b64 = base64.b64encode(buf.getvalue()).decode()
        where = ('<span class="where">（%s）</span>' % html.escape(s['where'])) if s.get('where') else ''
        parts.append('<section class="step"><div class="head"><span class="no"></span>'
                     '<span class="auto"><span class="act">%s</span>%s</span>'
                     '<button class="del" title="この手順を消す">×</button></div>'
                     '<div class="desc" contenteditable="true">%s</div>'
                     '<img src="data:image/png;base64,%s"></section>\n'
                     % (html.escape(s.get('act', '')), where, html.escape(s.get('desc', '')), b64))
    now = datetime.datetime.now()
    t = template(app_cs)
    t = t.replace('{{DATE}}', '%d年%d月%d日' % (now.year, now.month, now.day)).replace('{{STEPS}}', ''.join(parts))
    title = spec.get('title', '')
    if title:
        t = t.replace('<h1 contenteditable="true"></h1>', '<h1 contenteditable="true">%s</h1>' % html.escape(title), 1)
        t = t.replace('<title>手順書</title>', '<title>%s</title>' % html.escape(title), 1)
    name = re.sub(r'[\\/:*?"<>|]', '', title or '操作の手順').strip()
    bas = os.path.join(out, '%04d-%02d-%02d %s' % (now.year, now.month, now.day, name))
    p, n = bas + '.html', 2
    while os.path.exists(p):
        p = bas + '（%d）.html' % n; n += 1
    open(p, 'w', encoding='utf-8-sig', newline='\r\n').write(t)
    print(p)

if __name__ == '__main__':
    main()
