from pathlib import Path
import difflib

root=Path.cwd(); before=root/'Validation/MarketColors/before'
out=['# Market color theme — 신규 코드 및 원본 비교\n\n함수별 변경 의도와 테스트 결과는 market-color-theme.md에 있다.\n']
for name in ['Assets/Scripts/Settings/MarketColors.cs','Assets/Editor/MarketColorsValidation.cs']:
    out.extend(['\n## 신규 '+name+'\n```csharp\n',Path(name).read_text(encoding='utf-8-sig'),'\n```\n'])
count=0
for old in sorted(before.rglob('*.cs')):
    path=old.relative_to(before); a=old.read_text(encoding='utf-8-sig'); b=path.read_text(encoding='utf-8-sig')
    if a==b: continue
    count+=1
    diff=''.join(difflib.unified_diff(a.splitlines(keepends=True),b.splitlines(keepends=True),fromfile='원본/'+path.as_posix(),tofile='수정/'+path.as_posix(),n=8))
    out.extend(['\n## '+path.as_posix()+'\n```diff\n',diff,'\n```\n'])
(root/'docs/market-color-theme-code.md').write_text(''.join(out),encoding='utf-8')
print('Documented',count,'existing files and 2 complete new scripts.')
