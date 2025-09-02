CREATE DATABASE edu_bi;
CREATE EXTENSION IF NOT EXISTS pgcrypto;
CREATE EXTENSION IF NOT EXISTS pg_trgm;

-- Dimensões básicas
CREATE TABLE IF NOT EXISTS periodo_letivo (
  id INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  ano INTEGER NOT NULL,
  semestre INTEGER NOT NULL CHECK (semestre IN (1,2)),
  descricao TEXT
);

CREATE TABLE IF NOT EXISTS import_batch (
  id UUID PRIMARY KEY,
  created_at_utc TIMESTAMPTZ NOT NULL,
  original_file_name TEXT,
  storage_uri TEXT,
  status SMALLINT,
  error TEXT,
  file_hash TEXT,
  periodo_letivo_id INTEGER REFERENCES periodo_letivo(id) ON UPDATE CASCADE ON DELETE SET NULL
);

CREATE TABLE IF NOT EXISTS aluno (
  id INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  import_id UUID REFERENCES import_batch(id) ON UPDATE CASCADE ON DELETE SET NULL,
  nome TEXT NOT NULL,
  matricula VARCHAR(50),
  foto_path TEXT
);

CREATE TABLE IF NOT EXISTS disciplina (
  id INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  import_id UUID REFERENCES import_batch(id) ON UPDATE CASCADE ON DELETE SET NULL,
  sigla TEXT UNIQUE NOT NULL,
  nome_area TEXT,
  carga_horaria_rotulo TEXT
);

CREATE TABLE IF NOT EXISTS situacao (
  id INTEGER PRIMARY KEY,
  descricao TEXT NOT NULL
);
INSERT INTO situacao(id, descricao) VALUES
  (1,'APR'),(2,'REP'),(3,'CAN'),(4,'CUR'),(5,'OUT')
ON CONFLICT DO NOTHING;

CREATE TABLE IF NOT EXISTS periodo_avaliativo (
  id INTEGER PRIMARY KEY,
  nome TEXT NOT NULL,
  final BOOLEAN NOT NULL
);
INSERT INTO periodo_avaliativo(id, nome, final) VALUES
  (1,'1',false),(2,'2',false),(3,'3',false),(4,'4',false),(99,'Final',true)
ON CONFLICT DO NOTHING;

CREATE TABLE IF NOT EXISTS aluno_observacao (
  id INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  aluno_id INTEGER NOT NULL REFERENCES aluno(id) ON DELETE CASCADE,
  texto TEXT NOT NULL,
  criado_em_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  import_id UUID REFERENCES import_batch(id)
);

CREATE TABLE IF NOT EXISTS fato_nota (
  id INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  import_id UUID NOT NULL REFERENCES import_batch(id) ON DELETE CASCADE,
  aluno_id INTEGER NOT NULL REFERENCES aluno(id) ON DELETE CASCADE,
  disciplina_id INTEGER NOT NULL REFERENCES disciplina(id) ON DELETE RESTRICT,
  periodo_avaliativo_id INTEGER NOT NULL REFERENCES periodo_avaliativo(id) ON DELETE RESTRICT,
  situacao_id INTEGER REFERENCES situacao(id),
  periodo_letivo_id INTEGER NOT NULL REFERENCES periodo_letivo(id) ON DELETE RESTRICT,
  nota NUMERIC(5,2),
  frequencia NUMERIC(5,2)
);

CREATE TABLE IF NOT EXISTS aluno_status_import (
  id INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  import_id UUID NOT NULL REFERENCES import_batch(id) ON DELETE CASCADE,
  aluno_id INTEGER NOT NULL REFERENCES aluno(id) ON DELETE CASCADE,
  periodo_letivo_id INTEGER REFERENCES periodo_letivo(id),
  frequencia_geral NUMERIC(5,2),
  situacao_curso TEXT,
  criado_em_utc TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Status por etapa (para UI e concorrência-safe)
CREATE TABLE IF NOT EXISTS import_stage (
  id               INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  import_id        UUID NOT NULL REFERENCES import_batch(id) ON DELETE CASCADE,
  etapa_id         INTEGER,
  name             TEXT NOT NULL,
  status           SMALLINT NOT NULL, -- 1=Pendente, 2=Processando, 3=Finalizado, 4=Erro
  started_at_utc   TIMESTAMPTZ,
  finished_at_utc  TIMESTAMPTZ,
  error            TEXT
);

CREATE INDEX IF NOT EXISTS idx_import_stage_import ON import_stage(import_id);
CREATE INDEX IF NOT EXISTS idx_import_stage_import_etapa ON import_stage(import_id, etapa_id);

ALTER TABLE import_stage ADD COLUMN IF NOT EXISTS status SMALLINT NOT NULL DEFAULT 1;
ALTER TABLE import_stage ADD COLUMN IF NOT EXISTS started_at_utc TIMESTAMPTZ NULL;
ALTER TABLE import_stage ADD COLUMN IF NOT EXISTS finished_at_utc TIMESTAMPTZ NULL;
ALTER TABLE import_stage ADD COLUMN IF NOT EXISTS error TEXT NULL;
ALTER TABLE import_stage ADD COLUMN IF NOT EXISTS name TEXT NOT NULL DEFAULT '';
ALTER TABLE import_stage ADD COLUMN IF NOT EXISTS etapa_id INTEGER NULL;

ALTER TABLE import_stage ALTER COLUMN status DROP DEFAULT;
ALTER TABLE import_stage ALTER COLUMN name DROP DEFAULT;

-- Índices de performance e idempotência
CREATE UNIQUE INDEX IF NOT EXISTS ux_disciplina_sigla ON disciplina(sigla);
CREATE INDEX IF NOT EXISTS ix_fato_import  ON fato_nota(import_id);
CREATE INDEX IF NOT EXISTS ix_fato_aluno   ON fato_nota(aluno_id);
CREATE INDEX IF NOT EXISTS ix_fato_disc    ON fato_nota(disciplina_id);
CREATE INDEX IF NOT EXISTS ix_fato_periodo ON fato_nota(periodo_letivo_id);
CREATE INDEX IF NOT EXISTS ix_fato_etapa   ON fato_nota(periodo_avaliativo_id);

-- (Opcional) Evitar duplicatas por import+aluno+disciplina+etapa
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'ux_fato_unico'
    ) THEN
        ALTER TABLE fato_nota
        ADD CONSTRAINT ux_fato_unico UNIQUE (import_id, aluno_id, disciplina_id, periodo_avaliativo_id);
    END IF;
END $$;
