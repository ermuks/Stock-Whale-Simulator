# -*- coding: utf-8 -*-
"""노드 1000에서 상장폐지(1997,1998,1999)까지 소요 시간 시뮬레이션. 시간 단위: 초(sec)."""
import json
import random
from pathlib import Path

def sec_to_str(sec):
    """Convert seconds to readable string (e.g. 51m 2s, 1h 23m)."""
    s = int(round(sec))
    if s < 60:
        return f"{s}s"
    m, s = divmod(s, 60)
    if m < 60:
        return f"{m}m {s}s"
    h, m = divmod(m, 60)
    return f"{h}h {m}m {s}s"

def load_data():
    js_path = Path(__file__).resolve().parent.parent / "itCompany.js"
    text = js_path.read_text(encoding="utf-8")
    start = text.find("const itCompanyData = ")
    if start < 0:
        raise SystemExit("itCompanyData 선언을 찾을 수 없음")
    start += len("const itCompanyData = ")
    # 첫 { 부터 괄호 짝이 맞는 끝 } 까지
    depth = 0
    for i, c in enumerate(text[start:], start):
        if c == "{":
            depth += 1
        elif c == "}":
            depth -= 1
            if depth == 0:
                json_str = text[start : i + 1]
                break
    else:
        raise SystemExit("JSON 객체 끝을 찾을 수 없음")
    return json.loads(json_str)

def run_one_path(nodes_by_id, start_id=1000, terminal_ids=(1997, 1998, 1999)):
    total_time = 0.0
    current_id = start_id
    steps = 0
    max_steps = 5000  # 무한 루프 방지
    while current_id not in terminal_ids and steps < max_steps:
        node = nodes_by_id.get(current_id)
        if not node:
            return None, steps  # 손실
        stay_min = node.get("stayTimeRangeMin", 0) or 0
        stay_max = node.get("stayTimeRangeMax", 0) or 0
        total_time += random.uniform(stay_min, stay_max) if stay_max > stay_min else stay_min
        next_ids = node.get("next", [])
        next_weights = node.get("nextWeight", [])
        if not next_ids:
            break
        if len(next_weights) != len(next_ids):
            next_weights = [1] * len(next_ids)
        current_id = random.choices(next_ids, weights=next_weights, k=1)[0]
        steps += 1
    return total_time if current_id in terminal_ids else None, steps

def main():
    random.seed(42)
    data = load_data()
    nodes = {n["id"]: n for n in data["nodes"]}
    n_runs = 10_000
    times = []
    for _ in range(n_runs):
        t, _ = run_one_path(nodes)
        if t is not None:
            times.append(t)
    if not times:
        print("상장폐지 도달 경로 없음 (시뮬레이션 실패)")
        return
    times.sort()
    n = len(times)
    avg = sum(times) / n
    print("=== node 1000 -> delisting(1997/1998/1999) time [unit: sec] ===")
    print(f"  runs: {n_runs}, reached: {n}\n")
    print(f"  mean:    {avg:.1f} sec  = {sec_to_str(avg)}")
    print(f"  median:  {times[n//2]:.1f} sec  = {sec_to_str(times[n//2])}")
    print(f"  min:     {times[0]:.1f} sec  = {sec_to_str(times[0])}")
    print(f"  max:     {times[-1]:.1f} sec  = {sec_to_str(times[-1])}")
    print(f"  10%ile:  {times[int(n*0.10)]:.1f} sec")
    print(f"  25%ile:  {times[int(n*0.25)]:.1f} sec")
    print(f"  75%ile:  {times[int(n*0.75)]:.1f} sec")
    print(f"  90%ile:  {times[int(n*0.90)]:.1f} sec")

if __name__ == "__main__":
    main()
