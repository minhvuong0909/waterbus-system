/**
 * Types cho luồng đăng nhập Staff - khớp AuthResponseDto / LoginCommand ở backend
 * (WaterbusSystem.Application.Features.Auth.Commands.Login).
 */

export interface LoginRequest {
  email: string;
  password: string;
}

/** Khớp AuthResponseDto(UserId, Email, FullName, Roles, Token) - JSON camelCase. */
export interface LoginResponseDto {
  userId: string;
  email: string;
  fullName: string;
  roles: string[];
  token: string;
}

export interface AuthUser {
  userId: string;
  email: string;
  fullName: string;
  roles: string[];
}
