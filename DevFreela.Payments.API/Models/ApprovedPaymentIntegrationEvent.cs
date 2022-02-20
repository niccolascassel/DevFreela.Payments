namespace DevFreela.Payments.API.Models
{
    public class ApprovedPaymentIntegrationEvent
    {
        public ApprovedPaymentIntegrationEvent(int projectId)
        {
            ProjectId = projectId;
        }

        public int ProjectId { get; set; }
    }
}
