using AutoMapper;
using BarberShop.DTOs;
using BarberShop.Models;
using BarberShop.Repositories;
using BarberShop.Repositories.Interfaces;

namespace BarberShop.Services;

public class WorkerService : IWorkerService
{
    private readonly IWorkerRepository _workerRepository;
    private readonly IServiceRepository _serviceRepository;
    private readonly IMapper _mapper;

    public WorkerService(IWorkerRepository workerRepository, IServiceRepository serviceRepository, IMapper mapper)
    {
        _workerRepository = workerRepository;
        _serviceRepository = serviceRepository;
        _mapper = mapper;
    }

    public async Task<Worker> CreateFromDTO(WorkerDTO dto)
    {
        var obj = _mapper.Map<Worker>(dto);
        foreach (var service in dto.ServicesId)
        {
            var serviceObj = await _serviceRepository.GetByIdAsync(service);
            if (serviceObj != null)
            {
                obj.ProvidedServices.Add(serviceObj);
            }
        }
        return obj;
    }
}
