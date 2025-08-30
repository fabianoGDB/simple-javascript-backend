using ClosedXML.Excel;
using NHibernate;
using SchoolETL.Core.Models;


namespace SchoolETL.Services;


public class ExcelEtlRunnerNH : IExcelEtlRunner
{
    private readonly NHibernate.ISession _session;
    public ExcelEtlRunnerNH(NHibernate.ISession session) => _session = session;


    public async Task RunAsync(ImportBatch import, CancellationToken ct)
    {
        // 1) Marcar Processando
        using (var tx = _session.BeginTransaction())
        {
            import.Status = 1; // Processando
            await _session.UpdateAsync(import);
            await tx.CommitAsync();
        }


        try
        {
            if (string.IsNullOrWhiteSpace(import.StorageUri) || !File.Exists(import.StorageUri))
                throw new FileNotFoundException("Arquivo do import não encontrado", import.StorageUri);


            using var wb = new XLWorkbook(import.StorageUri);


            // 2) Exemplificar uma inserção
            using (var tx = _session.BeginTransaction())
            {
                var aluno = new Aluno { Nome = "Aluno (exemplo)", ImportId = import.Id, Matricula = "0001" };
                await _session.SaveAsync(aluno);


                import.Status = 2; // Finalizado
                import.Error = null;
                await _session.UpdateAsync(import);
                await tx.CommitAsync();
            }
        }
        catch (Exception ex)
        {
            using var tx = _session.BeginTransaction();
            import.Status = 3; // Erro
            import.Error = ex.Message;
            await _session.UpdateAsync(import);
            await tx.CommitAsync();
        }
    }
}