SchoolETL/
├── SchoolETL.csproj
├── Program.cs
├── appsettings.json
├── schema_dw_enxuto.sql
├── Dockerfile
├── docker-compose.yml
│
├── Core/
│ └── Models/
│ ├── Aluno.cs
│ ├── AlunoObservacao.cs
│ ├── AlunoStatusImport.cs
│ ├── Disciplina.cs
│ ├── FatoNota.cs
│ ├── ImportBatch.cs
│ ├── ImportStage.cs
│ ├── PeriodoAvaliativo.cs
│ ├── PeriodoLetivo.cs
│ └── Situacao.cs
│
├── Infrastructure/
│ ├── NHibernateConfig.cs
│ ├── NHibernateExtensions.cs
│ └── Mappings/
│ ├── AlunoMap.cs
│ ├── AlunoObservacaoMap.cs
│ ├── AlunoStatusImportMap.cs
│ ├── DisciplinaMap.cs
│ ├── FatoNotaMap.cs
│ ├── ImportBatchMap.cs
│ ├── ImportStageMap.cs
│ ├── PeriodoAvaliativoMap.cs
│ ├── PeriodoLetivoMap.cs
│ └── SituacaoMap.cs
│
├── Services/
│ ├── IExcelEtlRunner.cs
│ └── ExcelEtlRunnerNH.cs
│
└── Worker/
├── Queue.cs
├── DispatchWorker.cs
└── StageWorker.cs
