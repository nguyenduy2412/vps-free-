import type { ContactRequestStatus } from "@/lib/api";

/**
 * Presentation metadata and UX-only transition hints for Contact Request.
 *
 * The authoritative workflow is ContactRequestService.CanTransition() in the
 * backend. These values only decide which action buttons the Admin UI presents;
 * the API remains responsible for accepting or rejecting every status update.
 */
export const contactRequestStatusValues: ContactRequestStatus[] = [1, 2, 3, 4, 5];

export const contactRequestStatusMeta: Record<ContactRequestStatus, { label: string; className: string }> = {
  1: { label: "Chờ tiếp nhận", className: "bg-amber-50 text-amber-700 ring-amber-200" },
  2: { label: "Đã liên hệ", className: "bg-sky-50 text-sky-700 ring-sky-200" },
  3: { label: "Đã duyệt", className: "bg-emerald-50 text-emerald-700 ring-emerald-200" },
  4: { label: "Từ chối", className: "bg-rose-50 text-rose-700 ring-rose-200" },
  5: { label: "Đã huỷ", className: "bg-slate-100 text-slate-700 ring-slate-200" },
};

const contactRequestTransitionHints: Record<ContactRequestStatus, ContactRequestStatus[]> = {
  1: [2, 4, 5],
  2: [3, 4, 5],
  3: [],
  4: [],
  5: [],
};

export const getContactRequestTransitionHints = (status: ContactRequestStatus): ContactRequestStatus[] =>
  contactRequestTransitionHints[status];

export const requiresContactRequestStatusNote = (status: ContactRequestStatus): boolean =>
  status === 4 || status === 5;
