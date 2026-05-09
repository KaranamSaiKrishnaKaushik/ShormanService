from __future__ import annotations

import subprocess
import sys
from pathlib import Path


def _prefer_workspace_venv() -> None:
    current_python = Path(sys.executable).resolve()
    workspace_root = Path(__file__).resolve().parent.parent
    candidates = [
        workspace_root / ".venv" / "Scripts" / "python.exe",
        workspace_root / ".venv" / "bin" / "python",
    ]
    for candidate in candidates:
        if candidate.exists() and candidate.resolve() != current_python:
            completed = subprocess.run([str(candidate), __file__, *sys.argv[1:]], check=False)
            raise SystemExit(completed.returncode)


if __name__ == "__main__":
    _prefer_workspace_venv()
    from web_scraper.cli import main

    raise SystemExit(main())