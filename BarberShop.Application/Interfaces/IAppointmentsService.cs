using BarberShop.Application.Common;
using BarberShop.Application.DTOs;
using BarberShop.Domain.Models;

namespace BarberShop.Application.Interfaces;

public interface IAppointmentsService
{
    Task<List<AppointmentResponseDTO>> GetAllAsync();
    Task<AppointmentResponseDTO?> GetByIdAsync(int id);

    Task<Result<AppointmentResponseDTO>> Create(AppointmentRequestDTO dto);
    Task<Result<AppointmentResponseDTO>> Update(int id, AppointmentRequestDTO dto);
    Task<Result<AppointmentResponseDTO>> Delete(int id);

    Task<List<AppointmentResponseDTO>> GetByDateRange(DateTime start, DateTime end);
    Task<List<AppointmentResponseDTO>> GetByWorker(int workerId);
    Task<List<AppointmentResponseDTO>> GetByCustomer(int customerId);
    Task<List<AppointmentResponseDTO>> GetByService(int serviceId);
    Task<List<AppointmentResponseDTO>> GetByStatus(Status status);

    Task<Result<List<AppointmentResponseDTO>>> DelayAppointments(List<int> idList, TimeSpan time);
    Task<Result<List<AppointmentResponseDTO>>> CancelAppointments(List<int> idList);
}
