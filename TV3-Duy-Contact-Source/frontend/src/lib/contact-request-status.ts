import type { ContactRequestStatus } from "@/lib/api";

/** Presentation-only labels and visual treatment for Contact Request statuses. */
export const contactRequestStatusValues: ContactRequestStatus[] = [1, 2, 3, 4, 5];

export const contactRequestStatusMeta: Record<ContactRequestStatus, { label: string; className: string }> = {
  1: { label: "Chờ tiếp nhận", className: "bg-amber-50 text-amber-700 ring-amber-200" },
  2: { label: "Đã liên hệ", className: "bg-sky-50 text-sky-700 ring-sky-200" },
  3: { label: "Đã duyệt", className: "bg-emerald-50 text-emerald-700 ring-emerald-200" },
  4: { label: "Từ chối", className: "bg-rose-50 text-rose-700 ring-rose-200" },
  5: { label: "Đã huỷ", className: "bg-slate-100 text-slate-700 ring-slate-200" },
};
