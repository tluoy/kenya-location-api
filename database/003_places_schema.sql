CREATE TABLE IF NOT EXISTS places (
    id text PRIMARY KEY,
    name text NOT NULL,
    normalized_name text NOT NULL,
    place_type text NOT NULL,
    centroid geometry(Point,4326) NOT NULL,
    geometry geometry(MultiPolygon,4326) NOT NULL,

    admin_unit_id text REFERENCES admin_units(id),

    source text NOT NULL,
    source_version text NOT NULL,

    attributes jsonb NOT NULL DEFAULT '{}'::jsonb,

    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT places_place_type_check
        CHECK (place_type IN ('urban_area', 'settlement'))
);

CREATE INDEX IF NOT EXISTS idx_places_geometry
    ON places USING GIST (geometry);

CREATE INDEX IF NOT EXISTS idx_places_centroid
    ON places USING GIST (centroid);

CREATE INDEX IF NOT EXISTS idx_places_name_trgm
    ON places USING GIN (normalized_name gin_trgm_ops);

CREATE INDEX IF NOT EXISTS idx_places_type
    ON places (place_type);

CREATE INDEX IF NOT EXISTS idx_places_admin_unit
    ON places (admin_unit_id);