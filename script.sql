-- Tabela de Alunos
CREATE TABLE aluno (
    id_aluno SERIAL PRIMARY KEY,
    nome VARCHAR(255) NOT NULL,
    matricula VARCHAR(50),
    frequencia_geral DECIMAL(5,2),
    situacao_curso VARCHAR(50),
    foto_path VARCHAR(255)
);

-- Tabela de Disciplinas
CREATE TABLE disciplina (
    id_disciplina SERIAL PRIMARY KEY,
    sigla VARCHAR(20) NOT NULL,
    nome_area VARCHAR(100),
    carga_horaria VARCHAR(50)
);

-- Tabela de Bimestres
CREATE TABLE bimestre (
    id_bimestre SERIAL PRIMARY KEY,
    nome VARCHAR(20) NOT NULL  -- Ex: '1º', '2º', '3º', '4º', 'Rec'
);

-- Tabela de Etapas
CREATE TABLE etapa (
    id_etapa SERIAL PRIMARY KEY,
    nome VARCHAR(50) NOT NULL  -- Ex: 'Etapa 1', 'Etapa 2', ..., 'Etapa Final'
);

-- Tabela de Cursos / Conjuntos de aulas
CREATE TABLE curso (
    id_curso SERIAL PRIMARY KEY,
    sigla VARCHAR(50) NOT NULL,        -- Ex: INT.09888
    descricao VARCHAR(100)             -- Ex: '44H de 160H'
);

-- Tabela de Situação (ex: APR, REP, CAN)
CREATE TABLE situacao (
    id_situacao SERIAL PRIMARY KEY,
    descricao VARCHAR(20) NOT NULL     -- APR, REP, CAN, etc.
);

-- Tabela de Períodos Letivos
CREATE TABLE periodo_letivo (
    id_periodo SERIAL PRIMARY KEY,
    ano INT NOT NULL,
    semestre INT CHECK (semestre BETWEEN 1 AND 2),
    descricao VARCHAR(100)
);

-- Tabela Fato: Notas
CREATE TABLE fato_notas (
    id_nota SERIAL PRIMARY KEY,
    id_aluno INT NOT NULL REFERENCES aluno(id_aluno),
    id_disciplina INT NOT NULL REFERENCES disciplina(id_disciplina),
    id_bimestre INT NOT NULL REFERENCES bimestre(id_bimestre),
    id_etapa INT NOT NULL REFERENCES etapa(id_etapa),
    id_curso INT REFERENCES curso(id_curso),
    id_situacao INT REFERENCES situacao(id_situacao),
    id_periodo INT REFERENCES periodo_letivo(id_periodo),
    nota DECIMAL(5,2),
    frequencia DECIMAL(5,2)
);
