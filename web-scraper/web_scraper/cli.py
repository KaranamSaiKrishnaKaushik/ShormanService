from __future__ import annotations

import argparse
import logging
from datetime import datetime, timezone
from pathlib import Path

from .db_importer import ImportSummary, PromotionSummary, StoreStatus, get_store_statuses, import_store_json, promote_staged_products
from .exporters import write_csv, write_json, write_jsonl
from .openfoodfacts import OpenFoodFactsStoreClient, SUPPORTED_STORES


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Open Food Facts store export filter")
    subparsers = parser.add_subparsers(dest="source", required=True)

    off_store = subparsers.add_parser("off-store", help="Filter store-linked rows from the Open Food Facts export")
    off_store.add_argument("--store", choices=list(SUPPORTED_STORES), required=True)
    off_store.add_argument("--max-matches", type=int)
    off_store.add_argument("--max-rows-scanned", type=int)
    off_store.add_argument("--delay", type=float, default=0.0)
    off_store.add_argument("--format", choices=["json", "jsonl", "csv", "all"], default="all")
    off_store.add_argument("--output-dir", default="data")
    off_store.add_argument("--verbose", action="store_true")

    import_store = subparsers.add_parser("import-store-json", help="Import a store latest JSON file into database staging tables")
    import_store.add_argument("--store", choices=list(SUPPORTED_STORES), required=True)
    import_store.add_argument("--input-file")
    import_store.add_argument("--appsettings-path", default="../shorman-api/ShormanServicesBackend/appsettings.Development.Local.json")
    import_store.add_argument("--provider", choices=["sqlserver", "mysql"])
    import_store.add_argument("--connection-string")
    import_store.add_argument("--dry-run", action="store_true")
    import_store.add_argument("--verbose", action="store_true")

    sync_all = subparsers.add_parser("sync-all-stores", help="Export and import all supported stores")
    sync_all.add_argument("--max-matches", type=int)
    sync_all.add_argument("--max-rows-scanned", type=int)
    sync_all.add_argument("--delay", type=float, default=0.0)
    sync_all.add_argument("--output-dir", default="data")
    sync_all.add_argument("--appsettings-path", default="../shorman-api/ShormanServicesBackend/appsettings.Development.Local.json")
    sync_all.add_argument("--provider", choices=["sqlserver", "mysql"])
    sync_all.add_argument("--connection-string")
    sync_all.add_argument("--promote-to-app", action="store_true")
    sync_all.add_argument("--verbose", action="store_true")

    promote_store = subparsers.add_parser("promote-staged-products", help="Promote staged OFF rows into the app products table")
    promote_store.add_argument("--store", choices=[*SUPPORTED_STORES, "all"], default="all")
    promote_store.add_argument("--appsettings-path", default="../shorman-api/ShormanServicesBackend/appsettings.Development.Local.json")
    promote_store.add_argument("--provider", choices=["sqlserver", "mysql"])
    promote_store.add_argument("--connection-string")
    promote_store.add_argument("--dry-run", action="store_true")
    promote_store.add_argument("--verbose", action="store_true")

    status_cmd = subparsers.add_parser("status", help="Show latest file and database status per store")
    status_cmd.add_argument("--store", choices=[*SUPPORTED_STORES, "all"], default="all")
    status_cmd.add_argument("--output-dir", default="data")
    status_cmd.add_argument("--appsettings-path", default="../shorman-api/ShormanServicesBackend/appsettings.Development.Local.json")
    status_cmd.add_argument("--provider", choices=["sqlserver", "mysql"])
    status_cmd.add_argument("--connection-string")
    status_cmd.add_argument("--verbose", action="store_true")
    return parser


def main(argv: list[str] | None = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)

    logging.basicConfig(
        level=logging.DEBUG if args.verbose else logging.INFO,
        format="%(asctime)s %(levelname)-7s %(message)s",
    )

    if args.source == "off-store":
        records = _export_store(
            store=args.store,
            output_dir=Path(args.output_dir),
            delay=args.delay,
            max_matches=args.max_matches,
            max_rows_scanned=args.max_rows_scanned,
            output_format=args.format,
        )
        logging.info("Wrote %d Open Food Facts %s rows to %s", len(records), args.store.upper(), (Path(args.output_dir) / args.store).resolve())
        return 0

    if args.source == "import-store-json":
        _log_import_summary(_run_import(args))
        return 0

    if args.source == "sync-all-stores":
        import_summaries, promotion_summaries = _run_sync_all(args)
        for summary in import_summaries:
            _log_import_summary(summary)
        for summary in promotion_summaries:
            _log_promotion_summary(summary)
        return 0

    if args.source == "promote-staged-products":
        for summary in _run_promote(args):
            _log_promotion_summary(summary)
        return 0

    if args.source == "status":
        for summary in _run_status(args):
            _log_status_summary(summary)
        return 0

    parser.error(f"Unsupported source: {args.source}")


def _run_import(args: argparse.Namespace) -> ImportSummary:
    input_file = Path(args.input_file) if args.input_file else Path("data") / args.store / f"openfoodfacts_{args.store}_latest.json"
    appsettings_path = Path(args.appsettings_path)
    return import_store_json(
        store=args.store,
        input_file=input_file,
        appsettings_path=appsettings_path,
        provider=args.provider,
        connection_string=args.connection_string,
        dry_run=args.dry_run,
    )


def _run_sync_all(args: argparse.Namespace) -> tuple[list[ImportSummary], list[PromotionSummary]]:
    import_summaries: list[ImportSummary] = []
    for store in SUPPORTED_STORES:
        _export_store(
            store=store,
            output_dir=Path(args.output_dir),
            delay=args.delay,
            max_matches=args.max_matches,
            max_rows_scanned=args.max_rows_scanned,
            output_format="json",
        )
        import_summaries.append(
            import_store_json(
                store=store,
                input_file=Path(args.output_dir) / store / f"openfoodfacts_{store}_latest.json",
                appsettings_path=Path(args.appsettings_path),
                provider=args.provider,
                connection_string=args.connection_string,
                dry_run=False,
            )
        )

    promotion_summaries: list[PromotionSummary] = []
    if args.promote_to_app:
        promotion_summaries = promote_staged_products(
            stores=list(SUPPORTED_STORES),
            appsettings_path=Path(args.appsettings_path),
            provider=args.provider,
            connection_string=args.connection_string,
            dry_run=False,
        )
    return import_summaries, promotion_summaries


def _run_promote(args: argparse.Namespace) -> list[PromotionSummary]:
    stores = list(SUPPORTED_STORES) if args.store == "all" else [args.store]
    return promote_staged_products(
        stores=stores,
        appsettings_path=Path(args.appsettings_path),
        provider=args.provider,
        connection_string=args.connection_string,
        dry_run=args.dry_run,
    )


def _run_status(args: argparse.Namespace) -> list[StoreStatus]:
    stores = list(SUPPORTED_STORES) if args.store == "all" else [args.store]
    return get_store_statuses(
        stores=stores,
        output_dir=Path(args.output_dir),
        appsettings_path=Path(args.appsettings_path),
        provider=args.provider,
        connection_string=args.connection_string,
    )


def _export_store(
    *,
    store: str,
    output_dir: Path,
    delay: float,
    max_matches: int | None,
    max_rows_scanned: int | None,
    output_format: str,
) -> list[dict]:
    client = OpenFoodFactsStoreClient(delay_seconds=delay)
    records = client.fetch_store_products(
        store=store,
        max_matches=max_matches,
        max_rows_scanned=max_rows_scanned,
    )

    timestamp = datetime.now(timezone.utc).strftime("%Y%m%d_%H%M%S")
    store_output_dir = output_dir / store
    file_stub = f"openfoodfacts_{store}_{timestamp}"
    latest_stub = f"openfoodfacts_{store}_latest"

    if output_format in {"json", "all"}:
        write_json(store_output_dir / f"{file_stub}.json", records)
        write_json(store_output_dir / f"{latest_stub}.json", records)
    if output_format in {"jsonl", "all"}:
        write_jsonl(store_output_dir / f"{file_stub}.jsonl", records)
        write_jsonl(store_output_dir / f"{latest_stub}.jsonl", records)
    if output_format in {"csv", "all"}:
        write_csv(store_output_dir / f"{file_stub}.csv", records)
        write_csv(store_output_dir / f"{latest_stub}.csv", records)

    return records


def _log_import_summary(summary: ImportSummary) -> None:
    logging.info(
        "Import %s for %s using %s (%d records)",
        "validated" if summary.dry_run else "completed",
        summary.store.upper(),
        summary.provider,
        summary.record_count,
    )
    if summary.run_id is not None:
        logging.info("Import run id: %s", summary.run_id)
    logging.info("Source JSON: %s", summary.input_file)


def _log_promotion_summary(summary: PromotionSummary) -> None:
    logging.info(
        "Promotion %s for %s using %s (%d staged, %d promoted)",
        "validated" if summary.dry_run else "completed",
        summary.store.upper(),
        summary.provider,
        summary.staged_count,
        summary.promoted_count,
    )


def _log_status_summary(summary: StoreStatus) -> None:
    latest_file = str(summary.latest_file) if summary.latest_file else "<missing>"
    latest_mtime = summary.latest_file_modified_utc.isoformat() if summary.latest_file_modified_utc else "<missing>"
    logging.info(
        "Status %s | latest=%s | modified=%s | staged=%d | promoted=%d | latest import=%s | latest promotion=%s",
        summary.store.upper(),
        latest_file,
        latest_mtime,
        summary.staged_row_count,
        summary.promoted_row_count,
        summary.latest_import_run_id if summary.latest_import_run_id is not None else "<none>",
        summary.latest_promoted_run_id if summary.latest_promoted_run_id is not None else "<none>",
    )