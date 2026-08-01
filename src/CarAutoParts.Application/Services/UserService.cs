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
    private readonly IRepository<UserBranch> _userBranches;
    private readonly IRepository<Branch> _branches;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IValidator<UserCreateDto> _validator;

    public UserService(
        IRepository<AppUser> users,
        IRepository<Role> roles,
        IRepository<UserRole> userRoles,
        IRepository<UserBranch> userBranches,
        IRepository<Branch> branches,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        IValidator<UserCreateDto> validator)
    {
        _users = users;
        _roles = roles;
        _userRoles = userRoles;
        _userBranches = userBranches;
        _branches = branches;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _validator = validator;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UserDto>> GetUsersAsync(CancellationToken ct = default)
    {
        var users = await _users.Query()
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(u => u.UserBranches)
            .Where(u => !u.IsDeleted)
            .OrderBy(u => u.Username)
            .ToListAsync(ct);

        return _mapper.Map<List<UserDto>>(users);
    }

    /// <inheritdoc />
    public async Task<PagedResult<UserDto>> GetUsersPagedAsync(QuerySpec query, CancellationToken ct = default)
    {
        var q = _users.Query()
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(u => u.UserBranches)
            .Where(u => !u.IsDeleted);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(u => u.Username.Contains(s)
                || u.DisplayName.Contains(s)
                || (u.Email != null && u.Email.Contains(s)));
        }

        q = q.OrderBy(u => u.Username);
        var paged = await q.ToPagedResultAsync(query.Page, query.PageSize, ct);
        return new PagedResult<UserDto>
        {
            Items = _mapper.Map<List<UserDto>>(paged.Items),
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize
        };
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

        var branchError = await ValidateBranchesAsync(dto.BranchIds, dto.DefaultBranchId, ct);
        if (branchError is not null)
            return Result<UserDto>.Failure(branchError);

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

        ApplyBranches(user, dto.BranchIds, dto.DefaultBranchId);

        _users.Add(user);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<UserDto>.Success(await LoadUserDtoAsync(user.Id, ct));
    }

    /// <inheritdoc />
    public async Task<Result<UserDto>> UpdateAsync(int id, UserCreateDto dto, CancellationToken ct = default)
    {
        var validation = await _validator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return Result<UserDto>.Failure(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));

        var user = await _users.Query()
            .Include(u => u.UserRoles)
            .Include(u => u.UserBranches)
            .FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted, ct);

        if (user is null)
            return Result<UserDto>.Failure("User not found.");

        if (await _users.ExistsAsync(u => u.Username == dto.Username && u.Id != id && !u.IsDeleted, ct))
            return Result<UserDto>.Failure("Username already exists.");

        var branchError = await ValidateBranchesAsync(dto.BranchIds, dto.DefaultBranchId, ct);
        if (branchError is not null)
            return Result<UserDto>.Failure(branchError);

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

        foreach (var ur in user.UserRoles.ToList())
            _userRoles.Remove(ur);

        foreach (var roleId in dto.RoleIds)
        {
            if (await _roles.ExistsAsync(r => r.Id == roleId && !r.IsDeleted, ct))
                user.UserRoles.Add(new UserRole { RoleId = roleId, UserId = user.Id });
        }

        foreach (var ub in user.UserBranches.ToList())
            _userBranches.Remove(ub);

        ApplyBranches(user, dto.BranchIds, dto.DefaultBranchId, user.Id);

        _users.Update(user);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<UserDto>.Success(await LoadUserDtoAsync(id, ct));
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

    private async Task<UserDto> LoadUserDtoAsync(int userId, CancellationToken ct)
    {
        var loaded = await _users.Query()
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(u => u.UserBranches)
            .FirstAsync(u => u.Id == userId, ct);
        return _mapper.Map<UserDto>(loaded);
    }

    private async Task<string?> ValidateBranchesAsync(IReadOnlyList<int>? branchIds, int? defaultBranchId, CancellationToken ct)
    {
        if (branchIds is null || branchIds.Count == 0)
            return null;

        var distinct = branchIds.Distinct().ToList();
        var count = await _branches.Query().CountAsync(b => distinct.Contains(b.Id) && b.IsActive && !b.IsDeleted, ct);
        if (count != distinct.Count)
            return "One or more branches are invalid.";

        if (defaultBranchId.HasValue && !distinct.Contains(defaultBranchId.Value))
            return "Default branch must be in the assigned branch list.";

        return null;
    }

    private static void ApplyBranches(AppUser user, IReadOnlyList<int>? branchIds, int? defaultBranchId, int? userId = null)
    {
        if (branchIds is null || branchIds.Count == 0)
            return;

        var distinct = branchIds.Distinct().ToList();
        var def = defaultBranchId ?? distinct[0];
        foreach (var branchId in distinct)
        {
            user.UserBranches.Add(new UserBranch
            {
                UserId = userId ?? 0,
                BranchId = branchId,
                IsDefault = branchId == def
            });
        }
    }
}
