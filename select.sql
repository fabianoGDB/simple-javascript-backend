SELECT a.nome, d.sigla, fn.nota, s.descricao AS situacao
FROM fato_notas fn
JOIN aluno a ON fn.id_aluno = a.id_aluno
JOIN disciplina d ON fn.id_disciplina = d.id_disciplina
JOIN situacao s ON fn.id_situacao = s.id_situacao
JOIN etapa e ON fn.id_etapa = e.id_etapa
WHERE e.nome = 'Etapa Final'
  AND s.descricao = 'REP';
