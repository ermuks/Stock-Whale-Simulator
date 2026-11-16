from pathlib import Path
import difflib
import hashlib
import json
import re

root=Path.cwd()
before=root/'Validation/TabletKit/before'
changed=[]
for old in sorted(before.rglob('*')):
    if not old.is_file() or old.suffix!='.cs':
        continue
    path=old.relative_to(before)
    current=root/path
    if current.is_file() and old.read_text(encoding='utf-8-sig')!=current.read_text(encoding='utf-8-sig'):
        changed.append(path)

new=[Path('Assets/Scripts/NightShift/TabletKitStyle.cs'),Path('Assets/Scripts/NightShift/TabletKitLayout.cs'),Path('Assets/Editor/TabletKitValidation.cs')]
out=['# Tablet kit 신규 C# 전문과 작업 전/후 비교\n',
     '함수별 변경 의도는 `tablet-kit-changes.md`의 3절을 참고한다. 아래 diff는 이번 작업 직전 스냅샷 기준이다.\n']
for path in new:
    out.extend(['\n## 신규: '+path.as_posix()+'\n','```csharp\n',path.read_text(encoding='utf-8-sig'),'\n```\n'])
for path in changed:
    a=(before/path).read_text(encoding='utf-8-sig').splitlines(keepends=True)
    b=path.read_text(encoding='utf-8-sig').splitlines(keepends=True)
    diff=''.join(difflib.unified_diff(a,b,fromfile='작업 전/'+path.as_posix(),tofile='수정본/'+path.as_posix(),n=8))
    out.extend(['\n## 수정: '+path.as_posix()+'\n','```diff\n',diff,'\n```\n'])
out.append('\n## NewsManager.cs\n\n이번 작업 전 별도 스냅샷은 없으며 이번 변경은 기사 생성 두 경로에서 `DeviceMarketJournal.Stamp()`를 `DeviceMarketJournal.FullStamp()`로 교체한 두 부분이다. 이전 작업 전체와 Git HEAD를 비교하여 이번 변경이라고 제시하지 않는다.\n')
(root/'docs/tablet-kit-code.md').write_text(''.join(out),encoding='utf-8')

progress=root/'docs/tablet-kit-progress.md'
initial=progress.read_text(encoding='utf-8').split('\n## Final implementation')[0]
lines=['\n## Final implementation\n',
       '- 구조 조사 → 공통 테마/atlas → 자산·주문 → 기업 경영 → 작업 계좌 → 시간·다국어·검증 순으로 완료.\n',
       '- 새 런타임 2개, 검증 에디터 1개. 기존 컴포넌트 재사용. 정확한 비교/전문: tablet-kit-code.md.\n',
       '- 기능·제한·데이터 및 시간 정책: tablet-kit-changes.md.\n',
       '\n### 이번 작업에서 스냅샷과 달라진 기존 C#\n']
lines += ['- `'+p.as_posix()+'`\n' for p in changed]
lines += ['- `Assets/Scripts/News/NewsManager.cs` (FullStamp 두 곳)\n',
          '- `Assets/Localization/Import/DonDon.csv`, `Assets/Resources/Localization/SourceIndex.json` 및 기존 Localization importer가 갱신하는 문자열 테이블/SDF 자료.\n',
          '- `Tests/TerminalOrders/Host.cs`: 예약/게임 시각 스텁 보완. 별도 호스트 전체의 기존 도메인 불일치로 이 테스트는 아직 실패.\n',
          '- 준비·검증 도구: `Tests/TabletKit/prepare.py`, `build_validation.py`, `rewrite_spread.py`, `report.py`. rewrite_spread.py는 최초 편집용 도구이며 후속 수동 수정본을 덮어쓰므로 재실행하지 않는다.\n',
          '\n### 최종 Unity 실행 결과\n']
total=0
for job in ['TabletKitAssets02','TabletKitOrders04','TabletKitCompany03','TabletKitAuto03','TabletKitSave01','TabletKitCold01','TabletKitSpecial02']:
    folder=root/'Validation/Settings'/job
    result=(folder/'result.txt').read_text(encoding='utf-8-sig').strip()
    resources=json.loads((folder/'resources.json').read_text(encoding='utf-8-sig'))
    count=int(re.search(r'Checks:\s*(\d+)',result).group(1))
    assert 'PASS' in result and resources['exitCode']==0
    total+=count
    lines.append(f'- {job}: PASS {count} checks; {resources["seconds"]}초; peak {resources["peakMB"]} MiB. `Validation/Settings/{job}/`.\n')
lines.append(f'\n최종 7개 실행의 체크 합계 {total}개. 각 실행마다 반복하는 48개 아이콘 검증과 공통 검증이 포함되어 있으므로 서로 독립적인 {total}개 시나리오라는 뜻은 아니다.\n')
lines.append('\n### 회귀·실패 기록\n- CompanyGrowth 21 / Corporate 16 / DailyIncome 271 = 308 PASS. 로그는 `Validation/TabletKit/*-core.log`.\n- TerminalOrders 독립 호스트: 컴파일 실패. 중복 StockRiskGrade / InvestorSociety.day, 누락 NewsArticle / CompanyBusiness / CompanyOpinionBook. Unity 주문 검증을 별도 통과했으며 이 호스트는 성공 처리하지 않음.\n- 최종 전체 C# 컴파일 로그: `Validation/TabletKit/compile-final.log`.\n- 초기 Orders01/02, Company01, Auto02 실패 및 수정은 tablet-kit-changes.md에 기록.\n')
scene=root/'Assets/Scenes 1/TradeScene.unity'
sha=hashlib.sha256(scene.read_bytes()).hexdigest().upper()
assert sha=='622351BBCC1A603CE6C96A757148EB11E82C0262BDEAA4CE5ABF2A0D88217C40'
lines.append('\n### 씬 무변경 확인\n`'+sha+'` (시작/완료 일치).\n')
progress.write_text(initial+''.join(lines),encoding='utf-8')
print('Documented',len(changed),'snapshot-relative modified C# files;',len(new),'new C# sources;',total,'Unity checks (including shared repeats).')
