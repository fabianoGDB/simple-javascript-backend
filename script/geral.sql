
CREATE EXTENSION IF NOT EXISTS pg_trgm;

-- =========================================
-- 1) Tabelas de apoio (dimensões estáveis)
-- =========================================

-- Período letivo (ex.: 1º/2025)
CREATE TABLE periodo_letivo (
    id               INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    ano              INTEGER NOT NULL,
    semestre         INTEGER NOT NULL CHECK (semestre IN (1,2)),
    descricao        TEXT
);

-- Lote de importação (GUID por upload)
CREATE TABLE import_batch (
    id               UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    created_at_utc   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    file_name        TEXT,
    periodo_letivo_id INTEGER REFERENCES periodo_letivo(id) ON UPDATE CASCADE ON DELETE SET NULL
);

-- Aba/planilha processada dentro do lote
CREATE TABLE import_sheet (
    id               UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    import_id        UUID NOT NULL REFERENCES import_batch(id) ON UPDATE CASCADE ON DELETE CASCADE,
    name             TEXT NOT NULL,
    created_at_utc   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_import_sheet UNIQUE (import_id, name)
);

-- Aluno
CREATE TABLE aluno (
    id               INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    import_id        UUID,                    -- lote que criou este registro (pode ser NULL em cargas antigas)
    nome             TEXT,
    matricula        VARCHAR(50),             -- pode ser nula
    frequencia_geral NUMERIC(5,2),            -- % (0..100)
    situacao_curso   TEXT,                    -- Matriculado, Evasão, Cancelado, etc.
    foto_path        TEXT
);
CREATE INDEX idx_aluno_import    ON aluno(import_id);
CREATE INDEX idx_aluno_matricula ON aluno(matricula);
CREATE INDEX idx_aluno_nome_trgm ON aluno USING GIN (nome gin_trgm_ops);  -- opcional (necessário pg_trgm)

-- Observações por aluno
CREATE TABLE aluno_observacao (
    id               INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    aluno_id         INTEGER NOT NULL REFERENCES aluno(id) ON UPDATE CASCADE ON DELETE CASCADE,
    texto            TEXT NOT NULL,
    criado_em_utc    TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Curso (ex.: código do itinerário/turma)
CREATE TABLE curso (
    id               INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    import_id        UUID,
    sigla            VARCHAR(50) NOT NULL,
    descricao        TEXT,
    CONSTRAINT uq_curso_sigla UNIQUE (sigla)
);
CREATE INDEX idx_curso_import ON curso(import_id);

-- Disciplina (podemos reutilizar a mesma sigla do cabeçalho)
CREATE TABLE disciplina (
    id                   INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    import_id            UUID,
    sigla                VARCHAR(50) NOT NULL,
    nome_area            TEXT,
    carga_horaria_rotulo TEXT,
    CONSTRAINT uq_disc_sigla UNIQUE (sigla)
);
CREATE INDEX idx_disc_import ON disciplina(import_id);

-- Bimestre
CREATE TABLE bimestre (
    id    INTEGER PRIMARY KEY,
    nome  TEXT NOT NULL
);

-- Etapa (1..4 e 99=Final)
CREATE TABLE etapa (
    id    INTEGER PRIMARY KEY,
    nome  TEXT NOT NULL
);

-- Situação (APR/REP/CAN/CUR/OUT)
CREATE TABLE situacao (
    id         INTEGER PRIMARY KEY,
    descricao  TEXT NOT NULL
);

-- =========================================
-- 2) Fato
-- =========================================

CREATE TABLE fato_nota (
    id               INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    import_id        UUID NOT NULL REFERENCES import_batch(id) ON UPDATE CASCADE ON DELETE RESTRICT,
    aluno_id         INTEGER NOT NULL REFERENCES aluno(id)      ON UPDATE CASCADE ON DELETE RESTRICT,
    disciplina_id    INTEGER NOT NULL REFERENCES disciplina(id) ON UPDATE CASCADE ON DELETE RESTRICT,
    bimestre_id      INTEGER NOT NULL REFERENCES bimestre(id)   ON UPDATE CASCADE ON DELETE RESTRICT,
    etapa_id         INTEGER NOT NULL REFERENCES etapa(id)      ON UPDATE CASCADE ON DELETE RESTRICT,
    curso_id         INTEGER REFERENCES curso(id)               ON UPDATE CASCADE ON DELETE SET NULL,
    situacao_id      INTEGER REFERENCES situacao(id)            ON UPDATE CASCADE ON DELETE SET NULL,
    periodo_letivo_id INTEGER REFERENCES periodo_letivo(id)     ON UPDATE CASCADE ON DELETE SET NULL,
    nota             NUMERIC(4,2),  -- 0..10 (não impomos check para flexibilizar)
    frequencia       NUMERIC(5,2)   -- % (0..100) se vier por disciplina
);

-- Índices de consulta frequente
CREATE INDEX idx_fato_import        ON fato_nota(import_id);
CREATE INDEX idx_fato_aluno         ON fato_nota(aluno_id);
CREATE INDEX idx_fato_disciplina    ON fato_nota(disciplina_id);
CREATE INDEX idx_fato_bimestre      ON fato_nota(bimestre_id);
CREATE INDEX idx_fato_etapa         ON fato_nota(etapa_id);
CREATE INDEX idx_fato_situacao      ON fato_nota(situacao_id);
CREATE INDEX idx_fato_periodo       ON fato_nota(periodo_letivo_id);
CREATE INDEX idx_fato_aluno_periodo ON fato_nota(aluno_id, periodo_letivo_id);
CREATE INDEX idx_fato_lookup        ON fato_nota(import_id, aluno_id, disciplina_id, etapa_id);



-- =========================================
-- 2) Status
-- =========================================


CREATE TABLE status_processamento_planilha (
    id                 UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    import_sheet_id    UUID NOT NULL REFERENCES import_sheet(id) ON UPDATE CASCADE ON DELETE CASCADE,
    status             INTEGER NOT NULL,  -- ex.: 0=processando, 1=concluído, 2=erro
    ultima_atualizacao TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_status_planilha UNIQUE (import_sheet_id)
);

-- Índice para buscar rapidamente pelo guid da planilha
CREATE INDEX idx_status_sheet ON status_processamento_planilha(import_sheet_id);