-- Database initialization script for SportsbookLite
-- This script runs automatically when the PostgreSQL container starts

-- Create additional database for testing if needed
-- CREATE DATABASE sportsbook_test;

-- Create extensions that might be needed
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- Grant permissions
GRANT ALL PRIVILEGES ON DATABASE sportsbook TO dev;

-- Set timezone
SET timezone = 'UTC';