using DevFreela.Payments.API.Models;
using DevFreela.Payments.API.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace DevFreela.Payments.API.Consumers
{
    public class ProjectPaymentConsumer : BackgroundService
    {
        private const string QUEUE = "Payments";
        private const string APPROVED_PAYMENTS = "ApprovedPayments";
        private readonly IConnection connection;
        private readonly IModel channel;
        private readonly IServiceProvider serviceProvider;

        public ProjectPaymentConsumer(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;

            var factory = new ConnectionFactory
            {
                HostName = "localhost"
            };

            connection = factory.CreateConnection();
            channel = connection.CreateModel();

            channel.QueueDeclare(
                queue: QUEUE,
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: null
            );

            channel.QueueDeclare(
                queue: APPROVED_PAYMENTS,
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: null
            );
        }
        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var consumer = new EventingBasicConsumer(channel);

            consumer.Received += (sender, eventArgs) =>
            {
                var byteArray = eventArgs.Body.ToArray();
                var paymentInfoJson = Encoding.UTF8.GetString(byteArray);

                var paymentInfo = JsonSerializer.Deserialize<PaymentInfoInputModel>(paymentInfoJson);

                ProcessPayment(paymentInfo);

                var approvedPayment = new ApprovedPaymentIntegrationEvent(paymentInfo.ProjectId);
                var approvedPaymentJson = JsonSerializer.Serialize(approvedPayment);
                var approvedPaymentBytes = Encoding.UTF8.GetBytes(approvedPaymentJson);

                channel.BasicPublish(
                    exchange: "", 
                    routingKey: APPROVED_PAYMENTS,
                    basicProperties: null,
                    body: approvedPaymentBytes
                );

                channel.BasicAck(eventArgs.DeliveryTag, false);
            };

            channel.BasicConsume(QUEUE, false, consumer);

            return Task.CompletedTask;
        }

        private void ProcessPayment (PaymentInfoInputModel paymentInfo)
        {
            using (var scope = serviceProvider.CreateScope())
            {
                var paymentService = scope.ServiceProvider.GetRequiredService<IPaymentService>();

                paymentService.Process(paymentInfo);
            }
        }
    }
}
