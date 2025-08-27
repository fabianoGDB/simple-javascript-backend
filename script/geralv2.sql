-- ============================
-- EXTENSÕES
-- ============================
CREATE EXTENSION IF NOT EXISTS pgcrypto;     -- gen_random_uuid()
CREATE EXTENSION IF NOT EXISTS pg_trgm;      -- índice GIN para busca por nome (trigram)

-- ============================
-- 1) Dimensões / Apoio
-- ============================

CREATE TABLE periodo_letivo (
    id                INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    ano               INTEGER NOT NULL,
    semestre          INTEGER NOT NULL CHECK (semestre IN (1,2)),
    descricao         TEXT
);

CREATE TABLE import_batch (
    id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    created_at_utc    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    file_name         TEXT,
    periodo_letivo_id INTEGER REFERENCES periodo_letivo(id) ON UPDATE CASCADE ON DELETE SET NULL
);

CREATE TABLE import_sheet (
    id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    import_id         UUID NOT NULL REFERENCES import_batch(id) ON UPDATE CASCADE ON DELETE CASCADE,
    name              TEXT NOT NULL,
    created_at_utc    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_import_sheet UNIQUE (import_id, name)
);

CREATE TABLE aluno (
    id                INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    import_id         UUID,                    -- lote que criou este registro (pode ser NULL)
    nome              TEXT,
    matricula         VARCHAR(50),             -- pode ser nula
    frequencia_geral  NUMERIC(5,2),            -- % (0..100)
    situacao_curso    TEXT,                    -- Matriculado, Evasão, Cancelado, etc.
    foto_path         TEXT
);
CREATE INDEX idx_aluno_import     ON aluno(import_id);
CREATE INDEX idx_aluno_matricula  ON aluno(matricula);
CREATE INDEX idx_aluno_nome_trgm  ON aluno USING GIN (nome gin_trgm_ops);

CREATE TABLE aluno_observacao (
    id                INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    aluno_id          INTEGER NOT NULL REFERENCES aluno(id) ON UPDATE CASCADE ON DELETE CASCADE,
    texto             TEXT NOT NULL,
    criado_em_utc     TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE curso (
    id                INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    import_id         UUID,
    sigla             VARCHAR(50) NOT NULL,
    descricao         TEXT,
    CONSTRAINT uq_curso_sigla UNIQUE (sigla)
);
CREATE INDEX idx_curso_import ON curso(import_id);

CREATE TABLE disciplina (
    id                    INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    import_id             UUID,
    sigla                 VARCHAR(50) NOT NULL,
    nome_area             TEXT,
    carga_horaria_rotulo  TEXT,
    CONSTRAINT uq_disc_sigla UNIQUE (sigla)
);
CREATE INDEX idx_disc_import ON disciplina(import_id);

CREATE TABLE bimestre (
    id    INTEGER PRIMARY KEY,
    nome  TEXT NOT NULL
);

CREATE TABLE etapa (
    id    INTEGER PRIMARY KEY,
    nome  TEXT NOT NULL
);

CREATE TABLE situacao (
    id         INTEGER PRIMARY KEY,
    descricao  TEXT NOT NULL
);

-- ============================
-- 2) Fato
-- ============================

CREATE TABLE fato_nota (
    id                 INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    import_id          UUID     NOT NULL REFERENCES import_batch(id) ON UPDATE CASCADE ON DELETE RESTRICT,
    aluno_id           INTEGER  NOT NULL REFERENCES aluno(id)        ON UPDATE CASCADE ON DELETE RESTRICT,
    disciplina_id      INTEGER  NOT NULL REFERENCES disciplina(id)   ON UPDATE CASCADE ON DELETE RESTRICT,
    bimestre_id        INTEGER  NOT NULL REFERENCES bimestre(id)     ON UPDATE CASCADE ON DELETE RESTRICT,
    etapa_id           INTEGER  NOT NULL REFERENCES etapa(id)        ON UPDATE CASCADE ON DELETE RESTRICT,
    curso_id           INTEGER      REFERENCES curso(id)             ON UPDATE CASCADE ON DELETE SET NULL,
    situacao_id        INTEGER      REFERENCES situacao(id)          ON UPDATE CASCADE ON DELETE SET NULL,
    periodo_letivo_id  INTEGER      REFERENCES periodo_letivo(id)    ON UPDATE CASCADE ON DELETE SET NULL,
    nota               NUMERIC(4,2),  -- 0..10 (use CHECK abaixo se quiser)
    frequencia         NUMERIC(5,2)   -- % (0..100)
    -- , CONSTRAINT ck_nota_range CHECK (nota IS NULL OR (nota >= 0 AND nota <= 10))
    -- , CONSTRAINT ck_freq_range CHECK (frequencia IS NULL OR (frequencia >= 0 AND frequencia <= 100))
);

CREATE INDEX idx_fato_import        ON fato_nota(import_id);
CREATE INDEX idx_fato_aluno         ON fato_nota(aluno_id);
CREATE INDEX idx_fato_disciplina    ON fato_nota(disciplina_id);
CREATE INDEX idx_fato_bimestre      ON fato_nota(bimestre_id);
CREATE INDEX idx_fato_etapa         ON fato_nota(etapa_id);
CREATE INDEX idx_fato_situacao      ON fato_nota(situacao_id);
CREATE INDEX idx_fato_periodo       ON fato_nota(periodo_letivo_id);
CREATE INDEX idx_fato_aluno_periodo ON fato_nota(aluno_id, periodo_letivo_id);
CREATE INDEX idx_fato_lookup        ON fato_nota(import_id, aluno_id, disciplina_id, etapa_id);

-- ============================
-- 3) Status de processamento por aba
-- ============================

-- Se preferir, troque INTEGER por ENUM (ex.: 'processando'|'concluido'|'erro').
CREATE TABLE status_processamento_planilha (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    import_sheet_id     UUID NOT NULL REFERENCES import_sheet(id) ON UPDATE CASCADE ON DELETE CASCADE,
    status              INTEGER NOT NULL CHECK (status IN (0,1,2)),  -- 0=processando, 1=concluído, 2=erro
    ultima_atualizacao  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_status_planilha UNIQUE (import_sheet_id)
);
CREATE INDEX idx_status_sheet ON status_processamento_planilha(import_sheet_id);

-- gatilho para atualizar 'ultima_atualizacao' em updates
CREATE OR REPLACE FUNCTION set_status_update_timestamp()
RETURNS TRIGGER AS $$
BEGIN
  NEW.ultima_atualizacao := NOW();
  RETURN NEW;
END; $$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_status_touch ON status_processamento_planilha;
CREATE TRIGGER trg_status_touch
BEFORE UPDATE ON status_processamento_planilha
FOR EACH ROW EXECUTE FUNCTION set_status_update_timestamp();

-- ============================
-- 4) Seeds
-- ============================

INSERT INTO bimestre (id, nome) VALUES
    (1,'1º'),(2,'2º'),(3,'3º'),(4,'4º')
ON CONFLICT (id) DO NOTHING;

INSERT INTO etapa (id, nome) VALUES
    (1,'Etapa 1'),(2,'Etapa 2'),(3,'Etapa 3'),(4,'Etapa 4'),(99,'Etapa Final')
ON CONFLICT (id) DO NOTHING;

INSERT INTO situacao (id, descricao) VALUES
    (1,'APR'),(2,'REP'),(3,'CAN'),(4,'CUR'),(5,'OUT')
ON CONFLICT (id) DO NOTHING;

-- ============================
-- 5) Views úteis (opcional)
-- ============================

-- Resumo por import
CREATE OR REPLACE VIEW vw_import_resumo AS
SELECT
  i.id                AS import_id,
  i.created_at_utc,
  i.file_name,
  pl.descricao        AS periodo,
  COUNT(DISTINCT fn.aluno_id)      AS alunos,
  COUNT(DISTINCT fn.disciplina_id) AS disciplinas,
  COUNT(fn.id)                      AS notas
FROM import_batch i
LEFT JOIN fato_nota fn       ON fn.import_id = i.id
LEFT JOIN periodo_letivo pl  ON pl.id = i.periodo_letivo_id
GROUP BY i.id, i.created_at_utc, i.file_name, pl.descricao;

-- Médias por bimestre do aluno
CREATE OR REPLACE VIEW vw_aluno_media_bimestre AS
SELECT
  fn.aluno_id,
  fn.bimestre_id,
  ROUND(AVG(COALESCE(fn.nota,0))::numeric, 2) AS media
FROM fato_nota fn
GROUP BY fn.aluno_id, fn.bimestre_id;
