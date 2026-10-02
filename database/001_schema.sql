CREATE EXTENSION IF NOT EXISTS postgis;
CREATE EXTENSION IF NOT EXISTS pg_trgm;

CREATE TABLE IF NOT EXISTS admin_units (
    id TEXT PRIMARY KEY,
    code TEXT,
    name TEXT NOT NULL,
    normalized_name TEXT NOT NULL,
    level INTEGER NOT NULL,
    level_name TEXT NOT NULL,
    parent_id TEXT NULL REFERENCES admin_units(id),
    centroid geometry(Point, 4326) NOT NULL,
    geometry geometry(MultiPolygon, 4326) NOT NULL,
    source TEXT NOT NULL,
    source_version TEXT NOT NULL,
    attributes JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_admin_units_geometry ON admin_units USING GIST(geometry);
CREATE INDEX IF NOT EXISTS idx_admin_units_centroid ON admin_units USING GIST(centroid);
CREATE INDEX IF NOT EXISTS idx_admin_units_name_trgm ON admin_units USING GIN(normalized_name gin_trgm_ops);
CREATE INDEX IF NOT EXISTS idx_admin_units_level ON admin_units(level);
CREATE INDEX IF NOT EXISTS idx_admin_units_parent ON admin_units(parent_id);
