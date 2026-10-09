#!/usr/bin/env python3
"""Consume downloaded artifacts without rebuilding or changing package identities."""
import hashlib
import json
import subprocess
import sys
from pathlib import Path

candidates = list(Path('artifacts/candidate').rglob('candidate.json'))
if len(candidates) != 1:
    raise ValueError('Expected one candidate manifest.')
manifest = candidates[0]
evidence = json.loads(manifest.read_text())
for name, digest in evidence['packageSha256'].items():
    if hashlib.sha256((manifest.parent / name).read_bytes()).hexdigest() != digest:
        raise ValueError(f'Candidate package changed: {name}')
subprocess.run([sys.executable, 'scripts/sharedkernel/smoke.py', str(manifest.parent),
                '--version', evidence['packageVersion'], '--aot'], check=True)
evidence['windowsPackageConsumption'] = 'passed'
manifest.write_text(json.dumps(evidence, indent=2) + '\n')
