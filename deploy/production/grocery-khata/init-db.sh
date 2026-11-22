#!/bin/sh
# Runs once, on the database's first start. The app signs in as a normal role (not a superuser), so row-level
# security applies to it exactly as designed: each shop sees only its own rows.
set -e
psql -v ON_ERROR_STOP=1 -U postgres -v pw="$APP_DB_PASSWORD" <<'SQL'
CREATE ROLE khata_app LOGIN PASSWORD :'pw';
CREATE DATABASE grocery_khata OWNER khata_app;
SQL
