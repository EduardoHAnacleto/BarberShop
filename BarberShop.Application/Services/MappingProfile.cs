using AutoMapper;
using BarberShop.Application.DTOs;
using BarberShop.Domain.Models;

namespace BarberShop.Application.Services
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // Service
            CreateMap<Service, ServiceDTO>();
            CreateMap<ServiceDTO, Service>();   

            // Customer
            CreateMap<Customer, CustomerDTO>();
            CreateMap<CustomerDTO, Customer>(); 

            // Worker
            CreateMap<Worker, WorkerDTO>();
            // Skip ProvidedServices on the inbound mapping; the service layer
            // re-attaches existing Service entities via _uow.Services.GetByIdAsync.
            // Without this Ignore, AutoMapper builds new Service instances with
            // explicit Ids and EF tries to INSERT them, hitting IDENTITY_INSERT.
            CreateMap<WorkerDTO, Worker>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.ProvidedServices, opt => opt.Ignore());

            // Appointment
            CreateMap<AppointmentRequestDTO, Appointment>()
                .ForMember(dest => dest.CompletedAt,
                    opt => opt.Ignore());
            CreateMap<Appointment, AppointmentResponseDTO>()
                .ForMember(dest => dest.WorkerName,
                    opt => opt.MapFrom(src => src.Worker.Name))
                .ForMember(dest => dest.CustomerName,
                    opt => opt.MapFrom(src => src.Customer.Name))
                .ForMember(dest => dest.ServiceName,
                    opt => opt.MapFrom(src => src.Service.Name));

            // User
            CreateMap<User, UserRequestDTO>();
            CreateMap<UserRequestDTO, User>()
                .ForMember(dest => dest.PasswordHash,
                    opt => opt.MapFrom(src =>
                        BCrypt.Net.BCrypt.HashPassword(src.PasswordHash)));
            CreateMap<UserRequestDTO, UserResponseDTO>();
            CreateMap<User, UserResponseDTO>(); 

            // BusinessSchedule
            CreateMap<BusinessSchedule, BusinessScheduleDTO>().ReverseMap();

            // WorkingHours (Closure)
            CreateMap<ClosureDTO, WorkingHours>()
                .ForMember(dest => dest.Id, opt => opt.Ignore());
        }
    }
}
