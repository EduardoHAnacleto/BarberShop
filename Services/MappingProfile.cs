using AutoMapper;
using BarberShop.DTOs;
using BarberShop.Models;

namespace BarberShop.Services
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<Service, ServiceDTO>();
            CreateMap<Customer, CustomerDTO>();
            CreateMap<Worker, WorkerDTO>();
            CreateMap<AppointmentRequestDTO, Appointment>();
            CreateMap<Appointment, AppointmentResponseDTO>()
                .ForMember(dest => dest.WorkerName, opt => opt.MapFrom(src => src.Worker.Name))
                .ForMember(dest => dest.CustomerName, opt => opt.MapFrom(src => src.Customer.Name))
                .ForMember(dest => dest.ServiceName, opt => opt.MapFrom(src => src.Service.Name));
            CreateMap<User, UserRequestDTO>();
            CreateMap<UserRequestDTO, User>()
                .ForMember(dest => dest.PasswordHash, opt => opt.MapFrom(src => BCrypt.Net.BCrypt.HashPassword(src.PasswordHash)));
            CreateMap<UserRequestDTO, UserResponseDTO>();
            CreateMap<BusinessSchedule, BusinessScheduleDTO>().ReverseMap();
        }
    }
}
