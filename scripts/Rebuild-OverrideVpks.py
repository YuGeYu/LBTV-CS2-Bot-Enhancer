from __future__ import annotations

import argparse
import shutil
import sys
import tempfile
from pathlib import Path

try:
    import vpk
except ImportError as exc:  # pragma: no cover
    raise SystemExit(
        "Missing dependency 'vpk'. Install it with: python -m pip install vpk"
    ) from exc


DIFFICULTIES = ("Low", "Medium", "High")


def build_vpk(db_path: Path, out_path: Path) -> None:
    if not db_path.is_file():
        raise FileNotFoundError(f"Missing bot profile database: {db_path}")

    out_path.parent.mkdir(parents=True, exist_ok=True)

    with tempfile.TemporaryDirectory(prefix="cs2bot_vpk_") as temp_dir:
        temp_root = Path(temp_dir)
        shutil.copy2(db_path, temp_root / "botprofile.db")
        package = vpk.new(str(temp_root))
        package.save(str(out_path))


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Rebuild CS2 botprofile VPKs without legacy localization overrides."
    )
    parser.add_argument(
        "--repo-root",
        type=Path,
        default=Path(__file__).resolve().parents[1],
        help="Path to the CS2-Bot-Improver repository root.",
    )
    parser.add_argument(
        "--game-csgo",
        type=Path,
        required=True,
        help="Path to the game's game/csgo directory.",
    )
    parser.add_argument(
        "--backup",
        action="store_true",
        help="Create .bak copies before overwriting existing VPKs.",
    )
    args = parser.parse_args()

    repo_root = args.repo_root.resolve()
    game_csgo = args.game_csgo.resolve()
    source_overrides = repo_root / "overrides"
    target_overrides = game_csgo / "overrides"

    if not source_overrides.is_dir():
        raise FileNotFoundError(f"Overrides directory not found: {source_overrides}")
    if not game_csgo.is_dir():
        raise FileNotFoundError(f"Game csgo directory not found: {game_csgo}")

    builds = [
        (source_overrides / "Medium" / "botprofile.db", target_overrides / "botprofile.vpk"),
    ]
    for difficulty in DIFFICULTIES:
        builds.append(
            (
                source_overrides / difficulty / "botprofile.db",
                target_overrides / difficulty / "botprofile.vpk",
            )
        )

    for db_path, out_path in builds:
        if args.backup and out_path.exists():
            backup_path = out_path.with_suffix(out_path.suffix + ".bak")
            shutil.copy2(out_path, backup_path)
        build_vpk(db_path, out_path)
        print(f"rebuilt: {out_path}")

    return 0


if __name__ == "__main__":
    sys.exit(main())
