import { apiRequest } from "@/lib/api/http";

export interface SubmitContactPayload {
  name: string;
  email: string;
  subject: string;
  body: string;
  turnstileToken?: string | null;
}

export function submitContactMessage({ turnstileToken, ...payload }: SubmitContactPayload) {
  return apiRequest<string>({
    method: "POST",
    path: "/api/v1/contact",
    body: payload,
    turnstileToken,
  });
}
