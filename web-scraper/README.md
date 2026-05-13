# Web Scraper

This project now uses the official Open Food Facts and Open Beauty Facts bulk exports as the first source for store-linked product data.

## Why this approach

- Direct REWE scraping was unstable because of anti-bot protection.
- Open Food Facts recommends using their exports for large-scale reuse instead of bulk-calling the live API.
- Filtering the official export by store name gives a stable first ingestion path.

## What it does

- streams the official Open Food Facts or Open Beauty Facts compressed CSV export
- filters rows whose `stores` field contains grocery store slugs such as `rewe`, `aldi`, `edeka`, `penny`, `lidl` or beauty store slugs such as `dm`, `rossmann`
- keeps only the requested fields in the output
- writes the filtered rows to JSON, JSONL, or CSV

## Quick start

```powershell
cd web-scraper
python main.py off-store --store rewe --max-matches 20 --format all
```

For beauty products:

```powershell
python main.py off-store --catalog beauty --store dm --max-matches 20 --format all
```

## Example commands

Smoke test the first matching REWE rows:

```powershell
python main.py off-store --store rewe --max-matches 20 --format all --verbose
```

Fetch more REWE-linked rows:

```powershell
python main.py off-store --store rewe --max-matches 1000 --format json
```

Fetch every REWE-linked row from the export:

```powershell
python main.py off-store --store rewe --format json
```

This writes the full REWE result set into:

- `data/rewe/openfoodfacts_rewe_YYYYMMDD_HHMMSS.json`
- `data/rewe/openfoodfacts_rewe_latest.json`

Other supported stores:

```powershell
python main.py off-store --store aldi --max-matches 100 --format all
python main.py off-store --store edeka --max-matches 100 --format all
python main.py off-store --store penny --max-matches 100 --format all
python main.py off-store --store lidl --max-matches 100 --format all
```

Beauty stores:

```powershell
python main.py off-store --catalog beauty --store dm --max-matches 100 --format all
python main.py off-store --catalog beauty --store rossmann --max-matches 100 --format all
```

## Output

Files are written under store-specific folders in `web-scraper/data/`.

Generated filenames look like:

- `data/rewe/openfoodfacts_rewe_YYYYMMDD_HHMMSS.json`
- `data/rewe/openfoodfacts_rewe_latest.json`
- `data/aldi/openfoodfacts_aldi_YYYYMMDD_HHMMSS.json`
- `data/aldi/openfoodfacts_aldi_latest.json`
- `data/edeka/openfoodfacts_edeka_YYYYMMDD_HHMMSS.json`
- `data/penny/openfoodfacts_penny_YYYYMMDD_HHMMSS.json`
- `data/lidl/openfoodfacts_lidl_YYYYMMDD_HHMMSS.json`
- `data/dm/openbeautyfacts_dm_YYYYMMDD_HHMMSS.json`
- `data/dm/openbeautyfacts_dm_latest.json`
- `data/rossmann/openbeautyfacts_rossmann_YYYYMMDD_HHMMSS.json`
- `data/rossmann/openbeautyfacts_rossmann_latest.json`

The `latest` files are intended to be the stable handoff point for your later SQL Server or Azure MySQL import jobs, while the timestamped files keep a historical snapshot.

## Import JSON into database staging tables

The importer loads a store's `latest.json` into two database tables:

- `off_import_runs`: one row per import execution
- `off_store_products`: the latest imported snapshot for each store

Imported rows currently get a default `Price` value of `0.00` in the database staging table because Open Food Facts does not provide the store price data you want yet.

The same `Price = 0.00` default is applied to Open Beauty Facts imports for `dm` and `rossmann`.

## Sync all stores

To export every supported store and import all `latest.json` files into the staging tables in one run:

```powershell
python main.py sync-all-stores --verbose
```

To do the same and also promote the staged rows into the app `products` table:

```powershell
python main.py sync-all-stores --promote-to-app --verbose
```

Beauty catalog variant:

```powershell
python main.py sync-all-stores --catalog beauty --promote-to-app --verbose
```

## Promote staged rows into app products

This command reads from `off_store_products` and upserts rows into the app `products` table with:

- default `Price = 0.00`
- an auto-created fallback category `imported-products`
- missing supermarket rows created automatically, including `edeka`
- stable deduplication via the `off_product_links` mapping table

Promote one store:

```powershell
python main.py promote-staged-products --store rewe --verbose
```

Promote all staged stores:

```powershell
python main.py promote-staged-products --store all --verbose
```

Dry run without writing app products:

```powershell
python main.py promote-staged-products --store all --dry-run --verbose
```

## Status

To see the current latest file, last staging import, and last product promotion per store:

```powershell
python main.py status --store all --verbose
```

To inspect a single store:

```powershell
python main.py status --store rewe --verbose
```

Beauty catalog status:

```powershell
python main.py status --catalog beauty --store dm --verbose
```

By default it reads your backend database config from `../shorman-api/ShormanServicesBackend/appsettings.Development.Local.json`.

Dry run against the configured local database settings:

```powershell
python main.py import-store-json --store rewe --dry-run --verbose
```

Beauty catalog dry run:

```powershell
python main.py import-store-json --catalog beauty --store dm --dry-run --verbose
```

Run the real import for the current REWE latest file:

```powershell
python main.py import-store-json --store rewe --verbose
```

Import another store the same way:

```powershell
python main.py import-store-json --store aldi --verbose
python main.py import-store-json --store edeka --verbose
python main.py import-store-json --store lidl --verbose
python main.py import-store-json --store penny --verbose
```

Import beauty products the same way:

```powershell
python main.py import-store-json --catalog beauty --store dm --verbose
python main.py import-store-json --catalog beauty --store rossmann --verbose
```

```
python main.py off-store --store rewe --format json --verbose
python main.py import-store-json --store rewe --verbose

python main.py off-store --store aldi --format json --verbose
python main.py import-store-json --store aldi --verbose

python main.py off-store --store edeka --format json --verbose
python main.py import-store-json --store edeka --verbose

python main.py off-store --store lidl --format json --verbose
python main.py import-store-json --store lidl --verbose

python main.py off-store --store penny --format json --verbose
python main.py import-store-json --store penny --verbose

python main.py off-store --catalog beauty --store dm --format json --verbose
python main.py import-store-json --catalog beauty --store dm --verbose

python main.py off-store --catalog beauty --store rossmann --format json --verbose
python main.py import-store-json --catalog beauty --store rossmann --verbose
```

You can also override the source JSON or database settings explicitly:

```powershell
python main.py import-store-json --store rewe --input-file data/rewe/openfoodfacts_rewe_latest.json --provider sqlserver --connection-string "Server=localhost,1433;Database=HS_DB_DELIVERY_SERVICE;User ID=sa;Password=YourStrong!Passw0rd;Encrypt=False;"
```

Beauty catalog override example:

```powershell
python main.py import-store-json --catalog beauty --store dm --input-file data/dm/openbeautyfacts_dm_latest.json --provider sqlserver --connection-string "Server=localhost,1433;Database=HS_DB_DELIVERY_SERVICE;User ID=sa;Password=YourStrong!Passw0rd;Encrypt=False;"
```

## Azure plan

For Azure Flexible MySQL, the practical approach is to run this scraper as a scheduled job rather than manually.

Recommended setup:

1. Package `web-scraper` as a Docker image.
2. Run it on a schedule with Azure Container Apps Jobs.
3. Store the MySQL connection string as a secret.
4. Execute the scraper with `--provider mysql --connection-string ...`.

Build the image locally:

```powershell
docker build -t shorman-web-scraper .
```

Run it locally against MySQL the same way Azure would run it:

```powershell
docker run --rm shorman-web-scraper sync-all-stores --provider mysql --connection-string "Server=<host>;Port=3306;Database=<db>;User ID=<user>;Password=<password>;SslMode=Required;" --promote-to-app --verbose
```

Beauty catalog variant:

```powershell
docker run --rm shorman-web-scraper sync-all-stores --catalog beauty --provider mysql --connection-string "Server=<host>;Port=3306;Database=<db>;User ID=<user>;Password=<password>;SslMode=Required;" --promote-to-app --verbose
```

Example scheduled job command inside Azure:

```powershell
python main.py sync-all-stores --provider mysql --connection-string "Server=<host>;Port=3306;Database=<db>;User ID=<user>;Password=<password>;SslMode=Required;" --promote-to-app --verbose
```

Beauty catalog variant:

```powershell
python main.py sync-all-stores --catalog beauty --provider mysql --connection-string "Server=<host>;Port=3306;Database=<db>;User ID=<user>;Password=<password>;SslMode=Required;" --promote-to-app --verbose
```

Example Azure CLI flow:

```powershell
$RESOURCE_GROUP = "rg-shorman"
$LOCATION = "westeurope"
$ACR_NAME = "shormanacr"
$ENV_NAME = "shorman-jobs-env"
$JOB_NAME = "shorman-openfoodfacts-sync"
$IMAGE_NAME = "web-scraper:latest"
$MYSQL_CONN = "Server=<host>;Port=3306;Database=<db>;User ID=<user>;Password=<password>;SslMode=Required;"

az group create --name $RESOURCE_GROUP --location $LOCATION
az acr create --resource-group $RESOURCE_GROUP --name $ACR_NAME --sku Basic
az acr build --registry $ACR_NAME --image $IMAGE_NAME .
az containerapp env create --name $ENV_NAME --resource-group $RESOURCE_GROUP --location $LOCATION
az containerapp job create --name $JOB_NAME --resource-group $RESOURCE_GROUP --environment $ENV_NAME --trigger-type Schedule --cron-expression "0 3 * * *" --replica-timeout 7200 --replica-retry-limit 1 --parallelism 1 --replica-completion-count 1 --image "$ACR_NAME.azurecr.io/$IMAGE_NAME" --registry-server "$ACR_NAME.azurecr.io" --cpu 1.0 --memory 2.0Gi --secrets mysql-conn="$MYSQL_CONN" --command python --args main.py sync-all-stores --provider mysql --connection-string secretref:mysql-conn --promote-to-app --verbose
az containerapp job start --name $JOB_NAME --resource-group $RESOURCE_GROUP
```

If you only want status from Azure after a run, use the same image with:

```powershell
python main.py status --store all --provider mysql --connection-string secretref:mysql-conn --verbose
```

That gives you one scheduled pipeline on Azure:

- pull from Open Food Facts or Open Beauty Facts
- write store JSON snapshots
- import into `off_store_products`
- promote into the app `products` table

## Notes

- This project follows Open Food Facts guidance more closely by using the official export rather than bulk-calling the live API.
- The exported rows currently include only these fields:
	`code`, `url`, `creator`, `created_datetime`, `last_modified_datetime`, `last_updated_datetime`, `product_name`, `brands`, `origins`, `stores`, `countries`, `countries_tags`, `countries_en`, `ingredients_text`, `ingredients_tags`, `ingredients_analysis_tags`, `image_url`, `image_small_url`