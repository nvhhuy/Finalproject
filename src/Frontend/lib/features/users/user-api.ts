import { apiRequest } from "@/lib/api/http";

export type UserProfileDto = {
  id: string;
  userName: string;
  email: string;
  firstName: string;
  lastName: string;
  role: string;
  twoFactorEnabled: boolean;
  rowVersion: number;
};

export const userApi = {
  async getMyProfile() {
    return await apiRequest<UserProfileDto>({ path: "/api/v1/users/profile", auth: true });
  },
  async updateMyProfile(input: { firstName: string; lastName: string; rowVersion: number }) {
    return await apiRequest<unknown>({
      method: "PUT",
      path: "/api/v1/users/profile",
      auth: true,
      body: input,
    });
  },
};

