from __future__ import annotations

import csv
import gzip
import logging
import sys
import time
import urllib.request

EXPORT_URL = "https://static.openfoodfacts.org/data/en.openfoodfacts.org.products.csv.gz"
DEFAULT_USER_AGENT = "ShormanApp-WebScraper/1.0 (Open Food Facts store export filter)"
SUPPORTED_STORES = ("rewe", "aldi", "edeka", "penny", "lidl")
REQUESTED_FIELDS = (
    "code",
    "url",
    "creator",
    "created_datetime",
    "last_modified_datetime",
    "last_updated_datetime",
    "product_name",
    "brands",
    "origins",
    "stores",
    "countries",
    "countries_tags",
    "countries_en",
    "ingredients_text",
    "ingredients_tags",
    "ingredients_analysis_tags",
    "image_url",
    "image_small_url",
)

log = logging.getLogger("web_scraper.openfoodfacts")


def _set_max_csv_field_size() -> None:
    max_size = sys.maxsize
    while True:
        try:
            csv.field_size_limit(max_size)
            return
        except OverflowError:
            max_size //= 10


class OpenFoodFactsStoreClient:
    def __init__(self, delay_seconds: float = 0.0) -> None:
        self.delay_seconds = delay_seconds

    def fetch_store_products(
        self,
        store: str,
        max_matches: int | None = None,
        max_rows_scanned: int | None = None,
    ) -> list[dict]:
        normalized_store = store.strip().lower()
        if normalized_store not in SUPPORTED_STORES:
            raise ValueError(f"Unsupported store: {store}")

        request = urllib.request.Request(EXPORT_URL, headers={"User-Agent": DEFAULT_USER_AGENT})
        matches: list[dict] = []
        _set_max_csv_field_size()

        with urllib.request.urlopen(request, timeout=120) as response:
            with gzip.open(response, mode="rt", encoding="utf-8", newline="") as handle:
                reader = csv.DictReader(handle, delimiter="\t")
                for row_index, row in enumerate(reader, start=1):
                    if max_rows_scanned is not None and row_index > max_rows_scanned:
                        break

                    stores = (row.get("stores") or "").lower()
                    if normalized_store not in stores:
                        continue

                    matches.append(self._select_fields(row))
                    if len(matches) % 100 == 0:
                        log.info(
                            "Matched %d %s rows after scanning %d export rows",
                            len(matches),
                            normalized_store.upper(),
                            row_index,
                        )

                    if max_matches is not None and len(matches) >= max_matches:
                        break

                    if self.delay_seconds > 0:
                        time.sleep(self.delay_seconds)

        log.info("Finished export scan with %d %s matches", len(matches), normalized_store.upper())
        return matches

    def _select_fields(self, row: dict[str, str]) -> dict[str, str]:
        return {field: row.get(field, "") for field in REQUESTED_FIELDS}