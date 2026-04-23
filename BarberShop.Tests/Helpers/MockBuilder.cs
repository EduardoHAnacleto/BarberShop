using BarberShop.Application.Interfaces;
using BarberShop.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BarberShop.Tests.Helpers;

public static class MockBuilder
{
    public static Mock<IUnitOfWork> UnitOfWork()
    {
        var uow = new Mock<IUnitOfWork>();

        // Configura cada repositório como mock dentro do UoW
        uow.Setup(u => u.Appointments)
           .Returns(new Mock<IAppointmentRepository>().Object);

        uow.Setup(u => u.Customers)
           .Returns(new Mock<ICustomerRepository>().Object);

        uow.Setup(u => u.Workers)
           .Returns(new Mock<IWorkerRepository>().Object);

        uow.Setup(u => u.Services)
           .Returns(new Mock<IServiceRepository>().Object);

        uow.Setup(u => u.Users)
           .Returns(new Mock<IUserRepository>().Object);

        return uow;
    }

    public static RedisService Redis()
    {
        // RedisService depende de IConnectionMultiplexer — mockamos ela também
        var multiplexer = new Mock<StackExchange.Redis.IConnectionMultiplexer>();
        var database = new Mock<StackExchange.Redis.IDatabase>();

        multiplexer.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                   .Returns(database.Object);

        return new RedisService(multiplexer.Object);
    }

    public static ILogger<T> Logger<T>()
        => NullLogger<T>.Instance; // logger que não faz nada — ideal para testes
}
