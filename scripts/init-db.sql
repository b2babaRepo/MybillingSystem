-- Runs once, when the Docker volume is first created. Development passwords only.
CREATE ROLE khata_app LOGIN PASSWORD 'khata_dev_password';
CREATE DATABASE grocery_khata OWNER khata_app;
