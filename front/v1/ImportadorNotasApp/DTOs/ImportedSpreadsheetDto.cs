namespace ImportadorNotasApp.DTOs
{
    public record ImportedSpreadsheetDto(int Id, string ClassName, int Year, DateTime ImportedAt, int Status, int StudentCount);
}
