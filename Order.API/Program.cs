using MassTransit;
using Microsoft.EntityFrameworkCore;
using Order.API.Consumers;
using Order.API.Enums;
using Order.API.Models.Context;
using Order.API.ViewModels;
using Shared;
using Shared.Events;
using Shared.Messages;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddMassTransit(configurator =>
{
    configurator.AddConsumer<PaymentCompletedEventConsumer>();
    configurator.AddConsumer<PaymentFailedEventConsumer>();
    configurator.AddConsumer<StockNotReservedEventConsumer>();
    configurator.UsingRabbitMq((context, _configure) =>
    {
        _configure.Host(builder.Configuration["RabbitMQ"]);
        _configure.ReceiveEndpoint(RabbitMQSettings.Order_PaymentCompletedEventQueue, e => e.ConfigureConsumer<PaymentCompletedEventConsumer>(context));
        _configure.ReceiveEndpoint(RabbitMQSettings.Order_PaymentFailedEventQueue, e => e.ConfigureConsumer<PaymentFailedEventConsumer>(context));
        _configure.ReceiveEndpoint(RabbitMQSettings.Order_StockNotReservedEventQueue, e => e.ConfigureConsumer<StockNotReservedEventConsumer>(context));
    });
});
builder.Services.AddDbContext<OrderAPIDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("MSSQLServer")));


var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapPost("/create-order", async (CreateOrderVM model, OrderAPIDbContext context, IPublishEndpoint publishenpoint) =>
{
    Order.API.Models.Order order = new()
    {
        BuyerId = Guid.TryParse(model.BuyerId, out Guid _buyerId) ? _buyerId : Guid.NewGuid(),
        OrderItems = model.OrderItems.Select(oi => new Order.API.Models.OrderItem()
        {
            Count = oi.Count,
            Price = oi.Price,
            ProductId = Guid.Parse(oi.ProductId)
        }).ToList(),
        OrderStatus = OrderStatus.Suspend,
        CreatedDate = DateTime.UtcNow,
        TotalPrice = model.OrderItems.Sum(oi => oi.Price * oi.Count)
    };
    await context.Orders.AddAsync(order);
    await context.SaveChangesAsync();

    OrderCreatedEvent orderCreatedEvent = new()
    {
        BuyerId = order.BuyerId,
        OrderId = order.Id,
        TotalPrice = order.TotalPrice,
        OrderItemMessages = order.OrderItems.Select(oi => new OrderItemMessage()
        {
            Count = oi.Count,
            Price = oi.Price,
            ProductId = oi.ProductId
        }).ToList()
    };
    await publishenpoint.Publish(orderCreatedEvent);

});
app.Run();
