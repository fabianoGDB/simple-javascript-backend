SELECT a.nome, d.sigla, fn.nota, s.descricao AS situacao
FROM fato_notas fn
JOIN aluno a ON fn.id_aluno = a.id_aluno
JOIN disciplina d ON fn.id_disciplina = d.id_disciplina
JOIN situacao s ON fn.id_situacao = s.id_situacao
JOIN etapa e ON fn.id_etapa = e.id_etapa
WHERE e.nome = 'Etapa Final'
  AND s.descricao = 'REP';


SELECT *
FROM fato_nota
WHERE import_id = '00000000-0000-0000-0000-000000000000';


SELECT name, created_at_utc
FROM import_sheet
WHERE import_id = '00000000-0000-0000-0000-000000000000'
ORDER BY created_at_utc;

SELECT texto, criado_em_utc
FROM aluno_observacao
WHERE aluno_id = 123
ORDER BY criado_em_utc DESC;
