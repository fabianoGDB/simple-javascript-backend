namespace SchoolETL.Worker.DTOs
{
    public record ImportJob
    {
        public Guid ImportId { get; init; }
        public int Progress { get; set; }
    }
}
