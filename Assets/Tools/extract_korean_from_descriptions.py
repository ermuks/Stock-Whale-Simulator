# -*- coding: utf-8 -*-
"""itCompany.js의 description에서 한글만 추출 (TMP 폰트용 문자 집합)."""
import re
from pathlib import Path

def main():
    js_path = Path(__file__).resolve().parent.parent / "itCompany.js"
    text = js_path.read_text(encoding="utf-8")
    pat = re.compile(r'"description":\s*"([^"]*)"')
    descs = pat.findall(text)
    all_text = "".join(descs)

    def is_korean(c):
        o = ord(c)
        return (
            (0xAC00 <= o <= 0xD7A3)  # Hangul Syllables
            or (0x1100 <= o <= 0x11FF)  # Hangul Jamo
            or (0x3130 <= o <= 0x318F)  # Hangul Compatibility Jamo
        )

    korean_only = "".join(c for c in all_text if is_korean(c))
    unique = sorted(set(korean_only))

    out_dir = Path(__file__).resolve().parent.parent
    unique_file = out_dir / "itCompany_description_korean_unique.txt"
    full_file = out_dir / "itCompany_description_korean_full.txt"
    unique_file.write_text(
        "=== TMP 폰트용: description에 등장한 한글 유니크 문자 (총 {}개) ===\n\n".format(len(unique))
        + "".join(unique)
        + "\n",
        encoding="utf-8",
    )
    full_file.write_text(korean_only, encoding="utf-8")
    print("Wrote:", unique_file)
    print("Wrote:", full_file)
    print("Unique Korean count:", len(unique))


if __name__ == "__main__":
    main()
