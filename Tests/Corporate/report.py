"""Capture this change against its pre-task backups, not unrelated dirty-worktree changes."""
from pathlib import Path
import difflib, re

root=Path.cwd()
new_files=[root/'Assets/Scripts/NightShift'/n for n in ('CompanyBusiness.cs','CorporateEconomy.cs','CompanyDashboardUI.cs')]
new_files.append(root/'Assets/Editor/CorporateValidation.cs')
new_files.append(root/'Tests/Corporate/Program.cs')

def function(source,name):
    # Whole functions, masking strings and comments before counting braces.
    masked=re.sub(r'//[^\n]*|/\*[\s\S]*?\*/|@?"(?:""|\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'',lambda m:' '*len(m[0]),source)
    pattern=r'^    (?:(?:public|private|protected|internal|static|virtual|override|async|sealed)\s+)*(?:[\w<>,\[\]?]+\s+)?'+re.escape(name)+r'\('
    match=re.search(pattern,masked,re.M)
    if not match:
        return None
    start=match.start(); opening=masked.find('{',match.end()); arrow=masked.find('=>',match.end())
    if arrow>=0 and (opening<0 or arrow<opening):
        return source[start:masked.find(';',arrow)+1]
    level=0
    for i in range(opening,len(masked)):
        if masked[i]=='{': level+=1
        elif masked[i]=='}':
            level-=1
            if level==0: return source[start:i+1]
    return None

methods={
 'StockManager.Economy.cs':['BuildEconomyStocks'],
 'MarketDayController.cs':['Start','AdvancePhase'],
 'WorldDramaDirector.cs':['Awake','AcquireShares','BoardDecision'],
 'CorporateControlUI.cs':['Update'],
 'TabletAccountUI.cs':['Start'],
 'NewsManager.cs':['Publish','PublishAcquisition'],
 'DailyIncomeLedger.cs':['CloseDay','Copy','CanRestore'],
 'DailyIncomeController.cs':['Settle'],
 'DailyIncomeUI.cs':['BuildTablet','Update'],
 'SystemMenuUI.cs':['SystemMenuUI','Report','ReportCompanies'],
 'SessionStore.cs':['RestoreData','FillFields'],
}
out=['# 기업 결산·인수 변경 코드\n','신규 스크립트 전문과 작업 시작 시점 대비 변경 내용입니다. 기존의 다른 미커밋 작업을 비교 기준에 섞지 않았습니다.\n']
for path in new_files:
    out+=['## 신규: '+path.relative_to(root).as_posix()+'\n','```csharp\n'+path.read_text(encoding='utf-8')+'\n```\n']
for before in (root/'Validation/Corporate/before').glob('*.cs'):
    candidates=list((root/'Assets').rglob(before.name))
    if len(candidates)!=1: raise RuntimeError(before.name)
    path=candidates[0]; a=before.read_text(encoding='utf-8-sig'); b=path.read_text(encoding='utf-8-sig')
    if a==b: continue
    diff=list(difflib.unified_diff(a.splitlines(),b.splitlines(),fromfile='original',tofile='modified',lineterm=''))
    out+=['## 수정: '+path.relative_to(root).as_posix()+'\n','```diff\n'+'\n'.join(diff)+'\n```\n']
    if sum(line.startswith('@@') for line in diff)>3:
        out+=['### 함수 단위 원본 / 수정본\n']
        for name in methods.get(before.name,[]):
            old,new=function(a,name),function(b,name)
            if old==new: continue
            out+=['#### '+name+'\n','원본\n\n```csharp\n'+(old or '// 신규 함수')+'\n```\n','수정본\n\n```csharp\n'+(new or '// 해당 함수 없음')+'\n```\n']
(root/'docs/corporate-economy-code.md').write_text('\n'.join(out),encoding='utf-8')
print('Wrote code appendix; captured '+str(len(new_files))+' new C# files.')
