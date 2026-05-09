from __future__ import annotations

import json
from dataclasses import dataclass
from datetime import UTC, datetime
from pathlib import Path

DEFAULT_PRICE = 0
MYSQL_INSERT_BATCH_SIZE = 200
IMPORTED_CATEGORY_SLUG = "imported-products"
IMPORTED_CATEGORY_NAME = "Imported Products"
IMPORTED_CATEGORY_ICON = "box"
CATEGORY_METADATA = {
    "eggs-dairy": {"name": "Eggs & Dairy", "icon": "egg"},
    "meat": {"name": "Meat", "icon": "meat"},
    "fruits-vegetables": {"name": "Fruits & Vegetables", "icon": "leaf"},
    "bakery": {"name": "Bakery", "icon": "bread"},
    "beverages": {"name": "Beverages", "icon": "cup"},
    "snacks": {"name": "Snacks", "icon": "popcorn"},
}
CATEGORY_KEYWORDS: tuple[tuple[str, tuple[str, ...]], ...] = (
    ("eggs-dairy", ("milk", "cheese", "yogurt", "yoghurt", "butter", "cream", "egg", "eggs", "mozzarella", "quark", "kefir", "skyr", "gouda", "brie", "camembert")),
    ("meat", ("chicken", "beef", "pork", "meat", "sausage", "salami", "ham", "bacon", "turkey", "lamb", "veal", "steak", "mince", "ground beef", "salmon", "fish", "tuna")),
    ("fruits-vegetables", ("apple", "apples", "banana", "bananas", "tomato", "tomatoes", "potato", "potatoes", "onion", "carrot", "lettuce", "salad", "cucumber", "pepper", "broccoli", "fruit", "vegetable", "avocado", "orange", "lemon", "lime", "grape", "berries", "berry", "mango")),
    ("bakery", ("bread", "toast", "bun", "roll", "bagel", "croissant", "brioche", "cake", "muffin", "donut", "pastry", "baguette", "sourdough", "bakery", "crackerbread")),
    ("beverages", ("water", "juice", "cola", "soda", "drink", "tea", "coffee", "espresso", "latte", "beer", "wine", "smoothie", "lemonade", "energy drink", "sparkling", "still water")),
    ("snacks", ("chips", "crisps", "chocolate", "candy", "cookie", "cookies", "biscuit", "biscuits", "cracker", "pretzel", "nuts", "nut", "popcorn", "snack", "wafer", "bar", "granola")),
)
STORE_METADATA = {
    "rewe": {"name": "REWE", "color": "#CC0000"},
    "aldi": {"name": "ALDI", "color": "#00519C"},
    "edeka": {"name": "EDEKA", "color": "#003A70"},
    "penny": {"name": "PENNY", "color": "#CC0000"},
    "lidl": {"name": "LIDL", "color": "#0050AA"},
}


REQUESTED_DB_FIELDS = (
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


@dataclass(frozen=True)
class DatabaseSettings:
    provider: str
    connection_string: str


@dataclass(frozen=True)
class ImportSummary:
    store: str
    provider: str
    input_file: Path
    record_count: int
    dry_run: bool
    run_id: int | None = None


@dataclass(frozen=True)
class PromotionSummary:
    store: str
    provider: str
    staged_count: int
    promoted_count: int
    dry_run: bool


@dataclass(frozen=True)
class StoreStatus:
    store: str
    provider: str
    latest_file: Path | None
    latest_file_modified_utc: datetime | None
    latest_file_size_bytes: int
    staged_row_count: int
    promoted_row_count: int
    latest_import_run_id: int | None
    latest_promoted_run_id: int | None


def import_store_json(
    *,
    store: str,
    input_file: Path,
    appsettings_path: Path,
    provider: str | None,
    connection_string: str | None,
    dry_run: bool,
) -> ImportSummary:
    resolved_input_file = _resolve_input_file(store=store, input_file=input_file)
    records = _load_records(resolved_input_file)
    settings = _resolve_database_settings(
        appsettings_path=appsettings_path,
        provider=provider,
        connection_string=connection_string,
    )
    if dry_run:
        return ImportSummary(
            store=store,
            provider=settings.provider,
            input_file=resolved_input_file.resolve(),
            record_count=len(records),
            dry_run=True,
        )

    if settings.provider == "sqlserver":
        run_id = _import_sqlserver(store=store, input_file=resolved_input_file, connection_string=settings.connection_string, records=records)
    elif settings.provider == "mysql":
        run_id = _import_mysql(store=store, input_file=resolved_input_file, connection_string=settings.connection_string, records=records)
    else:
        raise ValueError(f"Unsupported database provider: {settings.provider}")

    return ImportSummary(
        store=store,
        provider=settings.provider,
        input_file=resolved_input_file.resolve(),
        record_count=len(records),
        dry_run=False,
        run_id=run_id,
    )


def _resolve_input_file(*, store: str, input_file: Path) -> Path:
    if input_file.exists():
        return input_file

    snapshot_pattern = f"openfoodfacts_{store}_*.json"
    sibling_snapshots = sorted(
        path for path in input_file.parent.glob(snapshot_pattern) if path.name != input_file.name
    )
    if sibling_snapshots:
        return sibling_snapshots[-1]

    raise FileNotFoundError(
        f"Input JSON file not found: {input_file}. Run 'python main.py off-store --store {store} --format json --verbose' first. "
        f"That command will create the folder and write openfoodfacts_{store}_latest.json automatically."
    )


def _load_records(input_file: Path) -> list[dict[str, str]]:
    raw = json.loads(input_file.read_text(encoding="utf-8"))
    if not isinstance(raw, list):
        raise ValueError(f"Expected a JSON array in {input_file}")

    records: list[dict[str, str]] = []
    for entry in raw:
        if not isinstance(entry, dict):
            continue
        normalized = {field: _to_text(entry.get(field)) for field in REQUESTED_DB_FIELDS}
        if not normalized["code"]:
            continue
        records.append(normalized)
    return records


def _to_text(value: object) -> str:
    if value is None:
        return ""
    if isinstance(value, str):
        return value
    return str(value)


def _chunked[T](items: list[T], chunk_size: int) -> list[list[T]]:
    if chunk_size <= 0:
        raise ValueError("chunk_size must be greater than 0")

    return [items[index:index + chunk_size] for index in range(0, len(items), chunk_size)]


def _resolve_database_settings(
    *,
    appsettings_path: Path,
    provider: str | None,
    connection_string: str | None,
) -> DatabaseSettings:
    if connection_string:
        if not provider:
            raise ValueError("--provider is required when --connection-string is supplied")
        return DatabaseSettings(provider=provider.lower(), connection_string=connection_string)

    config = json.loads(appsettings_path.read_text(encoding="utf-8"))
    db_provider = provider or config.get("Database", {}).get("Provider")
    api_connection = config.get("ConnectionStrings", {}).get("ApiConnection") or config.get("ConnectionStrings", {}).get("DefaultConnection")
    if not db_provider or not api_connection:
        raise ValueError(f"Could not resolve database provider/connection from {appsettings_path}")
    return DatabaseSettings(provider=str(db_provider).lower(), connection_string=str(api_connection))


def _import_sqlserver(*, store: str, input_file: Path, connection_string: str, records: list[dict[str, str]]) -> int:
    import pyodbc

    sqlserver_connection_string = _to_sqlserver_odbc_connection_string(connection_string, pyodbc.drivers())
    now = datetime.now(UTC).replace(tzinfo=None)

    with pyodbc.connect(sqlserver_connection_string) as connection:
        connection.autocommit = False
        cursor = connection.cursor()
        _ensure_sqlserver_tables(cursor)
        run_id = _insert_sqlserver_run(cursor, store=store, input_file=input_file, started_at=now)
        cursor.execute("DELETE FROM off_store_products WHERE StoreSlug = ?", store)
        rows = [_record_tuple(record, store=store, run_id=run_id, imported_at=now) for record in records]
        if rows:
            cursor.executemany(
                """
                INSERT INTO off_store_products (
                    ImportRunId, StoreSlug, Code, Url, Creator, CreatedDatetime, LastModifiedDatetime,
                    LastUpdatedDatetime, ProductName, Brands, Origins, Stores, Countries, CountriesTags,
                    CountriesEn, IngredientsText, IngredientsTags, IngredientsAnalysisTags, Price, ImageUrl,
                    ImageSmallUrl, ImportedAtUtc
                ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                """,
                rows,
            )
        cursor.execute(
            "UPDATE off_import_runs SET CompletedAtUtc = ?, Status = ?, RecordCount = ? WHERE Id = ?",
            now,
            "completed",
            len(records),
            run_id,
        )
        connection.commit()
        return run_id


def _to_sqlserver_odbc_connection_string(connection_string: str, available_drivers: list[str]) -> str:
    settings = _parse_connection_string(connection_string)
    normalized = {key.lower(): value for key, value in settings.items()}
    if "driver" in normalized:
        return _compose_sqlserver_connection_string(settings)

    preferred = ["ODBC Driver 18 for SQL Server", "ODBC Driver 17 for SQL Server", "SQL Server"]
    driver = next((name for name in preferred if name in available_drivers), None)
    if not driver:
        raise RuntimeError("No SQL Server ODBC driver found. Install ODBC Driver 17 or 18 for SQL Server.")

    settings["Driver"] = f"{{{driver}}}"
    if not any(key.lower() == "trustservercertificate" for key in settings):
        settings["TrustServerCertificate"] = "yes"
    return _compose_sqlserver_connection_string(settings)


def _compose_sqlserver_connection_string(settings: dict[str, str]) -> str:
    odbc_settings: list[tuple[str, str]] = []
    for key, value in settings.items():
        normalized_key = _normalize_sqlserver_key(key)
        if normalized_key is None:
            continue
        normalized_value = value.strip()
        if normalized_key.lower() in {"encrypt", "trustservercertificate"}:
            normalized_value = _normalize_odbc_bool(normalized_value)
        odbc_settings.append((normalized_key, normalized_value))
    return ";".join(f"{key}={value}" for key, value in odbc_settings) + ";"


def _normalize_sqlserver_key(key: str) -> str | None:
    lowered = key.strip().lower()
    mapping = {
        "driver": "Driver",
        "server": "Server",
        "data source": "Server",
        "address": "Server",
        "addr": "Server",
        "network address": "Server",
        "database": "Database",
        "initial catalog": "Database",
        "user id": "UID",
        "uid": "UID",
        "user": "UID",
        "password": "PWD",
        "pwd": "PWD",
        "encrypt": "Encrypt",
        "trustservercertificate": "TrustServerCertificate",
        "connection timeout": "Connection Timeout",
        "timeout": "Connection Timeout",
    }
    return mapping.get(lowered)


def _normalize_odbc_bool(value: str) -> str:
    lowered = value.strip().lower()
    if lowered in {"true", "1", "yes"}:
        return "yes"
    if lowered in {"false", "0", "no"}:
        return "no"
    return value


def _ensure_sqlserver_tables(cursor: object) -> None:
    cursor.execute(
        """
        IF OBJECT_ID('off_import_runs', 'U') IS NULL
        CREATE TABLE off_import_runs (
            Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
            StoreSlug NVARCHAR(100) NOT NULL,
            SourceFile NVARCHAR(500) NOT NULL,
            StartedAtUtc DATETIME2 NOT NULL,
            CompletedAtUtc DATETIME2 NULL,
            Status NVARCHAR(30) NOT NULL,
            RecordCount INT NOT NULL DEFAULT 0
        );
        """
    )
    cursor.execute(
        """
        IF OBJECT_ID('off_store_products', 'U') IS NULL
        CREATE TABLE off_store_products (
            ImportRunId INT NOT NULL,
            StoreSlug NVARCHAR(100) NOT NULL,
            Code NVARCHAR(64) NOT NULL,
            Url NVARCHAR(1000) NULL,
            Creator NVARCHAR(255) NULL,
            CreatedDatetime NVARCHAR(64) NULL,
            LastModifiedDatetime NVARCHAR(64) NULL,
            LastUpdatedDatetime NVARCHAR(64) NULL,
            ProductName NVARCHAR(500) NULL,
            Brands NVARCHAR(500) NULL,
            Origins NVARCHAR(500) NULL,
            Stores NVARCHAR(500) NULL,
            Countries NVARCHAR(MAX) NULL,
            CountriesTags NVARCHAR(MAX) NULL,
            CountriesEn NVARCHAR(MAX) NULL,
            IngredientsText NVARCHAR(MAX) NULL,
            IngredientsTags NVARCHAR(MAX) NULL,
            IngredientsAnalysisTags NVARCHAR(MAX) NULL,
            Price DECIMAL(10,2) NOT NULL DEFAULT 0,
            ImageUrl NVARCHAR(1000) NULL,
            ImageSmallUrl NVARCHAR(1000) NULL,
            ImportedAtUtc DATETIME2 NOT NULL,
            CONSTRAINT PK_off_store_products PRIMARY KEY (StoreSlug, Code),
            CONSTRAINT FK_off_store_products_Run FOREIGN KEY (ImportRunId) REFERENCES off_import_runs(Id) ON DELETE CASCADE
        );
        """
    )
    cursor.execute(
        """
        IF COL_LENGTH('off_store_products', 'Price') IS NULL
            ALTER TABLE off_store_products ADD Price DECIMAL(10,2) NOT NULL CONSTRAINT DF_off_store_products_Price DEFAULT 0;
        """
    )
    cursor.execute("ALTER TABLE off_store_products ALTER COLUMN Url NVARCHAR(MAX) NULL")
    cursor.execute("ALTER TABLE off_store_products ALTER COLUMN ProductName NVARCHAR(MAX) NULL")
    cursor.execute("ALTER TABLE off_store_products ALTER COLUMN Brands NVARCHAR(MAX) NULL")
    cursor.execute("ALTER TABLE off_store_products ALTER COLUMN Origins NVARCHAR(MAX) NULL")
    cursor.execute("ALTER TABLE off_store_products ALTER COLUMN Stores NVARCHAR(MAX) NULL")
    cursor.execute("ALTER TABLE off_store_products ALTER COLUMN ImageUrl NVARCHAR(MAX) NULL")
    cursor.execute("ALTER TABLE off_store_products ALTER COLUMN ImageSmallUrl NVARCHAR(MAX) NULL")
    cursor.execute("UPDATE off_store_products SET Price = 0 WHERE Price IS NULL")


def _insert_sqlserver_run(cursor: object, *, store: str, input_file: Path, started_at: datetime) -> int:
    cursor.execute(
        """
        INSERT INTO off_import_runs (StoreSlug, SourceFile, StartedAtUtc, Status, RecordCount)
        OUTPUT INSERTED.Id
        VALUES (?, ?, ?, ?, ?)
        """,
        store,
        str(input_file.resolve()),
        started_at,
        "running",
        0,
    )
    row = cursor.fetchone()
    if row is None or row[0] is None:
        raise RuntimeError("Failed to create import run record")
    return int(row[0])


def _import_mysql(*, store: str, input_file: Path, connection_string: str, records: list[dict[str, str]]) -> int:
    import mysql.connector

    settings = _parse_connection_string(connection_string)
    now = datetime.now(UTC).replace(tzinfo=None)
    connection = mysql.connector.connect(
        host=settings.get("server") or settings.get("host") or "localhost",
        port=int(settings.get("port") or 3306),
        database=settings.get("database"),
        user=settings.get("user id") or settings.get("user") or settings.get("uid"),
        password=settings.get("password") or settings.get("pwd"),
        ssl_disabled=(settings.get("sslmode", "").lower() in {"none", "disabled", "disable"}),
    )
    try:
        cursor = connection.cursor()
        _ensure_mysql_tables(cursor)
        run_id = _insert_mysql_run(cursor, store=store, input_file=input_file, started_at=now)
        cursor.execute("DELETE FROM off_store_products WHERE StoreSlug = %s", (store,))
        rows = [_record_tuple(record, store=store, run_id=run_id, imported_at=now) for record in records]
        if rows:
            for batch in _chunked(rows, MYSQL_INSERT_BATCH_SIZE):
                cursor.executemany(
                    """
                    INSERT INTO off_store_products (
                        ImportRunId, StoreSlug, Code, Url, Creator, CreatedDatetime, LastModifiedDatetime,
                        LastUpdatedDatetime, ProductName, Brands, Origins, Stores, Countries, CountriesTags,
                        CountriesEn, IngredientsText, IngredientsTags, IngredientsAnalysisTags, Price, ImageUrl,
                        ImageSmallUrl, ImportedAtUtc
                    ) VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s)
                    """,
                    batch,
                )
        cursor.execute(
            "UPDATE off_import_runs SET CompletedAtUtc = %s, Status = %s, RecordCount = %s WHERE Id = %s",
            (now, "completed", len(records), run_id),
        )
        connection.commit()
        return run_id
    finally:
        connection.close()


def _parse_connection_string(connection_string: str) -> dict[str, str]:
    settings: dict[str, str] = {}
    for part in connection_string.split(";"):
        piece = part.strip()
        if not piece or "=" not in piece:
            continue
        key, value = piece.split("=", 1)
        settings[key.strip().lower()] = value.strip()
    return settings


def _ensure_mysql_tables(cursor: object) -> None:
    cursor.execute(
        """
        CREATE TABLE IF NOT EXISTS off_import_runs (
            Id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
            StoreSlug VARCHAR(100) NOT NULL,
            SourceFile VARCHAR(500) NOT NULL,
            StartedAtUtc DATETIME NOT NULL,
            CompletedAtUtc DATETIME NULL,
            Status VARCHAR(30) NOT NULL,
            RecordCount INT NOT NULL DEFAULT 0
        )
        """
    )
    cursor.execute(
        """
        CREATE TABLE IF NOT EXISTS off_store_products (
            ImportRunId INT NOT NULL,
            StoreSlug VARCHAR(100) NOT NULL,
            Code VARCHAR(64) NOT NULL,
            Url LONGTEXT NULL,
            Creator VARCHAR(255) NULL,
            CreatedDatetime VARCHAR(64) NULL,
            LastModifiedDatetime VARCHAR(64) NULL,
            LastUpdatedDatetime VARCHAR(64) NULL,
            ProductName LONGTEXT NULL,
            Brands LONGTEXT NULL,
            Origins LONGTEXT NULL,
            Stores LONGTEXT NULL,
            Countries LONGTEXT NULL,
            CountriesTags LONGTEXT NULL,
            CountriesEn LONGTEXT NULL,
            IngredientsText LONGTEXT NULL,
            IngredientsTags LONGTEXT NULL,
            IngredientsAnalysisTags LONGTEXT NULL,
            Price DECIMAL(10,2) NOT NULL DEFAULT 0,
            ImageUrl LONGTEXT NULL,
            ImageSmallUrl LONGTEXT NULL,
            ImportedAtUtc DATETIME NOT NULL,
            PRIMARY KEY (StoreSlug, Code),
            CONSTRAINT FK_off_store_products_run FOREIGN KEY (ImportRunId) REFERENCES off_import_runs(Id) ON DELETE CASCADE
        )
        """
    )
    cursor.execute("ALTER TABLE off_store_products MODIFY COLUMN Url LONGTEXT NULL")
    cursor.execute("ALTER TABLE off_store_products MODIFY COLUMN ProductName LONGTEXT NULL")
    cursor.execute("ALTER TABLE off_store_products MODIFY COLUMN Brands LONGTEXT NULL")
    cursor.execute("ALTER TABLE off_store_products MODIFY COLUMN Origins LONGTEXT NULL")
    cursor.execute("ALTER TABLE off_store_products MODIFY COLUMN Stores LONGTEXT NULL")
    cursor.execute("ALTER TABLE off_store_products MODIFY COLUMN Price DECIMAL(10,2) NOT NULL DEFAULT 0")
    cursor.execute("ALTER TABLE off_store_products MODIFY COLUMN ImageUrl LONGTEXT NULL")
    cursor.execute("ALTER TABLE off_store_products MODIFY COLUMN ImageSmallUrl LONGTEXT NULL")
    cursor.execute("UPDATE off_store_products SET Price = 0 WHERE Price IS NULL")


def _insert_mysql_run(cursor: object, *, store: str, input_file: Path, started_at: datetime) -> int:
    cursor.execute(
        """
        INSERT INTO off_import_runs (StoreSlug, SourceFile, StartedAtUtc, Status, RecordCount)
        VALUES (%s, %s, %s, %s, %s)
        """,
        (store, str(input_file.resolve()), started_at, "running", 0),
    )
    return int(cursor.lastrowid)


def _record_tuple(record: dict[str, str], *, store: str, run_id: int, imported_at: datetime) -> tuple[object, ...]:
    return (
        run_id,
        store,
        record["code"],
        record["url"],
        record["creator"],
        record["created_datetime"],
        record["last_modified_datetime"],
        record["last_updated_datetime"],
        record["product_name"],
        record["brands"],
        record["origins"],
        record["stores"],
        record["countries"],
        record["countries_tags"],
        record["countries_en"],
        record["ingredients_text"],
        record["ingredients_tags"],
        record["ingredients_analysis_tags"],
        DEFAULT_PRICE,
        record["image_url"],
        record["image_small_url"],
        imported_at,
    )


def promote_staged_products(
    *,
    stores: list[str],
    appsettings_path: Path,
    provider: str | None,
    connection_string: str | None,
    dry_run: bool,
) -> list[PromotionSummary]:
    settings = _resolve_database_settings(
        appsettings_path=appsettings_path,
        provider=provider,
        connection_string=connection_string,
    )

    if settings.provider == "sqlserver":
        return _promote_sqlserver(stores=stores, connection_string=settings.connection_string, dry_run=dry_run)
    if settings.provider == "mysql":
        return _promote_mysql(stores=stores, connection_string=settings.connection_string, dry_run=dry_run)
    raise ValueError(f"Unsupported database provider: {settings.provider}")


def get_store_statuses(
    *,
    stores: list[str],
    output_dir: Path,
    appsettings_path: Path,
    provider: str | None,
    connection_string: str | None,
) -> list[StoreStatus]:
    settings = _resolve_database_settings(
        appsettings_path=appsettings_path,
        provider=provider,
        connection_string=connection_string,
    )

    latest_files = {store: _resolve_latest_file_metadata(output_dir / store, store) for store in stores}

    if settings.provider == "sqlserver":
        return _get_sqlserver_statuses(stores=stores, latest_files=latest_files, connection_string=settings.connection_string)
    if settings.provider == "mysql":
        return _get_mysql_statuses(stores=stores, latest_files=latest_files, connection_string=settings.connection_string)
    raise ValueError(f"Unsupported database provider: {settings.provider}")


def _promote_sqlserver(*, stores: list[str], connection_string: str, dry_run: bool) -> list[PromotionSummary]:
    import pyodbc

    sqlserver_connection_string = _to_sqlserver_odbc_connection_string(connection_string, pyodbc.drivers())
    summaries: list[PromotionSummary] = []

    with pyodbc.connect(sqlserver_connection_string) as connection:
        connection.autocommit = False
        cursor = connection.cursor()
        _ensure_sqlserver_tables(cursor)
        _ensure_sqlserver_product_link_table(cursor)
        category_ids = _ensure_sqlserver_catalog_categories(cursor)

        for store in stores:
            staged_rows = _fetch_sqlserver_stage_rows(cursor, store)
            if dry_run:
                summaries.append(PromotionSummary(store=store, provider="sqlserver", staged_count=len(staged_rows), promoted_count=len(staged_rows), dry_run=True))
                continue

            supermarket_id = _ensure_sqlserver_supermarket(cursor, store)
            promoted_count = _upsert_sqlserver_products(cursor, store, staged_rows, category_ids, supermarket_id)
            summaries.append(PromotionSummary(store=store, provider="sqlserver", staged_count=len(staged_rows), promoted_count=promoted_count, dry_run=False))

        connection.commit()

    return summaries


def _promote_mysql(*, stores: list[str], connection_string: str, dry_run: bool) -> list[PromotionSummary]:
    import mysql.connector

    settings = _parse_connection_string(connection_string)
    connection = mysql.connector.connect(
        host=settings.get("server") or settings.get("host") or "localhost",
        port=int(settings.get("port") or 3306),
        database=settings.get("database"),
        user=settings.get("user id") or settings.get("user") or settings.get("uid"),
        password=settings.get("password") or settings.get("pwd"),
        ssl_disabled=(settings.get("sslmode", "").lower() in {"none", "disabled", "disable"}),
    )
    summaries: list[PromotionSummary] = []
    try:
        cursor = connection.cursor()
        _ensure_mysql_tables(cursor)
        _ensure_mysql_product_link_table(cursor)
        category_ids = _ensure_mysql_catalog_categories(cursor)

        for store in stores:
            staged_rows = _fetch_mysql_stage_rows(cursor, store)
            if dry_run:
                summaries.append(PromotionSummary(store=store, provider="mysql", staged_count=len(staged_rows), promoted_count=len(staged_rows), dry_run=True))
                continue

            supermarket_id = _ensure_mysql_supermarket(cursor, store)
            promoted_count = _upsert_mysql_products(cursor, store, staged_rows, category_ids, supermarket_id)
            summaries.append(PromotionSummary(store=store, provider="mysql", staged_count=len(staged_rows), promoted_count=promoted_count, dry_run=False))

        connection.commit()
        return summaries
    finally:
        connection.close()


def _ensure_sqlserver_product_link_table(cursor: object) -> None:
    cursor.execute(
        """
        IF OBJECT_ID('off_product_links', 'U') IS NULL
        CREATE TABLE off_product_links (
            StoreSlug NVARCHAR(100) NOT NULL,
            Code NVARCHAR(64) NOT NULL,
            ProductId INT NOT NULL,
            LastImportRunId INT NULL,
            CONSTRAINT PK_off_product_links PRIMARY KEY (StoreSlug, Code),
            CONSTRAINT FK_off_product_links_Product FOREIGN KEY (ProductId) REFERENCES products(Id) ON DELETE CASCADE,
            CONSTRAINT FK_off_product_links_Run FOREIGN KEY (LastImportRunId) REFERENCES off_import_runs(Id)
        );
        """
    )


def _ensure_mysql_product_link_table(cursor: object) -> None:
    cursor.execute(
        """
        CREATE TABLE IF NOT EXISTS off_product_links (
            StoreSlug VARCHAR(100) NOT NULL,
            Code VARCHAR(64) NOT NULL,
            ProductId INT NOT NULL,
            LastImportRunId INT NULL,
            PRIMARY KEY (StoreSlug, Code),
            CONSTRAINT FK_off_product_links_Product FOREIGN KEY (ProductId) REFERENCES products(Id) ON DELETE CASCADE,
            CONSTRAINT FK_off_product_links_Run FOREIGN KEY (LastImportRunId) REFERENCES off_import_runs(Id)
        )
        """
    )


def _ensure_sqlserver_catalog_categories(cursor: object) -> dict[str, int]:
    category_ids: dict[str, int] = {}
    for slug, metadata in CATEGORY_METADATA.items():
        cursor.execute("SELECT Id FROM categories WHERE Slug = ?", slug)
        row = cursor.fetchone()
        if row:
            category_ids[slug] = int(row[0])
            continue

        cursor.execute(
            """
            INSERT INTO categories (Name, Slug, Icon)
            OUTPUT INSERTED.Id
            VALUES (?, ?, ?)
            """,
            metadata["name"],
            slug,
            metadata["icon"],
        )
        category_ids[slug] = int(cursor.fetchone()[0])

    category_ids[IMPORTED_CATEGORY_SLUG] = _ensure_sqlserver_import_category(cursor)
    return category_ids


def _ensure_mysql_catalog_categories(cursor: object) -> dict[str, int]:
    category_ids: dict[str, int] = {}
    for slug, metadata in CATEGORY_METADATA.items():
        cursor.execute("SELECT Id FROM categories WHERE Slug = %s", (slug,))
        row = cursor.fetchone()
        if row:
            category_ids[slug] = int(row[0])
            continue

        cursor.execute(
            "INSERT INTO categories (Name, Slug, Icon) VALUES (%s, %s, %s)",
            (metadata["name"], slug, metadata["icon"]),
        )
        category_ids[slug] = int(cursor.lastrowid)

    category_ids[IMPORTED_CATEGORY_SLUG] = _ensure_mysql_import_category(cursor)
    return category_ids


def _ensure_sqlserver_import_category(cursor: object) -> int:
    cursor.execute("SELECT Id FROM categories WHERE Slug = ?", IMPORTED_CATEGORY_SLUG)
    row = cursor.fetchone()
    if row:
        return int(row[0])

    cursor.execute(
        """
        INSERT INTO categories (Name, Slug, Icon)
        OUTPUT INSERTED.Id
        VALUES (?, ?, ?)
        """,
        IMPORTED_CATEGORY_NAME,
        IMPORTED_CATEGORY_SLUG,
        IMPORTED_CATEGORY_ICON,
    )
    return int(cursor.fetchone()[0])


def _ensure_mysql_import_category(cursor: object) -> int:
    cursor.execute("SELECT Id FROM categories WHERE Slug = %s", (IMPORTED_CATEGORY_SLUG,))
    row = cursor.fetchone()
    if row:
        return int(row[0])

    cursor.execute(
        "INSERT INTO categories (Name, Slug, Icon) VALUES (%s, %s, %s)",
        (IMPORTED_CATEGORY_NAME, IMPORTED_CATEGORY_SLUG, IMPORTED_CATEGORY_ICON),
    )
    return int(cursor.lastrowid)


def _ensure_sqlserver_supermarket(cursor: object, store: str) -> int:
    cursor.execute("SELECT Id FROM supermarkets WHERE Slug = ?", store)
    row = cursor.fetchone()
    if row:
        return int(row[0])

    metadata = STORE_METADATA[store]
    cursor.execute(
        """
        INSERT INTO supermarkets (Name, Slug, Color)
        OUTPUT INSERTED.Id
        VALUES (?, ?, ?)
        """,
        metadata["name"],
        store,
        metadata["color"],
    )
    return int(cursor.fetchone()[0])


def _ensure_mysql_supermarket(cursor: object, store: str) -> int:
    cursor.execute("SELECT Id FROM supermarkets WHERE Slug = %s", (store,))
    row = cursor.fetchone()
    if row:
        return int(row[0])

    metadata = STORE_METADATA[store]
    cursor.execute(
        "INSERT INTO supermarkets (Name, Slug, Color) VALUES (%s, %s, %s)",
        (metadata["name"], store, metadata["color"]),
    )
    return int(cursor.lastrowid)


def _fetch_sqlserver_stage_rows(cursor: object, store: str) -> list[tuple]:
    cursor.execute(
        """
        SELECT ImportRunId, Code, ProductName, IngredientsText, Brands, Origins, CountriesEn, ImageUrl, ImageSmallUrl, Price
        FROM off_store_products
        WHERE StoreSlug = ?
        ORDER BY Code
        """,
        store,
    )
    return list(cursor.fetchall())


def _fetch_mysql_stage_rows(cursor: object, store: str) -> list[tuple]:
    cursor.execute(
        """
        SELECT ImportRunId, Code, ProductName, IngredientsText, Brands, Origins, CountriesEn, ImageUrl, ImageSmallUrl, Price
        FROM off_store_products
        WHERE StoreSlug = %s
        ORDER BY Code
        """,
        (store,),
    )
    return list(cursor.fetchall())


def _upsert_sqlserver_products(cursor: object, store: str, staged_rows: list[tuple], category_ids: dict[str, int], supermarket_id: int) -> int:
    promoted_count = 0
    for row in staged_rows:
        import_run_id, code, product_name, ingredients_text, brands, origins, countries_en, image_url, image_small_url, price = row
        cursor.execute("SELECT ProductId FROM off_product_links WHERE StoreSlug = ? AND Code = ?", store, code)
        link_row = cursor.fetchone()
        prepared = _prepare_product_payload(
            code=code,
            product_name=product_name,
            ingredients_text=ingredients_text,
            brands=brands,
            origins=origins,
            countries_en=countries_en,
            image_url=image_url,
            image_small_url=image_small_url,
            price=price,
        )
        category_id = category_ids.get(str(prepared["category_slug"]), category_ids[IMPORTED_CATEGORY_SLUG])
        if link_row:
            product_id = int(link_row[0])
            cursor.execute(
                """
                UPDATE products
                SET Name = ?, Description = ?, Price = CASE WHEN ? > 0 THEN ? ELSE Price END, ImageUrl = ?, CategoryId = ?, SupermarketId = ?, Unit = ?, Stock = ?, IsAvailable = ?
                WHERE Id = ?
                """,
                prepared["name"],
                prepared["description"],
                prepared["price"],
                prepared["price"],
                prepared["image_url"],
                category_id,
                supermarket_id,
                None,
                None,
                1,
                product_id,
            )
            cursor.execute("UPDATE off_product_links SET LastImportRunId = ? WHERE StoreSlug = ? AND Code = ?", import_run_id, store, code)
        else:
            cursor.execute(
                """
                INSERT INTO products (Name, Description, Price, ImageUrl, CategoryId, SupermarketId, Unit, Stock, IsAvailable)
                OUTPUT INSERTED.Id
                VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
                """,
                prepared["name"],
                prepared["description"],
                prepared["price"],
                prepared["image_url"],
                category_id,
                supermarket_id,
                None,
                None,
                1,
            )
            product_id = int(cursor.fetchone()[0])
            cursor.execute("INSERT INTO off_product_links (StoreSlug, Code, ProductId, LastImportRunId) VALUES (?, ?, ?, ?)", store, code, product_id, import_run_id)
        promoted_count += 1
    return promoted_count


def _upsert_mysql_products(cursor: object, store: str, staged_rows: list[tuple], category_ids: dict[str, int], supermarket_id: int) -> int:
    promoted_count = 0
    for row in staged_rows:
        import_run_id, code, product_name, ingredients_text, brands, origins, countries_en, image_url, image_small_url, price = row
        cursor.execute("SELECT ProductId FROM off_product_links WHERE StoreSlug = %s AND Code = %s", (store, code))
        link_row = cursor.fetchone()
        prepared = _prepare_product_payload(
            code=code,
            product_name=product_name,
            ingredients_text=ingredients_text,
            brands=brands,
            origins=origins,
            countries_en=countries_en,
            image_url=image_url,
            image_small_url=image_small_url,
            price=price,
        )
        category_id = category_ids.get(str(prepared["category_slug"]), category_ids[IMPORTED_CATEGORY_SLUG])
        if link_row:
            product_id = int(link_row[0])
            cursor.execute(
                """
                UPDATE products
                SET Name = %s, Description = %s, Price = CASE WHEN %s > 0 THEN %s ELSE Price END, ImageUrl = %s, CategoryId = %s, SupermarketId = %s, Unit = %s, Stock = %s, IsAvailable = %s
                WHERE Id = %s
                """,
                (prepared["name"], prepared["description"], prepared["price"], prepared["price"], prepared["image_url"], category_id, supermarket_id, None, None, 1, product_id),
            )
            cursor.execute("UPDATE off_product_links SET LastImportRunId = %s WHERE StoreSlug = %s AND Code = %s", (import_run_id, store, code))
        else:
            cursor.execute(
                """
                INSERT INTO products (Name, Description, Price, ImageUrl, CategoryId, SupermarketId, Unit, Stock, IsAvailable)
                VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s)
                """,
                (prepared["name"], prepared["description"], prepared["price"], prepared["image_url"], category_id, supermarket_id, None, None, 1),
            )
            product_id = int(cursor.lastrowid)
            cursor.execute("INSERT INTO off_product_links (StoreSlug, Code, ProductId, LastImportRunId) VALUES (%s, %s, %s, %s)", (store, code, product_id, import_run_id))
        promoted_count += 1
    return promoted_count


def _prepare_product_payload(
    *,
    code: str,
    product_name: str | None,
    ingredients_text: str | None,
    brands: str | None,
    origins: str | None,
    countries_en: str | None,
    image_url: str | None,
    image_small_url: str | None,
    price: object,
) -> dict[str, object]:
    category_slug = _classify_category_slug(product_name, ingredients_text, brands, origins, countries_en)
    return {
        "name": _truncate_text(product_name or code, 220) or code,
        "description": _truncate_text(_first_nonempty(ingredients_text, brands, origins, countries_en), 1000),
        "price": float(price) if price is not None else DEFAULT_PRICE,
        "image_url": _truncate_text(_first_nonempty(image_url, image_small_url), 1000),
        "category_slug": category_slug,
    }


def _classify_category_slug(*values: object) -> str:
    haystack = " ".join(_to_text(value).lower() for value in values if value is not None)
    normalized = " ".join(haystack.replace("/", " ").replace("-", " ").replace(",", " ").split())

    for slug, keywords in CATEGORY_KEYWORDS:
        if any(keyword in normalized for keyword in keywords):
            return slug

    return IMPORTED_CATEGORY_SLUG


def _first_nonempty(*values: object) -> str:
    for value in values:
        text = _to_text(value).strip()
        if text:
            return text
    return ""


def _truncate_text(value: str, limit: int) -> str | None:
    if not value:
        return None
    return value[:limit]


def _resolve_latest_file_metadata(store_dir: Path, store: str) -> tuple[Path | None, datetime | None, int]:
    latest = store_dir / f"openfoodfacts_{store}_latest.json"
    candidate = latest if latest.exists() else None
    if candidate is None and store_dir.exists():
        snapshots = sorted(store_dir.glob(f"openfoodfacts_{store}_*.json"))
        candidate = snapshots[-1] if snapshots else None

    if candidate is None:
        return None, None, 0

    stat = candidate.stat()
    modified_utc = datetime.fromtimestamp(stat.st_mtime, tz=UTC)
    return candidate.resolve(), modified_utc, stat.st_size


def _get_sqlserver_statuses(
    *,
    stores: list[str],
    latest_files: dict[str, tuple[Path | None, datetime | None, int]],
    connection_string: str,
) -> list[StoreStatus]:
    import pyodbc

    sqlserver_connection_string = _to_sqlserver_odbc_connection_string(connection_string, pyodbc.drivers())
    statuses: list[StoreStatus] = []
    with pyodbc.connect(sqlserver_connection_string) as connection:
        cursor = connection.cursor()
        for store in stores:
            latest_file, modified_utc, size_bytes = latest_files[store]
            cursor.execute("SELECT COUNT(*) FROM off_store_products WHERE StoreSlug = ?", store)
            staged_count = int(cursor.fetchone()[0])
            cursor.execute("SELECT TOP 1 Id FROM off_import_runs WHERE StoreSlug = ? ORDER BY Id DESC", store)
            import_row = cursor.fetchone()
            latest_import_run_id = int(import_row[0]) if import_row else None
            cursor.execute("SELECT COUNT(*) FROM off_product_links WHERE StoreSlug = ?", store)
            promoted_count = int(cursor.fetchone()[0])
            cursor.execute("SELECT TOP 1 LastImportRunId FROM off_product_links WHERE StoreSlug = ? AND LastImportRunId IS NOT NULL ORDER BY LastImportRunId DESC", store)
            promotion_row = cursor.fetchone()
            latest_promoted_run_id = int(promotion_row[0]) if promotion_row else None
            statuses.append(
                StoreStatus(
                    store=store,
                    provider="sqlserver",
                    latest_file=latest_file,
                    latest_file_modified_utc=modified_utc,
                    latest_file_size_bytes=size_bytes,
                    staged_row_count=staged_count,
                    promoted_row_count=promoted_count,
                    latest_import_run_id=latest_import_run_id,
                    latest_promoted_run_id=latest_promoted_run_id,
                )
            )
    return statuses


def _get_mysql_statuses(
    *,
    stores: list[str],
    latest_files: dict[str, tuple[Path | None, datetime | None, int]],
    connection_string: str,
) -> list[StoreStatus]:
    import mysql.connector

    settings = _parse_connection_string(connection_string)
    connection = mysql.connector.connect(
        host=settings.get("server") or settings.get("host") or "localhost",
        port=int(settings.get("port") or 3306),
        database=settings.get("database"),
        user=settings.get("user id") or settings.get("user") or settings.get("uid"),
        password=settings.get("password") or settings.get("pwd"),
        ssl_disabled=(settings.get("sslmode", "").lower() in {"none", "disabled", "disable"}),
    )
    statuses: list[StoreStatus] = []
    try:
        cursor = connection.cursor()
        for store in stores:
            latest_file, modified_utc, size_bytes = latest_files[store]
            cursor.execute("SELECT COUNT(*) FROM off_store_products WHERE StoreSlug = %s", (store,))
            staged_count = int(cursor.fetchone()[0])
            cursor.execute("SELECT Id FROM off_import_runs WHERE StoreSlug = %s ORDER BY Id DESC LIMIT 1", (store,))
            import_row = cursor.fetchone()
            latest_import_run_id = int(import_row[0]) if import_row else None
            cursor.execute("SELECT COUNT(*) FROM off_product_links WHERE StoreSlug = %s", (store,))
            promoted_count = int(cursor.fetchone()[0])
            cursor.execute("SELECT LastImportRunId FROM off_product_links WHERE StoreSlug = %s AND LastImportRunId IS NOT NULL ORDER BY LastImportRunId DESC LIMIT 1", (store,))
            promotion_row = cursor.fetchone()
            latest_promoted_run_id = int(promotion_row[0]) if promotion_row else None
            statuses.append(
                StoreStatus(
                    store=store,
                    provider="mysql",
                    latest_file=latest_file,
                    latest_file_modified_utc=modified_utc,
                    latest_file_size_bytes=size_bytes,
                    staged_row_count=staged_count,
                    promoted_row_count=promoted_count,
                    latest_import_run_id=latest_import_run_id,
                    latest_promoted_run_id=latest_promoted_run_id,
                )
            )
        return statuses
    finally:
        connection.close()