"""Task-scoped implementation evidence, based on before-task source snapshots."""
from pathlib import Path
import difflib,re
root=Path.cwd()
new=['Assets/Scripts/NightShift/CompanyGrowth.cs','Assets/Scripts/NightShift/CorporateEconomy.Growth.cs','Assets/Scripts/NightShift/CompanyGrowthUI.cs','Assets/Editor/CompanyGrowthValidation.cs','Assets/Editor/CompanyGrowthContinuationValidation.cs']
parts=['# 기업 성장 패치: 신규 C# 전문과 함수 비교\n','이번 작업 시작 시 보관한 `Validation/CompanyGrowth/before/`와 비교합니다. 이전 작업의 미커밋 변경을 이번 작업의 변경으로 계산하지 않습니다.\n']
for file in new:
    parts.append(f'## 신규 파일: {file}\n\n```csharp\n{(root/file).read_text(encoding="utf-8-sig")}\n```\n')
methods={
'StockManager.Economy.cs':['RegisterPrivateCompany','ReconnectListedCompanies','BuildEconomyStocks','PreviewPlayer','ConfirmPlayer','InitializeEconomy','ListOwnerCompany'],
'CorporateEconomy.cs':['Awake','Ensure','Update','SetPolicy','ReconnectSession','PlayerDividends','Settle'],
'CompanyDashboardUI.cs':['Build','Refresh','Select','ShowCompany','OpenGrowth'],
'MarketDayController.cs':['Start','PrepareSessionRestore','AdvancePhase','OnListed'],
'CompanyBusiness.cs':['BeginClose','FinishClose'],
'Trader.cs':['ContributePrivateCapital','SubscribeShares'],
'SessionStore.cs':['RestoreData','Restore'],
}
def method(source,name):
    for match in re.finditer(r'^    (?:public |private |protected |internal |static |sealed |override |virtual |async )*(?:[\w<>,?\[\]]+)\s+'+re.escape(name)+r'\(',source,re.M):
        start=match.start(); brace=source.find('{',match.end()); arrow=source.find('=>',match.end()); semi=source.find(';',match.end())
        if arrow>=0 and arrow<brace and semi>arrow: return source[start:semi+1].strip()
        level=0
        for i in range(brace,len(source)):
            if source[i]=='{': level+=1
            elif source[i]=='}':
                level-=1
                if level==0: return source[start:i+1].strip()
    return '(없음 — 신규 함수)'
changed=[]
for before in sorted((root/'Validation/CompanyGrowth/before').glob('*.cs')):
    candidates=list((root/'Assets/Scripts').rglob(before.name))
    if len(candidates)!=1: continue
    file=candidates[0]; old=before.read_text(encoding='utf-8-sig'); now=file.read_text(encoding='utf-8-sig')
    if old==now: continue
    changed.append(str(file.relative_to(root)).replace('\\','/'))
    parts.append(f'## 수정 파일: {file.relative_to(root)}\n')
    if before.name in methods:
        for name in methods[before.name]:
            a,b=method(old,name),method(now,name)
            if a==b: continue
            parts.append(f'### {name}\n\n원본:\n```csharp\n{a}\n```\n\n수정본:\n```csharp\n{b}\n```\n')
    else:
        delta=''.join(difflib.unified_diff(old.splitlines(True),now.splitlines(True),fromfile='before',tofile='after',n=3))
        parts.append('```diff\n'+delta+'\n```\n')
(root/'docs/company-growth-code.md').write_text('\n'.join(parts),encoding='utf-8')
(root/'Validation/CompanyGrowth/changed-files.txt').write_text('\n'.join(changed+new)+'\n',encoding='utf-8')
