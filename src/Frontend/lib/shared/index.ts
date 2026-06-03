/**
 * Cross-cutting helpers — import from `@/lib/shared` instead of reimplementing in pages.
 *
 * @example
 * import { formatDateTimeOrDash, formatCount, calcTotalPages } from "@/lib/shared";
 */
export { formatDate, formatDateTime, formatRelativeTime, formatBytes, formatCount } from "@/lib/shared/format";
export { dash, formatDateOrDash, formatDateTimeOrDash, formatEmpty } from "@/lib/shared/display";
export { calcTotalPages, clampPage } from "@/lib/shared/pagination";
export { createAuthSchemas, firstZodError } from "@/lib/shared/validation";
export {
  ApiClientError,
  formatValidationErrors,
  getApiErrorMessageFromPayload,
  getNetworkErrorMessage,
  isConcurrencyError,
} from "@/lib/errors";
