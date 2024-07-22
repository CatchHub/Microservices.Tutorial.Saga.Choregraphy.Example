using MassTransit;
using MongoDB.Driver;
using Shared;
using Shared.Events;
using Shared.Messages;
using Stock.API.Services;

namespace Stock.API.Consumers
{
    public class OrderCreatedEventConsumer(MongoDBService mongoDbService, ISendEndpointProvider sendEndpointProvider, IPublishEndpoint publishEndpoint) : IConsumer<OrderCreatedEvent>
    {
        public async Task Consume(ConsumeContext<OrderCreatedEvent> context)
        {
            List<bool> stockResult = new();
            IMongoCollection<Models.Stock> collection = mongoDbService.GetCollection<Models.Stock>();

            foreach (var item in context.Message.OrderItemMessages)
            {
                stockResult.Add(await (await collection.FindAsync(s => s.ProductId == item.ProductId.ToString() && s.Count >= (long)item.Count)).AnyAsync());
            }

            if (stockResult.TrueForAll(s => s.Equals(true)))
            {
                // Stock update
                foreach (var orderItem in context.Message.OrderItemMessages)
                {
                    Models.Stock stock  =await  (await collection.FindAsync(s => s.ProductId == orderItem.ProductId.ToString())).FirstOrDefaultAsync();
                    stock.Count -= orderItem.Count;

                    await collection.FindOneAndReplaceAsync(x => x.ProductId == orderItem.ProductId.ToString(), stock);
                }
                // payment event trigger
                var sendEndpoint = await sendEndpointProvider.GetSendEndpoint(
                    new Uri($"queue:{RabbitMQSettings.Payment_StockResevedEventQueue}"));
                StockReservedEvent stockReservedEvent = new()
                {
                    BuyerId = context.Message.BuyerId,
                    TotalPrice = context.Message.TotalPrice,
                    OrderId = context.Message.OrderId,
                    OrderItems = context.Message.OrderItemMessages
                };
                //Sending event to specific url.
                await sendEndpoint.Send(stockReservedEvent);
            }
            else
            {
                // stock process failed
                // Order service warning

                StockNotReservedEvent stockNotReservedEvent = new()
                {
                    OrderId = context.Message.OrderId,
                    BuyerId = context.Message.BuyerId,
                    Message = "Stocks are insufficient!"
                };
                await publishEndpoint.Publish(stockNotReservedEvent);

            }
        }
    }
}
