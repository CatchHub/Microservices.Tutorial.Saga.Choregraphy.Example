﻿using MassTransit;
using Microsoft.EntityFrameworkCore;
using Order.API.Models.Context;
using Shared.Events;

namespace Order.API.Consumers
{
    public class StockNotReservedEventConsumer(OrderAPIDbContext _context) : IConsumer<StockNotReservedEvent>
    {
        public async  Task Consume(ConsumeContext<StockNotReservedEvent> context)
        {
            var order = await _context.Orders.FindAsync(context.Message.OrderId);
            if (order == null)
            {
                throw new NullReferenceException("Order is null!");
            }

            order.OrderStatus = Enums.OrderStatus.Fail;
            await _context.SaveChangesAsync();
        }
    }
}
