from pathlib import Path
import json,hashlib,sys
root=Path(__file__).resolve().parents[3]
out=root/'Validation/ChatSocial/Market';out.mkdir(parents=True,exist_ok=True)
host=(root/'Tests/ChatSocial/Host.cs').read_text(encoding='utf-8-sig')
unity=host[:host.index('public sealed class Stock')]
unity=unity.replace('public class MonoBehaviour { }','public partial class MonoBehaviour { }').replace('public class Coroutine { }','public partial class Coroutine { }').replace('public static class Time','public static partial class Time').replace('public static class Mathf','public static partial class Mathf')
unity=unity.replace('public struct ProfilerMarker { public ProfilerMarker(string key) { } }','public struct ProfilerMarker { public ProfilerMarker(string key) { } public System.IDisposable Auto()=>new Scope(); private sealed class Scope:System.IDisposable { public void Dispose() { } } }')
society=host[host.index('public sealed partial class InvestorSociety'):host.index('public sealed partial class MarketDayController')]
(out/'HostCommon.cs').write_text(unity+society,encoding='utf-8')
sys.path.insert(0,str(root/'Tests/ChatSocial'))
from Prepare import extract_member
source=(root/'Assets/Scripts/NightShift/ChatSocialUnity.cs').read_text(encoding='utf-8-sig')
(out/'FillAdapter.cs').write_text('using System;using UnityEngine;\npublic sealed partial class InvestorSociety { public MarketDayController day;public LeadingRoomOrders Orders;\n'+extract_member(source,'ObserveSocialFill')+'\n}\n',encoding='utf-8')
(out/'provenance.json').write_text(json.dumps(dict(source='Assets/Scripts/NightShift/ChatSocialUnity.cs',sha256=hashlib.sha256(source.encode()).hexdigest(),verbatimMethods=['ObserveSocialFill']),indent=2),encoding='utf-8')
