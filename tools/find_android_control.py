#!/usr/bin/env python3
import re
import sys
import xml.etree.ElementTree as ET

try:
    tree = ET.parse(sys.argv[1])
except ET.ParseError:
    raise SystemExit(1)

label = sys.argv[2]
matches = []
for node in tree.iter("node"):
    if label not in (node.get("text"), node.get("content-desc")):
        continue
    if node.get("visible-to-user", "true") == "false" or node.get("enabled", "true") == "false":
        continue
    bounds = list(map(int, re.findall(r"\d+", node.get("bounds", ""))))
    if len(bounds) != 4 or bounds[2] <= bounds[0] or bounds[3] <= bounds[1]:
        continue
    cls = node.get("class", "")
    priority = (
        label == "Renderizador" and cls.endswith(".Spinner"),
        node.get("clickable") == "true",
        node.get("content-desc") == label,
    )
    matches.append((priority, (bounds[0] + bounds[2]) // 2, (bounds[1] + bounds[3]) // 2))

if not matches:
    raise SystemExit(1)
matches.sort(key=lambda item: item[0], reverse=True)
print(matches[0][1], matches[0][2])
