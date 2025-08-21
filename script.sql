-- Habilite extensões úteis (opcional)
-- CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- =========================
-- Dimensões de calendário / catálogos
-- =========================

CREATE TABLE periodo_letivo (
    id              SERIAL PRIMARY KEY,
    ano             INT NOT NULL,
    semestre        INT NOT NULL CHECK (semestre BETWEEN 1 AND 2),
    descricao       VARCHAR(100)
);

CREATE TABLE bimestre (
    id      INT PRIMARY KEY,            -- 1..4 (pode reservar 5 para recuperação)
    nome    VARCHAR(20) NOT NULL        -- '1º', '2º', '3º', '4º'
);

CREATE TABLE etapa (
    id      INT PRIMARY KEY,            -- 1..4, 99='Etapa Final'
    nome    VARCHAR(50) NOT NULL
);

CREATE TABLE situacao (
    id          INT PRIMARY KEY,
    descricao   VARCHAR(20) NOT NULL    -- APR, REP, CAN, CUR, OUT...
);

-- =========================
-- Controle de importações
-- =========================

CREATE TABLE import_batch (
    id               UUID PRIMARY KEY,                 -- GUID do lote de import
    created_at_utc   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    file_name        VARCHAR(255),
    periodo_letivo_id INT REFERENCES periodo_letivo(id)
);

CREATE TABLE import_sheet (
    id               UUID PRIMARY KEY,                 -- GUID da planilha/aba
    import_id        UUID NOT NULL REFERENCES import_batch(id) ON DELETE CASCADE,
    name             VARCHAR(120) NOT NULL,            -- nome da aba (ex.: 'Etapa 1')
    created_at_utc   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_importsheet UNIQUE (import_id, name)
);

-- =========================
-- Dimensões de negócio
-- =========================

CREATE TABLE aluno (
    id                SERIAL PRIMARY KEY,
    import_id         UUID,                            -- opcional: quem criou este aluno
    nome              VARCHAR(255),
    matricula         VARCHAR(50),                     -- pode ser nulo
    frequencia_geral  DECIMAL(5,2),                    -- %
    situacao_curso    VARCHAR(50),                     -- Matriculado, Evasão, Cancelado...
    foto_path         VARCHAR(255),
    CONSTRAINT fk_aluno_import FOREIGN KEY (import_id) REFERENCES import_batch(id)
);

CREATE INDEX idx_aluno_nome ON aluno (nome);
CREATE INDEX idx_aluno_matricula ON aluno (matricula);

CREATE TABLE curso (
    id          SERIAL PRIMARY KEY,
    import_id   UUID,                                  -- opcional: quem criou
    sigla       VARCHAR(50) NOT NULL,                  -- ex.: INT.09888
    descricao   VARCHAR(100),                          -- '44H de 160H'
    CONSTRAINT uq_curso_sigla UNIQUE (sigla),
    CONSTRAINT fk_curso_import FOREIGN KEY (import_id) REFERENCES import_batch(id)
);

CREATE TABLE disciplina (
    id                   SERIAL PRIMARY KEY,
    import_id            UUID,                          -- opcional: quem criou
    sigla                VARCHAR(50) NOT NULL,          -- pode reutilizar o mesmo código do curso
    nome_area            VARCHAR(100),
    carga_horaria_rotulo VARCHAR(50),                   -- '44H de 160H'
    CONSTRAINT uq_disciplina_sigla UNIQUE (sigla),
    CONSTRAINT fk_disciplina_import FOREIGN KEY (import_id) REFERENCES import_batch(id)
);

-- =========================
-- Fato principal (notas)
-- =========================

CREATE TABLE fato_nota (
    id                SERIAL PRIMARY KEY,
    import_id         UUID NOT NULL REFERENCES import_batch(id) ON DELETE CASCADE,
    -- opcional: se quiser amarrar por aba específica, descomente:
    -- import_sheet_id  UUID REFERENCES import_sheet(id) ON DELETE SET NULL,

    aluno_id          INT NOT NULL REFERENCES aluno(id) ON DELETE CASCADE,
    disciplina_id     INT NOT NULL REFERENCES disciplina(id),
    bimestre_id       INT NOT NULL REFERENCES bimestre(id),
    etapa_id          INT NOT NULL REFERENCES etapa(id),
    curso_id          INT REFERENCES curso(id),
    situacao_id       INT REFERENCES situacao(id),
    periodo_letivo_id INT REFERENCES periodo_letivo(id),

    nota              DECIMAL(5,2),
    frequencia        DECIMAL(5,2)                      -- se houver por disciplina
);

CREATE INDEX idx_fato_import        ON fato_nota(import_id);
CREATE INDEX idx_fato_aluno         ON fato_nota(aluno_id);
CREATE INDEX idx_fato_disciplina    ON fato_nota(disciplina_id);
CREATE INDEX idx_fato_bimestre      ON fato_nota(bimestre_id);
CREATE INDEX idx_fato_etapa         ON fato_nota(etapa_id);
CREATE INDEX idx_fato_situacao      ON fato_nota(situacao_id);
CREATE INDEX idx_fato_periodo       ON fato_nota(periodo_letivo_id);

-- =========================
-- Observações por aluno
-- =========================

CREATE TABLE aluno_observacao (
    id            SERIAL PRIMARY KEY,
    aluno_id      INT NOT NULL REFERENCES aluno(id) ON DELETE CASCADE,
    texto         TEXT NOT NULL,
    criado_em_utc TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_obs_aluno ON aluno_observacao(aluno_id);

-- =========================
-- SEEDS (opcionais)
-- =========================

-- INSERT INTO bimestre (id, nome) VALUES
--     (1,'1º'), (2,'2º'), (3,'3º'), (4,'4º')
-- ON CONFLICT (id) DO NOTHING;

-- INSERT INTO etapa (id, nome) VALUES
--     (1,'Etapa 1'), (2,'Etapa 2'), (3,'Etapa 3'), (4,'Etapa 4'), (99,'Etapa Final')
-- ON CONFLICT (id) DO NOTHING;

-- INSERT INTO situacao (id, descricao) VALUES
--     (1,'APR'), (2,'REP'), (3,'CAN'), (4,'CUR'), (5,'OUT')
-- ON CONFLICT (id) DO NOTHING;
