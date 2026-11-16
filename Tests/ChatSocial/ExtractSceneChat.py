"""Read-only extraction of complete, naturally mediated conversations from a scene observation."""
import argparse
import json
import sys
from collections import Counter
from pathlib import Path


def read_jsonl(path):
    result = []
    for line in path.read_text(encoding="utf-8-sig").splitlines():
        if line.strip():
            result.append(json.loads(line))
    return result


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser()
    parser.add_argument("directory", type=Path)
    args = parser.parse_args()
    directory = args.directory.resolve()
    status = json.loads((directory / "status.json").read_text(encoding="utf-8-sig"))
    messages = read_jsonl(directory / "social-messages.jsonl")
    events = read_jsonl(directory / "events.jsonl")
    changes = read_jsonl(directory / "emotion-changes.jsonl")
    rows = read_jsonl(directory / "chat.jsonl")
    bots = read_jsonl(directory / "initial-bots.jsonl")
    names = {bot["id"]: bot["name"] for bot in bots}
    by_id = {message["id"]: message for message in messages}
    chat_by_social = {row["socialId"]: row for row in rows}
    ended = {event["id"]: event for event in events if event["kind"] == "THREAD_END"}
    arguments = {event["id"]: event for event in events if event["kind"] == "ARGUMENT"}
    mediations = [event for event in events if event["kind"] == "MEDIATION"]
    selected = []
    for event in mediations:
        thread = event["threadId"]
        if thread in arguments and thread in ended and thread not in selected:
            selected.append(thread)
        if len(selected) == 2:
            break

    output = ["Actual TradeScene observation — generated text unchanged", json.dumps(status, ensure_ascii=False), ""]
    excerpts = []
    for thread in selected:
        ids = {message["id"] for message in messages if message["threadId"] == thread}
        for message_id in list(ids):
            parent = by_id[message_id]["replyToId"]
            while parent and parent in by_id and parent not in ids:
                ids.add(parent)
                parent = by_id[parent]["replyToId"]
        conversation = sorted((by_id[message_id] for message_id in ids), key=lambda m: (m["realAt"], m["id"]))
        evidence = [event for event in mediations if event["threadId"] == thread]
        mediation_ids = {event["id"] for event in evidence}
        emotion_evidence = [change for change in changes if change["causeId"] in mediation_ids and change["causeType"] == "mediation"]
        output.append(f"THREAD {thread} | room {arguments[thread]['room']} | end {ended[thread]['reason']}")
        for message in conversation:
            if message["id"] not in chat_by_social:
                raise RuntimeError(f"Social message {message['id']} was not found in actual chat delivery.")
            row = chat_by_social[message["id"]]
            if row["text"] != message["text"]:
                raise RuntimeError(f"Delivered text differs for message {message['id']}.")
            output.append(f"DAY {row['day']} {row['minute'] // 60:02}:{row['minute'] % 60:02} {row['nickname']}: {message['text']}")
        output.append("Mediation: " + ", ".join(event["reason"] for event in evidence))
        for change in emotion_evidence:
            output.append(f"  {names.get(change['target'], change['target'])} → {names.get(change['other'], change['other'])} {change['field']} {change['before']} → {change['after']}")
        output.append("")
        excerpts.append({"thread": thread, "argument": arguments[thread], "messages": conversation,
                         "mediations": evidence, "changes": emotion_evidence, "end": ended[thread]})

    summary = {
        "status": status,
        "chatCountsByRoom": dict(Counter(row["room"] for row in rows)),
        "eventCounts": dict(Counter(event["kind"] for event in events)),
        "directedMessageCount": sum(message["targetId"] > 0 for message in messages),
        "replyMessageCount": sum(message["replyToId"] > 0 for message in messages),
        "directedMessagesAnswered": sum(any(reply["replyToId"] == message["id"] and reply["senderId"] == message["targetId"] for reply in messages) for message in messages if message["targetId"] > 0),
        "selectedThreadIds": selected,
        "selection": "first two completed argument threads with a recorded MEDIATION event; ancestor messages included; unrelated interleaved messages omitted",
        "selectedMessagesVerifiedAgainstDeliveredChat": True if selected else None,
        "excerpts": excerpts,
    }
    (directory / "conversation-excerpts.txt").write_text("\n".join(output) + "\n", encoding="utf-8")
    (directory / "extraction.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    timeline = []
    for row in rows:
        label = "공용" if row["room"] == 0 else "새벽 관찰실" if row["room"] == 4 else f"리딩방 {row['room']}"
        line = f"DAY {row['day']} {row['minute'] // 60:02}:{row['minute'] % 60:02} [{label}] "
        line += f"── {row['text']} ──" if row["notice"] != "None" else f"{row['nickname']}: {row['text']}"
        timeline.append((row["day"], row["minute"], row["seconds"], row["room"], row["sequence"], line))
    timeline.sort(key=lambda item: item[:5])
    (directory / "chat-full.txt").write_text("\n".join(item[-1] for item in timeline) + "\n", encoding="utf-8")
    print("\n".join(output))


if __name__ == "__main__":
    main()
