using AutoMapper;
using CarAutoParts.Application.Common;
using CarAutoParts.Application.DTOs.Auth;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Application.Security;
using CarAutoParts.Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CarAutoParts.Application.Services;

/// <summary>User and role administration.</summary>
public class UserService : IUserService
{
    private readonly IRepository<AppUser> _users;
    private readonly IRepository<Role> _roles;
    private readonly IRepository<UserRole> _userRoles;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IValidator<UserCreateDto> _validator;

    public UserService(
        IRepository<AppUser> users,
        IRepository<Role> roles,
        IRepository<UserRole> userRoles,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        IValidator<UserCreateDto> validator)
    {
        _users = users;
        _roles = roles;
        _userRoles = userRoles;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _validator = validator;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UserDto>> GetUsersAsync(CancellationToken ct = default)
    {
        var users = await _users.Query()
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Where(u => !u.IsDeleted)
            .OrderBy(u => u.Username)
            .ToListAsync(ct);

        return _mapper.Map<List<UserDto>>(users);
    }

    /// <inheritdoc />
    public async Task<Result<UserDto>> CreateAsync(UserCreateDto dto, CancellationToken ct = default)
    {
        var validation = await _validator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return Result<UserDto>.Failure(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));

        if (string.IsNullOrWhiteSpace(dto.Password))
            return Result<UserDto>.Failure("Password is required for new users.");

        if (!PasswordPolicy.IsValid(dto.Password, out var passwordError))
            return Result<UserDto>.Failure(passwordError!);

        if (await _users.ExistsAsync(u => u.Username == dto.Username && !u.IsDeleted, ct))
            return Result<UserDto>.Failure("Username already exists.");

        var user = new AppUser
        {
            Username = dto.Username.Trim(),
            DisplayName = dto.DisplayName.Trim(),
            Email = dto.Email,
            IsActive = dto.IsActive,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password)
        };

        foreach (var roleId in dto.RoleIds)
        {
            if (await _roles.ExistsAsync(r => r.Id == roleId && !r.IsDeleted, ct))
                user.UserRoles.Add(new UserRole { RoleId = roleId });
        }

        _users.Add(user);
        await _unitOfWork.SaveChangesAsync(ct);

        var loaded = await _users.Query()
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstAsync(u => u.Id == user.Id, ct);

        return Result<UserDto>.Success(_mapper.Map<UserDto>(loaded));
    }

    /// <inheritdoc />
    public async Task<Result<UserDto>> UpdateAsync(int id, UserCreateDto dto, CancellationToken ct = default)
    {
        var validation = await _validator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return Result<UserDto>.Failure(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));

        var user = await _users.Query()
            .Include(u => u.UserRoles)
            .FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted, ct);

        if (user is null)
            return Result<UserDto>.Failure("User not found.");

        if (await _users.ExistsAsync(u => u.Username == dto.Username && u.Id != id && !u.IsDeleted, ct))
            return Result<UserDto>.Failure("Username already exists.");

        user.Username = dto.Username.Trim();
        user.DisplayName = dto.DisplayName.Trim();
        user.Email = dto.Email;
        user.IsActive = dto.IsActive;
        user.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(dto.Password))
        {
            if (!PasswordPolicy.IsValid(dto.Password, out var passwordError))
                return Result<UserDto>.Failure(passwordError!);
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
        }

        var existingRoles = user.UserRoles.ToList();
        foreach (var ur in existingRoles)
            _userRoles.Remove(ur);

        foreach (var roleId in dto.RoleIds)
        {
            if (await _roles.ExistsAsync(r => r.Id == roleId && !r.IsDeleted, ct))
                user.UserRoles.Add(new UserRole { RoleId = roleId, UserId = user.Id });
        }

        _users.Update(user);
        await _unitOfWork.SaveChangesAsync(ct);

        var loaded = await _users.Query()
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstAsync(u => u.Id == id, ct);

        return Result<UserDto>.Success(_mapper.Map<UserDto>(loaded));
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(id, ct);
        if (user is null || user.IsDeleted)
            return Result.Failure("User not found.");

        user.IsDeleted = true;
        user.UpdatedAt = DateTime.UtcNow;
        _users.Update(user);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RoleDto>> GetRolesAsync(CancellationToken ct = default)
    {
        var roles = await _roles.Query()
            .Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .Where(r => !r.IsDeleted)
            .OrderBy(r => r.Name)
            .ToListAsync(ct);

        return _mapper.Map<List<RoleDto>>(roles);
    }
}
