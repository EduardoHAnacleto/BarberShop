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
            CreateMap<WorkerDTO, Worker>();

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
        }
    }
}
