#!/usr/bin/env python3
import re
import sys
import xml.etree.ElementTree as ET

try:
    tree = ET.parse(sys.argv[1])
except ET.ParseError:
    raise SystemExit(1)
for node in tree.iter("node"):
    if sys.argv[2] not in (node.get("text"), node.get("content-desc")):
        continue
    bounds = list(map(int, re.findall(r"\d+", node.get("bounds", ""))))
    if len(bounds) == 4 and bounds[2] > bounds[0] and bounds[3] > bounds[1]:
        print((bounds[0]+bounds[2])//2, (bounds[1]+bounds[3])//2)
        break
else:
    raise SystemExit(1)
